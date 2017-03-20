using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BugReport.Util;
using BugReport.DataModel;
using BugReport.Query;

namespace BugReport.Reports
{
    public class Report
    {
        // Maximum of count links breakdown
        private static readonly int CountLinksMax = 8;

        protected static string GetQueryCountLinked(
            Expression query,
            IEnumerable<DataModelIssue> issues, 
            bool shouldHyperLink = true,
            bool useRepositoriesFromIssues = true)
        {
            int count = issues.Count();

            // TODO: Delete and remove once we have CSV export
            if (!shouldHyperLink)
            {
                return count.ToString();
            }

            query = query.Normalized;

            IEnumerable<Repository> repos = useRepositoriesFromIssues 
                ? Repository.GetReposOrDefault(issues) 
                : Repository.Repositories;

            IEnumerable<CountLink> countLinks = repos.SelectMany(repo => GetCountLinks(query, issues, repo)).ToArray();

            if ((countLinks.Count() == 0) || (countLinks.Count() > CountLinksMax))
            {
                return count.ToString();
            }
            if (countLinks.Count() == 1)
            {
                return countLinks.First().ToString();
            }
            return $"{count} <small>(" + string.Join(" + ", countLinks.Select(cl => cl.ToString())) + ")</small>";
        }

        private static IEnumerable<CountLink> GetCountLinks(
            Expression query,
            IEnumerable<DataModelIssue> issues,
            Repository repo)
        {
            if (query is ExpressionMultiRepo)
            {
                query = ((ExpressionMultiRepo)query).GetExpression(repo);
            }

            if (query is ExpressionOr)
            {
                foreach (Expression expr in ((ExpressionOr)query).Expressions)
                {
                    yield return CountLink.Create(expr, issues, repo);
                }
            }
            else if (query is ExpressionMultiRepo.ExpressionFilteredOutRepo)
            {   // Filter out repo entirely
            }
            else
            {
                yield return CountLink.Create(query, issues, repo);
            }
        }

        private class CountLink
        {
            private string Link { get; set; }
            public int Count { get; private set; }

            public override string ToString()
            {
                if (Link != null)
                {
                    return $"<a href=\"{Link}\">{Count}</a>";
                }
                return Count.ToString();
            }

            public CountLink(string link, int count)
            {
                Link = link;
                Count = count;
            }

            public static CountLink Create(
                Expression query,
                IEnumerable<DataModelIssue> issues,
                Repository repo)
            {
                string queryArgs = query.GetGitHubQueryURL();
                return new CountLink(
                    (queryArgs != null) ? repo.GetQueryUrl(queryArgs) : null,
                    query.Evaluate(issues.Where(repo)).Count());
            }
        }

