using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;
using BugReport.Reports;

class Program
{
    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  cache - will cache all GitHub issues into file Issues_YYY-MM-DD@HH-MM.json");
        Console.WriteLine("  report <input.json> <output.html> - Creates report of GitHub issues from cached .json file");
        Console.WriteLine("  diff <input1.json> <input2.json> <config.json> <out.html> - Creates diff report of GitHub issues between 2 cached .json files");
        Console.WriteLine("  alerts <input1.json> <input2.json> <alerts.xml> - Sends alert emails based on .xml config");
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
            if (args[0].Equals("alerts", StringComparison.OrdinalIgnoreCase) && (args.Length == 4))
            {
                SendAlerts(args[1], args[2], args[3]);
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

    static void DiffReport(string input1JsonFileName, string input2JsonFileName, string configJsonFileName, string outputHtmlFileName)
    {
        DiffReport report = new DiffReport(
            IssueCollection.LoadFrom(input1JsonFileName, IssueKindFlags.Issue),
            IssueCollection.LoadFrom(input2JsonFileName, IssueKindFlags.Issue));
        report.Report(configJsonFileName, outputHtmlFileName);
    }

    static void HtmlReport(string inputJsonFileName, string outputHtmlFileName)
    {
        HtmlReport report = new HtmlReport();
        report.Write(IssueCollection.LoadFrom(inputJsonFileName), outputHtmlFileName);
    }

    static void SendAlerts(string input1JsonFileName, string input2JsonFileName, string alertsXmlFileName)
    {
        AlertsReport report = new AlertsReport(alertsXmlFileName);
        report.SendEmails(
            IssueCollection.LoadFrom(input1JsonFileName),
            IssueCollection.LoadFrom(input2JsonFileName));
    }

    static void QueryReport(string inputJsonFileName, string congifgXmlFileName, string outputHtmlFileName)
    {
        IssueCollection issues = IssueCollection.LoadFrom(inputJsonFileName);

        using (StreamWriter output = new StreamWriter(outputHtmlFileName))
        {
            output.WriteLine("<html><body>");
            XElement rootElement = XElement.Load(congifgXmlFileName);
            foreach (XElement queryElement in rootElement.Descendants("query"))
            {
                string queryText = queryElement.Value;
                QueryParser queryParser = new QueryParser(queryText);
                Expression queryExpression = queryParser.Parse();

                queryExpression.Validate(issues);
                IEnumerable<Issue> queryIssues = issues.Issues.Where(i => queryExpression.Evaluate(i));

                output.WriteLine("<p>");
                output.WriteLine("Query: {0}", queryText);
                output.WriteLine("<br/>");
                output.WriteLine("Issues: <a href=\"{1}\">{0}</a>", queryIssues.Count(), GitHubQuery.GetHyperLink(queryIssues));
                output.WriteLine("</p>");

                output.WriteLine("<table border =\"1\">");
                foreach (Issue issue in queryIssues)
                {
                    string issueLink = issue.HtmlUrl;
                    output.WriteLine("  <tr>");
                    output.WriteLine("    <td><a href=\"{0}\">#{1}</a></td>", issue.HtmlUrl, issue.Number);
                    output.WriteLine("    <td>{0}</td>", issue.Title);
                    if (issue.Assignee != null)
                    {
                        output.WriteLine("    <td><a href=\"{0}\">@{1}</a></td>", issue.Assignee.HtmlUrl, issue.Assignee.Login);
                    }
                    else
                    {
                        output.WriteLine("    <td>&nbsp;</td>");
                    }
                    output.WriteLine("  </tr>");
                }
                output.WriteLine("</table>");
            }
            output.WriteLine("</body></html>");
        }
    }
}
