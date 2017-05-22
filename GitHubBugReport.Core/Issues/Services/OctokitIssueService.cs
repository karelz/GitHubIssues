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

        public int Create(DataModelIssue issue)
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

        public IEnumerable<DataModelIssue> GetList(IEnumerable<int> issueNumbers)
        {
            if (issueNumbers == null) { throw new ArgumentNullException(nameof(issueNumbers)); }
            if (!issueNumbers.Any()) { return new List<DataModelIssue>(); }

            var issues = new List<DataModelIssue>();

            foreach (int issueNumber in issueNumbers)
            {
                issues.Add(Get(issueNumber));
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

        private DataModelIssue MapIssueToDataModel(Issue issue)
        {
            // TODO: Implement this.
            return new DataModelIssue();
        }

        private DataModelComment MapCommentToDataModel(IssueComment issueComment)
        {
            // TOOD: Implement this.
            return new DataModelComment();
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

    public class DataModelComment
    {
        // TODO: Define members.

    }
}
