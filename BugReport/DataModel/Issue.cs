using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BugReport
{
    public class Issue
    {
        public int Number;
        public string Title;
        public Octokit.ItemState State;
        public User Assignee;
        public Label[] Labels;
        public User User;
        public string HtmlUrl;

        public DateTimeOffset? CreatedAt;
        public DateTimeOffset? UpdatedAt;
        public DateTimeOffset? ClosedAt;
        public User ClosedBy;

        public PullRequest PullRequest;
        public Milestone Milestone;

        public IssueKindFlags IssueKind
        {
            get { return (PullRequest == null) ? IssueKindFlags.Issue : IssueKindFlags.PullRequest; }
        }
        public bool IsIssueKind(IssueKindFlags issueKindFlags)
        {
            return (IssueKind & issueKindFlags) != 0;
        }

        [FlagsAttribute]
        public enum IssueKindFlags
        {
            Issue = 1,
            PullRequest = 2,
            All = Issue | PullRequest
        }

        public static IssueCollection LoadFrom(string fileName, IssueKindFlags issueKind = IssueKindFlags.All)
        {
            JsonSerializer serializer = new JsonSerializer();
            using (StreamReader sr = new StreamReader(fileName))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                return new IssueCollection(serializer.Deserialize<List<Issue>>(reader).Where(i => i.IsIssueKind(issueKind)));
            }
        }

        public void Print()
        {
            Console.WriteLine("Number: {0}", Number);
            Console.WriteLine("Type: {0}", (PullRequest == null) ? "Issue" : "PullRequest");
            Console.WriteLine("URL: {0}", HtmlUrl);
            Console.WriteLine("State: {0}", State);
            Console.WriteLine("Assignee.Name:  {0}", (Assignee == null) ? "<null>" : Assignee.Name);
            Console.WriteLine("        .Login: {0}", (Assignee == null) ? "<null>" : Assignee.Login);
            Console.WriteLine("Labels.Name:");
            foreach (Label label in Labels)
            {
                Console.WriteLine("    {0}", label.Name);
            }
            Console.WriteLine("Title: {0}", Title);
            //Console.WriteLine("Milestone.Title: {0}", (issue.Milestone == null) ? "<null>" : issue.Milestone.Title);
            Console.WriteLine("User.Name:  {0}", (User == null) ? "<null>" : User.Name);
            Console.WriteLine("    .Login: {0}", (User == null) ? "<null>" : User.Login);
            Console.WriteLine("CreatedAt: {0}", CreatedAt);
            Console.WriteLine("UpdatedAt: {0}", UpdatedAt);
            Console.WriteLine("ClosedAt:  {0}", ClosedAt);
            Console.WriteLine("ClosedBy.Name:  {0}", (ClosedBy == null) ? "<null>" : ClosedBy.Name);
            Console.WriteLine("        .Login: {0}", (ClosedBy == null) ? "<null>" : ClosedBy.Login);
        }
    }

    public class Label
    {
        public string Name;

        public List<Issue> Issues;
    }

    public class User
    {
        public string Name;
        public string Login;
    }

    public class Milestone
    {
        public int Number;
        public string Title;
        public string Description;
        public int OpenIssues;
        public int ClosedIssues;
        public Octokit.ItemState State;
        public User Creator;
        public DateTimeOffset CreatedAt;
        public DateTimeOffset? DueOn;
        public DateTimeOffset? ClosedAt;
    }

    public class PullRequest
    {
        //public string HtmlUrl;

        /*
        public DateTimeOffset? CreatedAt;
        public DateTimeOffset? UpdatedAt;
        public DateTimeOffset? ClosedAt;
        public DateTimeOffset? MergedAt;
        public bool Merged;
        */
    }
}
