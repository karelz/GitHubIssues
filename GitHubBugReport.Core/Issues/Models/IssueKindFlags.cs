using System;

namespace GitHubBugReport.Core.Issues.Models
{
    [Flags]
    public enum IssueKindFlags
    {
        Issue = 1,
        PullRequest = 2,
        Comment = 4,
        All = Issue | PullRequest | Comment
    }
}
