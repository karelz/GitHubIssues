using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class GitHubQuery
    {
        public static void GetHyperLink(StringBuilder sb, IEnumerable<DataModelIssue> issues)
        {
            sb.Append("https://github.com/dotnet/corefx/issues?q=");
            foreach (DataModelIssue i in issues)
            {
                sb.AppendFormat("{0}%20", i.Number);
            }
        }
        public static string GetHyperLink(IEnumerable<DataModelIssue> issues)
        {
            StringBuilder link = new StringBuilder();
            GetHyperLink(link, issues);
            return link.ToString();
        }
    }
}
