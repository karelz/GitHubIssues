using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class HtmlReport
    {
        StreamWriter File;
        IEnumerable<Issue> Issues;
        IEnumerable<Issue> PullRequests;

        [FlagsAttribute]
        public enum IssueFlags
        {
            Issue = 1,
            PullRequest = 2,
            IssueTypeMask = Issue | PullRequest,
            Open = 4,
            Closed = 8,
            IssueStatusMask = Open | Closed
        }

        public void Write(IssueCollection issuesCollection, string outputHtmlFile)
        {
            List<LabelGroup> areaLabelGroups = issuesCollection.GetAreaLabels().OrderBy(l => l.Name).Select(l => new LabelGroup(l)).ToList();

            List<LabelGroup> issueTypeLabelGroups = new List<LabelGroup>();
            LabelGroup.Add(issueTypeLabelGroups, issuesCollection.GetLabel("bug"));
            LabelGroup featureLabelGroup = new LabelGroup("Enhancement/api-*");
            featureLabelGroup.Labels.AddIfNotNull(issuesCollection.GetLabel("api-approved"));
            featureLabelGroup.Labels.AddIfNotNull(issuesCollection.GetLabel("api-needs-work"));
            featureLabelGroup.Labels.AddIfNotNull(issuesCollection.GetLabel("api-ready-for-review"));
            featureLabelGroup.Labels.AddIfNotNull(issuesCollection.GetLabel("api-needs-exposed"));
            //LabelGroup.Add(issueTypeLabelGroups, issuesCollection.GetLabel("enhancement"));
            featureLabelGroup.Labels.AddIfNotNull(issuesCollection.GetLabel("enhancement"));
            issueTypeLabelGroups.Add(featureLabelGroup);
            LabelGroup.Add(issueTypeLabelGroups, issuesCollection.GetLabel("test bug"));
            LabelGroup.Add(issueTypeLabelGroups, issuesCollection.GetLabel("test enhancement"));
            LabelGroup.Add(issueTypeLabelGroups, issuesCollection.GetLabel("documentation"));
            LabelGroup.Add(issueTypeLabelGroups, issuesCollection.GetLabel("question"));

            List<LabelGroup> ignoredIssueTypeLabelGroups = new List<LabelGroup>();
            LabelGroup.Add(ignoredIssueTypeLabelGroups, issuesCollection.GetLabel("tracking-external-issue"));
            LabelGroup.Add(ignoredIssueTypeLabelGroups, issuesCollection.GetLabel("netstandard2.0"));
            LabelGroup.Add(ignoredIssueTypeLabelGroups, issuesCollection.GetLabel("os-windows-uwp"));

            Issues = issuesCollection.Issues.Where(i => i.IsIssue);
            PullRequests = issuesCollection.Issues.Where(i => i.IsPullRequest);
            using (File = new StreamWriter(outputHtmlFile))
            {
                File.WriteLine("<html><body>");
                ReportLabelGroups(issueTypeLabelGroups, ignoredIssueTypeLabelGroups, areaLabelGroups);
                File.WriteLine("</body></html>");
            }
            File = null;
        }

        void ReportLabelGroups(IEnumerable<LabelGroup> xLabelGroups, IEnumerable<LabelGroup> xIgnoredLabelGroups, IEnumerable<LabelGroup> yLabelGroups)
        {
            File.WriteLine("<table border=\"1\">");

            ReportTableRow(
                "  ",
                "    ", 
                "<b>Issue type:</b>",
                "<b>PR</b>",
                string.Format("<b>Total (excl. {0})</b>", string.Join(", ", xIgnoredLabelGroups.SelectMany(lg => lg.Labels).Select(l => l.Name))),
                "&lt;no issue type&gt;", 
                xLabelGroups.Select(l => string.Format("<b>{0}</b>", l.Name)), 
                "&lt;multi-type&gt;",
                "&lt;multi-area&gt;",
                xIgnoredLabelGroups.Select(l => string.Format("<b>{0}</b>", l.Name)),
                "<b>Total</b>");

            // Issues without any area
            {
                IEnumerable<Label> areaLabels = yLabelGroups.SelectMany(lg => lg.Labels);
                IEnumerable<Issue> yNoAreaIssues = Issues.Where(i => !i.Labels.Intersect(areaLabels).Any());
                IEnumerable<Issue> yNoAreaPullRequests = PullRequests.Where(i => !i.Labels.Intersect(areaLabels).Any());
                ReportTableRow(
                    "  ",
                    "    ",
                    // Area
                    "&lt;no area&gt;",
                    // PR
                    ReportLabelGroups_Cell(LabelGroup.Empty, LabelGroup.EmptyList, yNoAreaPullRequests, LabelGroup.Empty, yLabelGroups, IssueFlags.PullRequest | IssueFlags.Open),
                    // Total (excl. ignored)
                    ReportLabelGroups_Cell(LabelGroup.Empty, LabelGroup.EmptyList, yNoAreaIssues, LabelGroup.Empty, yLabelGroups),
                    // No issue type
                    "--",
                    // Issue types (excl. ignored)
                    xLabelGroups.Select(l => "--"),
                    // multi-type (excl. ignored)
                    "--",
                    // multi-area
                    "--",
                    // Issue types (ignored)
                    xIgnoredLabelGroups.Select(l => "--"),
                    // Total
                    "--");
            }

            IEnumerable<LabelGroup> xAllLabelGroups = xLabelGroups.Concat(xIgnoredLabelGroups);
            var yIssueInfos = yLabelGroups.Select(y => new { LabelGroup = y, Issues = Issues.Where(i => i.ContainsLabel(y.Labels)) });

            foreach (var yIssueInfo in yIssueInfos.OrderByDescending(i => i.Issues.Count()))
            {
                LabelGroup y = yIssueInfo.LabelGroup;
                IEnumerable<Issue> yIssues = yIssueInfo.Issues;
                IEnumerable<Issue> yPullRequests = PullRequests.Where(i => i.ContainsLabel(y.Labels));

                IEnumerable<LabelGroup> yOtherAreaLabelGroups = yLabelGroups.Where(lg => (lg != y));
                IEnumerable<Label> yOtherAreaLabels = yOtherAreaLabelGroups.SelectMany(lg => lg.Labels);
                IEnumerable<Issue> yMultiAreaIssues = yIssueInfo.Issues.Where(i => i.Labels.Intersect(yOtherAreaLabels).Any());

                ReportTableRow(
                    "  ",
                    "    ",
                    // Area
                    string.Format("<b>{0}</b>", y.Name),
                    // PR
                    ReportLabelGroups_Cell(LabelGroup.Empty, LabelGroup.EmptyList, yPullRequests, y, LabelGroup.EmptyList, IssueFlags.PullRequest | IssueFlags.Open),
                    // Total (excl. ignored)
                    ReportLabelGroups_Cell(LabelGroup.Empty, xIgnoredLabelGroups, yIssues, y, LabelGroup.EmptyList),
                    // No issue type
                    ReportLabelGroups_Cell(LabelGroup.Empty, xAllLabelGroups, yIssues, y, LabelGroup.EmptyList),
                    // Issue types (excl. ignored)
                    xLabelGroups.Select(x => ReportLabelGroups_Cell(x, xIgnoredLabelGroups, yIssues, y, LabelGroup.EmptyList)),
                    // multi-type (excl. ignored)
                    ReportLabelGroups_Multiple(yIssues, xLabelGroups),
                    // multi-area
                    ReportLabelGroups_Cell(LabelGroup.Empty, LabelGroup.EmptyList, yMultiAreaIssues, forceIssueList: true),
                    // Issue types (ignored)
                    xIgnoredLabelGroups.Select(x => ReportLabelGroups_Cell(x, LabelGroup.EmptyList, yIssues, y, LabelGroup.EmptyList)),
                    // Total
                    ReportLabelGroups_Cell(LabelGroup.Empty, LabelGroup.EmptyList, yIssues, y, LabelGroup.EmptyList));
            }

            ReportTableRow(
                "  ",
                "    ",
                // Area
                "&lt;all&gt;",
                // PR
                ReportLabelGroups_Cell(LabelGroup.Empty, LabelGroup.EmptyList, PullRequests, IssueFlags.PullRequest | IssueFlags.Open),
                // Total (excl. ignored)
                ReportLabelGroups_Cell(LabelGroup.Empty, xIgnoredLabelGroups, Issues),
                // No issue type
                ReportLabelGroups_Cell(LabelGroup.Empty, xAllLabelGroups, Issues),
                // Issue types (excl. ignored)
                xLabelGroups.Select(x => ReportLabelGroups_Cell(x, xIgnoredLabelGroups, Issues)),
                // multi-type (excl. ignored)
                ReportLabelGroups_Multiple(Issues, xLabelGroups),
                // multi-area
                ReportLabelGroups_Multiple(Issues, yLabelGroups),
                // Issue types (ignored)
                xIgnoredLabelGroups.Select(x => ReportLabelGroups_Cell(x, LabelGroup.EmptyList, Issues)),
                // Total
                ReportLabelGroups_Cell(LabelGroup.Empty, LabelGroup.EmptyList, Issues));

            File.WriteLine("</table>");
        }

        string ReportLabelGroups_Cell(
            LabelGroup xLabelGroup,
            IEnumerable<LabelGroup> xMinusLabelGroups,
            IEnumerable<Issue> yIssues,
            IssueFlags issueFlags = IssueFlags.Issue | IssueFlags.Open,
            bool forceIssueList = false)
        {
            return ReportLabelGroups_Cell(xLabelGroup, xMinusLabelGroups, yIssues, LabelGroup.Empty, LabelGroup.EmptyList, issueFlags, forceIssueList);
        }



        string ReportLabelGroups_Cell(
            LabelGroup xLabelGroup,
            IEnumerable<LabelGroup> xMinusLabelGroups,
            IEnumerable<Issue> yIssues,
            LabelGroup yLabelGroup,
            IEnumerable<LabelGroup> yMinusLabelGroups,
            IssueFlags issueFlags = IssueFlags.Issue | IssueFlags.Open,
            bool forceIssueList = false)
        {
            IEnumerable<Issue> issues = yIssues;
            if (xLabelGroup.Labels.Any())
            {
                issues = issues.Where(i => i.Labels.Intersect(xLabelGroup.Labels).Any());
            }
            if (xMinusLabelGroups.Any())
            {
                IEnumerable<Label> xMinusLabels = xMinusLabelGroups.SelectMany(lg => lg.Labels);
                issues = issues.Where(i => !i.Labels.Intersect(xMinusLabels).Any());
            }

            IEnumerable<Label> labels = xLabelGroup.Labels.Concat(yLabelGroup.Labels);
            IEnumerable<Label> minusLabels = xMinusLabelGroups.Concat(yMinusLabelGroups).SelectMany(lg => lg.Labels);

            if (forceIssueList ||
                (xLabelGroup.Labels.Count > 1) ||
                (yLabelGroup.Labels.Count > 1) ||
                (minusLabels.Count() > 30))
            {
                return ReportLabelGroups_IssuesList(issues);
            }

            StringBuilder queryLink = new StringBuilder();
            queryLink.Append("https://github.com/dotnet/corefx/issues?utf8=%E2%9C%93&q=");
            switch (issueFlags & IssueFlags.IssueTypeMask)
            {
                case IssueFlags.Issue:
                    queryLink.Append("is%3Aissue%20");
                    break;
                case IssueFlags.PullRequest:
                    queryLink.Append("is%3Apr%20");
                    break;
            }
            switch (issueFlags & IssueFlags.IssueStatusMask)
            {
                case IssueFlags.Open:
                    queryLink.Append("is%3Aopen%20");
                    break;
                case IssueFlags.PullRequest:
                    queryLink.Append("is%3Aclosed%20");
                    break;
            }
            foreach (Label l in labels)
            {
                queryLink.AppendFormat("label%3A%22{0}%22%20", l.Name);
            }
            foreach (Label l in minusLabels)
            {
                queryLink.AppendFormat("-label%3A%22{0}%22%20", l.Name);
            }
            return string.Format("<a href=\"{1}\">{0}</a>", issues.Count().ToString(), queryLink.ToString());
        }

        const int GitHubQuery_IssuesLimit = 50;
        string ReportLabelGroups_IssuesList(IEnumerable<Issue> issues)
        {
            int issuesCount = issues.Count();
            if (issuesCount == 0)
            {
                return issuesCount.ToString();
            }

            StringBuilder text = new StringBuilder();
            text.Append(issuesCount.ToString());
            text.Append(" (");
            bool firstBatch = true;
            while (issues.Any())
            {
                if (!firstBatch)
                {
                    text.Append(",");
                }
                else
                {
                    firstBatch = false;
                }
                text.AppendFormat("<a href=\"{0}\">*</a>", GetGitHubQueryHyperLink(issues.Take(GitHubQuery_IssuesLimit)));
                issues = issues.Skip(GitHubQuery_IssuesLimit);
            }
            text.Append(")");
            return text.ToString();
        }

        static StringBuilder GetGitHubQueryHyperLink(IEnumerable<Issue> issues)
        {
            StringBuilder link = new StringBuilder();
            link.Append("https://github.com/dotnet/corefx/issues?utf8=%E2%9C%93&q=");
            foreach (Issue i in issues)
            {
                link.AppendFormat("{0}%20", i.Number);
            }
            return link;
        }

        string ReportLabelGroups_Multiple(
            IEnumerable<Issue> issues,
            IEnumerable<LabelGroup> labelGroups)
        {
            IEnumerable<Issue> multipleIssues = issues.Where(i => labelGroups.Where(lg => i.Labels.Intersect(lg.Labels).Any()).Count() > 1);
            return ReportLabelGroups_IssuesList(multipleIssues);
        }

        void ReportTableRow(string prefixTR, string prefixTD, string col1, string col2, string col3, string col4, IEnumerable<string> columns1, string colMid1, string colMid2, IEnumerable<string> columns2, string colLast)
        {
            ReportTableRow(prefixTR, prefixTD, new string[] { col1, col2, col3, col4 }.Concat(columns1).Concat(new string[] { colMid1, colMid2 }).Concat(columns2).Concat(new string[] { colLast }));
        }
        /*
        void ReportTableRow(string prefixTR, string prefixTD, string col1, string col2, string col3, IEnumerable<string> columns, string colLast2, string colLast1)
        {
            ReportTableRow(prefixTR, prefixTD, new string[] { col1, col2, col3 }.Concat(columns).Concat(new string[] { colLast2, colLast1 }));
        }
        */
        void ReportTableRow(string prefixTR, string prefixTD, IEnumerable<string> columns)
        {
            File.WriteLine("{0}<tr>", prefixTR);
            foreach (string column in columns)
            {
                File.WriteLine("{0}<td>{1}</td>", prefixTD, column);
            }
            File.WriteLine("{0}</tr>", prefixTR);
        }
    }
}
