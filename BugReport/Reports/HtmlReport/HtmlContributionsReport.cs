using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BugReport.Util;
using BugReport.DataModel;
using BugReport.Query;
using System.Text;

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
            ReportTableRow(file,
                "  ",
                "&nbsp;",
                "<b>Issues</b>",
                "<b>PRs</b>",
                report.Groups.SelectMany(group => new string[]
                {
                    "<b>Issues</b>",
                    "<b>PRs</b>"
                }),
                "<b>Issues</b>",
                "<b>PRs</b>");

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
            public BugReport.DataModel.User User { get; private set; }
            public List<DataModelIssue> Issues { get; private set; }

            public string DisplayName { get; private set; }
            public string Url { get; private set; }
            public string DisplayName_HtmlLink => (Url == null) ? DisplayName : $"<a href=\"{Url}\">{DisplayName}</a>";

            public UserInfo(BugReport.DataModel.User user)
            {
                User = user;
                Issues = new List<DataModelIssue>();

                if (user.Login == "ghost")
                {
                    DisplayName = $"ghost ({User.Id})";
                    Url = null;
                }
                else
                {
                    DisplayName = user.Login;
                    Url = user.HtmlUrl;
                }
            }
        }

        private static string StringJoin(string separator, int limit, string limitSeparator, IEnumerable<string> values)
        {
            StringBuilder sb = new StringBuilder();
            int count = values.Count();
            if (count <= limit)
            {
                return string.Join(separator, values);
            }

            int index = 0;
            foreach (string value in values)
            {
                if (index > 0)
                {
                    if (index % limit == 0)
                    {
                        sb.Append(limitSeparator);
                    }
                    else
                    {
                        sb.Append(separator);
                    }
                }
                sb.Append(value);
                index++;
            }
            return sb.ToString();
        }

        private static string GetUserBreakdown(IEnumerable<DataModelIssue> issues)
        {
            List<UserInfo> users = new List<UserInfo>();

            foreach (DataModelIssue issue in issues)
            {
                bool userFound = false;
                User user = issue.User;

                foreach (UserInfo userInfo in users)
                {
                    if (user.Equals(userInfo.User))
                    {
                        userFound = true;
                        userInfo.Issues.Add(issue);
                        break;
                    }
                }
                if (!userFound)
                {
                    UserInfo userInfo = new UserInfo(user);
                    userInfo.Issues.Add(issue);
                    users.Add(userInfo);
                }
            }

            users.Sort((a, b) =>
            {
                if (a.Issues.Count > b.Issues.Count)
                    return -1;
                if (a.Issues.Count == b.Issues.Count)
                {
                    return StringComparer.Ordinal.Compare(a.DisplayName, b.DisplayName);
                }
                return 1;
            });

            return string.Join("<br/>", users.Select(userInfo => 
                $"{userInfo.DisplayName_HtmlLink} - {userInfo.Issues.Count} - <small>" + 
                StringJoin(" ", 15, "<br/>", userInfo.Issues.Select(issue => $"<a href=\"{issue.HtmlUrl}\">#{issue.Number}</a>").ToList()) + 
                "</small>"));
        }

        private static void ReportTableRow_Users(
            StreamWriter file,
            string prefix,
            ContributionsReport.Report report,
            IEnumerable<DataModelIssue> allIssues)
        {
            ContributionsReport.Interval reprotInterval = report.FullInterval;
            IEnumerable<DataModelIssue> issues = allIssues.Where(issue => reprotInterval.Contains(issue.CreatedAt.Value));

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
