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
    public class HtmlContributionsReport
    {
        private ContributionsReport _report;

        private HtmlContributionsReport(ContributionsReport report)
        {
            _report = report;
        }

        public static void Write(ContributionsReport report, string outputHtmlFile)
        {
            HtmlContributionsReport htmlReport = new HtmlContributionsReport(report);
            htmlReport.Write(outputHtmlFile);
        }

        private void Write(string outputHtmlFile)
        {
            using (StreamWriter file = new StreamWriter(outputHtmlFile))
            {
                file.WriteLine("<html><body>");
                file.WriteLine($"Report created on {DateTime.Now}<br/>");

                file.WriteLine("<h2>Alerts</h2>");
                Report(file, _report.GetAlertRows(TableReport.Row.SortRows_ByFirstColumn));

                file.WriteLine("<h2>Teams</h2>");
                Report(file, _report.GetTeamAlertRows(TableReport.Row.SortRows_ByFirstColumn));

                file.WriteLine("<h2>Organizations</h2>");
                Report(file, _report.GetOrganizationAlertRows(TableReport.Row.SortRows_ByFirstColumn));

                file.WriteLine("<h2>Alerts - alphabetically</h2>");
                Report(file, _report.GetAlertRows(TableReport.Row.SortRows_ByName));

                file.WriteLine("<h2>Areas - alphabetically</h2>");
                Report(file, _report.GetAreaLabelRows(TableReport.Row.SortRows_ByName));

                if (_report.BeginFiles != null)
                {
                    file.WriteLine("/begin");
                    file.WriteLine("<ul>");
                    foreach (string fileName in _report.BeginFiles)
                    {
                        file.WriteLine($"    <li>{fileName}</li>");
                    }
                    file.WriteLine("</ul>");
                }

                if (_report.MiddleFiles != null)
                {
                    file.WriteLine("/middle");
                    file.WriteLine("<ul>");
                    foreach (string fileName in _report.MiddleFiles)
                    {
                        file.WriteLine($"    <li>{fileName}</li>");
                    }
                    file.WriteLine("</ul>");
                }

                file.WriteLine("/end");
                file.WriteLine("<ul>");
                foreach (string fileName in _report.EndFiles)
                {
                    file.WriteLine($"    <li>{fileName}</li>");
                }
                file.WriteLine("</ul>");

                file.WriteLine("</body></html>");
            }
        }

        private void Report(
            StreamWriter file,
            IEnumerable<TableReport.Row> rows,
            bool shouldHyperLink = true)
        {
            // Heading row
            {
                file.WriteLine("<table border=\"1\">");
                ReportTableRow(file, "  ",
                    "&nbsp;",
                    _report.Columns.SelectMany(col => new string[] {
                        $"<b title=\"{col.Query.ToString()}\">{col.Name}</b>",
                        "<i>(diff)</i>",
                        "<i>(new)</i>",
                        "<i>(gone)</i>" }));
            }

            // All "middle" rows
            {
                foreach (TableReport.Row row in rows)
                {
                    ReportTableRow(file, "  ", row, shouldHyperLink, useRepositoriesFromIssues: true, makeCountBold: false);
                }
            }

            // "Other (missing above)" row
            {
                Expression otherRowQuery = Expression.And(rows.Select(row => Expression.Not(row.Query)).ToArray());
                TableReport.Row otherRow = new TableReport.Row(
                    "Other (missing above)",
                    otherRowQuery,
                    null,
                    _report.Columns,
                    _report.BeginIssues,
                    _report.MiddleIssues,
                    _report.EndIssues);

                ReportTableRow(file, "  ", otherRow, shouldHyperLink, useRepositoriesFromIssues: false, makeCountBold: true);
            }

            // "Total" row
            {
                TableReport.Row totalRow = new TableReport.Row(
                    "Total",
                    ExpressionConstant.True,
                    null,
                    _report.Columns,
                    _report.BeginIssues,
                    _report.MiddleIssues,
                    _report.EndIssues);

                ReportTableRow(file, "  ", totalRow, shouldHyperLink, useRepositoriesFromIssues: false, makeCountBold: true);
            }

            file.WriteLine("</table>");
        }

        private static void ReportTableRow(
            StreamWriter file,
            string prefix,
            TableReport.Row row,
            bool shouldHyperLink,
            bool useRepositoriesFromIssues = true,
            bool makeCountBold = true)
        {
            ReportTableRow(file,
                "  ",
                $"<b title=\"{row.Query.ToString()}\">{row.Name}</b>" + 
                    (row.Team == null ? "" : $" - <small>{row.Team.Name}</small>"),
                row.Columns.SelectMany(filteredIssues =>
                {
                    string count = HtmlQueryCountLink.Create(
                        filteredIssues.Query,
                        filteredIssues.End,
                        shouldHyperLink,
                        useRepositoriesFromIssues);
                    return new string[] {
                        makeCountBold ? $"<b>{count}</b>" : count,
                        $"<i>{(filteredIssues.End.Count() - filteredIssues.Begin.Count()).ToString("+#;-#;0")}</i>",
                        $"<i>+{filteredIssues.EndOrMiddleOnly.Count()}</i>",
                        $"<i>-{filteredIssues.BeginOrMiddleOnly.Count()}</i>" };
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
