using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class AlertsReport
    {
        bool skipEmail;

        public AlertsReport(string alertsXmlFileName, bool skipEmail)
        {
            ConfigLoader loader = new ConfigLoader();
            Alerts = loader.Load(alertsXmlFileName);

            this.skipEmail = skipEmail;

            string sendEmailEnvironmentVariable = Environment.GetEnvironmentVariable("SEND_EMAIL");
            if (sendEmailEnvironmentVariable == "0")
            {
                skipEmail = true;
            }
        }

        public void SendEmails(IssueCollection issuesStart, IssueCollection issuesEnd, string htmlTemplateFileName, string filteredAlertName)
        {
            string htmlTemplate = File.ReadAllText(htmlTemplateFileName);

            SmtpClient smtpClient = new SmtpClient("smtphost");
            smtpClient.UseDefaultCredentials = true;

            foreach (Alert alert in Alerts)
            {
                alert.Query.Validate(issuesEnd);
                IEnumerable<Issue> queryStart = alert.Query.Evaluate(issuesStart);
                IEnumerable<Issue> queryEnd = alert.Query.Evaluate(issuesEnd);

                IEnumerable<Issue> goneIssues = queryStart.Except(queryEnd);
                IEnumerable<Issue> newIssues = queryEnd.Except(queryStart);

                Console.WriteLine("Alert: {0}", alert.Name);
                if (!goneIssues.Any() && !newIssues.Any())
                {
                    Console.WriteLine("    No changes to the query, skipping.");
                    Console.WriteLine();
                    continue;
                }

                if ((filteredAlertName != null) && (filteredAlertName != alert.Name))
                {
                    Console.WriteLine("    Filtered alert");
                    Console.WriteLine();
                    continue;
                }

                // 
                // Prepare email
                // 

                MailMessage message = new MailMessage();
                message.From = new MailAddress(Environment.UserName + "@microsoft.com");
                foreach (Alert.User user in alert.Owners)
                {
                    message.To.Add(user.EmailAddress);
                }
                foreach (Alert.User user in alert.CCs)
                {
                    message.CC.Add(user.EmailAddress);
                }
                message.IsBodyHtml = true;

                // 
                // Substitute values in email template (incl. subject and body)
                // 

                string text = htmlTemplate;

                text = text.Replace("%ALERT_NAME%", alert.Name);

                // 
                // Parse shouldSendEmail
                // 

                bool shouldSendEmail = true;
                {
                    Regex sendEmailRegex = new Regex("%SEND_EMAIL%=(.*)\r\n");
                    Match sendEmailMatch = sendEmailRegex.Match(text);
                    if (sendEmailMatch.Success)
                    {
                        string sendEmail = sendEmailMatch.Groups[1].Value;
                        if (sendEmailMatch.NextMatch().Success)
                        {
                            throw new InvalidDataException(string.Format("Multiple %SEND_EMAIL% entries in email template {0}", htmlTemplateFileName));
                        }

                        if (sendEmail == "0")
                        {
                            shouldSendEmail = false;
                        }
                        else if (sendEmail == "1")
                        {
                            shouldSendEmail = true;
                        }
                        else
                        {
                            throw new InvalidDataException(string.Format("Invalid %SEND_EMAIL% value \"{1}\", either 0 or 1 value expected in email template {0}", htmlTemplateFileName, sendEmail));
                        }

                        text = sendEmailRegex.Replace(text, "");
                    }
                }
                if (skipEmail)
                {
                    shouldSendEmail = false;
                }

                // 
                // Parse file name
                // 

                string fileName;
                {
                    Regex fileNameRegex = new Regex("%FILE_NAME%=(.*)\r\n");
                    Match fileNameMatch = fileNameRegex.Match(text);
                    if (!fileNameMatch.Success)
                    {
                        throw new InvalidDataException(string.Format("Missing %FILE_NAME% entry in email template {0}", htmlTemplateFileName));
                    }
                    fileName = fileNameMatch.Groups[1].Value;
                    if (fileNameMatch.NextMatch().Success)
                    {
                        throw new InvalidDataException(string.Format("Multiple %FILE_NAME% entries in email template {0}", htmlTemplateFileName));
                    }
                    text = fileNameRegex.Replace(text, "");
                }

                // 
                // Parse email subject
                // 

                {
                    Regex titleRegex = new Regex("%SUBJECT%=(.*)\r\n");
                    Match titleMatch = titleRegex.Match(text);
                    if (!titleMatch.Success)
                    {
                        throw new InvalidDataException(string.Format("Can't find subject in email template {0}", htmlTemplateFileName));
                    }
                    message.Subject = titleMatch.Groups[1].Value;
                    if (titleMatch.NextMatch().Success)
                    {
                        throw new InvalidDataException(string.Format("Multiple subjects in email template {0}", htmlTemplateFileName));
                    }
                    text = titleRegex.Replace(text, "");
                }

                // 
                // Substitute parts of email body
                // 

                if (!goneIssues.Any() || !newIssues.Any())
                {
                    Regex regex = new Regex("%ALL_ISSUES_START%(.|\n)*%ALL_ISSUES_END%");
                    text = regex.Replace(text, "");

                    if (!goneIssues.Any())
                    {
                        regex = new Regex("%GONE_ISSUES_START%(.|\n)*%GONE_ISSUES_END%");
                        text = regex.Replace(text, "");
                    }
                    if (!newIssues.Any())
                    {
                        regex = new Regex("%NEW_ISSUES_START%(.|\n)*%NEW_ISSUES_END%");
                        text = regex.Replace(text, "");
                    }
                }
                text = text.Replace("%ALL_ISSUES_START%", "");
                text = text.Replace("%ALL_ISSUES_END%", "");
                text = text.Replace("%GONE_ISSUES_START%", "");
                text = text.Replace("%GONE_ISSUES_END%", "");
                text = text.Replace("%NEW_ISSUES_START%", "");
                text = text.Replace("%NEW_ISSUES_END%", "");

                text = text.Replace("%ALL_ISSUES_LINK%", GitHubQuery.GetHyperLink(newIssues.Concat(goneIssues)));
                text = text.Replace("%ALL_ISSUES_COUNT%", (goneIssues.Count() + newIssues.Count()).ToString());
                text = text.Replace("%GONE_ISSUES_LINK%", GitHubQuery.GetHyperLink(goneIssues));
                text = text.Replace("%GONE_ISSUES_COUNT%", goneIssues.Count().ToString());
                text = text.Replace("%NEW_ISSUES_LINK%", GitHubQuery.GetHyperLink(newIssues));
                text = text.Replace("%NEW_ISSUES_COUNT%", newIssues.Count().ToString());

                IEnumerable<IssueEntry> newIssueEntries = newIssues.Select(issue => new IssueEntry(issue));
                text = text.Replace("%NEW_ISSUES_TABLE%", FormatIssueTable(newIssueEntries));
                IEnumerable<IssueEntry> goneIssueEntries = goneIssues.Select(issue =>
                    {
                        Issue newIssue = issuesEnd.GetIssue(issue.Number);
                        if (newIssue == null)
                        {   // Closed issue
                            return new IssueEntry(issue, "Closed");
                        }
                        return new IssueEntry(newIssue);
                    });
                text = text.Replace("%GONE_ISSUES_TABLE%", FormatIssueTable(goneIssueEntries));

                message.Body = text;

                // 
                // Generate report file and send email
                // 

                bool fileWritten = false;
                if (fileName != "")
                {
                    try
                    {
                        File.WriteAllText(fileName, text);
                        fileWritten = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR writing alert {0} into file {1}", alert.Name, fileName);
                        Console.WriteLine(ex);
                    }
                }

                bool emailSent = false;
                try
                {
                    if (shouldSendEmail)
                    {
                        smtpClient.Send(message);
                        emailSent = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR sending alert {0}", alert.Name);
                    Console.WriteLine(ex);
                }

                // 
                // Logging
                // 

                if (fileName != "")
                {
                    Console.WriteLine("    Report: {0}", fileName);
                    if (!fileWritten)
                    {
                        Console.WriteLine("        FAILED!!!");
                    }
                }
                Console.WriteLine("    Email: {0}", emailSent ? "sent" : (shouldSendEmail ? "FAILED!!!" : "skipped"));
                Console.WriteLine("        Subject: {0}", message.Subject);
                Console.WriteLine("        To:");
                foreach (Alert.User user in alert.Owners)
                {
                    Console.WriteLine("            {0} - {1}", user.Name, user.EmailAddress);
                }
                Console.WriteLine("        CC:");
                foreach (Alert.User user in alert.CCs)
                {
                    Console.WriteLine("            {0} - {1}", user.Name, user.EmailAddress);
                }
                if (fileName == "")
                {
                    Console.Write(text.Replace("<br/>", ""));
                }
                Console.WriteLine();
            }
        }

        struct IssueEntry
        {
            public string IssueId;
            public string Title;
            public string LabelsText;
            public string AssignedToText;

            public IssueEntry(Issue issue, string assignedToOverride = null)
            {
                string idPrefix = "";
                if (issue.IsPullRequest)
                {
                    idPrefix = "PR ";
                }
                IssueId = string.Format("{0}#<a href=\"{1}\">{2}</a>", idPrefix, issue.HtmlUrl, issue.Number);
                Title = issue.Title;
                LabelsText = string.Join(", ", issue.Labels.Select(l => l.Name));
                if (assignedToOverride != null)
                {
                    AssignedToText = assignedToOverride;
                }
                else if (issue.Assignee != null)
                {
                    AssignedToText = string.Format("<a href=\"{0}\">@{1}</a>", issue.Assignee.HtmlUrl, issue.Assignee.Login);
                }
                else
                {
                    AssignedToText = "";
                }
            }
        }

        string FormatIssueTable(IEnumerable<IssueEntry> issues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<table>");
            text.AppendLine("  <tr>");
            text.AppendLine("    <th>Issue #</th>");
            text.AppendLine("    <th>Title</th>");
            text.AppendLine("    <th>Assigned To</th>");
            text.AppendLine("  </tr>");
            foreach (IssueEntry issue in issues)
            {
                text.AppendLine(  "  <tr>");
                text.AppendFormat("    <td>{0}</td>", issue.IssueId).AppendLine();
                text.AppendLine(  "    <td>");
                text.AppendFormat("      {0}", issue.Title).AppendLine();
                if (issue.LabelsText != null)
                {
                    text.AppendFormat("      <br/><div class=\"labels\">Labels: {0}</div>", issue.LabelsText).AppendLine();
                }
                text.AppendLine(  "    </td>");
                text.AppendFormat("    <td>{0}</td>", issue.AssignedToText).AppendLine();
                text.AppendLine(  "  </tr>");
            }
            text.AppendLine("</table>");

            return text.ToString();
        }

        IEnumerable<Alert> Alerts = new List<Alert>();

        private class ConfigLoader
        {
            List<Alert.User> Users = new List<Alert.User>();

            public IEnumerable<Alert> Load(string alertsXmlFileName)
            {
                XElement root = XElement.Load(alertsXmlFileName);

                foreach (XElement usersNode in root.Descendants("users"))
                {
                    string defaultEmailServer = null;
                    XAttribute defaultEmailServerAttribute = usersNode.Attribute("default-email-server");
                    if (defaultEmailServerAttribute != null)
                    {
                        defaultEmailServer = defaultEmailServerAttribute.Value;
                        if (!defaultEmailServer.Contains('@'))
                        {
                            defaultEmailServer = "@" + defaultEmailServer;
                        }
                    }

                    foreach (XElement userNode in usersNode.Descendants("user"))
                    {
                        string name = userNode.Attribute("name").Value;
                        string emailAlias = userNode.Attribute("alias").Value;
                        string gitHubLogin = userNode.Attribute("github").Value;

                        if (!gitHubLogin.StartsWith("@"))
                        {
                            throw new InvalidDataException("GitHub login expected to start with @: " + gitHubLogin);
                        }
                        if (emailAlias.StartsWith("@"))
                        {
                            throw new InvalidDataException("Alias cannot start with @: " + emailAlias);
                        }

                        if (FindUser(gitHubLogin) != null)
                        {
                            throw new InvalidDataException("Duplicate user defined with GitHub login: " + gitHubLogin);
                        }
                        if (FindUser(emailAlias) != null)
                        {
                            throw new InvalidDataException("Duplicate user defined with alias: " + emailAlias);
                        }

                        string email;
                        if (emailAlias.Contains('@'))
                        {
                            email = emailAlias;
                            emailAlias = null;
                        }
                        else
                        {
                            email = emailAlias + defaultEmailServer;
                        }

                        Users.Add(new Alert.User(name, email, emailAlias, gitHubLogin));
                    }
                }

                foreach (XElement alertsNode in root.Descendants("alerts"))
                {
                    foreach (XElement alertNode in alertsNode.Descendants("alert"))
                    {
                        string alertName = alertNode.Attribute("name").Value;

                        string query = alertNode.Descendants("query").First().Value;
                        IEnumerable<Alert.User> owners = alertNode.Descendants("owner").Select(e => FindUserOrThrow(e.Value));
                        IEnumerable<Alert.User> ccUsers = alertNode.Descendants("cc").Select(e => FindUserOrThrow(e.Value));

                        Alert alert;
                        try
                        {
                            alert = new Alert(alertName, query, owners, ccUsers);
                        }
                        catch (InvalidQueryException ex)
                        {
                            throw new InvalidDataException("Invalid query in alert: " + alertName, ex);
                        }
                        yield return alert;
                    }
                }
            }

            Alert.User FindUser(string id)
            {
                foreach (Alert.User user in Users)
                {
                    if ((user.EmailAlias == id) || (user.GitHubLogin == id) || (user.EmailAddress == id))
                    {
                        return user;
                    }
                }
                return null;
            }
            Alert.User FindUserOrThrow(string id)
            {
                Alert.User user = FindUser(id);
                if (user == null)
                {
                    throw new InvalidDataException("Cannot find user: " + id);
                }
                return user;
            }
        }
    }
}
