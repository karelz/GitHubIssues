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
    public class AlertReport_NeedsMSResponse : AlertReport
    {
        private TimeSpan _acceptableResponseDelay = new TimeSpan(5, 0, 0, 0, 0); // 5 days

        public AlertReport_NeedsMSResponse(Alert alert, bool sendEmail, string htmlTemplateFileName) 
            : base(alert, sendEmail, htmlTemplateFileName)
        {
        }

        /// <summary>
        /// Returns true if the body is filled from this method
        /// </summary>
        public override bool FillReportBody(IssueCollection collection1, IssueCollection collection2)
        {
            // Create a Dictionary mapping issues to comments for that issue
            Dictionary<int, List<DataModelIssue>> issueComments = new Dictionary<int, List<DataModelIssue>>();
            Dictionary<int, DataModelIssue> issues = new Dictionary<int, DataModelIssue>();
            IEnumerable<DataModelIssue> matchingIssues = _alert.Query.Evaluate(collection1);
            if (!matchingIssues.Any())
            {
                Console.WriteLine("    No changes to the query, skipping.");
                Console.WriteLine();
                return false;
            }
            foreach (DataModelIssue issue in matchingIssues)
            {
                issueComments.Add(issue.Number, new List<DataModelIssue>());
                issues.Add(issue.Number, issue);
            }
            foreach (DataModelIssue comment in collection2.Issues)
            {
                int startIndex = comment.HtmlUrl.IndexOf("/issues/") + 8;
                if (startIndex < 8)
                    startIndex = comment.HtmlUrl.IndexOf("/pull/") + 6;
                int endIndex = comment.HtmlUrl.IndexOf("#");
                string issueString = comment.HtmlUrl.Substring(startIndex, endIndex - startIndex);
                int issueID = int.Parse(issueString);
                if (issueComments.ContainsKey(issueID))
                    issueComments[issueID].Add(comment);
            }

            // Filter our issues to ones that haven't had an owner response after our grace waiting period
            Dictionary<DataModelIssue, TimeSpan?> needsResponse = new Dictionary<DataModelIssue, TimeSpan?>();
            foreach (KeyValuePair<int, List<DataModelIssue>> pair in issueComments)
            {
                TimeSpan? lastComment;
                // First check if there are no comments and the issue was opened past the threshold.
                if (pair.Value.Count == 0 && ((lastComment = (DateTime.Now - issues[pair.Key].CreatedAt)) > _acceptableResponseDelay))
                    needsResponse.Add(issues[pair.Key], lastComment);

                // Next check if the last issue occurred past the threshold
                else if (pair.Value.Count > 0 && ((lastComment = (DateTime.Now - pair.Value.Max((issue) => issue.CreatedAt))) > _acceptableResponseDelay))
                    needsResponse.Add(issues[pair.Key], lastComment);
            }

            if (!needsResponse.Any())
            {
                Console.WriteLine("    No changes to the query, skipping.");
                Console.WriteLine();
                return false;
            }
            else
            {
                BodyText = BodyText.Replace("%NEEDSMSRESPONSE_ACCEPTABLE_RESPONSE_DELAY%", _acceptableResponseDelay.Days.ToString());

                BodyText = BodyText.Replace("%NEEDSMSRESPONSE_ISSUES_START%", "");
                BodyText = BodyText.Replace("%NEEDSMSRESPONSE_ISSUES_END%", "");

                BodyText = BodyText.Replace("%NEEDSMSRESPONSE_ISSUES_LINK%", GitHubQuery.GetHyperLink(needsResponse.Keys));
                BodyText = BodyText.Replace("%NEEDSMSRESPONSE_ISSUES_COUNT%", needsResponse.Count().ToString());

                BodyText = BodyText.Replace("%NEEDSMSRESPONSE_ISSUES_TABLE%", FormatIssueTable(needsResponse.OrderByDescending((pair) => pair.Value.Value.Days)));
                return true;
            }
        }

        protected string FormatIssueTable(IEnumerable<KeyValuePair<DataModelIssue, TimeSpan?>> issues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table>");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Days since last comment</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("  </tr>");
            foreach (var pair in issues)
            {
                IssueEntry entry = new IssueEntry(pair.Key);
                text.AppendLine("  <tr>");
                text.AppendLine($"    <td>{entry.IssueId}</td>");
                text.AppendLine($"    <td>{pair.Value.Value.Days}</td>");
                text.AppendLine("    <td>");
                text.AppendLine($"      {entry.Title}");
                if (entry.LabelsText != null)
                {
                    text.AppendLine($"      <br/><div class=\"labels\">Labels: {entry.LabelsText}</div>");
                }
                text.AppendLine("    </td>");
                text.AppendLine($"    <td>{entry.AssignedToText}</td>");
                text.AppendLine("  </tr>");
            }
            text.AppendLine("</table>");

            return text.ToString();
        }
    }
}
