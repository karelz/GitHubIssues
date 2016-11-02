using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Octokit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Repository
{
    public string Owner { get; private set; }
    public string Name { get; private set; }

    public Repository(string alertsXmlFileName)
    {
        XElement root = XElement.Load(alertsXmlFileName);

        IEnumerable<XElement> repositoryNodes = root.Descendants("repository");
        if (!repositoryNodes.Any())
        {
            throw new InvalidDataException("Missing 'repository' node");
        }
        if (repositoryNodes.Count() > 1)
        {
            throw new InvalidDataException("Duplicate 'repository' node defined");
        }

        string repoName = repositoryNodes.First().Attribute("name").Value;
        string[] repoNameParts = repoName.Split('/');
        if (repoNameParts.Length != 2)
        {
            throw new InvalidDataException("Invalid repository name format: " + repoName);
        }
        Owner = repoNameParts[0];
        Name = repoNameParts[1];
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
