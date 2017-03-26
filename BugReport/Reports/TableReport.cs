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
    public class TableReport
    {
        private Config _config;

        public readonly IEnumerable<string> BeginFiles;
        public readonly IEnumerable<string> EndFiles;

        public readonly IEnumerable<DataModelIssue> BeginIssues;
        public readonly IEnumerable<DataModelIssue> EndIssues;

        public TableReport(IEnumerable<string> configFiles, IEnumerable<string> beginFiles, IEnumerable<string> endFiles)
        {
            _config = new Config(configFiles);

            BeginFiles = beginFiles;
            EndFiles = endFiles;

            IEnumerable<DataModelIssue> beginIssuesAll = IssueCollection.LoadIssues(beginFiles, _config);
            IEnumerable<DataModelIssue> endIssuesAll = IssueCollection.LoadIssues(endFiles, _config);

            BeginIssues = beginIssuesAll.Where(i => i.IsIssueOrComment).ToArray();
            EndIssues = endIssuesAll.Where(i => i.IsIssueOrComment).ToArray();
        }

        public IEnumerable<NamedQuery> Columns
        {
            get => _config.Queries;
        }

        private IEnumerable<Row> GetSortedRows(IEnumerable<NamedQuery> rowQueries, Row.SortRows sortRows)
        {
            return sortRows(rowQueries.Select(rowQuery =>
                new Row(
                    rowQuery.Name, 
                    rowQuery.Query, 
                    rowQuery.Team, 
                    Columns, 
                    BeginIssues, 
                    EndIssues))).ToList();
        }

        public IEnumerable<Row> GetAlertRows(Row.SortRows sortRows)
        {
            return GetSortedRows(_config.Alerts, sortRows);
        }

        public IEnumerable<Row> GetTeamAlertRows(Row.SortRows sortRows)
        {
            return GetSortedRows(
                _config.Teams.Select(
                    team =>
                    new NamedQuery(
                        team.Name,
                        Expression.Or(
                            _config.Alerts.Where(alert => alert.Team == team)
                                .Select(alert => alert.Query)))),
                sortRows);
        }

        public IEnumerable<Row> GetOrganizationAlertRows(Row.SortRows sortRows)
        {
            return GetSortedRows(
                _config.Organizations.Select(
                    org =>
                    new NamedQuery(
                        org.Description,
                        Expression.Or(
                            _config.Alerts.Where(alert => alert.IsOrganization(org))
                                .Select(alert => alert.Query)))),
                sortRows);
        }

        private IEnumerable<NamedQuery> _areaLabelQueries = null;
        public IEnumerable<Row> GetAreaLabelRows(Row.SortRows sortRows)
        {
            if (_areaLabelQueries == null)
            {
                _areaLabelQueries = _config.AreaLabels.Select(label => label.Name).Distinct()
                    .Select(labelName => new NamedQuery(labelName, new ExpressionLabel(labelName)))
                    .ToList();
            }
            return GetSortedRows(_areaLabelQueries, sortRows);
        }

        public class Row : NamedQuery
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

            public delegate IEnumerable<Row> SortRows(IEnumerable<Row> rows);

            public static IEnumerable<Row> SortRows_ByName(IEnumerable<Row> rows) => rows.OrderBy(row => row.Name);
            public static IEnumerable<Row> SortRows_ByFirstColumn(IEnumerable<Row> rows) =>
                rows.OrderByDescending(row => row.Columns[0].End.Count());
        }

        public class FilteredIssues
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
    }
}
