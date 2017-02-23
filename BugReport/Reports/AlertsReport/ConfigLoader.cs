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
    public class ConfigLoader
    {
        List<Alert.User> Users = new List<Alert.User>();

        public void Load(string alertsXmlFileName, out IEnumerable<Alert> alerts, out IEnumerable<Label> labels)
        {
            XElement root = XElement.Load(alertsXmlFileName);
            LoadUsers(root);
            alerts = LoadAlerts(root);
            labels = LoadLabels(root);
        }

        void LoadUsers(XElement root)
        {
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
        }

        IEnumerable<Alert> LoadAlerts(XElement root)
        {
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

        IEnumerable<Label> LoadLabels(XElement root)
        {
            foreach (XElement labelsNode in root.Descendants("labels"))
            {
                foreach (XElement labelNode in labelsNode.Descendants("label"))
                {
                    yield return new Label(labelNode.Attribute("name").Value);
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
