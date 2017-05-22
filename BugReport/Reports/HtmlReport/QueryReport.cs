using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using GitHubBugReport.Core.DataModel;

namespace BugReport.Reports
{
    public class QueryReport
    {
        private Config _config;

        private IEnumerable<string> _beginFiles;
        private IEnumerable<string> _endFiles;

        private IEnumerable<DataModelIssue> _beginIssues;
        private IEnumerable<DataModelIssue> _endIssues;

        public QueryReport(Config config, IEnumerable<string> beginFiles, IEnumerable<string> endFiles)
        {
            _config = config;

            _beginFiles = beginFiles;
            _endFiles = endFiles;

            _beginIssues = IssueCollection.LoadIssues(beginFiles, config);
            _endIssues = IssueCollection.LoadIssues(endFiles, config);
        }

        public void Write(string outputHtmlFile, string outputJsonFile)
        {
            using (StreamWriter file = new StreamWriter(outputHtmlFile))
            {
                file.WriteLine(
@"<html>
<head>
    <style>
        table
        {
            /* border-collapse: collapse; */
            border: 1px solid black;
        }
        table td
        {
            border: 1px solid black;
        }
        table th
        {
            border: 1px solid black;
        }
        div.labels
        {
            color: #808080;
            font-style: italic;
            margin-left: 1cm;
        }
        tr.stable
        {   /* no color change - should be white */
        }
        tr.new
        {
            background-color: #f0fff0; /* light green */
        }
        tr.gone
        {
            background-color: #fff0f0; /* light red */
        }
    </style>
</head>
<body>");

                file.WriteLine($"Report created on {DateTime.Now}<br/>");
                if (_beginFiles != null)
                {
                    file.WriteLine("/begin");
                    file.WriteLine("<ul>");
                    foreach (string fileName in _beginFiles)
                    {
                        file.WriteLine($"    <li>{fileName}</li>");
                    }
                    file.WriteLine("</ul>");
                }

                file.WriteLine("/end");
                file.WriteLine("<ul>");
                foreach (string fileName in _endFiles)
                {
                    file.WriteLine($"    <li>{fileName}</li>");
                }
                file.WriteLine("</ul>");

                foreach (NamedQuery query in _config.Queries)
                {
                    IEnumerable<DataModelIssue> queryBeginIssues = query.Query.Evaluate(_beginIssues).ToArray();
                    IEnumerable<DataModelIssue> queryEndIssues = query.Query.Evaluate(_endIssues).ToArray();

                    file.WriteLine($"<h2>Query: {query.Name}</h2>");
                    file.WriteLine($"<p>Query: <code>{query.Query}</code></p>");
                    file.WriteLine("Count: " +
                        HtmlQueryCountLink.Create(
                            query.Query, 
                            queryEndIssues,
                            shouldHyperLink: true, 
                            useRepositoriesFromIssues: true) + 
                        "<br/>");
                    file.WriteLine(FormatIssueTable(queryBeginIssues, queryEndIssues));
                }

                file.WriteLine("</body></html>");
            }

            if (outputJsonFile != null)
            {
                Repository.SerializeToFile(outputJsonFile, _endIssues);
            }
        }

        private static string FormatIssueTable(IEnumerable<DataModelIssue> beginIssues, IEnumerable<DataModelIssue> endIssues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table border=\"1\">");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Status</th>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("    <th>Milestone</th>");
            text.AppendLine("  </tr>");

            FormatIssues(text, endIssues.Except_ByIssueNumber(beginIssues), IssueStatus.New);
            FormatIssues(text, endIssues.Intersect_ByIssueNumber(beginIssues), IssueStatus.Stable);
            FormatIssues(text, beginIssues.Except_ByIssueNumber(endIssues), IssueStatus.Gone);

            text.AppendLine("</table>");

            return text.ToString();
        }

        private enum IssueStatus
        {
            Stable, // exists at both the beginning and at the end
            New,
            Gone
        }

        private static void FormatIssues(StringBuilder text, IEnumerable<DataModelIssue> issues, IssueStatus issueStatus)
        {
            string statusText;
            string issueStyle;
            switch (issueStatus)
            {
                case IssueStatus.Stable:
                    statusText = "&nbsp;";
                    issueStyle = "stable";
                    break;
                case IssueStatus.New:
                    statusText = "NEW";
                    issueStyle = "new";
                    break;
                case IssueStatus.Gone:
                    statusText = "GONE";
                    issueStyle = "gone";
                    break;
                default:
                    throw new InvalidProgramException();
            }

            IEnumerable<IssueEntry> issueEntries = issues
                .OrderBy(i => i.IsPullRequest ? 1 : 0)
                .ThenBy(i => i.Number)
                .Select(i => new IssueEntry(i));
            foreach (IssueEntry issue in issueEntries)
            {
                text.AppendLine($"  <tr class=\"{issueStyle}\">");
                text.AppendLine($"    <td>{statusText}</td>");
                text.AppendLine($"    <td>{issue.IssueId}</td>");
                text.AppendLine("    <td>");
                text.AppendLine($"      {HttpUtility.HtmlEncode(issue.Title)}");
                if (issue.LabelsText != null)
                {
                    text.AppendLine($"      <br/><div class=\"labels\">Labels: {issue.LabelsText}</div>");
                }
                text.AppendLine("    </td>");
                text.AppendLine($"    <td>{issue.AssignedToText}</td>");
                text.AppendLine($"    <td>{issue.MilestoneText}</td>");
                text.AppendLine("  </tr>");
            }
        }
    }
}
