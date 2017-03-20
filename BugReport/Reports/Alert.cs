using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using BugReport.Query;

namespace BugReport.Reports
{
    public class NamedQuery
    {
        public string Name { get; private set; }
        public Expression Query { get; private set; }

        // customIsValues - customization of is:* (e.g. is:untriaged) syntax in queries
        public NamedQuery(
            string name, 
            IEnumerable<RepoQuery> queries, 
            IReadOnlyDictionary<string, Expression> customIsValues)
        {
            Name = name;

            int count = queries.Count();
            if (count == 0)
            {
                throw new InvalidDataException("Expected at least 1 query");
            }
            if ((count == 1) && (queries.First().Repo == null))
            {
                Query = QueryParser.Parse(queries.First().Query, customIsValues);
            }
            else
            {
                Query = new ExpressionMultiRepo(queries.Select(q =>
                    new RepoExpression(
                        (q.Repo != null) ? Repository.From(q.Repo) : null,
                        QueryParser.Parse(q.Query, customIsValues))));
            }
        }

        public NamedQuery(string name, Expression query)
        {
            Name = name;
            Query = query;
        }

        public struct RepoQuery
        {
            public string Repo { get; private set; }
            public string Query { get; private set; }

            public RepoQuery(string repo, string query)
            {
                Repo = repo;
                Query = query;
            }
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
            IReadOnlyDictionary<string, Expression> customIsValues, 
            IEnumerable<User> owners, 
            IEnumerable<User> ccList) 
            : base(name, queries, customIsValues)
        {
            Owners = owners;
            CCs = ccList;
        }
    }
}
