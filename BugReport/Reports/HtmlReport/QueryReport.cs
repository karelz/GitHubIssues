using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;
using BugReport.Query;

namespace BugReport.Reports
{
    public class QueryReport
    {
        public QueryReport(string configFileName)
        {
            ConfigLoader loader = new ConfigLoader();
            loader.Load(configFileName, out _, out _);
        }

        public void Write(IssueCollection issuesCollection, string outputHtmlFile)
        {
            string queryString = @"is:issue AND is:open AND milestone:2.0.0 AND " +
                //"-label:os-windows AND (label:os-linux OR label:mac-os-x) AND 
                @"-label:os-linux AND -label:mac-os-x AND 
                -label:test-run-core AND 
                label:area-System.Net.Security";
                //"(label:area-System.Net OR label:area-System.Net.Sockets OR label:area-System.Net.Security OR label:area-System.Net.Http)";
            Expression query = QueryParser.Parse(queryString);

            IEnumerable<DataModelIssue> issues = query.Evaluate(issuesCollection.Issues);
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
                file.WriteLine("<h2>Query</h2>");
                file.WriteLine($"<p>{queryString}</p>");
                file.WriteLine($"Count: {issues.Count()}<br/>");
                file.WriteLine(FormatIssueTable(issues.Select(issue => new IssueEntry(issue))));
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
                text.AppendLine($"      {issue.Title}");
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
