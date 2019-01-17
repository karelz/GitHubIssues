using System;
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

        public IDictionary<string, Label> LabelAliasesMap { get; private set; }
        public IDictionary<string, string> MilestoneAliasesMap { get; private set; }

        public IEnumerable<Repository> Repositories { get; private set; }

        public IEnumerable<Team> Teams { get; private set; }
        public IEnumerable<Organization> Organizations { get; private set; }

        public int IssuesMinimalCount { get; private set; }
        public double IssuesMaximumRatio { get; private set; }

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

            Organizations = LoadOrganizations();
            Teams = LoadTeams();

            IEnumerable<AreaLabel> allAreaLabels = LoadAreaLabels().Distinct().ToList();
            AreaLabels = FilterAreaLabels(
                allAreaLabels,
                LoadAreaLabelFilters(allAreaLabels, FilterKind.In),
                LoadAreaLabelFilters(allAreaLabels, FilterKind.Out)).ToList();
            IssueTypeLabels = LoadLabels("issueType").ToList();
            UntriagedLabels = LoadLabels("untriaged").ToList();

            LabelAliasesMap = LoadLabelAliasesMap();
            MilestoneAliasesMap = LoadMilestoneAliasesMap();

            UntriagedExpression = new ExpressionUntriaged(
                IssueTypeLabels,
                allAreaLabels.Select(labelInfo => labelInfo.Label),
                UntriagedLabels);
            var customIsValues = new Dictionary<string, Expression>() { { "untriaged", UntriagedExpression } };

            IEnumerable<Alert> allAlerts = LoadAlerts(customIsValues);
            Alerts = FilterAlerts(
                allAlerts,
                LoadAlertFilters(allAlerts, FilterKind.In).ToList(),
                LoadAlertFilters(allAlerts, FilterKind.Out).ToList()).ToList();
            Queries = LoadQueryReports(customIsValues).ToList();

            LoadLargeChangeThresholds(out int issuesMinimalCount, out double issuesMaximumRatio);
            IssuesMinimalCount = issuesMinimalCount;
            IssuesMaximumRatio = issuesMaximumRatio;
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

        private class AlertFilter
        {
            private Team _team;
            private string _alertName;

            public AlertFilter(Team team, string alertName)
            {
                _team = team;
                _alertName = alertName;
            }

            public bool IsMatch(Alert alert)
            {
                if (_team != null)
                {
                    return (_team == alert.Team);
                }
                Debug.Assert(_alertName != null);
                return alert.EqualsByName(_alertName);
            }
        }

        private IEnumerable<AlertFilter> LoadAlertFilters(
            IEnumerable<Alert> alerts,
            FilterKind filterKind)
        {
            string kindValue = GetFilterKindValue(filterKind);

            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement alertsNode in configFile.Root.Descendants("alertFilters")
                    .Where(n => (n.Attribute("kind").Value == kindValue)))
                {
                    foreach (XElement alertNode in alertsNode.Descendants("alertFilter"))
                    {
                        string teamName = alertNode.Attribute("team")?.Value;
                        string alertName = alertNode.Attribute("name")?.Value;

                        if ((teamName == null) && (alertName == null))
                        {
                            throw new InvalidDataException("Invalid alert filter - either 'name' or 'team' attribute has to be defined.");
                        }
                        if ((teamName != null) && (alertName != null))
                        {
                            throw new InvalidDataException($"Invalid alert filter '{alertName}' for team '{teamName}' - cannot filter on both 'name' and 'team' attributes at once.");
                        }
                        if ((alertName != null) &&
                            alerts.Where(alert => alert.EqualsByName(alertName)).None())
                        {
                            throw new InvalidDataException($"Invalid alert filter on name '{alertName}' - cannot find alert with that name");
                        }

                        yield return new AlertFilter(FindTeam(teamName), alertName);
                    }
                }
            }
        }

        private IEnumerable<Alert> FilterAlerts(
            IEnumerable<Alert> alerts,
            IEnumerable<AlertFilter> inFilters,
            IEnumerable<AlertFilter> outFilters)
        {
            if (inFilters.Any())
            {
                alerts = alerts.Where(alert => inFilters.Where(filter => filter.IsMatch(alert)).Any());
            }
            if (outFilters.Any())
            {
                alerts = alerts.Where(alert => outFilters.Where(filter => filter.IsMatch(alert)).None());
            }
            return alerts;
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

        private class AreaLabel
        {
            public Label Label { get; private set; }
            public Team Team { get; private set; }

            public AreaLabel(string labelName, Team team)
            {
                Label = new Label(labelName);
                Team = team;
            }
        }

        private IEnumerable<AreaLabel> LoadAreaLabels()
        {
            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement labelsNode in
                    configFile.Root.Descendants("labels").Where(n => n.Attribute("kind")?.Value == "area"))
                {
                    foreach (XElement labelNode in labelsNode.Descendants("label"))
                    {
                        yield return new AreaLabel(
                            labelNode.Attribute("name").Value, 
                            FindTeam(labelNode.Attribute("team")?.Value));
                    }
                }
            }
        }

        private enum FilterKind
        {
            In,
            Out
        }

        private static string GetFilterKindValue(FilterKind kind)
        {
            Debug.Assert((kind == FilterKind.In) || (kind == FilterKind.Out));
            return (kind == FilterKind.In) ? "in" : "out";
        }

        private class AreaLabelFilter
        {
            private Team _team;
            private string _labelName;

            public AreaLabelFilter(Team team, string labelName)
            {
                Debug.Assert((team != null) || (labelName != null));
                _team = team;
                _labelName = labelName;
            }

            public bool IsMatch(AreaLabel areaLabel)
            {
                if (_team != null)
                {
                    if (_labelName != null)
                    {
                        return ((areaLabel.Team == _team) && areaLabel.Label.Equals(_labelName));
                    }
                    return (areaLabel.Team == _team);
                }
                Debug.Assert(_labelName != null);
                return areaLabel.Label.Equals(_labelName);
            }
        }

        private IEnumerable<AreaLabelFilter> LoadAreaLabelFilters(
            IEnumerable<AreaLabel> areaLabels,
            FilterKind filterKind)
        {
            // Teams have to load first
            Debug.Assert(Teams != null);

            string kindValue = GetFilterKindValue(filterKind);

            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement alertsNode in configFile.Root.Descendants("areaLabelFilters")
                    .Where(n => (n.Attribute("kind").Value == kindValue)))
                {
                    foreach (XElement alertNode in alertsNode.Descendants("areaLabelFilter"))
                    {
                        string teamName = alertNode.Attribute("team")?.Value;
                        string labelName = alertNode.Attribute("name")?.Value;

                        if ((teamName == null) && (labelName == null))
                        {
                            throw new InvalidDataException("Invalid area label filter - either 'name' or 'team' attribute has to be defined.");
                        }
                        if ((labelName != null) && 
                            areaLabels.Where(areaLabel => areaLabel.Label.Equals(labelName)).None())
                        {
                            throw new InvalidDataException($"Invalid areal label filter on name '{labelName}' - cannot find label with that name");
                        }

                        yield return new AreaLabelFilter(FindTeam(teamName), labelName);
                    }
                }
            }
        }

        private IEnumerable<Label> FilterAreaLabels(
            IEnumerable<AreaLabel> areaLabels,
            IEnumerable<AreaLabelFilter> inFilters,
            IEnumerable<AreaLabelFilter> outFilters)
        {
            if (inFilters.Any())
            {
                areaLabels = areaLabels.Where(areaLabel =>
                    inFilters.Where(filter => filter.IsMatch(areaLabel)).Any());
            }
            if (outFilters.Any())
            {
                areaLabels = areaLabels.Where(areaLabel =>
                    outFilters.Where(filter => filter.IsMatch(areaLabel)).None());
            }
            return areaLabels.Select(areaLabel => areaLabel.Label);
        }

        private Dictionary<string, Label> LoadLabelAliasesMap()
        {
            Dictionary<string, Label> labelAliases = new Dictionary<string, Label>(Label.NameEqualityComparer);
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

        private Dictionary<string, string> LoadMilestoneAliasesMap()
        {
            Dictionary<string, string> milestoneAliases = new Dictionary<string, string>(Milestone.TitleComparer);
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
            return _organizations.Where(org => org.EqualsByName(organizationName)).FirstOrDefault();
        }

        private Dictionary<string, Team> _teams;
        private IEnumerable<Team> LoadTeams()
        {
            // LoadOrganizations has to be called first
            Debug.Assert(_organizations != null);
            Debug.Assert(_teams == null);

            _teams = new Dictionary<string, Team>(Team.NameComparer);
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

        private void LoadLargeChangeThresholds(out int issuesMinimalCount, out double issuesMaximumRatio)
        {
            // Defaults
            issuesMinimalCount = 20;
            issuesMaximumRatio = 0.1;

            bool isThresholdsParsed = false;

            foreach (ConfigFile configFile in _configFiles)
            {
                foreach (XElement thresholdsNode in configFile.Root.Descendants("thresholds"))
                {
                    if (isThresholdsParsed)
                    {
                        throw new InvalidDataException($"Duplicate `thresholds` element defined.");
                    }
                    isThresholdsParsed = true;

                    string issuesMinimalCountValue = thresholdsNode.Attribute("IssuesMinimalCount")?.Value;
                    if (issuesMinimalCountValue != null)
                    {
                        if (!int.TryParse(issuesMinimalCountValue, out issuesMinimalCount) ||
                            issuesMinimalCount < 0)
                        {
                            throw new InvalidDataException($"IssuesMinimalCount value `{issuesMinimalCountValue}` has to be positive number.");
                        }
                    }

                    string issuesMaximumRatioValue = thresholdsNode.Attribute("IssuesMaximumRatio")?.Value;
                    if (issuesMaximumRatioValue != null)
                    {
                        if (!double.TryParse(issuesMaximumRatioValue, out issuesMaximumRatio) ||
                            issuesMaximumRatio < 0 ||
                            issuesMaximumRatio > 1)
                        {
                            throw new InvalidDataException($"IssuesMaximumRatio value `{issuesMaximumRatioValue}` has to be number in the interval <0,1>.");
                        }
                    }
                }
            }
        }
    }
}
