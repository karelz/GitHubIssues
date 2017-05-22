using System.Collections.Generic;
using GitHubBugReport.Core.Issues.Models;

namespace GitHubBugReport.Core.Issues.Services
{
    public interface IIssueService
    {
        /// <summary>
        /// Create a new issue.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="issue">The issue to create.</param>
        /// <param name="owner"></param>
        /// <returns>Id of the issue created.</returns>
        int Create(string owner, string name, DataModelIssue issue);

        /// <summary>
        /// Retrieve a specific issue.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="id">Id of the issue we are looking for.</param>
        /// <param name="owner"></param>
        /// <returns>The issue if it exists, null otherwise.</returns>
        DataModelIssue Get(string owner, string name, int id);

        /// <summary>
        /// Get a list of issues by id.
        /// </summary>
        /// <returns>Issues for the given set of ids.</returns>
        IEnumerable<DataModelIssue> GetList(string owner, string name, IEnumerable<int> issueNumbers);

        /// <summary>
        /// Get all issues.
        /// </summary>
        /// <returns>All issues.</returns>
        IEnumerable<DataModelIssue> GetAll(string owner, string name);
    }
}
