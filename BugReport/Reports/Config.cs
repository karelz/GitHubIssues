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
    public class Config
    {
        private List<ConfigFile> _configFiles;

        private List<Alert.User> _users = new List<Alert.User>();

        public IEnumerable<Alert> Alerts { get; private set; }
        public IEnumerable<NamedQuery> Queries { get; private set; }
        public IEnumerable<Label> AreaLabels { get; private set; }
        public IEnumerable<Label> IssueTypeLabels { get; private set; }
        public IEnumerable<Label> UntriagedLabels { get; private set; }
        public ExpressionUntriaged UntriagedExpression { get; private set; }

        public IEnumerable<Repository> Repositories { get; private set; }

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

        public Config(IEnumerable<string> configFiles)
        {
            // List of XML config files to load
            Queue<string> configFilesToLoad = new Queue<string>();
            foreach (string configFile in configFiles)
            {
                configFilesToLoad.Enqueue(configFile);
            }

            // List of all XML roots from all XML config files
            _configFiles = new List<ConfigFile>();

            while (configFilesToLoad.Count > 0)
            {
                string fileName = configFilesToLoad.Dequeue();
                XElement root = XElement.Load(fileName);
                _configFiles.Add(new ConfigFile(fileName, root));

                string directoryName = Path.GetDirectoryName(fileName);

                foreach (XElement fileNode in root.Descendants("file"))
                {
                    configFilesToLoad.Enqueue(Path.Combine(directoryName, fileNode.Attribute("include").Value));
                }
            }

            Repositories = LoadRepositories().ToArray();

            LoadUsers();

            AreaLabels = LoadLabels("area").ToList();
            IssueTypeLabels = LoadLabels("issueType").ToList();
            UntriagedLabels = LoadLabels("untriaged").ToList();

            UntriagedExpression = new ExpressionUntriaged(
                IssueTypeLabels,
                AreaLabels,
                UntriagedLabels);
            var customIsValues = new Dictionary<string, Expression>() { { "untriaged", UntriagedExpression } };

            Alerts = LoadAlerts(customIsValues).ToList();
            Queries = LoadQueries(customIsValues).ToList();
        }

        private void LoadUsers()
        {
            foreach (ConfigFile configFile in _configFiles)
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

                        _users.Add(new Alert.User(name, email, emailAlias, gitHubLogin));
                    }
                }
            }
        }

        private IEnumerable<Alert> LoadAlerts(IReadOnlyDictionary<string, Expression> customIsValues)
        {
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement alertsNode in configFile.Root.Descendants("alerts"))
                {
                    foreach (XElement alertNode in alertsNode.Descendants("alert"))
                    {
                        string alertName = alertNode.Attribute("name").Value;

                        IEnumerable<Alert.User> owners = alertNode.Descendants("owner").Select(e => FindUserOrThrow(e.Value));
                        IEnumerable<Alert.User> ccUsers = alertNode.Descendants("cc").Select(e => FindUserOrThrow(e.Value));

                        Alert alert;
                        try
                        {
                            alert = new Alert(
                                alertName,
                                alertNode.Descendants("query").Select(q => 
                                    new NamedQuery.RepoQuery(q.Attribute("repo")?.Value, q.Value)),
                                customIsValues,
                                owners,
                                ccUsers);
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

        private IEnumerable<NamedQuery> LoadQueries(IReadOnlyDictionary<string, Expression> customIsValues)
        {
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement reportNode in configFile.Root.Descendants("report"))
                {
                    foreach (XElement queryNode in reportNode.Descendants("query"))
                    {
                        string queryName = queryNode.Attribute("name").Value;

                        string queryString = queryNode.Value.ToString();

                        NamedQuery query;
                        try
                        {
                            query = new NamedQuery(queryName, queryString, customIsValues);
                        }
                        catch (InvalidQueryException ex)
                        {
                            throw new InvalidDataException($"Invalid query '{queryName}'", ex);
                        }
                        yield return query;
                    }
                }
            }
        }

        private IEnumerable<Repository> LoadRepositories()
        {
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement repoNode in configFile.Root.Descendants("repository"))
                {
                    string repoName = repoNode.Attribute("name").Value;
                    string filterQuery = repoNode.Descendants("filterQuery").FirstOrDefault()?.Value;
                    yield return Repository.From(repoName, filterQuery);
                }
            }
        }

        private IEnumerable<Label> LoadLabels(string kind)
        {
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement labelsNode in 
                    configFile.Root.Descendants("labels").Where(n => n.Attribute("kind")?.Value == kind))
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
            foreach (Alert.User user in _users)
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
