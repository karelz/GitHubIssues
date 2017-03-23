using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;
using BugReport.Util;

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

        public IDictionary<string, Label> LabelAliases { get; private set; }
        public IDictionary<string, string> MilestoneAliases { get; private set; }

        public IEnumerable<Repository> Repositories { get; private set; }

        public IEnumerable<Team> Teams { get; private set; }
        public IEnumerable<Organization> Organizations { get; private set; }

        private class ConfigFile
        {
            public string FileName;
            public XElement Root;

            public ConfigFile(string fileName, XElement root)
            {
                FileName = fileName;
                Root = root;
            }

            public override string ToString() => FileName;
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

            // Repositories have to be first - they are used in other elements (e.g. alerts and queries)
            Repositories = LoadRepositories().ToArray();

            LoadUsers();

            AreaLabels = LoadLabels("area").Distinct().ToList();
            IssueTypeLabels = LoadLabels("issueType").ToList();
            UntriagedLabels = LoadLabels("untriaged").ToList();

            LabelAliases = LoadLabelAliases();
            MilestoneAliases = LoadMilestoneAliases();

            Organizations = LoadOrganizations();
            Teams = LoadTeams();

            UntriagedExpression = new ExpressionUntriaged(
                IssueTypeLabels,
                AreaLabels,
                UntriagedLabels);
            var customIsValues = new Dictionary<string, Expression>() { { "untriaged", UntriagedExpression } };

            Alerts = LoadAlerts(customIsValues).ToList();
            Queries = LoadQueryReports(customIsValues).ToList();
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
                            throw new InvalidDataException($"GitHub login expected to start with @ '{gitHubLogin}'");
                        }
                        if (emailAlias.StartsWith("@"))
                        {
                            throw new InvalidDataException($"Alias cannot start with @ '{emailAlias}'");
                        }

                        if (FindUser(gitHubLogin) != null)
                        {
                            throw new InvalidDataException($"Duplicate user defined with GitHub login '{gitHubLogin}'");
                        }
                        if (FindUser(emailAlias) != null)
                        {
                            throw new InvalidDataException($"Duplicate user defined with alias '{emailAlias}'");
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

        private static string GetInheritedAttributeValue(XElement element, string name)
        {
            while (element != null)
            {
                string value = element.Attribute(name)?.Value;
                if (value != null)
                {
                    return value;
                }
                element = element.Parent;
            }
            return null;
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

                        if (ccUsers.Any() && owners.None())
                        {
                            throw new InvalidDataException($"Missing owner in alert '{alertName}'");
                        }

                        Alert alert;
                        try
                        {
                            alert = new Alert(
                                alertName,
                                alertNode.Descendants("query").Select(q =>
                                    new NamedQuery.RepoQuery(GetInheritedAttributeValue(q, "repo"), q.Value)),
                                FindTeam(alertNode.Attribute("team")?.Value),
                                customIsValues,
                                owners,
                                ccUsers);
                        }
                        catch (InvalidQueryException ex)
                        {
                            throw new InvalidDataException($"Invalid query in alert '{alertName}'", ex);
                        }
                        yield return alert;
                    }
                }
            }
        }

        private IEnumerable<NamedQuery> LoadQueryReports(IReadOnlyDictionary<string, Expression> customIsValues)
        {
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement reportNode in configFile.Root.Descendants("report"))
                {
                    foreach (XElement queryReportNode in reportNode.Descendants("queryReport"))
                    {
                        string queryReportName = queryReportNode.Attribute("name").Value;

                        NamedQuery query;
                        try
                        {
                            query = new NamedQuery(
                                queryReportName,
                                queryReportNode.Descendants("query").Select(q =>
                                    new NamedQuery.RepoQuery(GetInheritedAttributeValue(q, "repo"), q.Value)),
                                null,
                                customIsValues);
                        }
                        catch (InvalidQueryException ex)
                        {
                            throw new InvalidDataException($"Invalid queryReport '{queryReportName}'", ex);
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
                    string alias = repoNode.Attribute("alias")?.Value;
                    yield return Repository.From(repoName, alias, filterQuery);
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

        private Dictionary<string, Label> LoadLabelAliases()
        {
            Dictionary<string, Label> labelAliases = new Dictionary<string, Label>();
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement labelsNode in
                    configFile.Root.Descendants("labels").Where(n => n.Attribute("kind")?.Value == "aliases"))
                {
                    foreach (XElement aliasNode in labelsNode.Descendants("alias"))
                    {
                        string aliasName = aliasNode.Attribute("name").Value;
                        Label targetLabel = new Label(aliasNode.Value);

                        if (labelAliases.TryGetValue(aliasName, out _))
                        {
                            throw new InvalidDataException($"Label alias '{aliasName}' defined more than once.");
                        }
                        labelAliases[aliasName] = targetLabel;
                    }
                }
            }

            return labelAliases;
        }

        private Dictionary<string, string> LoadMilestoneAliases()
        {
            Dictionary<string, string> milestoneAliases = new Dictionary<string, string>();
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement labelsNode in
                    configFile.Root.Descendants("milestones").Where(n => n.Attribute("kind")?.Value == "aliases"))
                {
                    foreach (XElement aliasNode in labelsNode.Descendants("alias"))
                    {
                        string aliasName = aliasNode.Attribute("name").Value;
                        string targetMilestoneName = aliasNode.Value;

                        if (milestoneAliases.TryGetValue(aliasName, out _))
                        {
                            throw new InvalidDataException($"Milestone alias '{aliasName}' defined more than once.");
                        }
                        milestoneAliases[aliasName] = targetMilestoneName;
                    }
                }
            }

            return milestoneAliases;
        }

        private List<Organization> _organizations;
        private IEnumerable<Organization> LoadOrganizations()
        {
            Debug.Assert(_organizations == null);

            _organizations = new List<Organization>();
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement organizationsNode in configFile.Root.Descendants("organizations"))
                {
                    foreach (XElement orgNode in organizationsNode.Descendants("organization"))
                    {
                        string orgName = orgNode.Attribute("name").Value;

                        if (FindOrganization(orgName) != null)
                        {
                            throw new InvalidDataException($"Organization '{orgName}' defined more than once.");
                        }

                        _organizations.Add(new Organization(orgName, orgNode.Value));
                    }
                }
            }

            return _organizations;
        }
        private Organization FindOrganization(string organizationName)
        {
            return _organizations.Where(org => org.Name == organizationName).FirstOrDefault();
        }

        private Dictionary<string, Team> _teams;
        private IEnumerable<Team> LoadTeams()
        {
            // LoadOrganizations has to be called first
            Debug.Assert(_organizations != null);
            Debug.Assert(_teams == null);

            _teams = new Dictionary<string, Team>();
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement teamsNode in configFile.Root.Descendants("teams"))
                {
                    foreach (XElement teamNode in teamsNode.Descendants("team"))
                    {
                        string teamName = teamNode.Attribute("name").Value;
                        string teamOrganizationName = teamNode.Attribute("organization").Value;

                        if (_teams.TryGetValue(teamName, out _))
                        {
                            throw new InvalidDataException($"Team '{teamName}' defined more than once.");
                        }

                        Organization organization = FindOrganization(teamOrganizationName);
                        if (organization == null)
                        {
                            throw new InvalidDataException($"Team's '{teamName}' organization '{teamOrganizationName}' does not exist.");
                        }

                        _teams[teamName] = new Team(teamName, organization);
                    }
                }
            }

            return _teams.Values;
        }
        private Team FindTeam(string teamName)
        {
            if (teamName == null)
            {
                return null;
            }
            if (_teams.TryGetValue(teamName, out Team team))
            {
                return team;
            }
            throw new InvalidDataException($"Team '{teamName}' does not exist.");
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
                throw new InvalidDataException($"Cannot find user '{id}'");
            }
            return user;
        }
    }
}
