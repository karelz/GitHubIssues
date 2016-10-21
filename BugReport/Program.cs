using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BugReport;
using BugReport.Reports;

class Program
{
    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  cache - will cache all GitHub issues into file Issues_YYY-MM-DD@HH-MM.json");
        Console.WriteLine("  report <input.json> <output.html> - Creates report of GitHub issues from cached .json file");
        Console.WriteLine("  diff <input1.json> <input2.json> <config.json> <out.html> - Creates diff report of GitHub issues between 2 cached .json files");
    }

    static void Main(string[] args)
    {
        if (args.Length >= 1)
        {
            if (args[0].Equals("cache", StringComparison.OrdinalIgnoreCase) && (args.Length == 1))
            {
                CacheGitHubIssues();
                return;
            }
            if (args[0].Equals("report", StringComparison.OrdinalIgnoreCase) && (args.Length == 3))
            {
                HtmlReport(args[1], args[2]);
                return;
            }
            if (args[0].Equals("diff", StringComparison.OrdinalIgnoreCase) && (args.Length == 5))
            {
                DiffReport(args[1], args[2], args[3], args[4]);
                return;
            }
        }
        PrintUsage();
        
        //DeserializeTest("Issues_2016-09-07@16-55.test.json");
    }

    static void CacheGitHubIssues()
    {
        Repository repo = new Repository("dotnet", "corefx");
        repo.LoadIssues();
        repo.SerializeIssues(string.Format("Issues_{0:yyyy-MM-dd@HH-mm}.json", DateTime.Now));
    }

    static void DiffReport(string input1Json, string input2Json, string configJson, string outputHtml)
    {
        DiffReport report = new DiffReport(
            Issue.LoadFrom(input1Json, Issue.IssueKindFlags.Issue),
            Issue.LoadFrom(input2Json, Issue.IssueKindFlags.Issue));
        report.Report(configJson, outputHtml);
    }

    static void HtmlReport(string inputJson, string outputHtml)
    {
        HtmlReport report = new HtmlReport();
        report.Write(Issue.LoadFrom(inputJson), outputHtml);
    }

    static void Report1(string inputJson, string outputHtml)
    {
        IssueCollection issues = Issue.LoadFrom(inputJson, Issue.IssueKindFlags.Issue);

        Console.WriteLine("Stats:");
        Console.WriteLine("All Issues: {0}", issues.Issues.Count());
        Console.WriteLine("Issues: {0}", issues.Issues.Where(i => i.IssueKind == Issue.IssueKindFlags.Issue).Count());
        Console.WriteLine("PullRequests: {0}", issues.Issues.Where(i => i.IssueKind == Issue.IssueKindFlags.PullRequest).Count());
        Console.WriteLine();

        List<Label> ignoredLabels = issues.Labels.Where(l => l.Name.StartsWith("dev/api-")).ToList();

        IEnumerable<Issue> filteredIssues = issues.Issues.Where(i => !i.Labels.Where(l => ignoredLabels.Contains(l)).Any());

        List<Label> systemAreaLabels = issues.Labels.Where(l => l.Name.StartsWith("System") || l.Name.StartsWith("Microsoft.")).ToList();
        systemAreaLabels.Add(issues.GetLabel("Serialization"));
        systemAreaLabels.Add(issues.GetLabel("Meta"));
        systemAreaLabels.Add(issues.GetLabel("tracking-external-issue"));
        List<Label> areaLabels = new List<Label>(systemAreaLabels);
        areaLabels.Add(issues.GetLabel("Infrastructure"));

        {
            // At least 1 area label
            //IEnumerable<Issue> issuesWithAreaLabel = filteredIssues.Where(i => i.Labels.Where(l => areaLabels.Contains(l)).Any());
            //IEnumerable<Issue> issuesWithoutAreaLabel = filteredIssues.Where(i => !i.Labels.Where(l => areaLabels.Contains(l)).Any());
            IEnumerable<Issue> issuesWithMultipleAreaLabels = filteredIssues.Where(i => i.Labels.Where(l => areaLabels.Contains(l)).Count() >= 2);
            IEnumerable<Issue> issuesWithMultipleSystemAreaLabels = filteredIssues.Where(i => i.Labels.Where(l => systemAreaLabels.Contains(l)).Count() >= 2);

            //Console.WriteLine("Issues with 1+ area label: {0}", issuesWithAreaLabel.Count());
            //Console.WriteLine("Issues with 0 area labels: {0}", issuesWithoutAreaLabel.Count());
            Console.WriteLine("Issues with multiple area labels: {0}", issuesWithMultipleAreaLabels.Count());
            Console.WriteLine("Issues with multiple System area labels: {0}", issuesWithMultipleSystemAreaLabels.Count());
            Console.WriteLine();

            /*
            Console.WriteLine("==============================================");
            Console.WriteLine("Lables in issues without area labels");
            Console.WriteLine("==============================================");
            IEnumerable<IssueCollection.FilteredLabel> labelCountsInIssuesWithoutAreaLabel = IssueCollection.FilterLabels(issuesWithoutAreaLabel);
            foreach (IssueCollection.FilteredLabel label in labelCountsInIssuesWithoutAreaLabel.OrderByDescending(l => l.Issues.Count))
            {
                Console.WriteLine("{0} ({2}) - {1}", label.Issues.Count, label.Label.Name, label.Label.Issues.Count);
            }
            */
            /*
            string queryText = "is:issue is:open" + areaLabels.Select(l => " -label:" + l.Name).Aggregate((a, b) => string.Concat(a, b));
            string queryWithoutAreaLabel = string.Format("https://github.com/dotnet/corefx/issues?utf8=%E2%9C%93&q={0}", System.Web.HttpUtility.UrlEncode(queryText));

            using (StreamWriter file = new StreamWriter(outputHtml))
            {
                file.WriteLine("<html><body>");
                file.WriteLine("<p>");
                file.WriteLine("<b>Issues:</b>");
                file.WriteLine("<a href=\"{0}\">GitHub query</a>", queryWithoutAreaLabel);
                file.WriteLine("<br/>");
                //file.WriteLine("<b>Query text:</b>");
                //file.WriteLine("{0}", queryText);
                file.WriteLine("<br/>");
                ReportIssues(issuesWithoutAreaLabel, file);
                file.WriteLine("</body></html>");
            }
            */
            /*
            Console.WriteLine("==============================================");
            Console.WriteLine("Multiple area labels");
            Console.WriteLine("==============================================");
            ReportIssues(issuesWithMultipleAreaLabels);

            Console.WriteLine("==============================================");
            Console.WriteLine("Multiple System area labels");
            Console.WriteLine("==============================================");
            ReportIssues(issuesWithMultipleSystemAreaLabels);
            */
        }

        List<Label> issueTypeLabels = new List<Label>();
        issueTypeLabels.Add(issues.GetLabel("documentation"));
        issueTypeLabels.Add(issues.GetLabel("test bug"));
        issueTypeLabels.Add(issues.GetLabel("question"));
        issueTypeLabels.Add(issues.GetLabel(""));
        issueTypeLabels.Add(issues.GetLabel(""));
    }
    static void ReportIssues(IEnumerable<Issue> issues, StreamWriter file)
    {
        file.WriteLine("<table border=\"1\">");
        foreach (Issue issue in issues)
        {
            file.WriteLine("  <tr>");
            file.WriteLine("    <td><a href=\"{0}\">{1}</a></td>", issue.HtmlUrl, issue.Number);
            file.WriteLine("    <td>{0}</td>", issue.Title);
            file.WriteLine("    <td>{0}</td>", string.Join(", ", issue.Labels.Select(l => l.Name)));
            file.WriteLine("  </tr>");
        }
        file.WriteLine("</table>");
    }

    static void ReportIssues(IEnumerable<Issue> issues)
    {
        Console.WriteLine("# of issues: {0}", issues.Count());
        Console.WriteLine();

        foreach (Issue issue in issues)
        {
            Console.WriteLine("{0} - {1}", issue.Number, issue.Title);
            Console.WriteLine("    {0}", issue.HtmlUrl);
            if (issue.Labels.Any())
            {
                Console.WriteLine("    Labels:");
                foreach (Label label in issue.Labels.OrderBy(l => l.Name))
                {
                    Console.WriteLine("        {0}", label.Name);
                }
            }
            Console.WriteLine();
        }
    }

    static void Test()
    {
        int[] issueNumbers = new int[] { 9859, 20, 10, 11519 };
        string fileName = "test.json";
        SerializeTest(issueNumbers, fileName);
        DeserializeTest(fileName);
    }

    static void SerializeTest(IEnumerable<int> issueNumbers, string fileName)
    {
        Repository repo = new Repository("dotnet", "corefx");
        repo.LoadIssues(issueNumbers);
        repo.SerializeIssues(fileName);

        foreach (Octokit.Issue issue in repo.Issues)
        {
            issue.PrintIssue();
            Console.WriteLine();
        }
    }

    static void DeserializeTest(string fileName)
    {
        IEnumerable<Issue> issues = Issue.LoadFrom(fileName).Issues;

        Console.WriteLine("Stats:");
        Console.WriteLine("All Issues: {0}", issues.Count());
        Console.WriteLine("Issues: {0}", issues.Where(i => (i.PullRequest == null)).Count());
        Console.WriteLine("PullRequests: {0}", issues.Where(i => (i.PullRequest != null)).Count());
        Console.WriteLine();

        foreach (Issue issue in issues)
        {
            issue.Print();
            Console.WriteLine();
        }
    }
}
