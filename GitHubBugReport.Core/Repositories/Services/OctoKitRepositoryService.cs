using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Repositories.Models;

namespace GitHubBugReport.Core.Repositories.Services
{
    public class OctoKitRepositoryService : IRepositoryService
    {
        private static readonly string _htmlUrlGitHubPrefix = "https://github.com/";

        // Captures order of definitions
        // todo: Should this be setup like this?
        private static readonly List<Repository> _repositories = new List<Repository>();

        public static IEnumerable<Repository> Repositories => _repositories;

        public Repository Find(string respositoryName)
        {
            return _repositories.FirstOrDefault(repo => repo.IsRepoName(respositoryName));
        }

        public Repository FindByAlias(string alias)
        {
            return _repositories.FirstOrDefault(repo => repo.IsAlias(alias));
        }

        public Repository FromHtmlUrl(string htmlUrl)
        {
            Debug.Assert(htmlUrl.StartsWith(_htmlUrlGitHubPrefix));
            string[] urlSplit = htmlUrl.Substring(_htmlUrlGitHubPrefix.Length).Split('/');

            if ((urlSplit.Length < 2) ||
                String.IsNullOrEmpty(urlSplit[0]) ||
                String.IsNullOrEmpty(urlSplit[1]))
            {
                throw new InvalidDataException($"Invalid GitHub URL '{htmlUrl}', can't parse repo name");
            }

            return Repository.From(urlSplit[0] + "/" + urlSplit[1]);
        }

        // Returns repos in order of their definition, or the first one as default
        public IEnumerable<Repository> GetReposOrDefault(IEnumerable<DataModelIssue> issues)
        {
            Repository[] repos = issues.Select(i => i.Repo).ToArray();

            if (repos.Length == 0)
            {
                yield return _repositories[0];
            }
            else
            {   // Return repos in order of their definitions
                foreach (Repository repo in _repositories)
                {
                    if (repos.Contains(repo))
                    {
                        yield return repo;
                    }
                }
            }
        }
    }
}
