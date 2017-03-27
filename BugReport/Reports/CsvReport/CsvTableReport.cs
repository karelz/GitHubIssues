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
    public class CsvTableReport
    {
        private TableReport _report;
        private string _reportName;

        private CsvTableReport(TableReport report, string reportName)
        {
            _report = report;
            _reportName = reportName;
        }

        public static void Write(TableReport report, string csvFileNamePrefix, string reportName)
        {
            CsvTableReport csvReport = new CsvTableReport(report, reportName);
            csvReport.Write(csvFileNamePrefix);
        }

        private void Write(string csvFileNamePrefix)
        {
            Report(csvFileNamePrefix + "_alerts.csv", _report.GetAlertRows(TableReport.Row.SortRows_ByName));
            Report(csvFileNamePrefix + "_areas.csv", _report.GetAreaLabelRows(TableReport.Row.SortRows_ByName));
            Report(csvFileNamePrefix + "_teams.csv", _report.GetTeamAlertRows(TableReport.Row.SortRows_ByName));
            Report(csvFileNamePrefix + "_organizations.csv", _report.GetOrganizationAlertRows(TableReport.Row.SortRows_ByName));
        }

        private void Report(
            string csvFileName,
            IEnumerable<TableReport.Row> rows)
        {
            using (CsvWriter file = new CsvWriter(csvFileName))
            {
                // Prepare last row - "Other (missing above)"
                Expression otherRowQuery = Expression.And(rows.Select(row => Expression.Not(row.Query)).ToArray());
                TableReport.Row otherRow = new TableReport.Row(
                    "Other (missing above)",
                    otherRowQuery,
                    null,
                    _report.Columns,
                    _report.BeginIssues,
                    _report.EndIssues);

                TableReport.Row totalRow = new TableReport.Row(
                    "Total",
                    ExpressionConstant.True,
                    null,
                    _report.Columns,
                    _report.BeginIssues,
                    _report.EndIssues);

                // Write heading row
                file.Write(_reportName ?? "");
                file.WriteLine(_report.Columns.SelectMany(col => new string[] { col.Name, "new", "gone" }));

                // Write all "middle" rows and last row
                foreach (TableReport.Row row in rows.Concat(new TableReport.Row[] { otherRow, totalRow }))
                {
                    file.Write(row.Name);
                    file.WriteLine(row.Columns.SelectMany(filteredIssues => new string[] {
                            filteredIssues.End.Count().ToString(),
                            filteredIssues.EndOnly.Count().ToString(),
                            filteredIssues.BeginOnly.Count().ToString() }));
                }
            }
        }
    }
}
