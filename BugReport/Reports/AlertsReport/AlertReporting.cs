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
    public enum AlertType
    {
        Diff,
        Untriaged,
        NeedsMSResponse
    }

    public class AlertReporting
    {
        private bool _skipEmail;
        private AlertType _type;
        private IEnumerable<Alert> _alerts;
        private IEnumerable<Label> _labels;
        private string _htmlTemplateFileName;

        public AlertReporting(string configFileName, bool skipEmail, string htmlTemplateFileName, AlertType type)
        {
            ConfigLoader loader = new ConfigLoader();
            loader.Load(configFileName, out _alerts, out _labels);

            _type = type;
            _skipEmail = skipEmail;
            _htmlTemplateFileName = htmlTemplateFileName;
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
        public bool SendEmails(IssueCollection collection1, IssueCollection collection2, string filteredAlertName)
        {
            bool isAllEmailsSendSuccessful = true;
            SmtpClient smtpClient = null;
            if (!_skipEmail)
            {
                smtpClient = new SmtpClient("smtphost");
                smtpClient.UseDefaultCredentials = true;
            }
            foreach (Alert alert in _alerts)
            {
                Console.WriteLine("Alert: {0}", alert.Name);
                if ((filteredAlertName != null) && (filteredAlertName != alert.Name))
                {
                    Console.WriteLine("    Filtered alert");
                    Console.WriteLine();
                }
                else
                {
                    isAllEmailsSendSuccessful &= SendEmail(collection1, collection2, alert, smtpClient);
                }
            }

            return isAllEmailsSendSuccessful;
        }

        /// <summary>
        /// Produces a report for the given alert and optionally sends it using the smptclient or writes
        /// it to a file depending on the html template.
        /// </summary>
        /// <returns>True if the email was successfully sent or if it didn't need to be sent</returns>
        private bool SendEmail(IssueCollection collection1, IssueCollection collection2, Alert alert, SmtpClient smtpClient)
        {
            AlertReport report;
            if (_type == AlertType.Diff)
                report = new AlertReport_Diff(alert, !_skipEmail, _htmlTemplateFileName);
            else if (_type == AlertType.Untriaged)
                report = new AlertReport_Untriaged(alert, !_skipEmail, _htmlTemplateFileName);
            else if (_type == AlertType.NeedsMSResponse)
                report = new AlertReport_NeedsMSResponse(alert, !_skipEmail, _htmlTemplateFileName);
            else
                throw new Exception("Invalid Alert Type for reporting");

            // Create the report and send the email for it
            if (report.FillReportBody(collection1, collection2))
            {
                bool fileWritten = report.FileName != "" ? WriteReportFile(alert, report) : true;
                bool emailSent = report.SendEmail ? SendEmail(alert, report, smtpClient) : false;
                PrintLogs(report, alert, fileWritten, emailSent);
                return emailSent;
            }
            else
                return true;
        }

        /// <summary>
        /// Writes the full body text of the given report to the filename specified in the reporting template.
        /// </summary>
        /// <returns>true if succeeded</returns>
        private bool WriteReportFile(Alert alert, AlertReport report)
        {
            try
            {
                File.WriteAllText(report.FileName, report.BodyText);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR writing alert {0} into file {1}", alert.Name, report.FileName);
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
            if (report.FileName != "")
            {
                Console.WriteLine("    Report: {0}", report.FileName);
                if (!fileWritten)
                {
                    Console.WriteLine("        FAILED!!!");
                }
            }
            Console.WriteLine("    Email: {0}", emailSent ? "sent" : (report.SendEmail ? "FAILED!!!" : "skipped"));
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
            if (report.FileName == "")
            {
                Console.Write(report.BodyText.Replace("<br/>", ""));
            }
            Console.WriteLine();
        }
    }
}
