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

public class Repository
{
    public string Owner { get; private set; }
    public string Name { get; private set; }
    public Expression FilterQuery { get; private set; }
    // owner/name lowercase
    public string RepoName { get; private set; }
    public bool IsRepoName(string repoName) => (RepoName == repoName.ToLower());
    // https://github.com/owner/name/
    public string HtmlUrlPrefix { get; private set; }
    public string AuthenticationToken { get; set; }

    private Repository(string repoName, string filterQuery)
    {
        RepoName = repoName.ToLower();
        Debug.Assert(_repositories.Where(repo => (repo.RepoName == RepoName)).None());

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

    public static Repository From(string repoName, string filterQuery = null)
    {
        Repository repo = FindRepo(repoName);
        if (repo == null)
        {
            repo = new Repository(repoName, filterQuery);
            Debug.Assert(FindRepo(repoName) == repo);
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
        return From(urlSplit[0] + "/" + urlSplit[1], null);
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
            Task.Run(async () =>
            {
                issue = await client.Issue.Get(Owner, Name, issueNumber);
            }).Wait();
            issues.Add(issue);
        }

        Issues = issues;
    }

    public void SerializeToFile(string fileName, object objToSerialize)
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
