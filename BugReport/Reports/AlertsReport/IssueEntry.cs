using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public struct IssueEntry
    {
        public string IssueId;
        public string Title;
        public string LabelsText;
        public string AssignedToText;
        public string MilestoneText;

        // assignedToOverride - used for using 'Closed' as AssignedTo value in reports
        public IssueEntry(DataModelIssue issue, string assignedToOverride = null)
        {
            string idPrefix = "";
            if (issue.IsPullRequest)
            {
                idPrefix = "PR ";
            }

            IssueId = string.Format("{0}#<a href=\"{1}\">{2}</a>", idPrefix, issue.HtmlUrl, issue.Number);

            Title = issue.Title;

            LabelsText = string.Join(", ", issue.Labels.Select(l => l.Name));

            if (assignedToOverride != null)
            {
                AssignedToText = assignedToOverride;
            }
            else if (issue.Assignee != null)
            {
                AssignedToText = string.Format("<a href=\"{0}\">@{1}</a>", issue.Assignee.HtmlUrl, issue.Assignee.Login);
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