        /*
        protected static string GetQueryCountLinked(
            Expression query, 
            IEnumerable<DataModelIssue> issues, 
            bool shouldHyperLink = true, 
            bool useRepositoriesFromIssues = true)
        {
            int count = issues.Count();
            if (!shouldHyperLink)
            {
                return count.ToString();
            }
            string gitHubQueryURL = query.GetGitHubQueryURL();
            if (gitHubQueryURL != null)
            {
                IEnumerable<Repository> repos = useRepositoriesFromIssues ? Repository.GetReposOrDefault(issues) : Repository.Repositories;
                if (repos.Count() <= 1)
                {
                    Repository repo = repos.First();
                    return $"<a href=\"{repo.GetQueryUrl(gitHubQueryURL)}\">{count}</a>";
                }
                else
                {
                    return $"{count} <small>(" +
                        string.Join(" + ", repos.Select(
                            repo => $"<a href=\"{repo.GetQueryUrl(gitHubQueryURL)}\">{issues.Where(repo).Count()}</a>")) +
                        ")</small>";
                }
            }
            return count.ToString();
        }

        protected static string GetQueryCountLinked_Multiple(
            Expression query, 
            IEnumerable<DataModelIssue> issues, 
            bool shouldHyperLink = true, 
            bool useRepositoriesFromIssues = true)
        {
            Expression normalizedQuery = query.Normalized;
            string gitHubQueryURL = normalizedQuery.GetGitHubQueryURL();
            if (gitHubQueryURL != null)
            {
                return GetQueryCountLinked(normalizedQuery, issues, shouldHyperLink, useRepositoriesFromIssues);
            }

            int count = issues.Count();
            if (!shouldHyperLink)
            {
                return count.ToString();
            }

            // Pattern from code:QueryReport for repo-scoped queries
            if (normalizedQuery is ExpressionMultiRepo)
            {
                return GetQueryCountLinked_Multiple(
                    (ExpressionMultiRepo)normalizedQuery,
                    (Expression expr) => expr,
                    issues,
                    count,
                    useRepositoriesFromIssues);
            }

            // Recognize pattern used by code:HtmlReport - columnQuery AND rowQuery
            if (query is ExpressionAnd)
            {
                Expression[] andExpressions = ((ExpressionAnd)query).Expressions.ToArray();
                if (andExpressions.Length == 2)
                {
                    Expression colQuery = andExpressions[1];
                    if (colQuery.GetGitHubQueryURL() != null)
                    {
                        Expression rowQuery = andExpressions[0].Normalized;
                        if (rowQuery is ExpressionOr)
                        {
                            // Wrap the query by MultiRepo expression
                            ExpressionMultiRepo multiRepoRowQuery = new ExpressionMultiRepo(
                                new RepoExpression(null, rowQuery).ToEnumerable());
                            Debug.Assert(multiRepoRowQuery.IsNormalized());

                            return GetQueryCountLinked_Multiple(
                                multiRepoRowQuery,
                                (Expression expr) => Expression.And(expr, colQuery),
                                issues,
                                count,
                                useRepositoriesFromIssues);
                        }
                        else if (rowQuery is ExpressionMultiRepo)
                        {
                            return GetQueryCountLinked_Multiple(
                                (ExpressionMultiRepo)rowQuery,
                                (Expression expr) => Expression.And(expr, colQuery),
                                issues,
                                count,
                                useRepositoriesFromIssues);
                        }
                    }
                }
            }
            return count.ToString();
        }

        private static string GetQueryCountLinked_Multiple(
            ExpressionMultiRepo multiRepoQuery,
            Func<Expression, Expression> queryTransform,
            IEnumerable<DataModelIssue> issues,
            int issuesCount,
            bool useRepositoriesFromIssues = true)
        {
            IEnumerable<RepoExpression> repoExpressions =
                (useRepositoriesFromIssues ? Repository.GetReposOrDefault(issues) : Repository.Repositories)
                    .SelectMany(
                        repo =>
                        {
                            Expression expr = multiRepoQuery.GetExpression(repo);
                            if (expr is ExpressionOr)
                            {
                                return ((ExpressionOr)expr).Expressions.Select(e => new RepoExpression(repo, e));
                            }
                            return new RepoExpression(repo, expr).ToEnumerable();
                        });

            if ((repoExpressions.Count() <= 6) &&
                repoExpressions.Where(re => (re.Expr.GetGitHubQueryURL() == null)).None())
            {
                return
                    $"{issuesCount} <small>(" +
                    string.Join("+", repoExpressions.Select(
                        re =>
                        {
                            Expression subQuery = queryTransform(re.Expr);
                            int subCount = subQuery.Evaluate(issues.Where(re.Repo)).Count();
                            string subQueryURL = subQuery.GetGitHubQueryURL();
                            Debug.Assert(subQueryURL != null);
                            return $"<a href=\"{re.Repo.GetQueryUrl(subQueryURL)}\">{subCount}</a>";
                        })) +
                    ")</small>";
            }
            return issuesCount.ToString();
        }
        */
    }
}
