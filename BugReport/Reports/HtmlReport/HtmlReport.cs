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
        /*
        StreamWriter File;
        IEnumerable<DataModelIssue> Issues;
        IEnumerable<DataModelIssue> PullRequests;

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
        */
        public void Write(IssueCollection issuesCollection, string outputHtmlFile)
        {
            /*
            Issues = issuesCollection.Issues.Where(i => i.IsIssueOrComment);
            PullRequests = issuesCollection.Issues.Where(i => i.IsPullRequest);
            using (File = new StreamWriter(outputHtmlFile))
            {
                File.WriteLine("<html><body>");
                ReportLabelGroups(issueTypeLabelGroups, ignoredIssueTypeLabelGroups, areaLabelGroups);
                File.WriteLine("</body></html>");
            }
            File = null;
            */
        }
        /*
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
                IEnumerable<DataModelIssue> yNoAreaIssues = Issues.Where(i => !i.Labels.Intersect(areaLabels).Any());
                IEnumerable<DataModelIssue> yNoAreaPullRequests = PullRequests.Where(i => !i.Labels.Intersect(areaLabels).Any());
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
                IEnumerable<DataModelIssue> yIssues = yIssueInfo.Issues;
                IEnumerable<DataModelIssue> yPullRequests = PullRequests.Where(i => i.ContainsLabel(y.Labels));

                IEnumerable<LabelGroup> yOtherAreaLabelGroups = yLabelGroups.Where(lg => (lg != y));
                IEnumerable<Label> yOtherAreaLabels = yOtherAreaLabelGroups.SelectMany(lg => lg.Labels);
                IEnumerable<DataModelIssue> yMultiAreaIssues = yIssueInfo.Issues.Where(i => i.Labels.Intersect(yOtherAreaLabels).Any());

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
            IEnumerable<DataModelIssue> yIssues,
            IssueFlags issueFlags = IssueFlags.Issue | IssueFlags.Open,
            bool forceIssueList = false)
        {
            return ReportLabelGroups_Cell(xLabelGroup, xMinusLabelGroups, yIssues, LabelGroup.Empty, LabelGroup.EmptyList, issueFlags, forceIssueList);
        }



        string ReportLabelGroups_Cell(
            LabelGroup xLabelGroup,
            IEnumerable<LabelGroup> xMinusLabelGroups,
            IEnumerable<DataModelIssue> yIssues,
            LabelGroup yLabelGroup,
            IEnumerable<LabelGroup> yMinusLabelGroups,
            IssueFlags issueFlags = IssueFlags.Issue | IssueFlags.Open,
            bool forceIssueList = false)
        {
            IEnumerable<DataModelIssue> issues = yIssues;
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
        string ReportLabelGroups_IssuesList(IEnumerable<DataModelIssue> issues)
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

        static StringBuilder GetGitHubQueryHyperLink(IEnumerable<DataModelIssue> issues)
        {
            StringBuilder link = new StringBuilder();
            link.Append("https://github.com/dotnet/corefx/issues?utf8=%E2%9C%93&q=");
            foreach (DataModelIssue i in issues)
            {
                link.AppendFormat("{0}%20", i.Number);
            }
            return link;
        }
        */
    }
}
