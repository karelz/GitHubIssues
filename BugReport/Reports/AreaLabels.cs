using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public static class Extensions
    {
        public static IEnumerable<Label> GetAreaLabels(this IssueCollection issues)
        {
            foreach (Label label in issues.Labels)
            {
                if (label.Name.StartsWith("area-") || (label.Name == "Infrastructure"))
                {
                    yield return label;
                }
            }
        }
    }
}
