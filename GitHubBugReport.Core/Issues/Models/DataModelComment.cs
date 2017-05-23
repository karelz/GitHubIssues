using System;

namespace GitHubBugReport.Core.Issues.Models
{
    public class DataModelComment
    {
        // TODO: Define members.
        public String Body { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public Uri HtmlUrl { get; set; }
        public int Id { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public Uri Url { get; set; }
        public Octokit.User User { get; set; } // TODO: This is leaking. We need to create our own DataModelUser.
    }
}