using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private IEnumerable<string> _filteredAlertNames;

        public AlertReporting(
            AlertType alertType,
            IEnumerable<string> configFiles,
            string htmlTemplateFileName,
            bool skipEmail, 
            string outputHtmlFileName,
            IEnumerable<string> filteredAlertNames)
        {
            _config = new Config(configFiles);

            _alertType = alertType;
            _skipEmail = skipEmail;
            _htmlTemplateFileName = htmlTemplateFileName;
            _outputHtmlFileName = outputHtmlFileName;
            _filteredAlertNames = filteredAlertNames;

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
            IEnumerable<DataModelIssue> issues1, 
            IEnumerable<DataModelIssue> issues2)
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
                if ((_filteredAlertNames != null) && !_filteredAlertNames.ContainsIgnoreCase(alert.Name))
                {
                    Console.WriteLine("    Filtered alert");
                    Console.WriteLine();
                }
                else
                {
                    isAllEmailsSendSuccessful &= SendEmail(issues1, issues2, alert, smtpClient);
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
                report = new AlertReport_Diff(alert, _htmlTemplateFileName);
            }
            else if (_alertType == AlertType.Untriaged)
            {
                report = new AlertReport_Untriaged(alert, _htmlTemplateFileName, _config.UntriagedExpression);
            }
            else if (_alertType == AlertType.NeedsResponse)
            {
                report = new AlertReport_NeedsResponse(alert, _htmlTemplateFileName);
            }
            else
            {
                throw new Exception("Invalid Alert Type for reporting");
            }

            // Create the report and send the email for it
            if (report.FillReportBody(issues1, issues2))
            {
                bool fileWritten = WriteReportFile(alert, report);
                bool emailSent = _skipEmail ? false : SendEmail(alert, report, smtpClient);
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
            if (string.IsNullOrEmpty(_outputHtmlFileName))
            {
                Console.WriteLine("    Report: {0}", _outputHtmlFileName);
                if (!fileWritten)
                {
                    Console.WriteLine("        FAILED!!!");
                }
            }
            Console.WriteLine("    Email: {0}", emailSent ? "sent" : (_skipEmail ? "skipped" : "FAILED!!!"));
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
            if (string.IsNullOrEmpty(_outputHtmlFileName))
            {
                Console.Write(report.BodyText.Replace("<br/>", ""));
            }
            Console.WriteLine();
        }
    }
}
