using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BugReport.Util;
using GitHubBugReport.Core.DataModel;
using GitHubBugReport.Core.Query;

namespace BugReport.Reports
{
    public class TableReport
    {
        private Config _config;

        public readonly IEnumerable<string> BeginFiles;
        public readonly IEnumerable<string> MiddleFiles;
        public readonly IEnumerable<string> EndFiles;

        public readonly IEnumerable<DataModelIssue> BeginIssues;
        public readonly IEnumerable<DataModelIssue> MiddleIssues;
        public readonly IEnumerable<DataModelIssue> EndIssues;

        public TableReport(
            IEnumerable<string> configFiles, 
            IEnumerable<string> beginFiles,
            IEnumerable<string> middleFiles,
            IEnumerable<string> endFiles)
        {
            _config = new Config(configFiles);

            BeginFiles = beginFiles;
            MiddleFiles = middleFiles ?? new string[] { };
            EndFiles = endFiles;

            IEnumerable<DataModelIssue> beginIssuesAll = IssueCollection.LoadIssues(beginFiles, _config);
            IEnumerable<DataModelIssue> endIssuesAll = IssueCollection.LoadIssues(endFiles, _config);

            BeginIssues = beginIssuesAll.Where(i => i.IsIssueOrComment).ToArray();
            EndIssues = endIssuesAll.Where(i => i.IsIssueOrComment).ToArray();

            List<DataModelIssue> middleIssues = new List<DataModelIssue>();
            if (middleFiles != null)
            {
                // Enumerate the middle files in reverse order to capture the latest bug snapshot (the most 
                // "accurate" before its disappearance)
                foreach (string middleFile in middleFiles.Reverse())
                {
                    IEnumerable<DataModelIssue> issues = IssueCollection.LoadIssues(beginFiles, _config);
                    middleIssues.AddRange(issues.Where(i => i.IsIssueOrComment)
                        .Except_ByIssueNumber(BeginIssues)
                        .Except_ByIssueNumber(EndIssues));
                }
            }
            MiddleIssues = middleIssues;
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
                    MiddleIssues,
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
                IEnumerable<DataModelIssue> middleIssues,
                IEnumerable<DataModelIssue> endIssues)
                : base(namedQuery.Name, namedQuery.Query)
            {
                InitializeColumns(columns, beginIssues, middleIssues, endIssues);
            }

            public Row(
                string name,
                Expression query,
                Team team,
                IEnumerable<NamedQuery> columns,
                IEnumerable<DataModelIssue> beginIssues,
                IEnumerable<DataModelIssue> middleIssues,
                IEnumerable<DataModelIssue> endIssues)
                : base(name, query, team)
            {
                InitializeColumns(columns, beginIssues, middleIssues, endIssues);
            }

            private void InitializeColumns(
                IEnumerable<NamedQuery> columns,
                IEnumerable<DataModelIssue> beginIssues,
                IEnumerable<DataModelIssue> middleIssues,
                IEnumerable<DataModelIssue> endIssues)
            {
                Columns = columns.Select(col => new FilteredIssues(
                    Expression.And(Query, col.Query),
                    beginIssues,
                    middleIssues,
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
            public IEnumerable<DataModelIssue> BeginOrMiddleOnly
            {
                get => Begin.Except_ByIssueNumber(End).Concat(_middle);
            }
            public IEnumerable<DataModelIssue> EndOrMiddleOnly
            {
                get => End.Except_ByIssueNumber(Begin).Concat(_middle);
            }
            public IEnumerable<DataModelIssue> Begin { get; private set; }
            public IEnumerable<DataModelIssue> End { get; private set; }
            private DataModelIssue[] _middle;

            public FilteredIssues(
                Expression query,
                IEnumerable<DataModelIssue> beginIssues,
                IEnumerable<DataModelIssue> middleIssues,
                IEnumerable<DataModelIssue> endIssues)
            {
                Query = query;
                Begin = query.Evaluate(beginIssues).ToArray();
                End = query.Evaluate(endIssues).ToArray();
                _middle = query.Evaluate(middleIssues).ToArray();
            }
        }
    }
}
