using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Newtonsoft.Json;
using BugReport.DataModel;
using BugReport.Query;
using BugReport.Util;
using System;
using System.Threading;

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

    private Repository(string repoName, string alias, string filterQuery)
    {
        RepoName = repoName.ToLower();
        Debug.Assert(_repositories.Where(repo => (repo.RepoName == RepoName)).None());
        Alias = (alias != null) ? alias.ToLower() : RepoName;
        Debug.Assert(_repositories.Where(repo => (repo.Alias == Alias)).None());

        string[] repoNameParts = RepoName.Split('/');
        if ((repoNameParts.Length != 2) ||
            string.IsNullOrEmpty(repoNameParts[0]) ||
            string.IsNullOrEmpty(repoNameParts[1]))
        {
            throw new InvalidDataException($"Invalid repository name format in repo '{repoName}'");
        }

        Owner = repoNameParts[0];
        Name = repoNameParts[1];

        HtmlUrlPrefix = _htmlUrlGitHubPrefix + RepoName + "/";
        _repositories.Add(this);

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

    // TODO - Move to config
    private static readonly string s_GitHubProductIdentifier = "GitHubBugReporter";

    public IReadOnlyList<Issue> Issues { get; private set; }
    public ConcurrentBag<IssueComment> IssueComments { get; private set; }

    public string GetQueryUrl(string queryArgs)
    {
        return HtmlUrlPrefix + "issues?q=" + System.Net.WebUtility.UrlEncode(queryArgs);
    }

    public string GetQueryUrl(string queryPrefix, IEnumerable<DataModelIssue> issues)
    {
        return GetQueryUrl(queryPrefix + " " + string.Join(" ", issues.Select(i => i.Number)));
    }

    // Captures order of definitions
    private static List<Repository> _repositories = new List<Repository>();
    public static IEnumerable<Repository> Repositories
    {
        get => _repositories;
    }

    private static Repository FindRepo(string repoName)
    {
        return _repositories.Where(repo => repo.IsRepoName(repoName)).FirstOrDefault();
    }

    private static Repository FindRepoByAlias(string alias)
    {
        return _repositories.Where(repo => repo.IsAlias(alias)).FirstOrDefault();
    }

    public static Repository From(string repoName, string alias = null, string filterQuery = null)
    {
        Repository repo = FindRepo(repoName) ?? FindRepoByAlias(repoName);
        if (repo == null)
        {
            repo = new Repository(repoName, alias, filterQuery);
            Debug.Assert(FindRepo(repoName) == repo);
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

    private static readonly string _htmlUrlGitHubPrefix = "https://github.com/";
    public static Repository FromHtmlUrl(string htmlUrl)
    {
        Debug.Assert(htmlUrl.StartsWith(_htmlUrlGitHubPrefix));
        string[] urlSplit = htmlUrl.Substring(_htmlUrlGitHubPrefix.Length).Split('/');
        if ((urlSplit.Length < 2) || 
            string.IsNullOrEmpty(urlSplit[0]) || 
            string.IsNullOrEmpty(urlSplit[1]))
        {
            throw new InvalidDataException($"Invalid GitHub URL '{htmlUrl}', can't parse repo name");
        }
        return From(urlSplit[0] + "/" + urlSplit[1]);
    }

    // Returns repos in order of their definition, or the first one as default
    public static IEnumerable<Repository> GetReposOrDefault(IEnumerable<DataModelIssue> issues)
    {
        Repository[] repos = issues.Select(i => i.Repo).ToArray();
        if (repos.Length == 0)
        {
            yield return _repositories[0];
        }
        else
        {   // Return repos in order of their definitions
            foreach (Repository repo in _repositories)
            {
                if (repos.Contains(repo))
                {
                    yield return repo;
                }
            }
        }
    }

    public GitHubClient CreateGitHubClient()
    {
        var client = new GitHubClient(new ProductHeaderValue(s_GitHubProductIdentifier));
        if (AuthenticationToken != null)
        {
            client.Credentials = new Credentials(AuthenticationToken);
        }
        return client;
    }

    /// <summary>
    /// Gets all of the issues in the repository, closed and open
    /// </summary>
    public void LoadIssues()
    {
        GitHubClient client = CreateGitHubClient();

        RepositoryIssueRequest issueRequest = new RepositoryIssueRequest
        {
            State = ItemStateFilter.Open,
            Filter = IssueFilter.All
        };

        Task.Run(async () =>
        {
            Issues = await client.Issue.GetAllForRepository(Owner, Name, issueRequest);
        }).Wait();
    }

    public void LoadSubscribedIssues()
    {
        GitHubClient client = CreateGitHubClient();

        RepositoryIssueRequest issueRequest = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All,
            Filter = IssueFilter.Subscribed
        };

        Task.Run(async () =>
        {
            Issues = await client.Issue.GetAllForRepository(Owner, Name, issueRequest);
        }).Wait();
    }

    /// <summary>
    /// Get all comment info for each open issue in the repo
    /// </summary>
    public void LoadIssueComments()
    {
        GitHubClient client = CreateGitHubClient();

        IssueComments = new ConcurrentBag<IssueComment>();
        List<Task> tasks = new List<Task>();
        foreach (Issue issue in Issues)
        {
            if (issue.State == ItemState.Open)
            {
                tasks.Add(Task.Run(async () =>
                {
                    IReadOnlyList<IssueComment> comments = await client.Issue.Comment.GetAllForIssue(Owner, Name, issue.Number);
                    foreach (IssueComment comment in comments)
                        IssueComments.Add(comment);
                }));
            }
        }
        Task.WaitAll(tasks.ToArray());
    }

    // Returns list of issue numbers which failed to load
    public IEnumerable<int> LoadIssues(IEnumerable<int> issueNumbers)
    {
        GitHubClient client = CreateGitHubClient();

        List<int> issuesNotFound = new List<int>();
        List<Issue> issues = new List<Issue>();
        foreach (int issueNumber in issueNumbers)
        {
            Issue issue = null;
            Exception error = null;
            Task.Run(async () =>
            {
                try
                {
                    issue = await client.Issue.Get(Owner, Name, issueNumber);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }).Wait();

            if (error == null)
            {
                issues.Add(issue);
            }
            else
            {
                if (error is NotFoundException)
                {
                    Console.WriteLine($"NotFoundException #{issueNumber}");
                    issuesNotFound.Add(issueNumber);
                }
                else if (error is RateLimitExceededException)
                {
                    Console.WriteLine($"RateLimitExceededException #{issueNumber}");
                    issuesNotFound.Add(issueNumber);
                    break;  // No point to continue donwloading more ...
                }
                else
                {
                    Console.WriteLine($"Unknown ERROR for issue #{issueNumber} -- {error}");
                    issuesNotFound.Add(issueNumber);
                }
            }
        }

        Issues = issues;

        return issuesNotFound;
    }

    public void SetIssueSubscriptions(IEnumerable<int> issueIds, bool subscribe)
    {
        GitHubClient client = CreateGitHubClient();
        var tasks = new List<Task>();

        int completedOps = 0;
        bool doneAdding = false;

        foreach (int id in issueIds)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.Activity.Notifications.SetThreadSubscription(id, new NewThreadSubscription
                    {
                        Subscribed = subscribe
                    }).ConfigureAwait(false);
                }
                finally
                {
                    int x = Interlocked.Increment(ref completedOps);

                    if ((x % 10) == 0 && Volatile.Read(ref doneAdding) == true)
                    {
                        double pcnt = completedOps / (double)Math.Max(tasks.Count, 1);
                        Console.WriteLine($"Subscribed to {completedOps:N0} issues out of {tasks.Count:N0} ({pcnt:P})");
                    }
                }
            }));
        }

        Volatile.Write(ref doneAdding, true);
        Task.WaitAll(tasks.ToArray());

        Console.WriteLine($"Subscription update: {completedOps:N0}/{tasks.Count:N0} (100 %)");
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
        serializer.Converters.Add(new StringEnumOfItemStateConverter());
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

    internal class StringEnumOfItemStateConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(StringEnum<ItemState>));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return new StringEnum<ItemState>((string)reader.Value);
        }
    }
}
