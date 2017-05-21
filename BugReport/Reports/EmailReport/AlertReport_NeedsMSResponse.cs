using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using BugReport.DataModel;
using BugReport.Util;
using GitHubBugReport.Core.Issues.Models;

namespace BugReport.Reports.EmailReports
{
    public class AlertReport_NeedsResponse
    {
        // TODO - define in config
        private static TimeSpan _acceptableResponseDelay = new TimeSpan(5, 0, 0, 0, 0); // 5 days

        public static bool SendEmails(
            Config config,
            string htmlTemplateFileName,
            bool skipEmail,
            string outputHtmlFileName,
            IEnumerable<string> filteredAlertNames,
            IEnumerable<DataModelIssue> issues,
            IEnumerable<DataModelIssue> comments)
        {
            return AlertReport.SendEmails(
                config,
                htmlTemplateFileName,
                skipEmail,
                outputHtmlFileName,
                filteredAlertNames,
                (Alert alert, string htmlTemplate) =>
                    GenerateReport(alert, htmlTemplate, issues, comments));
        }

        // Returns null if the report is empty
        protected static string GenerateReport(
            Alert alert,
            string htmlTemplate,
            IEnumerable<DataModelIssue> issues,
            IEnumerable<DataModelIssue> comments)
        {
            // Create a Dictionary mapping issues to comments for that issue
            Dictionary<int, List<DataModelIssue>> issueComments = new Dictionary<int, List<DataModelIssue>>();
            Dictionary<int, DataModelIssue> issuesMap = new Dictionary<int, DataModelIssue>();
            IEnumerable<DataModelIssue> matchingIssues = alert.Query.Evaluate(issues);
            if (matchingIssues.None())
            {
                Console.WriteLine("    No changes to the query, skipping.");
                Console.WriteLine();
                return null;
            }
            foreach (DataModelIssue issue in matchingIssues)
            {
                issueComments.Add(issue.Number, new List<DataModelIssue>());
                issuesMap.Add(issue.Number, issue);
            }
            foreach (DataModelIssue comment in comments)
            {
                int startIndex = comment.HtmlUrl.IndexOf("/issues/") + 8;
                if (startIndex < 8)
                {
                    startIndex = comment.HtmlUrl.IndexOf("/pull/") + 6;
                }
                int endIndex = comment.HtmlUrl.IndexOf("#");

                string issueString = comment.HtmlUrl.Substring(startIndex, endIndex - startIndex);
                int issueID = int.Parse(issueString);
                if (issueComments.ContainsKey(issueID))
                {
                    issueComments[issueID].Add(comment);
                }
            }

            // Filter our issues to ones that haven't had an owner response after our grace waiting period
            Dictionary<DataModelIssue, TimeSpan?> needsResponse = new Dictionary<DataModelIssue, TimeSpan?>();
            foreach (KeyValuePair<int, List<DataModelIssue>> pair in issueComments)
            {
                TimeSpan? lastComment;
                // First check if there are no comments and the issue was opened past the threshold.
                if (pair.Value.Count == 0 && ((lastComment = (DateTime.Now - issuesMap[pair.Key].CreatedAt)) > _acceptableResponseDelay))
                    needsResponse.Add(issuesMap[pair.Key], lastComment);

                // Next check if the last issue occurred past the threshold
                else if (pair.Value.Count > 0 && ((lastComment = (DateTime.Now - pair.Value.Max((issue) => issue.CreatedAt))) > _acceptableResponseDelay))
                    needsResponse.Add(issuesMap[pair.Key], lastComment);
            }

            if (needsResponse.None())
            {
                Console.WriteLine("    No changes to the query, skipping.");
                Console.WriteLine();
                return null;
            }

            string text = htmlTemplate;

            text = text.Replace("%NEEDSMSRESPONSE_ACCEPTABLE_RESPONSE_DELAY%", _acceptableResponseDelay.Days.ToString());

            text = text.Replace("%NEEDSMSRESPONSE_ISSUES_START%", "");
            text = text.Replace("%NEEDSMSRESPONSE_ISSUES_END%", "");

            text = text.Replace("%NEEDSMSRESPONSE_ISSUES_LINKED_COUNTS%",
                AlertReport.GetLinkedCount("is:issue is:open", needsResponse.Keys));
            text = text.Replace("%NEEDSMSRESPONSE_ISSUES_COUNT%", needsResponse.Count().ToString());

            text = text.Replace("%NEEDSMSRESPONSE_ISSUES_TABLE%", FormatIssueTable(needsResponse.OrderByDescending((pair) => pair.Value.Value.Days)));

            return text;
        }

        private static string FormatIssueTable(IEnumerable<KeyValuePair<DataModelIssue, TimeSpan?>> issues)
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
                text.AppendLine($"      {HttpUtility.HtmlEncode(entry.Title)}");
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
