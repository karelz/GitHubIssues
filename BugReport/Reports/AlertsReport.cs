using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class AlertsReport
    {
        public AlertsReport(string alertsXmlFileName)
        {
            ConfigLoader loader = new ConfigLoader();
            Alerts = loader.Load(alertsXmlFileName);
        }

        public void SendEmails(IssueCollection issuesStart, IssueCollection issuesEnd)
        {
            SmtpClient smtpClient = new SmtpClient("smtphost");
            smtpClient.UseDefaultCredentials = true;

            foreach (Alert alert in Alerts)
            {
                alert.Query.Validate(issuesEnd);
                IEnumerable<Issue> queryStart = alert.Query.Evaluate(issuesStart);
                IEnumerable<Issue> queryEnd = alert.Query.Evaluate(issuesEnd);

                IEnumerable<Issue> onlyStart = queryStart.Except(queryEnd);
                IEnumerable<Issue> onlyEnd = queryEnd.Except(queryStart);

                if (!onlyStart.Any() && !onlyEnd.Any())
                {
                    continue;
                }

                MailMessage message = new MailMessage();
                message.From = new MailAddress(Environment.UserName + "@microsoft.com");
                foreach (Alert.User user in alert.Owners)
                {
                    message.To.Add(user.EmailAddress);
                }
                foreach (Alert.User user in alert.CCs)
                {
                    message.To.Add(user.EmailAddress);
                }
                message.Subject = "Test email from my tool";
                message.IsBodyHtml = true;

                StringBuilder text = new StringBuilder("Hello");
                text.AppendLine("<br/>");
                if (onlyStart.Any())
                {
                    text.AppendLine("  only start:");
                    text.AppendLine("<br/>");
                    foreach (Issue issue in onlyStart)
                    {
                        text.AppendFormat("    #{0} - {1}", issue.Number, issue.Title);
                        text.AppendLine("<br/>");
                    }
                }
                if (onlyEnd.Any())
                {
                    text.AppendLine("  only end:");
                    text.AppendLine("<br/>");
                    foreach (Issue issue in onlyEnd)
                    {
                        text.AppendFormat("    #{0} - {1}", issue.Number, issue.Title);
                        text.AppendLine("<br/>");
                    }
                }

                message.Body = text.ToString();

                try
                {
                    smtpClient.Send(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR sending alert {0}", alert.Name);
                    Console.WriteLine(ex);
                }

                Console.WriteLine("Alert {0}", alert.Name);
                Console.WriteLine("    Onwers:");
                foreach (Alert.User user in alert.Owners)
                {
                    Console.WriteLine("        {0} - {1}", user.Name, user.EmailAddress);
                }
                Console.WriteLine("    CC:");
                foreach (Alert.User user in alert.CCs)
                {
                    Console.WriteLine("        {0} - {1}", user.Name, user.EmailAddress);
                }
                Console.Write(text.Replace("<br/>", ""));
            }
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
