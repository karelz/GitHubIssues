using Octokit;

namespace GitHubBugReport.Core.Query
{
    public struct RepoExpression
    {
        public Repository Repo { get; private set; }
        public Expression Expr { get; private set; }

        public RepoExpression(Repository repo, Expression expr)
        {
            Repo = repo;
            Expr = expr;
        }
    }
}
