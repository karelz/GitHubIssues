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
        private List<Alert.User> Users = new List<Alert.User>();

        private struct ConfigFile
        {
            public string FileName;
            public XElement Root;

            public ConfigFile(string fileName, XElement root)
            {
                FileName = fileName;
                Root = root;
            }
        }

        public void Load(string configXmlFileName, out IEnumerable<Alert> alerts, out IEnumerable<Label> labels)
        {
            // List of XML config files to load
            Queue<string> configFilesToLoad = new Queue<string>();
            configFilesToLoad.Enqueue(configXmlFileName);

            // List of all XML roots to from all XML config files
            List<ConfigFile> configFiles = new List<ConfigFile>();

            while (configFilesToLoad.Count > 0)
            {
                string fileName = configFilesToLoad.Dequeue();
                XElement root = XElement.Load(fileName);
                configFiles.Add(new ConfigFile(fileName, root));

                string directoryName = Path.GetDirectoryName(fileName);

                foreach (XElement fileNode in root.Descendants("file"))
                {
                    configFilesToLoad.Enqueue(Path.Combine(directoryName, fileNode.Attribute("include").Value));
                }
            }

            LoadUsers(configFiles);
            alerts = LoadAlerts(configFiles);
            labels = LoadLabels(configFiles);
        }

        private void LoadUsers(IEnumerable<ConfigFile> configFiles)
        {
            foreach (ConfigFile configFile in configFiles)
            {
                foreach (XElement usersNode in configFile.Root.Descendants("users"))
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
        }

        private IEnumerable<Alert> LoadAlerts(IEnumerable<ConfigFile> configFiles)
        {
            foreach (ConfigFile configFile in configFiles)
            {
                foreach (XElement alertsNode in configFile.Root.Descendants("alerts"))
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
        }

        private IEnumerable<Label> LoadLabels(IEnumerable<ConfigFile> configFiles)
        {
            foreach (ConfigFile configFile in configFiles)
            {
                foreach (XElement labelsNode in configFile.Root.Descendants("labels"))
                {
                    foreach (XElement labelNode in labelsNode.Descendants("label"))
                    {
                        yield return new Label(labelNode.Attribute("name").Value);
                    }
                }
            }
        }

        private Alert.User FindUser(string id)
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

        private Alert.User FindUserOrThrow(string id)
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
