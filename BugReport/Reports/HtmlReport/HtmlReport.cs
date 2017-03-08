using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;
using BugReport.Query;

namespace BugReport.Reports
{
    public class HtmlReport
    {
        private IEnumerable<Alert> _alerts;
        private IEnumerable<NamedQuery> _labels;

        public HtmlReport(string configFileName)
        {
            ConfigLoader loader = new ConfigLoader();
            IEnumerable<Label> labels;
            loader.Load(configFileName, out _alerts, out labels);

            _labels = labels.Select(label => new NamedQuery(label.Name, new ExpressionLabel(label.Name)));
        }

        public void Write(IssueCollection issuesCollection, string outputHtmlFile)
        {
            List<NamedQuery> columns = new List<NamedQuery>();
            columns.Add(new NamedQuery("2.0 issues", "is:issue AND is:open AND milestone:2.0.0"));
            columns.Add(new NamedQuery("All issues", "is:issue AND is:open"));

            IEnumerable<DataModelIssue> issues = issuesCollection.Issues.Where(i => i.IsIssueOrComment);
            using (StreamWriter file = new StreamWriter(outputHtmlFile))
            {
                file.WriteLine("<html><body>");
                file.WriteLine("<h2>Alerts</h2>");
                Report(file, issues, columns, _alerts.OrderBy(alert => alert.Name));
                file.WriteLine("<h2>Areas</h2>");
                Report(file, issues, columns, _labels.OrderBy(labelQuery => labelQuery.Name));
                file.WriteLine("</body></html>");
            }
        }

        public void Report(
            StreamWriter file, 
            IEnumerable<DataModelIssue> issues,
            IEnumerable<NamedQuery> columns,
            IEnumerable<NamedQuery> rows)
        {
            file.WriteLine("<table border=\"1\">");
            file.WriteLine("<tr>");
            ReportTableRow(file, "  ", 
                "&nbsp;", 
                columns.Select(col => $"<b>{col.Name}</b"));
            file.WriteLine("</tr>");

            foreach (NamedQuery row in rows)
            {
                file.WriteLine("<tr>");
                ReportTableRow(file, "  ",
                    $"<b>{row.Name}</b>",
                    columns.Select(col => GetQueryCountLinked(Expression.And(row.Query, col.Query), issues)));
                file.WriteLine("</tr>");
            }

            file.WriteLine("<tr>");
            ReportTableRow(file, "  ", 
                "<b>Total</b>", 
                columns.Select(col => $"<b>{GetQueryCountLinked(col.Query, issues)}</b>"));
            file.WriteLine("</tr>");
            file.WriteLine("</table>");
        }

        private string GetQueryCountLinked(Expression query, IEnumerable<DataModelIssue> issues)
        {
            int count = query.Evaluate(issues).Count();

            string gitHubQueryURL = query.GetGitHubQueryURL();
            if (gitHubQueryURL != null)
            {
                return $"<a href=\"{GitHubQuery.GetHyperLink(gitHubQueryURL)}\">{count}</a>";
            }
            return count.ToString();
        }

        private void ReportTableRow(StreamWriter file, string prefix, string col1, IEnumerable<string> cols)
        {
            ReportTableRow(file, prefix, new string[] { col1 }.Concat(cols));
        }
        private void ReportTableRow(StreamWriter file, string prefix, IEnumerable<string> cols)
        {
            foreach (string col in cols)
            {
                file.WriteLine($"{prefix}<td>{col}</td>");
            }
        }
    }
}
