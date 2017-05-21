using System.Collections.Generic;
using System.Linq;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Repositories.Models;

namespace GitHubBugReport.Core.Issues.Extensions
{
    public static class DataModelExtensions
    {
        public static IEnumerable<DataModelIssue> Except_ByIssueNumber(this IEnumerable<DataModelIssue> issues, IEnumerable<DataModelIssue> exceptIssues)
        {
            return issues.Where(i => !exceptIssues.Contains_ByIssueNumber(i));
        }

        public static bool Contains_ByIssueNumber(this IEnumerable<DataModelIssue> issues, DataModelIssue issue)
        {
            return issues.Where(i => issue.EqualsByNumber(i)).Any();
        }

        public static IEnumerable<DataModelIssue> Intersect_ByIssueNumber(this IEnumerable<DataModelIssue> issues, IEnumerable<DataModelIssue> intersectIssues)
        {
            return issues.Where(i => intersectIssues.Contains_ByIssueNumber(i));
        }

        public static IEnumerable<DataModelIssue> Where(this IEnumerable<DataModelIssue> issues, Repository repo)
        {
            return issues.Where(i => (i.Repo == repo));
        }

        public static DataModelIssue FirstOrNull_ByIssueNumber(this IEnumerable<DataModelIssue> issues, DataModelIssue issue)
        {
            return issues.FirstOrDefault(i => i.EqualsByNumber(issue));
        }

        public static bool Contains_ByName(this IEnumerable<Label> labels, string labelName)
        {
            return labels.Any(l => l.Equals(labelName));
        }

        public static bool Contains_ByName(this IEnumerable<Label> labels, Label label)
        {
            return Contains_ByName(labels, label.Name);
        }

        public static IEnumerable<Label> Intersect_ByName(this IEnumerable<Label> labels, IEnumerable<Label> labels2)
        {
            return labels.Where(label => Contains_ByName(labels2, label.Name));
        }
    }
}
