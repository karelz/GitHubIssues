using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;
using System.Diagnostics;

namespace BugReport.Reports
{
    public class AlertReport_Diff : AlertReport
    {
        public AlertReport_Diff(Alert alert, bool sendEmail, string htmlTemplateFileName) 
            : base(alert, sendEmail, htmlTemplateFileName)
        {
        }

        /// <summary>
        /// Returns true if the body is filled from this method
        /// </summary>
        public override bool FillReportBody(IEnumerable<DataModelIssue> beginIssues, IEnumerable<DataModelIssue> endIssues)
        {
            IEnumerable<DataModelIssue> beginQuery = _alert.Query.Evaluate(beginIssues);
            IEnumerable<DataModelIssue> endQuery = _alert.Query.Evaluate(endIssues);
            IEnumerable<DataModelIssue> goneIssues = beginQuery.Except_ByIssueNumber(endQuery);
            IEnumerable<DataModelIssue> newIssues = endQuery.Except_ByIssueNumber(beginQuery);

            if (!goneIssues.Any() && !newIssues.Any())
            {
                Console.WriteLine("    No changes to the query, skipping.");
                Console.WriteLine();
                return false;
            }

            if (!goneIssues.Any() || !newIssues.Any())
            {
                Regex regex = new Regex("%ALL_ISSUES_START%(.|\n)*%ALL_ISSUES_END%");
                BodyText = regex.Replace(BodyText, "");

                if (!goneIssues.Any())
                {
                    regex = new Regex("%GONE_ISSUES_START%(.|\n)*%GONE_ISSUES_END%");
                    BodyText = regex.Replace(BodyText, "");
                }
                if (!newIssues.Any())
                {
                    regex = new Regex("%NEW_ISSUES_START%(.|\n)*%NEW_ISSUES_END%");
                    BodyText = regex.Replace(BodyText, "");
                }
            }
            BodyText = BodyText.Replace("%ALL_ISSUES_START%", "");
            BodyText = BodyText.Replace("%ALL_ISSUES_END%", "");
            BodyText = BodyText.Replace("%GONE_ISSUES_START%", "");
            BodyText = BodyText.Replace("%GONE_ISSUES_END%", "");
            BodyText = BodyText.Replace("%NEW_ISSUES_START%", "");
            BodyText = BodyText.Replace("%NEW_ISSUES_END%", "");

            BodyText = BodyText.Replace("%ALL_ISSUES_LINK%", GitHubQuery.GetHyperLink(newIssues.Concat(goneIssues)));
            BodyText = BodyText.Replace("%ALL_ISSUES_COUNT%", (goneIssues.Count() + newIssues.Count()).ToString());
            BodyText = BodyText.Replace("%GONE_ISSUES_LINK%", GitHubQuery.GetHyperLink(goneIssues));
            BodyText = BodyText.Replace("%GONE_ISSUES_COUNT%", goneIssues.Count().ToString());
            BodyText = BodyText.Replace("%NEW_ISSUES_LINK%", GitHubQuery.GetHyperLink(newIssues));
            BodyText = BodyText.Replace("%NEW_ISSUES_COUNT%", newIssues.Count().ToString());

            IEnumerable<IssueEntry> newIssueEntries = newIssues.Select(issue => new IssueEntry(issue));
            BodyText = BodyText.Replace("%NEW_ISSUES_TABLE%", FormatIssueTable(newIssueEntries));
            IEnumerable<IssueEntry> goneIssueEntries = goneIssues.Select(issue =>
            {
                DataModelIssue newIssue = endIssues.FirstOrNull_ByIssueNumber(issue);
                if (newIssue == null)
                {   // Closed issue
                    return new IssueEntry(issue, "Closed");
                }
                return new IssueEntry(newIssue);
            });
            BodyText = BodyText.Replace("%GONE_ISSUES_TABLE%", FormatIssueTable(goneIssueEntries));
            return true;
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
