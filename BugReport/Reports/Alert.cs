using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using GitHubBugReport.Core.Query;

namespace BugReport.Reports
{
    public class NamedQuery
    {
        public string Name { get; private set; }
        public Expression Query { get; private set; }
        public Team Team { get; private set; }

        public bool IsOrganization(Organization organization)
        {
            return (Team != null) && (Team.Organization == organization);
        }

        // customIsValues - customization of is:* (e.g. is:untriaged) syntax in queries
        public NamedQuery(
            string name, 
            IEnumerable<RepoQuery> queries, 
            Team team, 
            IReadOnlyDictionary<string, Expression> customIsValues)
        {
            Name = name;
            Team = team;

            int count = queries.Count();
            if (count == 0)
            {
                throw new InvalidDataException("Expected at least 1 query");
            }
            
            Query = new ExpressionMultiRepo(queries.SelectMany(q =>
                q.Repos.Select(repo => 
                    new RepoExpression(
                        (repo != null) ? Repository.From(repo) : null,
                        QueryParser.Parse(q.Query, customIsValues)))));
        }

        public NamedQuery(string name, Expression query, Team team = null)
        {
            Name = name;
            Query = query;
            Team = team;
        }

        public struct RepoQuery
        {
            public IEnumerable<string> Repos { get; private set; }
            public string Query { get; private set; }

            public RepoQuery(string repos, string query)
            {
                Query = query;
                if ((repos == null) || (repos == "*") || (repos == ""))
                {
                    Repos = new string[] { null };
                }
                else
                {
                    Repos = repos.Split(';');
                }
            }
        }

        public bool EqualsByName(string name)
        {
            return Name.Equals(name, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public class Alert : NamedQuery
    {
        public class User
        {
            public string Name { get; private set; }
            public string EmailAddress { get; private set; }
            public string EmailAlias { get; private set; }
            public string GitHubLogin { get; private set; }

            public User(string name, string emailAddress, string emailAlias, string gitHubLogin)
            {
                Name = name;
                EmailAddress = emailAddress;
                EmailAlias = emailAlias;
                GitHubLogin = gitHubLogin;
            }
        }

        public IEnumerable<User> Owners { get; private set; }
        public IEnumerable<User> CCs { get; private set; }

        // customIsValues - customization of is:* (e.g. is:untriaged) syntax in queries
        public Alert(
            string name, 
            IEnumerable<RepoQuery> queries, 
            Team team, 
            IReadOnlyDictionary<string, Expression> customIsValues, 
            IEnumerable<User> owners, 
            IEnumerable<User> ccList) 
            : base(name, queries, team, customIsValues)
        {
            Owners = owners;
            CCs = ccList;
        }
    }

    public class Organization
    {
        public string Name { get; private set; }
        public string Description { get; private set; }

        public Organization(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public bool EqualsByName(string name)
        {
            return Name.Equals(name, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public class Team
    {
        public string Name { get; private set; }
        public Organization Organization { get; private set; }

        public Team(string name, Organization organization)
        {
            Name = name;
            Organization = organization;
        }

        public bool EqualsByName(string name)
        {
            return Name.Equals(name, StringComparison.InvariantCultureIgnoreCase);
        }

        public static StringComparer NameComparer = StringComparer.InvariantCultureIgnoreCase;
    }
}
