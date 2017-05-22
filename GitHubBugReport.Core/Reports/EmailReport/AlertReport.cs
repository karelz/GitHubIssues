using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Repositories.Models;
using GitHubBugReport.Core.Repositories.Services;
using GitHubBugReport.Core.Util;

namespace GitHubBugReport.Core.Reports.EmailReport
{
    public class AlertReport
    {
        private readonly bool _skipEmail;
        private readonly Config _config;
        private readonly string _htmlTemplateFileName;
        private readonly string _outputHtmlFileName;
        private readonly IEnumerable<string> _filteredAlertNames;
        private readonly GenerateReport _generateReport;

        public delegate string GenerateReport(Alert alert, string htmlTemplate);

        private AlertReport(
            Config config,
            string htmlTemplateFileName,
            bool skipEmail, 
            string outputHtmlFileName,
            IEnumerable<string> filteredAlertNames,
            GenerateReport generateReport)
        {
            _config = config;

            _htmlTemplateFileName = htmlTemplateFileName;
            _skipEmail = skipEmail;
            _outputHtmlFileName = outputHtmlFileName;
            _filteredAlertNames = filteredAlertNames;
            _generateReport = generateReport;

            // Environment variable override of sending emails
            string sendEmailEnvironmentVariable = Environment.GetEnvironmentVariable("SEND_EMAIL");
            if (sendEmailEnvironmentVariable == "0")
            {
                _skipEmail = true;
            }
        }

        public static bool SendEmails(
            Config config,
            string htmlTemplateFileName,
            bool skipEmail,
            string outputHtmlFileName,
            IEnumerable<string> filteredAlertNames,
            GenerateReport generateReport)
        {
            AlertReport report = new AlertReport(
                config,
                htmlTemplateFileName,
                skipEmail,
                outputHtmlFileName,
                filteredAlertNames,
                generateReport);
            return report.SendEmails();
        }

        /// <summary>
        /// Sends all of the emails for the diffs of the given issues that match the filter.
        /// </summary>
        /// <returns>True if all emails successfully sent</returns>
        private bool SendEmails()
        {
            bool isAllEmailsSendSuccessful = true;

            SmtpClient smtpClient = null;
            if (!_skipEmail)
            {
                smtpClient = new SmtpClient("smtphost");
                smtpClient.UseDefaultCredentials = true;
            }

            foreach (Alert alert in _config.Alerts.Where(alert => alert.Owners.Any()))
            {
                Console.WriteLine("Alert: {0}", alert.Name);
                if ((_filteredAlertNames != null) && 
                    _filteredAlertNames.Where(name => alert.EqualsByName(name)).None())
                {
                    Console.WriteLine("    Filtered alert");
                    Console.WriteLine();
                }
                else
                {
                    // Create the email report
                    ReportEmail reportEmail = CreateReportEmail(alert, _htmlTemplateFileName);

                    if (reportEmail.HasContent)
                    {   // Send the email
                        bool fileWritten = WriteReportFile(alert, reportEmail);
                        bool isEmailSendSuccessful = _skipEmail ? true : SendEmail(alert, reportEmail, smtpClient);
                        PrintLogs(reportEmail, alert, fileWritten, isEmailSendSuccessful);

                        isAllEmailsSendSuccessful &= isEmailSendSuccessful;
                    }
                }
            }

            return isAllEmailsSendSuccessful;
        }

