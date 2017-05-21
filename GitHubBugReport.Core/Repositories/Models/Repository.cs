using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Repositories.Services;

namespace GitHubBugReport.Core.Repositories.Models
{
    public class Repository
    {
        public string Owner { get; private set; }
        public string Name { get; private set; }
        public string Alias { get; private set; }
        public bool IsAlias(string alias) => (Alias == alias.ToLower());
        public Expression FilterQuery { get; private set; }
        // owner/name lowercase
        public string RepoName { get; private set; }
        public bool IsRepoName(string repoName) => (RepoName == repoName.ToLower());
        // https://github.com/owner/name/
        public string HtmlUrlPrefix { get; private set; }
        public string AuthenticationToken { get; set; }

        // TODO - Move to config
        private static readonly string s_GitHubProductIdentifier = "GitHubBugReporter";

        public IReadOnlyList<Issue> Issues { get; private set; }
        public ConcurrentBag<IssueComment> IssueComments { get; private set; }

        private Repository(string repoName, string alias, string filterQuery)
        {
            RepoName = repoName.ToLower();
            Debug.Assert(RepositoryService._repositories.Where(repo => (repo.RepoName == RepoName)).None());
            Alias = (alias != null) ? alias.ToLower() : RepoName;
            Debug.Assert(RepositoryService._repositories.Where(repo => (repo.Alias == Alias)).None());

            // TODO: This code is duplicated in OctoKitRepositoryService.FromHtmlUrl
            string[] repoNameParts = RepoName.Split('/');
            if ((repoNameParts.Length != 2) ||
                string.IsNullOrEmpty(repoNameParts[0]) ||
                string.IsNullOrEmpty(repoNameParts[1]))
            {
                throw new InvalidDataException($"Invalid repository name format in repo '{repoName}'");
            }

            Owner = repoNameParts[0];
            Name = repoNameParts[1];

            HtmlUrlPrefix = OctoKitRepositoryService._htmlUrlGitHubPrefix + RepoName + "/";
            RepositoryService._repositories.Add(this);

            if (filterQuery != null)
            {
                FilterQuery = QueryParser.Parse(filterQuery, null);
            }
        }

        public IEnumerable<DataModelIssue> Filter(IEnumerable<DataModelIssue> issues)
        {
            if (FilterQuery == null)
            {
                return issues;
            }

            return issues.Where(i => ((i.Repo != this) || FilterQuery.Evaluate(i)));
        }

        public void UpdateAlias(string alias)
        {
            Debug.Assert(alias != null);
            if (Alias == RepoName)
            {
                Alias = alias;
            }
            else
            {
                if (Alias != alias)
                {
                    throw new InvalidDataException($"Repository '{RepoName}' has 2 aliases defined '{Alias}' and '{alias}'");
                }
            }
        }

        public void UpdateFilterQuery(string filterQuery)
        {
            Debug.Assert(filterQuery != null);
            if (FilterQuery == null)
            {
                FilterQuery = QueryParser.Parse(filterQuery, null);
            }
            else
            {
                Expression filterQueryExpr = QueryParser.Parse(filterQuery, null);
                if (!filterQueryExpr.Equals(FilterQuery) && !filterQueryExpr.Simplified.Equals(FilterQuery.Simplified))
                {
                    throw new InvalidDataException($"Repository '{RepoName}' has 2 filter queries defined '{FilterQuery.ToString()}' and '{filterQuery}'");
                }
            }
        }

        public string GetQueryUrl(string queryArgs)
        {
            return HtmlUrlPrefix + "issues?q=" + System.Net.WebUtility.UrlEncode(queryArgs);
        }

        public string GetQueryUrl(string queryPrefix, IEnumerable<DataModelIssue> issues)
        {
            return GetQueryUrl(queryPrefix + " " + string.Join(" ", issues.Select(i => i.Number)));
        }

        public static Repository From(string repoName, string alias = null, string filterQuery = null)
        {
            Repository repo = RepositoryService.FindRepo(repoName) ?? RepositoryService.FindRepoByAlias(repoName);
            if (repo == null)
            {
                repo = new Repository(repoName, alias, filterQuery);
                Debug.Assert(RepositoryService.FindRepo(repoName) == repo);
            }
            else
            {
                if (alias != null)
                {
                    repo.UpdateAlias(alias);
                }

                if (filterQuery != null)
                {
                    repo.UpdateFilterQuery(filterQuery);
                }
            }
            return repo;
        }

        /// <summary>
        /// Gets all of the issues in the repository, closed and open
        /// </summary>
        public void LoadIssues()
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(s_GitHubProductIdentifier));
            if (AuthenticationToken != null)
            {
                client.Credentials = new Credentials(AuthenticationToken);
            }
            RepositoryIssueRequest issueRequest = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Open,
                Filter = IssueFilter.All
            };

            Task.Run((Func<Task>) (async () =>
            {
                Issues = await client.Issue.GetAllForRepository(Owner, Name, issueRequest);
            })).Wait();
        }

        /// <summary>
        /// Get all comment info for each open issue in the repo
        /// </summary>
        public void LoadIssueComments()
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(s_GitHubProductIdentifier));
            if (AuthenticationToken != null)
            {
                client.Credentials = new Credentials(AuthenticationToken);
            }

            IssueComments = new ConcurrentBag<IssueComment>();
            List<Task> tasks = new List<Task>();
            foreach (Issue issue in Issues)
            {
                if (issue.State == ItemState.Open)
                {
                    tasks.Add(Task.Run((Func<Task>) (async () =>
                    {
                        IReadOnlyList<IssueComment> comments = await client.Issue.Comment.GetAllForIssue(Owner, Name, issue.Number);
                        foreach (IssueComment comment in comments)
                            IssueComments.Add(comment);
                    })));
                }
            }
            Task.WaitAll(tasks.ToArray());
        }

        public void LoadIssues(IEnumerable<int> issueNumbers)
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(s_GitHubProductIdentifier));
            if (AuthenticationToken != null)
            {
                client.Credentials = new Credentials(AuthenticationToken);
            }

            List<Issue> issues = new List<Issue>();
            foreach (int issueNumber in issueNumbers)
            {
                Issue issue = null;
                Task.Run((Func<Task>) (async () =>
                {
                    issue = await client.Issue.Get(Owner, Name, issueNumber);
                })).Wait();
                issues.Add(issue);
            }

            Issues = issues;
        }

        public static void SerializeToFile(string fileName, IReadOnlyCollection<Octokit.Issue> issues)
        {
            SerializeToFile(fileName, (object)issues);
        }

        public static void SerializeToFile(string fileName, IReadOnlyCollection<Octokit.IssueComment> issueComments)
        {
            SerializeToFile(fileName, (object)issueComments);
        }

        public static void SerializeToFile(string fileName, IEnumerable<DataModelIssue> issues)
        {
            SerializeToFile(fileName, (object)issues);
        }

        private static void SerializeToFile(string fileName, object objToSerialize)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;

            using (StreamWriter sw = new StreamWriter(fileName))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, objToSerialize);
            }

        }

        public override string ToString()
        {
            if (FilterQuery == null)
            {
                return RepoName;
            }

            return $"{RepoName} filtered by '{FilterQuery}'";
        }
    }
}
