using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GitHubBugReport.Core.Issues.Models;
using Octokit;
//using ProductHeaderValue = System.Net.Http.Headers.ProductHeaderValue;

namespace GitHubBugReport.Core.Issues.Services
{
    public class OctokitIssueService : IIssueService
    {
        private readonly GitHubClient _client;

        public OctokitIssueService(GitHubClient client)
        {
            if (client == null) { throw new ArgumentNullException(nameof(client)); }

            _client = client;
        }

        public int Create(string owner, string name, DataModelIssue issue)
        {
            throw new System.NotImplementedException();
        }

        public DataModelIssue Get(string owner, string name, int id)
        {
            // get the issue
            Issue issue = null;

            Task.Run(async () =>
            {
                issue = await _client.Issue.Get(owner, name, id);
            }).Wait();

            if (issue == null) { return null; }

            // map to DataModelIssue
            DataModelIssue dataModelIssue = MapIssueToDataModel(issue);

            // load comments
            LoadComments(owner, name, dataModelIssue);

            return dataModelIssue;
        }

        public IEnumerable<DataModelIssue> GetList(string owner, string name, IEnumerable<int> issueNumbers)
        {
            if (issueNumbers == null) { throw new ArgumentNullException(nameof(issueNumbers)); }
            if (!issueNumbers.Any()) { return new List<DataModelIssue>(); }

            var issues = new List<DataModelIssue>();

            foreach (int issueNumber in issueNumbers)
            {
                issues.Add(Get(owner, name, issueNumber));
            }

            return issues;
        }

        public IEnumerable<DataModelIssue> GetAll(string owner, string name)
        {
            RepositoryIssueRequest issueRequest = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Open,
                Filter = IssueFilter.All
            };

            IReadOnlyList<Issue> issues = null;

            Task.Run(async () =>
            {
                issues = await _client.Issue.GetAllForRepository(owner, name, issueRequest);
            }).Wait();

            if (issues == null) { return null; }

            // map to DataModelIssue
            var dataModelIssueList = new List<DataModelIssue>();

            foreach (Issue issue in issues)
            {
                dataModelIssueList.Add(MapIssueToDataModel(issue));
            }

            // now load comments for each issue
            foreach (DataModelIssue issue in dataModelIssueList)
            {
                LoadComments(owner, name, issue);
            }

            return dataModelIssueList;
        }

        private Models.Label[] MapToLabels(IReadOnlyList<Octokit.Label> octokitLabels)
        {
            Models.Label[] labels = new Models.Label[octokitLabels.Count];

            for (int i = 0; i < octokitLabels.Count; i++)
            {
                labels[i] = new Models.Label(octokitLabels[i].Name);
            }

            return labels;
        }

        private DataModelIssue MapIssueToDataModel(Issue issue)
        {
            // TODO: Implement this.
            return new DataModelIssue
            {
                Id = issue.Id, 
                Number = issue.Number, 
                Title = issue.Title, 
                State = issue.State, // TODO: This is leaking.
                Assignee = new Models.User { Name = issue.Assignee.Name, Login = issue.Assignee.Login, HtmlUrl = issue.Assignee.HtmlUrl},
                Labels = MapToLabels(issue.Labels), 
                User = new Models.User { Name = issue.User.Name, Login = issue.User.Login, HtmlUrl = issue.User.HtmlUrl },
                HtmlUrl = issue.HtmlUrl.ToString(), 
                CreatedAt = issue.CreatedAt, 
                UpdatedAt = issue.UpdatedAt, 
                ClosedAt = issue.ClosedAt, 
                ClosedBy = new Models.User { Name = issue.ClosedBy.Name, Login = issue.ClosedBy.Login, HtmlUrl = issue.ClosedBy.HtmlUrl },
                PullRequest = new Models.PullRequest(), // There is nothign in this class right now. 
                Milestone = new Models.Milestone
                {
                    Number = issue.Milestone.Number, 
                    Title = issue.Milestone.Title, 
                    Description = issue.Milestone.Description, 
                    OpenIssues = issue.Milestone.OpenIssues, 
                    ClosedIssues = issue.Milestone.ClosedIssues, 
                    State = issue.Milestone.State, 
                    Creator = new Models.User { Name = issue.Milestone.Creator.Name, Login = issue.Milestone.Creator.Login, HtmlUrl = issue.Milestone.Creator.HtmlUrl }, 
                    CreatedAt = issue.Milestone.CreatedAt, 
                    DueOn = issue.Milestone.DueOn, 
                    ClosedAt = issue.Milestone.ClosedAt
                }
            };
        }

        private DataModelComment MapCommentToDataModel(IssueComment issueComment)
        {
            return new DataModelComment
            {
                Body = issueComment.Body, 
                CreatedAt = issueComment.CreatedAt, 
                HtmlUrl = issueComment.HtmlUrl, 
                Id = issueComment.Id, 
                UpdatedAt = issueComment.UpdatedAt, 
                Url = issueComment.Url, 
                User = issueComment.User
            };
        }

        /// <summary>
        /// Get comments for an issue.
        /// </summary>
        private void LoadComments(string owner, string name, DataModelIssue issue)
        {
            IReadOnlyList<IssueComment> comments = null;

            Task.Run(async () =>
            {
                comments = await _client.Issue.Comment.GetAllForIssue(owner, name, issue.Number);
            }).Wait();

            if (comments == null) { return; }

            // map comments to DataModelComments
            List<DataModelComment> dataModelComments = new List<DataModelComment>();

            foreach (IssueComment issueComment in comments)
            {
                // map and add
                dataModelComments.Add(MapCommentToDataModel(issueComment));
            }

            issue.Comments = dataModelComments;
        }

        /// <summary>
        /// Get comments for a list of issues.
        /// </summary>
        //private void LoadComments(List<Issue> issues)
        //{
        //    foreach(Issue issue)
        //}
    }
}
