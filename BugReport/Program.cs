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
        Console.WriteLine("  cache <alerts.xml> - will cache all GitHub issues into file Issues_YYY-MM-DD@HH-MM.json");
        Console.WriteLine("  report <input.json> <output.html> - Creates report of GitHub issues from cached .json file");
        Console.WriteLine("  diff <input1.json> <input2.json> <config.json> <out.html> - Creates diff report of GitHub issues between 2 cached .json files");
        Console.WriteLine("  alerts <input1.json> <input2.json> <emailTemplate.html> <alerts.xml> [<alert_name>] - Sends alert emails based on .xml config, optinally filtered to just alert_name");
        Console.WriteLine("      alerts_SkipEmail or set SEND_EMAIL=0 - Won't send any emails");
    }

    static void Main(string[] args)
    {
        try
        {
            if (args.Length >= 1)
            {
                if (args[0].Equals("cache", StringComparison.OrdinalIgnoreCase) && (args.Length == 2))
                {
                    CacheGitHubIssues(args[1]);
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
                if ((args[0].Equals("alerts", StringComparison.OrdinalIgnoreCase)
                     || args[0].Equals("alerts_SkipEmail", StringComparison.OrdinalIgnoreCase))
                    && ((args.Length == 5) || (args.Length == 6)))
                {
                    bool skipEmail = args[0].Equals("alerts_SkipEmail", StringComparison.OrdinalIgnoreCase);
                    string alertName = (args.Length == 6) ? args[5] : null;
                    SendAlerts(args[1], args[2], args[3], args[4], alertName, skipEmail);
                    return;
                }
            }
            PrintUsage();
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Catastrophic failure:");
            Console.WriteLine(ex);
        }
    }

    static void CacheGitHubIssues(string alertsXmlFileName)
    {
        Repository repo = new Repository(alertsXmlFileName);
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

    static void SendAlerts(string input1JsonFileName, string input2JsonFileName, string htmlTemplateFileName, string alertsXmlFileName, string alertName, bool skipEmail)
    {
        AlertsReport report = new AlertsReport(alertsXmlFileName, skipEmail);
        report.SendEmails(
            IssueCollection.LoadFrom(input1JsonFileName),
            IssueCollection.LoadFrom(input2JsonFileName),
            htmlTemplateFileName,
            alertName);
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
