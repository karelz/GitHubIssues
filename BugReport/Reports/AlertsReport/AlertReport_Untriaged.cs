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

        [Flags]
        private enum UntriagedFlags
        {
            UntriagedLabel = 0x1,
            MissingMilestone = 0x2,
            MissingAreaLabel = 0x4,
            MissingIssueTypeLabel = 0x8,
            MultipleIssueTypeLabels = 0x10,
            MultipleAreaLabels = 0x20
        }

        private string UntriagedTypeToString(UntriagedFlags flags)
        {
            string ret = "";
            foreach (UntriagedFlags untriagedFlag in Enum.GetValues(typeof(UntriagedFlags)))
            {
                if ((flags & untriagedFlag) != 0)
                {
                    if (ret != "")
                    {
                        ret += ", ";
                    }
                    ret += untriagedFlag.ToString();
                }
            }
            return ret;
        }

        public AlertReport_Untriaged(Alert alert, bool sendEmail, string htmlTemplateFileName) : 
            base(alert, sendEmail, htmlTemplateFileName)
        {
        }

        /// <summary>
        /// Returns true if the body is filled from this method
        /// </summary>
        public override bool FillReportBody(IssueCollection collection1, IssueCollection collection2)
        {
            IEnumerable<DataModelIssue> matchingIssues = _alert.Query.Evaluate(collection1);
            Dictionary<DataModelIssue, UntriagedFlags> untriaged = new Dictionary<DataModelIssue, UntriagedFlags>();
            foreach (DataModelIssue issue in matchingIssues)
            {
                UntriagedFlags type = IssueIsUntriaged(issue);
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

        private UntriagedFlags IssueIsUntriaged(DataModelIssue issue)
        {
            UntriagedFlags triage = 0;

            // Check if this issue is marked as 'untriaged'
            if (issue.Labels.ContainsLabel("untriaged"))
                triage |= UntriagedFlags.UntriagedLabel;

            // check if this issue has a Milestone
            if (issue.Milestone == null)
                triage |= UntriagedFlags.MissingMilestone;

            // Count area labels
            int areaLabelsCount = issue.Labels.Where(label => label.Name.StartsWith("area-")).Count();
            if (areaLabelsCount == 0)
            {
                triage |= UntriagedFlags.MissingAreaLabel;
            }
            else if (areaLabelsCount > 1)
            {
                triage |= UntriagedFlags.MultipleAreaLabels;
            }

            // Count issue labels
            int issueTypeLabelsCount = issue.Labels.Select(label => label.Name).Intersect(_issueTypeLabels).Count();
            if (issueTypeLabelsCount == 0)
            {
                triage |= UntriagedFlags.MissingIssueTypeLabel;
            }
            else if (issueTypeLabelsCount > 1)
            {
                triage |= UntriagedFlags.MultipleIssueTypeLabels;
            }

            return triage;
        }

        private string FormatIssueTable_Untriaged(Dictionary<DataModelIssue, UntriagedFlags> issues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table>");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Problem</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("  </tr>");
            foreach (KeyValuePair<DataModelIssue, UntriagedFlags> issue in issues)
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
