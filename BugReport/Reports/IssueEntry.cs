using System.Collections.Generic;
using System.Linq;
using GitHubBugReport.Core.Issues.Extensions;
using GitHubBugReport.Core.Issues.Models;

namespace BugReport.Reports
{
    public class IssueEntry
    {
        public string IssueId { get; private set; }
        public string Title { get; private set; }
        public string LabelsText { get; private set; }
        public string AssignedToText { get; private set; }
        public string MilestoneText { get; private set; }

        // assignedToOverride - used for using 'Closed' as AssignedTo value in reports
        public IssueEntry(DataModelIssue issue, IEnumerable<Label> styledLabels = null, string styleName = null)
        {
            string idPrefix = "";
            if (issue.IsPullRequest)
            {
                idPrefix = "PR ";
            }

            IssueId = $"{idPrefix}#<a href=\"{issue.HtmlUrl}\">{issue.Number}</a>";

            Title = issue.Title;

            styledLabels = styledLabels ?? new Label[] { };

            LabelsText = string.Join(", ", issue.Labels.Select(l =>
                styledLabels.Contains_ByName(l.Name) ? $"<span class=\"{styleName}\">{l.Name}</span>" : l.Name));

            if (issue.Assignee != null)
            {
                AssignedToText = $"<a href=\"{issue.Assignee.HtmlUrl}\">@{issue.Assignee.Login}</a>";
            }
            else
            {
                AssignedToText = "";
            }

            if (issue.Milestone != null)
            {
                MilestoneText = issue.Milestone.Title;
            }
            else
            {
                MilestoneText = "";
            }
        }
    }
}
