using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Repository
{
    public string Owner { get; private set; }
    public string Name { get; private set; }

    public Repository(string owner, string name)
    {
        Owner = owner;
        Name = name;
    }

    public static string s_GitHubProductIdentifier = "GitHubBugReporter";

    public IReadOnlyList<Issue> Issues;

    public void LoadIssues()
    {
        GitHubClient client = new GitHubClient(new ProductHeaderValue(s_GitHubProductIdentifier));

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

    public void LoadIssues(IEnumerable<int> issueNumbers)
    {
        GitHubClient client = new GitHubClient(new ProductHeaderValue(s_GitHubProductIdentifier));

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

    public void SerializeIssues(string fileName)
    {
        JsonSerializer serializer = new JsonSerializer();
        serializer.Formatting = Formatting.Indented;

        using (StreamWriter sw = new StreamWriter(fileName))
        using (JsonWriter writer = new JsonTextWriter(sw))
        {
            serializer.Serialize(writer, Issues);
        }
    }
}

public static class IssueExtensions
{
    public static void PrintIssue(this Issue issue)
    {
        Console.WriteLine("Number: {0}", issue.Number);
        Console.WriteLine("PullRequest: {0}", (issue.PullRequest == null) ? "Issue" : ("PullRequest: " + issue.PullRequest.ToString()));
        Console.WriteLine("State: {0}", issue.State);
        Console.WriteLine("Assignee.Name:  {0}", (issue.Assignee == null) ? "<null>" : issue.Assignee.Name);
        Console.WriteLine("        .Login: {0}", (issue.Assignee == null) ? "<null>" : issue.Assignee.Login);
        Console.WriteLine("Labels.Name:");
        foreach (Label label in issue.Labels)
        {
            Console.WriteLine("    {0}", label.Name);
        }
        Console.WriteLine("Title: {0}", issue.Title);
        Console.WriteLine("Milestone.Title: {0}", (issue.Milestone == null) ? "<null>" : issue.Milestone.Title);
        Console.WriteLine("User.Name:  {0}", (issue.User == null) ? "<null>" : issue.User.Name);
        Console.WriteLine("    .Login: {0}", (issue.User == null) ? "<null>" : issue.User.Login);
        Console.WriteLine("CreatedAt: {0}", issue.CreatedAt);
        Console.WriteLine("UpdatedAt: {0}", issue.UpdatedAt);
        Console.WriteLine("ClosedAt:  {0}", issue.ClosedAt);
        Console.WriteLine("ClosedBy.Name:  {0}", (issue.ClosedBy == null) ? "<null>" : issue.ClosedBy.Name);
        Console.WriteLine("        .Login: {0}", (issue.ClosedBy == null) ? "<null>" : issue.ClosedBy.Login);
    }

    /*
    public static void SerializeIssue(this Issue issue, string fileName)
    {
        JsonSerializer serializer = new JsonSerializer();
        serializer.Formatting = Formatting.Indented;

        using (StreamWriter sw = new StreamWriter(fileName))
        using (JsonWriter writer = new JsonTextWriter(sw))
        {
            serializer.Serialize(writer, issue);
        }
    }
    */
}
