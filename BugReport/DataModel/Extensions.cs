using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BugReport.DataModel
{
    public static class Extensions
    {
        public static void AddIfNotNull<T>(this List<T> list, T value)
        {
            if (value != null)
            {
                list.Add(value);
            }
        }

        public static bool ContainsIssue(this IEnumerable<DataModelIssue> issues, int issueId)
        {
            return issues.Where(i => i.Number == issueId).Any();
        }
        public static IEnumerable<DataModelIssue> Intersect(this IEnumerable<DataModelIssue> issues1, IssueCollection issues2)
        {
            return issues1.Where(i => issues2.HasIssue(i.Number));
        }
        public static IEnumerable<DataModelIssue> Except(this IEnumerable<DataModelIssue> issues, IssueCollection exceptIssues)
        {
            return issues.Where(i => !exceptIssues.HasIssue(i.Number));
        }
        public static IEnumerable<DataModelIssue> Except(this IEnumerable<DataModelIssue> issues, IEnumerable<DataModelIssue> exceptIssues)
        {
            return issues.Where(i => !exceptIssues.Where(i2 => (i2.Number == i.Number)).Any());
        }
        public static IEnumerable<DataModelIssue> ExceptByNumber(this IEnumerable<DataModelIssue> issues, IEnumerable<DataModelIssue> exceptIssues)
        {
            return issues.Where(i => !exceptIssues.ContainsIssue(i.Number));
        }
        public static bool ContainsLabel(this IEnumerable<Label> labels, string labelName)
        {
            return labels.Where(l => l.Name == labelName).Any();
        }
        public static bool ContainsLabel(this DataModelIssue issue, IEnumerable<Label> labels)
        {
            return issue.Labels.Intersect(labels).Any();
        }
        public static IEnumerable<Label> ExceptByName(this IEnumerable<Label> labels, IEnumerable<Label> exceptLabels)
        {
            return labels.Where(l => !exceptLabels.ContainsLabel(l.Name));
        }
    }
}
