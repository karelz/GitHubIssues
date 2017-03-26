using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class QueryReport
    {
        Config _config;

        public QueryReport(Config config)
        {
            _config = config;
        }

        public void Write(IEnumerable<DataModelIssue> issues, string outputHtmlFile)
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
    </style>
</head>
<body>");

                foreach (NamedQuery query in _config.Queries)
                {
                    IEnumerable<DataModelIssue> queryIssues = query.Query.Evaluate(issues);

                    file.WriteLine($"<h2>Query: {query.Name}</h2>");
                    file.WriteLine($"<p>Query: <code>{query.Query}</code></p>");
                    file.WriteLine("Count: " +
                        HtmlQueryCountLink.Create(
                            query.Query, 
                            queryIssues, 
                            shouldHyperLink: true, 
                            useRepositoriesFromIssues: true) + 
                        "<br/>");
                    file.WriteLine(FormatIssueTable(queryIssues.Select(issue => new IssueEntry(issue))));
                }

                file.WriteLine("</body></html>");
            }
        }

        private string FormatIssueTable(IEnumerable<IssueEntry> issues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table border=\"1\">");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("    <th>Milestone</th>");
            text.AppendLine("  </tr>");
            foreach (IssueEntry issue in issues)
            {
                text.AppendLine("  <tr>");
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
            text.AppendLine("</table>");

            return text.ToString();
        }
    }
}
