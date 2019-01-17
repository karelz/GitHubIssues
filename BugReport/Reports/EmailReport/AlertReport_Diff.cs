using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using BugReport.DataModel;
using BugReport.Util;

namespace BugReport.Reports.EmailReports
{
    public class AlertReport_Diff
    {
        public static bool DetectLargeChanges(
            IEnumerable<DataModelIssue> beginIssues, 
            IEnumerable<DataModelIssue> endIssues, 
            Config config)
        {
            IEnumerable<DataModelIssue> goneIssues = beginIssues.Except_ByIssueNumber(endIssues).ToList();
            IEnumerable<DataModelIssue> newIssues = endIssues.Except_ByIssueNumber(beginIssues).ToList();

            foreach (Repository repo in config.Repositories)
            {
                int goneIssuesCount = goneIssues.Where(repo).Count();
                int newIssuesCount = newIssues.Where(repo).Count();

                if (goneIssuesCount > config.IssuesMinimalCount &&
                    beginIssues.Count() > 0 &&
                    ((double)goneIssuesCount / (double)beginIssues.Count()) > config.IssuesMaximumRatio)
                {
                    return true;
                }
                if (newIssuesCount > config.IssuesMinimalCount &&
                    endIssues.Count() > 0 &&
                    ((double)newIssuesCount / (double)endIssues.Count()) > config.IssuesMaximumRatio)
                {
                    return true;
                }
            }
            return false;
        }

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
                    GenerateReport(alert, htmlTemplate, beginIssues, endIssues, config.AreaLabels));
        }

        // Returns null if the report is empty
        protected static string GenerateReport(
            Alert alert,
            string htmlTemplate,
            IEnumerable<DataModelIssue> beginIssues,
            IEnumerable<DataModelIssue> endIssues,
            IEnumerable<Label> areaLabels)
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

            text = text.Replace("%ISSUES_LINKED_COUNTS%", 
                AlertReport.GetLinkedCount("", newIssues.Concat(goneIssues)));

            IEnumerable<IssueEntry> newIssueEntries = newIssues
                .Select(issue => new IssueEntry(issue, IssueEntry.EntryKind.New));
            IEnumerable<IssueEntry> goneIssueEntries = goneIssues
                .Select(issue =>
                {
                    DataModelIssue newIssue = endIssues.FirstOrNull_ByIssueNumber(issue);
                    if (newIssue == null)
                    {   // Closed issue
                        return new IssueEntry(issue, IssueEntry.EntryKind.Closed);
                    }
                    return new IssueEntry(newIssue, IssueEntry.EntryKind.Moved, areaLabels, "moved_area");
                });
            text = text.Replace("%ISSUES_TABLE%", FormatIssueTable(newIssueEntries.Concat(goneIssueEntries)
                .OrderBy(entry => entry.OrderType)
                .ThenBy(entry => entry.IssueNumber)));

            return text;
        }

        public class IssueEntry : Reports.IssueEntry
        {
            public bool IsPullRequest { get; private set; }
            public EntryKind Kind { get; private set; }
            public int IssueNumber { get; private set; }

            public string Class => GetKindClassPrefix(Kind) + "_" + (IsPullRequest ? "pr" : "issue");
            public string Status => Kind.ToString();

            // In order of EntryKind, PR go first
            public int OrderType => (int)Kind * 2 + (IsPullRequest ? 0 : 1);

            public enum EntryKind
            {
                // Order used in OrderType
                // Names used in Status
                New = 0,
                Moved = 1,
                Closed = 2
            }

            public IssueEntry(
                DataModelIssue issue, EntryKind kind, 
                IEnumerable<Label> styledLabels = null, 
                string styleName = null)
                    : base(issue, styledLabels, styleName)
            {
                Kind = kind;
                IsPullRequest = issue.IsPullRequest;
                IssueNumber = issue.Number;
            }

            private static string GetKindClassPrefix(EntryKind kind)
            {
                if (kind == EntryKind.New)
                {
                    return "new";
                }
                if ((kind == EntryKind.Moved) || (kind == EntryKind.Closed))
                {
                    return "gone";
                }
                throw new InvalidProgramException($"Unexpected issue kind {kind}");
            }
        }

        private static string FormatIssueTable(IEnumerable<IssueEntry> issues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table>");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Status</th>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("    <th>Milestone</th>");
            text.AppendLine("  </tr>");
            foreach (IssueEntry issue in issues)
            {
                text.AppendLine($"  <tr class=\"{issue.Class}\">");
                text.AppendLine($"    <td>{issue.Status}</td>");
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
