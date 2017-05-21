using System;

namespace GitHubBugReport.Core.Issues.Models
{
    public class Milestone
    {
        public int Number;
        public string Title;
        public string Description;
        public int OpenIssues;
        public int ClosedIssues;
        public Octokit.ItemState State;
        public User Creator;
        public DateTimeOffset CreatedAt;
        public DateTimeOffset? DueOn;
        public DateTimeOffset? ClosedAt;

        public bool Equals(string title)
        {
            return TitleComparer.Equals(Title, title);
        }

        public static StringComparer TitleComparer = StringComparer.InvariantCultureIgnoreCase;
    }
}
