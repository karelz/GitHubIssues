using System.Collections.Generic;
using GitHubBugReport.Core.Issues.Models;

namespace GitHubBugReport.Core.Storage.Services
{
    public interface IFileWriter
    {
        void SerializeToFile(string fileName, IReadOnlyCollection<Octokit.Issue> issues);
        void SerializeToFile(string fileName, IReadOnlyCollection<Octokit.IssueComment> issueComments);
        void SerializeToFile(string fileName, IEnumerable<DataModelIssue> issues);
    }
}