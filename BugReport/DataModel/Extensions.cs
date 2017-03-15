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
        public static IEnumerable<DataModelIssue> Except_ByIssueNumber(this IEnumerable<DataModelIssue> issues, IEnumerable<DataModelIssue> exceptIssues)
        {
            return issues.Where(i => !exceptIssues.Where(i2 => i2.EqualsByNumber(i)).Any());
        }
        public static IEnumerable<DataModelIssue> Where(this IEnumerable<DataModelIssue> issues, Repository repo)
        {
            return issues.Where(i => (i.Repo == repo));
        }

        public static DataModelIssue FirstOrNull_ByIssueNumber(this IEnumerable<DataModelIssue> issues, DataModelIssue issue)
        {
            return issues.Where(i => i.EqualsByNumber(issue)).FirstOrDefault();
        }

        public static bool ContainsLabel(this IEnumerable<Label> labels, string labelName)
        {
            return labels.Where(l => l.Name == labelName).Any();
        }

        public static IEnumerable<Label> Intersect_ByName(this IEnumerable<Label> labels, IEnumerable<Label> labels2)
        {
            return labels.Where(label => labels2.ContainsLabel(label.Name));
        }
    }
}
