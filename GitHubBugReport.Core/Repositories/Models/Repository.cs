﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Repositories.Services;
using Octokit;

namespace GitHubBugReport.Core.Repositories.Models
{
    public class Repository
    {
        public string Owner { get; private set; }
        public string Name { get; private set; }
        public string Alias { get; private set; }
        public bool IsAlias(string alias) => (Alias == alias.ToLower());
        public Expression FilterQuery { get; private set; }
        // owner/name lowercase
        public string RepoName { get; private set; }
        public bool IsRepoName(string repoName) => (RepoName == repoName.ToLower());
        // https://github.com/owner/name/
        public string HtmlUrlPrefix { get; private set; }
        public string AuthenticationToken { get; set; }

        // TODO - Move to config
        private static readonly string s_GitHubProductIdentifier = "GitHubBugReporter";

        public IReadOnlyList<Issue> Issues { get; set; }
        public ConcurrentBag<IssueComment> IssueComments { get; private set; }

        private Repository(string repoName, string alias, string filterQuery)
        {
            // TODO: Decouple this, shouldn't exist here
            IRepositoryService repositoryService = new OctoKitRepositoryService();

            RepoName = repoName.ToLower();
            Debug.Assert(repositoryService._repositories.Where(repo => (repo.RepoName == RepoName)).None());
            Alias = (alias != null) ? alias.ToLower() : RepoName;
            Debug.Assert(repositoryService._repositories.Where(repo => (repo.Alias == Alias)).None());

            // TODO: This code is duplicated in OctoKitRepositoryService.FromHtmlUrl
            string[] repoNameParts = RepoName.Split('/');
            if ((repoNameParts.Length != 2) ||
                string.IsNullOrEmpty(repoNameParts[0]) ||
                string.IsNullOrEmpty(repoNameParts[1]))
            {
                throw new InvalidDataException($"Invalid repository name format in repo '{repoName}'");
            }

            Owner = repoNameParts[0];
            Name = repoNameParts[1];

            HtmlUrlPrefix = OctoKitRepositoryService._htmlUrlGitHubPrefix + RepoName + "/";
            RepositoryService._repositories.Add(this);

            if (filterQuery != null)
            {
                FilterQuery = QueryParser.Parse(filterQuery, null);
            }
        }

        public IEnumerable<DataModelIssue> Filter(IEnumerable<DataModelIssue> issues)
        {
            if (FilterQuery == null)
            {
                return issues;
            }

            return issues.Where(i => ((i.Repo != this) || FilterQuery.Evaluate(i)));
        }

        public void UpdateAlias(string alias)
        {
            Debug.Assert(alias != null);
            if (Alias == RepoName)
            {
                Alias = alias;
            }
            else
            {
                if (Alias != alias)
                {
                    throw new InvalidDataException($"Repository '{RepoName}' has 2 aliases defined '{Alias}' and '{alias}'");
                }
            }
        }

        public void UpdateFilterQuery(string filterQuery)
        {
            Debug.Assert(filterQuery != null);
            if (FilterQuery == null)
            {
                FilterQuery = QueryParser.Parse(filterQuery, null);
            }
            else
            {
                Expression filterQueryExpr = QueryParser.Parse(filterQuery, null);
                if (!filterQueryExpr.Equals(FilterQuery) && !filterQueryExpr.Simplified.Equals(FilterQuery.Simplified))
                {
                    throw new InvalidDataException($"Repository '{RepoName}' has 2 filter queries defined '{FilterQuery.ToString()}' and '{filterQuery}'");
                }
            }
        }

        public string GetQueryUrl(string queryArgs)
        {
            return HtmlUrlPrefix + "issues?q=" + System.Net.WebUtility.UrlEncode(queryArgs);
        }

        public string GetQueryUrl(string queryPrefix, IEnumerable<DataModelIssue> issues)
        {
            return GetQueryUrl(queryPrefix + " " + string.Join(" ", issues.Select(i => i.Number)));
        }

        public static Repository From(string repoName, string alias = null, string filterQuery = null)
        {
            // TODO: When the time is right, move this or refactor it.
            IRepositoryService repositoryService = new OctoKitRepositoryService();

            Repository repo = repositoryService.Find(repoName) ?? repositoryService.FindByAlias(repoName);
            if (repo == null)
            {
                repo = new Repository(repoName, alias, filterQuery);
                Debug.Assert(repositoryService.Find(repoName) == repo);
            }
            else
            {
                if (alias != null)
                {
                    repo.UpdateAlias(alias);
                }

                if (filterQuery != null)
                {
                    repo.UpdateFilterQuery(filterQuery);
                }
            }
            return repo;
        }

        public override string ToString()
        {
            if (FilterQuery == null)
            {
                return RepoName;
            }

            return $"{RepoName} filtered by '{FilterQuery}'";
        }
    }
}