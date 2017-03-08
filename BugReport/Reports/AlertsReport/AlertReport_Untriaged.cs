using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class AlertReport_Untriaged : AlertReport
    {
        private readonly string[] _issueTypeLabels = { "bug", "test bug", "enhancement", "test enhancement", "api-needs-work", "api-ready-for-review", "api-approved", "documentation", "question" };

        private enum UntriagedType
        {
            UntriagedLabel = 1,
            MissingMilestone = 2,
            MissingAreaLabel = 4,
            MissingIssueTypeLabel = 8
        }

        private string UntriagedTypeToString(UntriagedType type)
        {
            string ret = "";
            foreach (UntriagedType enumVal in Enum.GetValues(typeof(UntriagedType)))
                ret += ((type & enumVal) > 0 ? (ret == "" ? enumVal.ToString() : ", " + enumVal.ToString()) : "");
            return ret;
        }

        public AlertReport_Untriaged(Alert alert, bool sendEmail, string htmlTemplateFileName) : base(alert, sendEmail, htmlTemplateFileName)
        {
        }

        /// <summary>
        /// Returns true if the body is filled from this method
        /// </summary>
        public override bool FillReportBody(IssueCollection collection1, IssueCollection collection2)
        {
            IEnumerable<DataModelIssue> matchingIssues = _alert.Query.Evaluate(collection1);
            Dictionary<DataModelIssue, UntriagedType> untriaged = new Dictionary<DataModelIssue, UntriagedType>();
            foreach (DataModelIssue issue in matchingIssues)
            {
                UntriagedType type = IssueIsUntriaged(issue);
                if (type != 0)
                    untriaged[issue] = type;
            }

            if (!untriaged.Any())
            {
                Console.WriteLine("    No untriaged issues, skipping.");
                Console.WriteLine();
                return false;
            }

            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_START%", "");
            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_END%", "");

            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_LINK%", GitHubQuery.GetHyperLink(untriaged.Keys));
            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_COUNT%", untriaged.Count().ToString());

            IEnumerable<IssueEntry> untriagedIssueEntries = untriaged.Select(issue => new IssueEntry(issue.Key));
            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_TABLE%", FormatIssueTable_Untriaged(untriaged));
            return true;
        }

        private UntriagedType IssueIsUntriaged(DataModelIssue issue)
        {
            UntriagedType triage = 0;

            // Check if this issue is marked as 'untriaged'
            if (issue.Labels.ContainsLabel("untriaged"))
                triage |= UntriagedType.UntriagedLabel;

            // check if this issue has a Milestone
            if (issue.Milestone == null)
                triage |= UntriagedType.MissingMilestone;

            // Check if this issue has an area label
            if (issue.Labels.FirstOrDefault((label) => label.Name.StartsWith("area-")) == default(Label))
                triage |= UntriagedType.MissingAreaLabel;

            // Check if this issue has an issue-type label
            if (issue.Labels.Select((label) => label.Name).Intersect(_issueTypeLabels).Count() == 0)
                triage |= UntriagedType.MissingIssueTypeLabel;

            return triage;
        }

        private string FormatIssueTable_Untriaged(Dictionary<DataModelIssue, UntriagedType> issues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table>");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Problem</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("  </tr>");
            foreach (KeyValuePair<DataModelIssue, UntriagedType> issue in issues)
            {
                IssueEntry entry = new IssueEntry(issue.Key);
                text.AppendLine("  <tr>");
                text.AppendFormat("    <td>{0}</td>", entry.IssueId).AppendLine();
                text.AppendFormat("    <td>{0}</td>", UntriagedTypeToString(issue.Value)).AppendLine();
                text.AppendLine("    <td>");
                text.AppendFormat("      {0}", entry.Title).AppendLine();
                if (entry.LabelsText != null)
                {
                    text.AppendFormat("      <br/><div class=\"labels\">Labels: {0}</div>", entry.LabelsText).AppendLine();
                }
                text.AppendLine("    </td>");
                text.AppendFormat("    <td>{0}</td>", entry.AssignedToText).AppendLine();
                text.AppendLine("  </tr>");
            }
            text.AppendLine("</table>");

            return text.ToString();
        }
    }
}
