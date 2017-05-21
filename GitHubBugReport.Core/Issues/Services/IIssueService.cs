using System.Collections.Generic;
using GitHubBugReport.Core.Issues.Models;

namespace GitHubBugReport.Core.Issues.Services
{
    public interface IIssueService
    {
        /// <summary>
        /// Create a new issue.
        /// </summary>
        /// <param name="issue">The issue to create.</param>
        /// <returns>Id of the issue created.</returns>
        int Create(DataModelIssue issue);

        /// <summary>
        /// Retrieve a specific issue.
        /// </summary>
        /// <param name="id">Id of the issue we are looking for.</param>
        /// <returns>The issue if it exists, null otherwise.</returns>
        DataModelIssue Get(int id);

        /// <summary>
        /// Get all issues.
        /// </summary>
        /// <returns>All issues.</returns>
        IEnumerable<DataModelIssue> GetAll();
    }
}
