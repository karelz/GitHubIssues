using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.Util;

namespace BugReport.DataModel
{
    public static class Extensions
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
        public static DataModelIssue First_ByIssueNumber(this IEnumerable<DataModelIssue> issues, DataModelIssue issue)
        {
            return issues.Where(i => issue.EqualsByNumber(i)).First();
        }
        public static DataModelIssue Last_ByIssueNumber(this IEnumerable<DataModelIssue> issues, DataModelIssue issue)
        {
            return issues.Where(i => issue.EqualsByNumber(i)).Last();
        }

        public static DataModelIssue FirstOrNull_ByIssueNumber(this IEnumerable<DataModelIssue> issues, DataModelIssue issue)
        {
            return issues.Where(i => i.EqualsByNumber(issue)).FirstOrDefault();
        }

        public static IEnumerable<DataModelIssue> DistinctFirst_ByIssueNumber(this IEnumerable<DataModelIssue> issues)
        {
            return issues.Where(i => (i == issues.First_ByIssueNumber(i)));
        }
        public static IEnumerable<DataModelIssue> DistinctLast_ByIssueNumber(this IEnumerable<DataModelIssue> issues)
        {
            return issues.Where(i => (i == issues.Last_ByIssueNumber(i)));
        }

        public static bool Contains_ByName(this IEnumerable<Label> labels, string labelName)
        {
            return labels.Where(l => l.Equals(labelName)).Any();
        }
        public static bool Contains_ByName(this IEnumerable<Label> labels, Label label)
        {
            return labels.Contains_ByName(label.Name);
        }

        public static IEnumerable<Label> Intersect_ByName(this IEnumerable<Label> labels, IEnumerable<Label> labels2)
        {
            return labels.Where(label => labels2.Contains_ByName(label.Name));
        }
    }
}
