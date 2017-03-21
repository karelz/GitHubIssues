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

            _areaLabelQueries = _config.AreaLabels.Select(label => label.Name).Distinct()
                .Select(labelName => new NamedQuery(labelName, new ExpressionLabel(labelName)))
                .ToList();
        }

        public void Write(IEnumerable<string> beginFiles, IEnumerable<string> endFiles, string outputHtmlFile)
        {
            IEnumerable<DataModelIssue> beginIssuesAll = IssueCollection.LoadIssues(beginFiles, _config);
            IEnumerable<DataModelIssue> endIssuesAll = IssueCollection.LoadIssues(endFiles, _config);

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

                file.WriteLine("<h2>Alerts</h2>");
                NamedQuery firstQuery = _config.Queries.First();
                Report(file,
                    beginIssues,
                    endIssues,
                    _config.Queries,
                    _config.Alerts,
                    SortRows_ByFirstColumn);

                file.WriteLine("<h2>Teams</h2>");
                Report(file,
                    beginIssues,
                    endIssues,
                    _config.Queries,
                    _config.Teams.Select(
                        team =>
                        new NamedQuery(
                            team.Name,
                            Expression.Or(
                                _config.Alerts.Where(alert => alert.Team == team)
                                    .Select(alert => alert.Query)))),
                    SortRows_ByFirstColumn);

                file.WriteLine("<h2>Organizations</h2>");
                Report(file,
                    beginIssues,
                    endIssues,
                    _config.Queries,
                    _config.Organizations.Select(
                        org =>
                        new NamedQuery(
                            org.Description,
                            Expression.Or(
                                _config.Alerts.Where(alert => alert.IsOrganization(org))
                                    .Select(alert => alert.Query)))),
                    SortRows_ByFirstColumn);

                file.WriteLine("<h2>Alerts - alphabetically</h2>");
                Report(file, 
                    beginIssues, 
                    endIssues, 
                    _config.Queries, 
                    _config.Alerts,
                    SortRows_ByName);

                file.WriteLine("<h2>Areas - alphabetically</h2>");
                Report(file, 
                    beginIssues, 
                    endIssues, 
                    _config.Queries, 
                    _areaLabelQueries,
                    SortRows_ByName);

                file.WriteLine("</body></html>");
            }
        }

        private class Row : NamedQuery
        {
            public FilteredIssues[] Columns { get; private set; }

            public Row(
                NamedQuery namedQuery,
                IEnumerable<NamedQuery> columns,
                IEnumerable<DataModelIssue> beginIssues,
                IEnumerable<DataModelIssue> endIssues)
                : base(namedQuery.Name, namedQuery.Query)
            {
                InitializeColumns(columns, beginIssues, endIssues);
            }

            public Row(
                string name, 
                Expression query,
                Team team,
                IEnumerable<NamedQuery> columns, 
                IEnumerable<DataModelIssue> beginIssues, 
                IEnumerable<DataModelIssue> endIssues)
                : base(name, query, team)
            {
                InitializeColumns(columns, beginIssues, endIssues);
            }

            private void InitializeColumns(
                IEnumerable<NamedQuery> columns,
                IEnumerable<DataModelIssue> beginIssues,
                IEnumerable<DataModelIssue> endIssues)
            {
                Columns = columns.Select(col => new FilteredIssues(
                    Expression.And(Query, col.Query),
                    beginIssues,
                    endIssues)).ToArray();
            }
        }

        private delegate IEnumerable<Row> SortRows(IEnumerable<Row> rows);
        //private static IEnumerable<Row> SortRows_KeepOrder(IEnumerable<Row> rows) => rows;
        private static IEnumerable<Row> SortRows_ByName(IEnumerable<Row> rows) => rows.OrderBy(row => row.Name);
        private static IEnumerable<Row> SortRows_ByFirstColumn(IEnumerable<Row> rows) => 
            rows.OrderByDescending(row => row.Columns[0].End.Count());

        private void Report(
            StreamWriter file,
            IEnumerable<DataModelIssue> beginIssues,
            IEnumerable<DataModelIssue> endIssues,
            IEnumerable<NamedQuery> columns,
            IEnumerable<NamedQuery> rowQueries,
            SortRows sortRows,
            bool shouldHyperLink = true)
        {
            // Heading row
            {
                file.WriteLine("<table border=\"1\">");
                ReportTableRow(file, "  ",
                    "&nbsp;",
                    columns.SelectMany(col => new string[] {
                        $"<b title=\"{col.Query.Normalized.ToString()}\">{col.Name}</b>",
                        "<i>(diff)</i>",
                        "<i>(new)</i>",
                        "<i>(gone)</i>" }));
            }

            // All "middle" rows
            {
                List<Row> rows = sortRows(rowQueries.Select(row =>
                    new Row(row.Name, row.Query, row.Team, columns, beginIssues, endIssues))).ToList();

                foreach (Row row in rows)
                {
                    ReportTableRow(file, "  ", row, shouldHyperLink, useRepositoriesFromIssues: true, makeCountBold: false);
                }
            }

            // "Other (missing above)" row
            {
                Expression otherRowQuery = Expression.And(rowQueries.Select(row => Expression.Not(row.Query)).ToArray());
                Row otherRow = new Row("Other (missing above)", otherRowQuery, null, columns, beginIssues, endIssues);

                ReportTableRow(file, "  ", otherRow, shouldHyperLink, useRepositoriesFromIssues: false, makeCountBold: true);
            }

            // "Total" row
            {
                Row totalRow = new Row("Total", ExpressionConstant.True, null, columns, beginIssues, endIssues);

                ReportTableRow(file, "  ", totalRow, shouldHyperLink, useRepositoriesFromIssues: false, makeCountBold: true);
            }

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

        private static void ReportTableRow(
            StreamWriter file,
            string prefix,
            Row row,
            bool shouldHyperLink,
            bool useRepositoriesFromIssues = true,
            bool makeCountBold = true)
        {
            ReportTableRow(file,
                "  ",
                $"<b title=\"{row.Query.Normalized.ToString()}\">{row.Name}</b>" + 
                    (row.Team == null ? "" : $" - <small>{row.Team.Name}</small>"),
                row.Columns.SelectMany(filteredIssues =>
                {
                    string count = GetQueryCountLinked(
                        filteredIssues.Query,
                        filteredIssues.End,
                        shouldHyperLink,
                        useRepositoriesFromIssues);
                    return new string[] {
                        makeCountBold ? $"<b>{count}</b>" : count,
                        $"<i>{(filteredIssues.End.Count() - filteredIssues.Begin.Count()).ToString("+#;-#;0")}</i>",
                        $"<i>+{filteredIssues.EndOnly.Count()}</i>",
                        $"<i>-{filteredIssues.BeginOnly.Count()}</i>" };
                }));
        }

        private static void ReportTableRow(StreamWriter file, string prefix, string col1, IEnumerable<string> cols)
        {
            ReportTableRow(file, prefix, col1.ToEnumerable().Concat(cols));
        }
        private static void ReportTableRow(StreamWriter file, string prefix, IEnumerable<string> cols)
        {
            file.WriteLine("<tr>");
            foreach (string col in cols)
            {
                file.WriteLine($"{prefix}<td>{col}</td>");
            }
            file.WriteLine("</tr>");
        }
    }
}
