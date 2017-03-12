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
using BugReport.Util;

namespace BugReport.Reports
{
    public enum AlertType
    {
        Diff,
        Untriaged,
        NeedsResponse
    }

    public class AlertReporting
    {
        private bool _skipEmail;
        private AlertType _alertType;
        private Config _config;
        private string _htmlTemplateFileName;
        private string _outputHtmlFileName;

        public AlertReporting(
            AlertType alertType,
            IEnumerable<string> configFiles,
            string htmlTemplateFileName,
            bool skipEmail, 
            string outputHtmlFileName)
        {
            _config = new Config(configFiles);

            _alertType = alertType;
            _skipEmail = skipEmail;
            _htmlTemplateFileName = htmlTemplateFileName;
            _outputHtmlFileName = outputHtmlFileName;

            // Environment variable override of sending emails
            string sendEmailEnvironmentVariable = Environment.GetEnvironmentVariable("SEND_EMAIL");
            if (sendEmailEnvironmentVariable == "0")
            {
                skipEmail = true;
            }
        }

        /// <summary>
        /// Sends all of the emails for the diffs of the given IssueCollections that match the filter.
        /// </summary>
        /// <returns>True if all emails successfully sent</returns>
        public bool SendEmails(
            IEnumerable<DataModelIssue> beginIssues, 
            IEnumerable<DataModelIssue> endIssues, 
            IEnumerable<string> filteredAlertNames)
        {
            bool isAllEmailsSendSuccessful = true;
            SmtpClient smtpClient = null;
            if (!_skipEmail)
            {
                smtpClient = new SmtpClient("smtphost");
                smtpClient.UseDefaultCredentials = true;
            }
            foreach (Alert alert in _config.Alerts)
            {
                Console.WriteLine("Alert: {0}", alert.Name);
                if ((filteredAlertNames != null) && !filteredAlertNames.ContainsIgnoreCase(alert.Name))
                {
                    Console.WriteLine("    Filtered alert");
                    Console.WriteLine();
                }
                else
                {
                    isAllEmailsSendSuccessful &= SendEmail(beginIssues, endIssues, alert, smtpClient);
                }
            }

            return isAllEmailsSendSuccessful;
        }

        /// <summary>
        /// Produces a report for the given alert and optionally sends it using the smptclient or writes
        /// it to a file depending on the html template.
        /// </summary>
        /// <returns>True if the email was successfully sent or if it didn't need to be sent</returns>
        private bool SendEmail(
            IEnumerable<DataModelIssue> issues1, 
            IEnumerable<DataModelIssue> issues2, 
            Alert alert, 
            SmtpClient smtpClient)
        {
            AlertReport report;
            if (_alertType == AlertType.Diff)
            {
                report = new AlertReport_Diff(alert, !_skipEmail, _htmlTemplateFileName, _outputHtmlFileName);
            }
            else if (_alertType == AlertType.Untriaged)
            {
                report = new AlertReport_Untriaged(alert, !_skipEmail, _htmlTemplateFileName, _outputHtmlFileName, _config.UntriagedExpression);
            }
            else if (_alertType == AlertType.NeedsResponse)
            {
                report = new AlertReport_NeedsResponse(alert, !_skipEmail, _htmlTemplateFileName, _outputHtmlFileName);
            }
            else
            {
                throw new Exception("Invalid Alert Type for reporting");
            }

            // Create the report and send the email for it
            if (report.FillReportBody(issues1, issues2))
            {
                bool fileWritten = string.IsNullOrEmpty(report.OutputHtmlFileName) ? WriteReportFile(alert, report) : true;
                bool emailSent = report.ShouldSendEmail ? SendEmail(alert, report, smtpClient) : false;
                PrintLogs(report, alert, fileWritten, emailSent);
                return emailSent;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Writes the full body text of the given report to the filename specified in the reporting template.
        /// </summary>
        /// <returns>true if succeeded</returns>
        private bool WriteReportFile(Alert alert, AlertReport report)
        {
            try
            {
                File.WriteAllText(report.OutputHtmlFileName, report.BodyText);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR writing alert {0} into file {1}", alert.Name, report.OutputHtmlFileName);
                Console.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Generates and sends an email for the given alert and report using the smtpClient.
        /// </summary>
        /// <returns>true if the email is generated and sent successfully</returns>
        private bool SendEmail(Alert alert, AlertReport report, SmtpClient smtpClient)
        {
            // Prepare email
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
            message.Subject = report.Subject;
            message.Body = report.BodyText;

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
        public void PrintLogs(AlertReport report, Alert alert, bool fileWritten, bool emailSent)
        {
            // Logging
            if (report.OutputHtmlFileName != "")
            {
                Console.WriteLine("    Report: {0}", report.OutputHtmlFileName);
                if (!fileWritten)
                {
                    Console.WriteLine("        FAILED!!!");
                }
            }
            Console.WriteLine("    Email: {0}", emailSent ? "sent" : (report.ShouldSendEmail ? "FAILED!!!" : "skipped"));
            Console.WriteLine("        Subject: {0}", report.Subject);
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
            if (report.OutputHtmlFileName == "")
            {
                Console.Write(report.BodyText.Replace("<br/>", ""));
            }
            Console.WriteLine();
        }
    }
}
