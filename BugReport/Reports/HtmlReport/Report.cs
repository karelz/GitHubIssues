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
    }
}
