using System;
using System.Diagnostics;
using System.IO;
using GitHubBugReport.Core.Issues.Extensions;
using GitHubBugReport.Core.Repositories.Models;

namespace GitHubBugReport.Core.Issues.Models
{
    public class DataModelIssue
    {
        public int Id;
        public int Number;
        public string Title;
        public Octokit.ItemState State;
        public User Assignee;
        public Label[] Labels;
        public User User;
        public string HtmlUrl;
        public DateTimeOffset? CreatedAt;
        public DateTimeOffset? UpdatedAt;
        public DateTimeOffset? ClosedAt;
        public User ClosedBy;
        public PullRequest PullRequest;
        public Milestone Milestone;

        private Repository _repo;
        public Repository Repo
        {
            get
            {
                if (_repo == null)
                {
                    _repo = Repository.FromHtmlUrl(HtmlUrl);
                }

                return _repo;
            }
        }

        public bool IsIssueOrComment => (PullRequest == null);

        public bool IsPullRequest => (PullRequest != null);

        public bool IsIssueKind(IssueKindFlags flags)
        {
            if (IsIssueOrComment)
            {
                if (HtmlUrl.Contains("issuecomment"))
                {
                    return ((flags & IssueKindFlags.Comment) != 0);
                }
                else
                {
                    return ((flags & IssueKindFlags.Issue) != 0);
                }
            }

            Debug.Assert(IsPullRequest);
            return ((flags & IssueKindFlags.PullRequest) != 0);
        }

        public bool IsOpen => (State == Octokit.ItemState.Open);

        public bool IsClosed => (State == Octokit.ItemState.Closed);

        public bool HasLabel(string labelName) => Labels.Contains_ByName(labelName);

        public bool IsMilestone(string milestoneName)
        {
            if (Milestone == null)
            {
                return (milestoneName == null);
            }
            return (Milestone.Title == milestoneName);
        }

        public bool HasAssignee(string assigneeName)
        {
            if (Assignee == null)
            {
                return (assigneeName == null);
            }
            return Assignee.Name.Equals(assigneeName, StringComparison.InvariantCultureIgnoreCase);
        }

        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            sw.WriteLine("Number: {0}", Number);
            sw.WriteLine("Type: {0}", (PullRequest == null) ? "Issue" : "PullRequest");
            sw.WriteLine("URL: {0}", HtmlUrl);
            sw.WriteLine((string) "State: {0}", (object) State);
            sw.WriteLine("Assignee.Name:  {0}", (Assignee == null) ? "<null>" : Assignee.Name);
            sw.WriteLine("        .Login: {0}", (Assignee == null) ? "<null>" : Assignee.Login);
            sw.WriteLine("Labels.Name:");
            foreach (Label label in Labels)
            {
                sw.WriteLine((string) "    {0}", (object) label.Name);
            }
            sw.WriteLine("Title: {0}", Title);
            //sw.WriteLine("Milestone.Title: {0}", (issue.Milestone == null) ? "<null>" : issue.Milestone.Title);
            sw.WriteLine("User.Name:  {0}", (User == null) ? "<null>" : User.Name);
            sw.WriteLine("    .Login: {0}", (User == null) ? "<null>" : User.Login);
            sw.WriteLine("CreatedAt: {0}", CreatedAt);
            sw.WriteLine("UpdatedAt: {0}", UpdatedAt);
            sw.WriteLine("ClosedAt:  {0}", ClosedAt);
            sw.WriteLine("ClosedBy.Name:  {0}", (ClosedBy == null) ? "<null>" : ClosedBy.Name);
            sw.Write("        .Login: {0}", (ClosedBy == null) ? "<null>" : ClosedBy.Login);

            string text = sw.ToString();
            sw.Close();
            return text;
        }

        // Returns true only if the issues represent the same issue number in the same repo
        public bool EqualsByNumber(DataModelIssue issue)
        {
            // Check also HtmlUrl which encodes repo
            return ((Number == issue.Number) && (HtmlUrl == issue.HtmlUrl));
        }
    }
}
