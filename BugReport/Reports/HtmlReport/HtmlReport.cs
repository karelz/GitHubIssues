using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BugReport.Util;
using BugReport.DataModel;
using BugReport.Query;

namespace BugReport.Reports
{
    public class HtmlReport : Report
    {
        private Config _config;
        private IEnumerable<NamedQuery> _areaLabelQueries;

        public HtmlReport(IEnumerable<string> configFiles)
        {
            _config = new Config(configFiles);

            _areaLabelQueries = _config.AreaLabels
                .Select(label => new NamedQuery(label.Name, new ExpressionLabel(label.Name)))
                .ToList();
        }

        public void Write(IEnumerable<string> beginFiles, IEnumerable<string> endFiles, string outputHtmlFile)
        {
            IEnumerable<DataModelIssue> beginIssuesAll = IssueCollection.LoadIssues(beginFiles, _config.LabelAliases);
            IEnumerable<DataModelIssue> endIssuesAll = IssueCollection.LoadIssues(endFiles, _config.LabelAliases);

            IEnumerable<DataModelIssue> beginIssues = beginIssuesAll.Where(i => i.IsIssueOrComment).ToArray();
            IEnumerable<DataModelIssue> endIssues = endIssuesAll.Where(i => i.IsIssueOrComment).ToArray();
            using (StreamWriter file = new StreamWriter(outputHtmlFile))
            {
                file.WriteLine("<html><body>");
                file.WriteLine($"Report create on {DateTime.Now}<br/>");

                file.WriteLine("/begin");
                file.WriteLine("<ul>");
                foreach (string fileName in beginFiles)
                {
                    file.WriteLine($"    <li>{fileName}</li>");
                }
                file.WriteLine("</ul>");

                file.WriteLine("/end");
                file.WriteLine("<ul>");
                foreach (string fileName in endFiles)
                {
                    file.WriteLine($"    <li>{fileName}</li>");
                }
                file.WriteLine("</ul>");

                file.WriteLine("<h2>Alerts - sorted by issue count</h2>");
                NamedQuery firstQuery = _config.Queries.First();
                Report(file, 
                    beginIssues, 
                    endIssues, 
                    _config.Queries, 
                    _config.Alerts.OrderByDescending(alert => 
                        Expression.And(alert.Query, firstQuery.Query).Evaluate(endIssues).Count()));

                file.WriteLine("<h2>Alerts - sorted alphabetically</h2>");
                Report(file, 
                    beginIssues, 
                    endIssues, 
                    _config.Queries, 
                    _config.Alerts.OrderBy(alert => alert.Name));

                file.WriteLine("<h2>Areas - sorted alphabetically</h2>");
                Report(file, 
                    beginIssues, 
                    endIssues, 
                    _config.Queries, 
                    _areaLabelQueries.OrderBy(labelQuery => labelQuery.Name));

                file.WriteLine("<h2>Alerts - sorted alphabetically (no links)</h2>");
                Report(file, 
                    beginIssues, 
                    endIssues, 
                    _config.Queries, 
                    _config.Alerts.OrderBy(alert => alert.Name), 
                    shouldHyperLink: false);

                file.WriteLine("<h2>Areas - sorted alphabetically (no links)</h2>");
                Report(file, 
                    beginIssues, 
                    endIssues, 
                    _config.Queries, 
                    _areaLabelQueries.OrderBy(labelQuery => labelQuery.Name),
                    shouldHyperLink: false);

                file.WriteLine("</body></html>");
            }
        }

