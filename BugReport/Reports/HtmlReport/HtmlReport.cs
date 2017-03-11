using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Config _config;
        private IEnumerable<NamedQuery> _areaLabelQueries;

        public HtmlReport(string configFileName)
        {
            _config = new Config(configFileName);

            _areaLabelQueries = _config.AreaLabels.Select(label => new NamedQuery(label.Name, new ExpressionLabel(label.Name))).ToList();
        }

        public void Write(IssueCollection issuesCollection, string outputHtmlFile)
        {
            IEnumerable<DataModelIssue> issues = issuesCollection.Issues.Where(i => i.IsIssueOrComment);
            using (StreamWriter file = new StreamWriter(outputHtmlFile))
            {
                file.WriteLine("<html><body>");
                file.WriteLine("<h2>Alerts - sorted by issue count</h2>");
                NamedQuery firstQuery = _config.Queries.First();
                Report(file, issues, _config.Queries, 
                    _config.Alerts.OrderByDescending(alert => Expression.And(alert.Query, firstQuery.Query).Evaluate(issues).Count()));
                file.WriteLine("<h2>Alerts - sorted alphabetically</h2>");
                Report(file, issues, _config.Queries, _config.Alerts.OrderBy(alert => alert.Name));
                file.WriteLine("<h2>Areas - sorted alphabetically</h2>");
                Report(file, issues, _config.Queries, _areaLabelQueries.OrderBy(labelQuery => labelQuery.Name));
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
                    columns.Select(col => GetQueryCountLinked_Multiple(row.Query, col.Query, issues)));
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
        private string GetQueryCountLinked_Multiple(Expression rowQuery, Expression colQuery, IEnumerable<DataModelIssue> issues)
        {
            Expression query = Expression.And(rowQuery, colQuery);
            int count = query.Evaluate(issues).Count();

            string gitHubQueryURL = query.GetGitHubQueryURL();
            if (gitHubQueryURL != null)
            {
                return $"<a href=\"{GitHubQuery.GetHyperLink(gitHubQueryURL)}\">{count}</a>";
            }
            else
            {
                if ((colQuery.GetGitHubQueryURL() != null) && (rowQuery is ExpressionOr))
                {
                    IEnumerable<Expression> expressions = ((ExpressionOr)rowQuery.Simplify()).Expressions;
                    if ((expressions.Count() <= 4) && 
                        !expressions.Where(e => e.GetGitHubQueryURL() == null).Any())
                    {
                        return 
                            $"{count} <small>(" +
                            string.Join("+", expressions.Select(
                                expr =>
                                {
                                    Expression subQuery = Expression.And(expr, colQuery);
                                    int subCount = subQuery.Evaluate(issues).Count();
                                    string subQueryURL = subQuery.GetGitHubQueryURL();
                                    Debug.Assert(subQueryURL != null);
                                    return $"<a href=\"{GitHubQuery.GetHyperLink(subQueryURL)}\">{subCount}</a>";
                                })) +
                            ")</small>";
                    }
                }
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