        /// <summary>
        /// Writes the full body text of the given report to the filename specified in the reporting template.
        /// </summary>
        /// <returns>true if succeeded</returns>
        private bool WriteReportFile(Alert alert, ReportEmail report)
        {
            if (string.IsNullOrEmpty(_outputHtmlFileName))
            {   // No file needs to be written - skipping
                return true;
            }
            try
            {
                File.WriteAllText(_outputHtmlFileName, report.BodyText);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR writing alert {0} into file {1}", alert.Name, _outputHtmlFileName);
                Console.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Generates and sends an email for the given alert and report using the smtpClient.
        /// </summary>
        /// <returns>true if the email is generated and sent successfully</returns>
        private bool SendEmail(Alert alert, ReportEmail reportEmail, SmtpClient smtpClient)
        {
            // Prepare email
            MailMessage message = new MailMessage { From = new MailAddress(Environment.UserName + "@microsoft.com") };

            foreach (Alert.User user in alert.Owners)
            {
                message.To.Add(user.EmailAddress);
            }

            foreach (Alert.User user in alert.CCs)
            {
                message.CC.Add(user.EmailAddress);
            }

            message.IsBodyHtml = true;
            message.Subject = reportEmail.Subject;
            message.Body = reportEmail.BodyText;

            // Send email
            try
            {
                smtpClient.Send(message);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR sending alert {0}", alert.Name);
                Console.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Prints the status of the full report generation and completion
        /// </summary>
        private void PrintLogs(ReportEmail reportEmail, Alert alert, bool fileWritten, bool emailSent)
        {
            // Logging
            if (string.IsNullOrEmpty(_outputHtmlFileName))
            {
                Console.WriteLine("    Report: {0}", _outputHtmlFileName);
                if (!fileWritten)
                {
                    Console.WriteLine("        FAILED!!!");
                }
            }

            Console.WriteLine("    Email: {0}", emailSent ? (_skipEmail ? "skipped" : "sent") : "FAILED!!!");
            Console.WriteLine("        Subject: {0}", reportEmail.Subject);
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

            if (string.IsNullOrEmpty(_outputHtmlFileName))
            {
                Console.Write(reportEmail.BodyText.Replace("<br/>", ""));
            }

            Console.WriteLine();
        }

        private struct ReportEmail
        {
            public string Subject;
            public string BodyText;

            public ReportEmail(string subject, string bodyText)
            {
                Subject = subject;
                BodyText = bodyText;
            }

            public bool HasContent => (BodyText != null);
        }

        private ReportEmail CreateReportEmail(Alert alert, string htmlTemplateFileName)
        {
            string bodyText = File.ReadAllText(htmlTemplateFileName);
            bodyText = bodyText.Replace("%ALERT_NAME%", alert.Name);

            string subject = ExtractTag("%SUBJECT%", ref bodyText, htmlTemplateFileName);

            bodyText = _generateReport(alert, bodyText);
            
            return new ReportEmail(subject, bodyText);
        }

        // Returns value from tag (e.g. %SUBJECT%=<value>), extracts the entire line from the bodyText
        private static string ExtractTag(string tag, ref string bodyText, string htmlTemplateFileName)
        {
            Regex regex = new Regex(tag + "=(.*)\r\n");
            Match match = regex.Match(bodyText);
            if (!match.Success)
            {
                throw new InvalidDataException($"Missing {tag} entry in email template {htmlTemplateFileName}");
            }
            string foundValue = match.Groups[1].Value;
            if (match.NextMatch().Success)
            {
                throw new InvalidDataException($"Multiple {tag} entries in email template {htmlTemplateFileName}");
            }
            bodyText = regex.Replace(bodyText, "");
            return foundValue;
        }

        public static string GetLinkedCount(string queryPrefix, IEnumerable<DataModelIssue> issues)
        {
            int count = issues.Count();

            // TODO: When the time is right, inject this.
            IRepositoryService repositoryService = new OctoKitRepositoryService();

            IEnumerable<Repository> repos = repositoryService.GetReposOrDefault(issues);

            if (repos.Count() <= 1)
            {
                Repository repo = repos.First();
                return $"<a href=\"{repo.GetQueryUrl(queryPrefix, issues)}\">{count}</a>";
            }
            else
            {
                return $"{count} <small>(" +
                    string.Join(" + ", repos.Select(
                        repo => $"<a href=\"{repo.GetQueryUrl(queryPrefix, issues.Where(repo))}\">{issues.Where(repo).Count()}</a>")) +
                    ")</small>";
            }
        }
    }
}
