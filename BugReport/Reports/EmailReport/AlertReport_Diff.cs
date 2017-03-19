using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BugReport.DataModel;
using BugReport.Util;
using BugReport.Query;

namespace BugReport.Reports.EmailReports
{
    public class AlertReport_Diff
    {
        public static bool SendEmails(
            Config config,
            string htmlTemplateFileName,
            bool skipEmail,
            string outputHtmlFileName,
            IEnumerable<string> filteredAlertNames,
            IEnumerable<DataModelIssue> beginIssues,
            IEnumerable<DataModelIssue> endIssues)
        {
            return AlertReport.SendEmails(
                config,
                htmlTemplateFileName,
                skipEmail,
                outputHtmlFileName,
                filteredAlertNames,
                (Alert alert, string htmlTemplate) => 
                    GenerateReport(alert, htmlTemplate, beginIssues, endIssues));
        }

        // Returns null if the report is empty
        protected static string GenerateReport(
            Alert alert, 
            string htmlTemplate, 
            IEnumerable<DataModelIssue> beginIssues, 
            IEnumerable<DataModelIssue> endIssues)
        {
            IEnumerable<DataModelIssue> beginQuery = alert.Query.Evaluate(beginIssues);
            IEnumerable<DataModelIssue> endQuery = alert.Query.Evaluate(endIssues);

            IEnumerable<DataModelIssue> goneIssues = beginQuery.Except_ByIssueNumber(endQuery);
            IEnumerable<DataModelIssue> newIssues = endQuery.Except_ByIssueNumber(beginQuery);

            if (goneIssues.None() && newIssues.None())
            {
                Console.WriteLine("    No changes to the query, skipping.");
                Console.WriteLine();
                return null;
            }

            string text = htmlTemplate;

            if (goneIssues.None() || newIssues.None())
            {
                Regex regex = new Regex("%ALL_ISSUES_START%(.|\n)*%ALL_ISSUES_END%");
                text = regex.Replace(text, "");

                if (goneIssues.None())
                {
                    regex = new Regex("%GONE_ISSUES_START%(.|\n)*%GONE_ISSUES_END%");
                    text = regex.Replace(text, "");
                }
                if (newIssues.None())
                {
                    regex = new Regex("%NEW_ISSUES_START%(.|\n)*%NEW_ISSUES_END%");
                    text = regex.Replace(text, "");
                }
            }
            text = text.Replace("%ALL_ISSUES_START%", "");
            text = text.Replace("%ALL_ISSUES_END%", "");
            text = text.Replace("%GONE_ISSUES_START%", "");
            text = text.Replace("%GONE_ISSUES_END%", "");
            text = text.Replace("%NEW_ISSUES_START%", "");
            text = text.Replace("%NEW_ISSUES_END%", "");

            text = text.Replace("%ALL_ISSUES_LINKED_COUNTS%", 
                AlertReport.GetLinkedCount("is:issue is:open", newIssues.Concat(goneIssues)));
            text = text.Replace("%GONE_ISSUES_LINKED_COUNTS%",
                AlertReport.GetLinkedCount("is:issue is:open", goneIssues));
            text = text.Replace("%NEW_ISSUES_LINKED_COUNTS%",
                AlertReport.GetLinkedCount("is:issue is:open", newIssues));

            IEnumerable<IssueEntry> newIssueEntries = newIssues.Select(issue => new IssueEntry(issue));
            text = text.Replace("%NEW_ISSUES_TABLE%", FormatIssueTable(newIssueEntries));
            IEnumerable<IssueEntry> goneIssueEntries = goneIssues.Select(issue =>
            {
                DataModelIssue newIssue = endIssues.FirstOrNull_ByIssueNumber(issue);
                if (newIssue == null)
                {   // Closed issue
                    return new IssueEntry(issue, "Closed");
                }
                return new IssueEntry(newIssue);
            });
            text = text.Replace("%GONE_ISSUES_TABLE%", FormatIssueTable(goneIssueEntries));

            return text;
        }

        private static string FormatIssueTable(IEnumerable<IssueEntry> issues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table>");
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
