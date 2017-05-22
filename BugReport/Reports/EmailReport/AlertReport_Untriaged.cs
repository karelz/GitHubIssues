using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using BugReport.Util;
using GitHubBugReport.Core.DataModel;
using GitHubBugReport.Core.Issues.Models;

namespace BugReport.Reports.EmailReports
{
    public class AlertReport_Untriaged
    {
        public static bool SendEmails(
            Config config,
            string htmlTemplateFileName,
            bool skipEmail,
            string outputHtmlFileName,
            IEnumerable<string> filteredAlertNames,
            IEnumerable<string> inputFiles)
        {
            IEnumerable<DataModelIssue> issues = IssueCollection.LoadIssues(
                inputFiles,
                config,
                IssueKindFlags.Issue);

            return AlertReport.SendEmails(
                config,
                htmlTemplateFileName,
                skipEmail,
                outputHtmlFileName,
                filteredAlertNames,
                (Alert alert, string htmlTemplate) =>
                    GenerateReport(alert, htmlTemplate, issues, inputFiles, config.UntriagedExpression));
        }

        // Returns null if the report is empty
        protected static string GenerateReport(
            Alert alert,
            string htmlTemplate,
            IEnumerable<DataModelIssue> issues,
            IEnumerable<string> inputFiles,
            ExpressionUntriaged untriagedExpression)
        {
            IEnumerable<DataModelIssue> matchingIssues = alert.Query.Evaluate(issues);
            var untriagedFlagsMap = new Dictionary<DataModelIssue, ExpressionUntriaged.Flags>();
            foreach (DataModelIssue issue in matchingIssues)
            {
                ExpressionUntriaged.Flags flags = untriagedExpression.GetUntriagedFlags(issue);
                if (flags != 0)
                {
                    untriagedFlagsMap[issue] = flags;
                }
            }

            if (untriagedFlagsMap.None())
            {
                Console.WriteLine("    No untriaged issues, skipping.");
                Console.WriteLine();
                return null;
            }

            string text = htmlTemplate;

            text = text.Replace("%UNTRIAGED_ISSUES_START%", "");
            text = text.Replace("%UNTRIAGED_ISSUES_END%", "");

            text = text.Replace("%UNTRIAGED_ISSUES_LINKED_COUNTS%", 
                AlertReport.GetLinkedCount("is:issue is:open", untriagedFlagsMap.Keys));

            IEnumerable<IssueEntry> untriagedIssueEntries = untriagedFlagsMap.Keys.Select(issue => new IssueEntry(issue));
            text = text.Replace("%UNTRIAGED_ISSUES_TABLE%", FormatIssueTable(untriagedFlagsMap));

            text = text.Replace("%INPUT_FILES_LIST%", FormatInputFilesList(inputFiles));

            return text;
        }

        private static string UntriagedTypeToString(ExpressionUntriaged.Flags flags)
        {
            return string.Join(", ", ExpressionUntriaged.EnumerateFlags(flags));
        }

        private static string FormatIssueTable(Dictionary<DataModelIssue, ExpressionUntriaged.Flags> issuesMap)
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine("<table>");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Problem</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("    <th>Milestone</th>");
            text.AppendLine("  </tr>");

            foreach (KeyValuePair<DataModelIssue, ExpressionUntriaged.Flags> mapEntry in issuesMap)
            {
                IssueEntry issue = new IssueEntry(mapEntry.Key);
                text.AppendLine("  <tr>");
                text.AppendLine($"    <td>{issue.IssueId}</td>");
                text.AppendLine($"    <td>{UntriagedTypeToString(mapEntry.Value)}</td>");
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

        private static string FormatInputFilesList(IEnumerable<string> inputFiles)
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine("<ul>");
            foreach (string fileName in inputFiles)
            {
                text.AppendLine($"    <li>{fileName}</li>");
            }
            text.AppendLine("</ul>");

            return text.ToString();
        }
    }
}
