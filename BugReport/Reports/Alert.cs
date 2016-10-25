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
    public class Alert
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

        public string Name { get; private set; }
        public Expression Query { get; private set; }
        public IEnumerable<User> Owners { get; private set; }
        public IEnumerable<User> CCs { get; private set; }

        public Alert(string name, string query, IEnumerable<User> owners, IEnumerable<User> ccList)
        {
            Name = name;

            QueryParser queryParser = new QueryParser(query);
            Query = queryParser.Parse();

            Owners = owners;
            CCs = ccList;
        }
    }
}
