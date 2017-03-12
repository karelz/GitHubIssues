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
    public abstract class AlertReport
    {
        protected string _htmlTemplateFileName;
        protected Alert _alert;
        public virtual bool ShouldSendEmail { get; protected set; }
        public virtual string OutputHtmlFileName { get; protected set; }
        public virtual string Subject { get; protected set; }
        public virtual string AlertName { get; protected set; }
        public virtual string BodyText { get; protected set; }

        public AlertReport(Alert alert, bool sendEmail, string htmlTemplateFileName)
        {
            _alert = alert;
            _htmlTemplateFileName = htmlTemplateFileName;
            ShouldSendEmail = sendEmail;
            BodyText = File.ReadAllText(_htmlTemplateFileName);
            AlertName = alert.Name;
            BodyText = BodyText.Replace("%ALERT_NAME%", alert.Name);
            ShouldSendEmail = sendEmail;
            Subject = ParseForValue("%SUBJECT%");
        }

        /// <summary>
        /// Returns true if the body is filled from this method
        /// </summary>
        public abstract bool FillReportBody(IEnumerable<DataModelIssue> issues1, IEnumerable<DataModelIssue> issues2);

        protected string ParseForValue(string tagToParseFor)
        {
            Regex titleRegex = new Regex(tagToParseFor + "=(.*)\r\n");
            Match titleMatch = titleRegex.Match(BodyText);
            if (!titleMatch.Success)
            {
                throw new InvalidDataException(string.Format("Missing {0} entry in email template {1}", tagToParseFor, _htmlTemplateFileName));
            }
            string foundValue = titleMatch.Groups[1].Value;
            if (titleMatch.NextMatch().Success)
            {
                throw new InvalidDataException(string.Format("Multiple {0} entries in email template {1}", tagToParseFor, _htmlTemplateFileName));
            }
            BodyText = titleRegex.Replace(BodyText, "");
            return foundValue;
        }
    }
}
