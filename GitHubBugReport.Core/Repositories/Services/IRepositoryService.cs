using System.Collections.Generic;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Repositories.Models;

namespace GitHubBugReport.Core.Repositories.Services
{
    public interface IRepositoryService
    {
        Repository Find(string respositoryName);
        Repository FindRepositoryByAlias(string alias);
        Repository FromHtmlUrl(string htmlUrl);
        IEnumerable<Repository> GetReposOrDefault(IEnumerable<DataModelIssue> issues);
    }
}