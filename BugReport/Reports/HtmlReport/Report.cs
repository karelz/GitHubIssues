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
            private string Expression { get; set; }
            public int Count { get; private set; }

            public override string ToString()
            {
                if (Link != null)
                {
                    return $"<a href=\"{Link}\" title=\"{Expression}\">{Count}</a>";
                }
                return $"<span title=\"{Expression}\">{Count}</span>";
            }

            public CountLink(string link, string expression, int count)
            {
                Link = link;
                Expression = expression;
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
                    $"[{repo.Alias}] {query.ToString()}",
                    query.Evaluate(issues.Where(repo)).Count());
            }
        }
    }
}
