using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class AlertReport_Diff : AlertReport
    {
        public AlertReport_Diff(Alert alert, bool sendEmail, string htmlTemplateFileName) : base(alert, sendEmail, htmlTemplateFileName)
        {
        }

        /// <summary>
        /// Returns true if the body is filled from this method
        /// </summary>
        public override bool FillReportBody(IssueCollection collection1, IssueCollection collection2)
        {
            IEnumerable<DataModelIssue> queryStart = _alert.Query.Evaluate(collection1);
            IEnumerable<DataModelIssue> queryEnd = _alert.Query.Evaluate(collection2);
            IEnumerable<DataModelIssue> goneIssues = queryStart.Except(queryEnd);
            IEnumerable<DataModelIssue> newIssues = queryEnd.Except(queryStart);

            if (!goneIssues.Any() && !newIssues.Any())
            {
                Console.WriteLine("    No changes to the query, skipping.");
                Console.WriteLine();
                return false;
            }

            if (!goneIssues.Any() || !newIssues.Any())
            {
                Regex regex = new Regex("%ALL_ISSUES_START%(.|\n)*%ALL_ISSUES_END%");
                BodyText = regex.Replace(BodyText, "");

                if (!goneIssues.Any())
                {
                    regex = new Regex("%GONE_ISSUES_START%(.|\n)*%GONE_ISSUES_END%");
                    BodyText = regex.Replace(BodyText, "");
                }
                if (!newIssues.Any())
                {
                    regex = new Regex("%NEW_ISSUES_START%(.|\n)*%NEW_ISSUES_END%");
                    BodyText = regex.Replace(BodyText, "");
                }
            }
            BodyText = BodyText.Replace("%ALL_ISSUES_START%", "");
            BodyText = BodyText.Replace("%ALL_ISSUES_END%", "");
            BodyText = BodyText.Replace("%GONE_ISSUES_START%", "");
            BodyText = BodyText.Replace("%GONE_ISSUES_END%", "");
            BodyText = BodyText.Replace("%NEW_ISSUES_START%", "");
            BodyText = BodyText.Replace("%NEW_ISSUES_END%", "");

            BodyText = BodyText.Replace("%ALL_ISSUES_LINK%", GitHubQuery.GetHyperLink(newIssues.Concat(goneIssues)));
            BodyText = BodyText.Replace("%ALL_ISSUES_COUNT%", (goneIssues.Count() + newIssues.Count()).ToString());
            BodyText = BodyText.Replace("%GONE_ISSUES_LINK%", GitHubQuery.GetHyperLink(goneIssues));
            BodyText = BodyText.Replace("%GONE_ISSUES_COUNT%", goneIssues.Count().ToString());
            BodyText = BodyText.Replace("%NEW_ISSUES_LINK%", GitHubQuery.GetHyperLink(newIssues));
            BodyText = BodyText.Replace("%NEW_ISSUES_COUNT%", newIssues.Count().ToString());

            IEnumerable<IssueEntry> newIssueEntries = newIssues.Select(issue => new IssueEntry(issue));
            BodyText = BodyText.Replace("%NEW_ISSUES_TABLE%", FormatIssueTable(newIssueEntries));
            IEnumerable<IssueEntry> goneIssueEntries = goneIssues.Select(issue =>
            {
                DataModelIssue newIssue = collection2.GetIssue(issue.Number);
                if (newIssue == null)
                {   // Closed issue
                    return new IssueEntry(issue, "Closed");
                }
                return new IssueEntry(newIssue);
            });
            BodyText = BodyText.Replace("%GONE_ISSUES_TABLE%", FormatIssueTable(goneIssueEntries));
            return true;
        }
    }
}
