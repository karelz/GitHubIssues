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
    public class ExpressionUntriaged : Expression
    {
        private IEnumerable<Label> _issueTypeLabels;
        private IEnumerable<Label> _areaLabels;
        private IEnumerable<Label> _untriagedLabels;

        public ExpressionUntriaged(
            IEnumerable<Label> issueTypeLabels, 
            IEnumerable<Label> areaLabels, 
            IEnumerable<Label> untriagedLabels)
        {
            _issueTypeLabels = issueTypeLabels;
            _areaLabels = areaLabels;
            _untriagedLabels = untriagedLabels;
        }

        [Flags]
        public enum Flags
        {
            UntriagedLabel = 0x1,
            MissingMilestone = 0x2,
            MissingAreaLabel = 0x4,
            MissingIssueTypeLabel = 0x8,
            MultipleIssueTypeLabels = 0x10,
            MultipleAreaLabels = 0x20
        }

        public static IEnumerable<Flags> EnumerateFlags(Flags flags)
        {
            foreach (Flags flag in Enum.GetValues(typeof(Flags)))
            {
                if ((flags & flag) != 0)
                {
                    yield return flag;
                }
            }
        }

        public Flags GetUntriagedFlags(DataModelIssue issue)
        {
            Flags triageFlags = 0;

            // Check if this issue is marked as 'untriaged'
            if (issue.Labels.IntersectByName(_untriagedLabels).Any())
            {
                triageFlags |= Flags.UntriagedLabel;
            }

            // check if this issue has a Milestone
            if (issue.Milestone == null)
            {
                triageFlags |= Flags.MissingMilestone;
            }

            // Count area labels
            int areaLabelsCount = issue.Labels.IntersectByName(_areaLabels).Count();
            if (areaLabelsCount == 0)
            {
                triageFlags |= Flags.MissingAreaLabel;
            }
            else if (areaLabelsCount > 1)
            {
                triageFlags |= Flags.MultipleAreaLabels;
            }

            // Count issue labels
            int issueTypeLabelsCount = issue.Labels.IntersectByName(_issueTypeLabels).Count();
            if (issueTypeLabelsCount == 0)
            {
                triageFlags |= Flags.MissingIssueTypeLabel;
            }
            else if (issueTypeLabelsCount > 1)
            {
                triageFlags |= Flags.MultipleIssueTypeLabels;
            }

            return triageFlags;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return GetUntriagedFlags(issue) != 0;
        }

        public override void Validate(IssueCollection collection)
        {
            // Nothing to validate
        }

        public override string GetGitHubQueryURL()
        {
            return null;
        }
    }

    public class AlertReport_Untriaged : AlertReport
    {
        private ExpressionUntriaged _untriagedExpression;

        public AlertReport_Untriaged(
            Alert alert, 
            bool sendEmail, 
            string htmlTemplateFileName, 
            ExpressionUntriaged untriagedExpression)
            : base(alert, sendEmail, htmlTemplateFileName)
        {
            _untriagedExpression = untriagedExpression;
        }

        /// <summary>
        /// Returns true if the body is filled from this method
        /// </summary>
        public override bool FillReportBody(IssueCollection collection1, IssueCollection collection2)
        {
            IEnumerable<DataModelIssue> matchingIssues = _alert.Query.Evaluate(collection1);
            var untriagedFlagsMap = new Dictionary<DataModelIssue, ExpressionUntriaged.Flags>();
            foreach (DataModelIssue issue in matchingIssues)
            {
                ExpressionUntriaged.Flags flags = _untriagedExpression.GetUntriagedFlags(issue);
                if (flags != 0)
                {
                    untriagedFlagsMap[issue] = flags;
                }
            }

            if (!untriagedFlagsMap.Any())
            {
                Console.WriteLine("    No untriaged issues, skipping.");
                Console.WriteLine();
                return false;
            }

            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_START%", "");
            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_END%", "");

            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_LINK%", GitHubQuery.GetHyperLink(untriagedFlagsMap.Keys));
            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_COUNT%", untriagedFlagsMap.Count().ToString());

            IEnumerable<IssueEntry> untriagedIssueEntries = untriagedFlagsMap.Keys.Select(issue => new IssueEntry(issue));
            BodyText = BodyText.Replace("%UNTRIAGED_ISSUES_TABLE%", FormatIssueTable_Untriaged(untriagedFlagsMap));
            return true;
        }

        private string UntriagedTypeToString(ExpressionUntriaged.Flags flags)
        {
            return string.Join(", ", ExpressionUntriaged.EnumerateFlags(flags));
        }

        private string FormatIssueTable_Untriaged(Dictionary<DataModelIssue, ExpressionUntriaged.Flags> issuesMap)
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