        public void Report(
            StreamWriter file,
            IEnumerable<DataModelIssue> beginIssues,
            IEnumerable<DataModelIssue> endIssues,
            IEnumerable<NamedQuery> columns,
            IEnumerable<NamedQuery> rows,
            bool shouldHyperLink = true)
        {
            file.WriteLine("<table border=\"1\">");
            file.WriteLine("<tr>");
            ReportTableRow(file, "  ", 
                "&nbsp;", 
                columns.SelectMany(col => new string[] {
                    $"<b>{col.Name}</b>",
                    "<i>(diff)</i>",
                    "<i>(new)</i>",
                    "<i>(gone)</i>" } ));
            file.WriteLine("</tr>");

            foreach (NamedQuery row in rows)
            {
                file.WriteLine("<tr>");
                ReportTableRow(file, "  ",
                    $"<b>{row.Name}</b>",
                    columns.SelectMany(col =>
                    {
                        FilteredIssues filteredIssues = new FilteredIssues(
                            Expression.And(row.Query, col.Query),
                            beginIssues,
                            endIssues);
                        return new string[] {
                            GetQueryCountLinked_Multiple(
                                filteredIssues.Query, 
                                filteredIssues.End, 
                                shouldHyperLink, 
                                useRepositoriesFromIssues: true),
                            $"<i>{(filteredIssues.End.Count() - filteredIssues.Begin.Count()).ToString("+#;-#;0")}</i>",
                            $"<i>+{filteredIssues.EndOnly.Count()}</i>",
                            $"<i>-{filteredIssues.BeginOnly.Count()}</i>" };
                    }));
                file.WriteLine("</tr>");
            }

            Expression noRowQuery = Expression.And(rows.Select(row => Expression.Not(row.Query)).ToArray());
            file.WriteLine("<tr>");
            ReportTableRow(file, "  ",
                "<b>Other (missing above)</b>",
                columns.SelectMany(col =>
                {
                    FilteredIssues filteredIssues = new FilteredIssues(
                        Expression.And(noRowQuery, col.Query),
                        beginIssues,
                        endIssues);
                    return new string[] {
                        $"<b>{GetQueryCountLinked_Multiple(filteredIssues.Query, filteredIssues.End, shouldHyperLink, useRepositoriesFromIssues: false)}</b>",
                        $"<i>{(filteredIssues.End.Count() - filteredIssues.Begin.Count()).ToString("+#;-#;0")}</i>",
                        $"<i>+{filteredIssues.EndOnly.Count()}</i>",
                        $"<i>-{filteredIssues.BeginOnly.Count()}</i>" };
                }));
            file.WriteLine("</tr>");
            file.WriteLine("<tr>");
            ReportTableRow(file, "  ", 
                "<b>Total</b>", 
                columns.SelectMany(col =>
                {
                    FilteredIssues filteredIssues = new FilteredIssues(
                        col.Query,
                        beginIssues,
                        endIssues);
                    return new string[] {
                        $"<b>{GetQueryCountLinked(filteredIssues.Query, filteredIssues.End, shouldHyperLink, useRepositoriesFromIssues: false)}</b>",
                        $"<i>{(filteredIssues.End.Count() - filteredIssues.Begin.Count()).ToString("+#;-#;0")}</i>",
                        $"<i>+{filteredIssues.EndOnly.Count()}</i>",
                        $"<i>-{filteredIssues.BeginOnly.Count()}</i>" };
                }));
            file.WriteLine("</tr>");
            file.WriteLine("</table>");
        }

        private class FilteredIssues
        {
            public Expression Query { get; private set; }
            public IEnumerable<DataModelIssue> BeginOnly
            {
                get => Begin.Except_ByIssueNumber(End);
            }
            public IEnumerable<DataModelIssue> EndOnly
            {
                get => End.Except_ByIssueNumber(Begin);
            }
            public IEnumerable<DataModelIssue> Begin { get; private set; }
            public IEnumerable<DataModelIssue> End { get; private set; }

            public FilteredIssues(Expression query, IEnumerable<DataModelIssue> beginIssues, IEnumerable<DataModelIssue> endIssues)
            {
                Query = query;
                Begin = query.Evaluate(beginIssues).ToArray();
                End = query.Evaluate(endIssues).ToArray();
            }
        }

        private static void ReportTableRow(StreamWriter file, string prefix, string col1, IEnumerable<string> cols)
        {
            ReportTableRow(file, prefix, col1.ToEnumerable().Concat(cols));
        }
        private static void ReportTableRow(StreamWriter file, string prefix, IEnumerable<string> cols)
        {
            foreach (string col in cols)
            {
                file.WriteLine($"{prefix}<td>{col}</td>");
            }
        }
    }
}
