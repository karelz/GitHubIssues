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

                foreach (ContributionsReport.Report report in _report.Reports)
                {
                    file.WriteLine($"<h2>{report.Name}</h2>");
                    Report(file, report);
                }

                file.WriteLine("<ul>");
                foreach (string fileName in _report.InputFiles)
                {
                    file.WriteLine($"    <li>{fileName}</li>");
                }
                file.WriteLine("</ul>");

                file.WriteLine("</body></html>");
            }
        }

        private void Report(
            StreamWriter file,
            ContributionsReport.Report report)
        {
            file.WriteLine("<table border=\"1\">");

            // Heading
            ReportTableRow(file,
                "  ",
                "<b>Date</b>",
                "<b>Total</b>",
                "&nbsp;",
                report.Groups.SelectMany(group => new string[]
                {
                    $"<b>{group.Name}</b>",
                    "&nbsp;"
                }),
                $"<b>{report.DefaultGroupName}</b>",
                "&nbsp;");

            // All intervals
            foreach (ContributionsReport.Interval interval in report.EnumerateIntervals())
            {
                IEnumerable<DataModelIssue> issues = _report.Issues
                    .Where(issue => interval.Contains(issue.CreatedAt.Value)).ToArray();
                ReportTableRow(file, "  ", interval, report, issues);
            }
            
            file.WriteLine("</table>");

            file.WriteLine($"<h3>Users</h3>");

            file.WriteLine("<table border=\"1\">");

            // Heading
            ReportTableRow(file,
                "  ",
                report.Groups.Select(group => $"<b>{group.Name}</b>"),
                $"<b>{report.DefaultGroupName}</b>");

            // Users breakdown
            ReportTableRow_Users(file, "  ", report, _report.Issues);

            file.WriteLine("</table>");
        }

        private static void ReportTableRow(
            StreamWriter file,
            string prefix,
            ContributionsReport.Interval interval,
            ContributionsReport.Report report,
            IEnumerable<DataModelIssue> issues)
        {
            IEnumerable<DataModelIssue> defaultGroupIssues = issues
                .Where(issue => report.Groups.Where(group => group.ContainsAuthor(issue)).None()).ToList();
            ReportTableRow(file,
                "  ",
                $"<b>{interval.GetLabel(report.Unit != ContributionsReport.Report.UnitKind.Month)}</b>",
                issues.NonPullRequests().Count().ToString(),
                issues.PullRequests().Count().ToString(),
                report.Groups.SelectMany(group =>
                {
                    IEnumerable<DataModelIssue> filteredIssues = issues.Where(issue => group.ContainsAuthor(issue)).ToList();
                    return new string[]
                    {
                        filteredIssues.NonPullRequests().Count().ToString(),
                        filteredIssues.PullRequests().Count().ToString()
                    };
                }),
                defaultGroupIssues.NonPullRequests().Count().ToString(),
                defaultGroupIssues.PullRequests().Count().ToString());
        }

        class UserInfo
        {
            // null for ghosts
            public string Name { get; private set; }
            public string Id { get; private set; }
            public int Issues { get; set; }


            public string DisplayName => Name ?? $"ghost ({Id})";
            public string Url => (Name == null) ? null : "https://github.com/" + Name;

            public UserInfo(BugReport.DataModel.User user)
            {
                Name = (user.Name == "ghost") ? null : user.Name;
                Id = user.Id;
                Issues = 0;
            }
            public UserInfo(ContributionsReport.User user)
            {
                Name = user.Name;
                Id = user.Id;
                Issues = 0;
            }
        }

        private static string GetUserBreakdown(IEnumerable<DataModelIssue> issues)
        {
            /*
            List<UserInfo> users = new List<UserInfo>();

            foreach (DataModelIssue issue in issues)
            {

            }
            */

            return "TODO analyze the issues per user";
        }

        private static void ReportTableRow_Users(
            StreamWriter file,
            string prefix,
            ContributionsReport.Report report,
            IEnumerable<DataModelIssue> issues)
        {
            IEnumerable<DataModelIssue> defaultGroupIssues = issues
                .Where(issue => report.Groups.Where(group => group.ContainsAuthor(issue)).None()).ToList();
            ReportTableRow(file,
                "  ",
                report.Groups.Select(group => GetUserBreakdown(issues.Where(issue => group.ContainsAuthor(issue)).ToList())),
                GetUserBreakdown(defaultGroupIssues));
        }

        private static void ReportTableRow(StreamWriter file, string prefix, string col1, string col2, string col3, IEnumerable<string> cols, string col4, string col5)
        {
            ReportTableRow(file, prefix, new string[] { col1, col2, col3 }.Concat(cols).Concat(new string[] { col4, col5 }));
        }
        private static void ReportTableRow(StreamWriter file, string prefix, IEnumerable<string> cols, string col)
        {
            ReportTableRow(file, prefix, cols.Concat(new string[] { col }));
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
