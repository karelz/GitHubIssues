using System.Collections.Generic;
using System.IO;
using System.Linq;
using BugReport.DataModel;
using BugReport.Util;
using System.Diagnostics;

namespace BugReport.Query
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

    public class ExpressionMultiRepo : Expression
    {
        readonly Dictionary<Repository, Expression> _expressions;

        readonly Expression _defaultExpression;

        public IEnumerable<RepoExpression> RepoExpressions
        {
            get => _expressions.Select(entry => new RepoExpression(entry.Key, entry.Value))
                    .Concat(new RepoExpression(null, _defaultExpression).ToEnumerable());
        }

        private ExpressionMultiRepo(Dictionary<Repository, Expression> expressions, Expression defaultExpression)
        {
            _expressions = expressions;
            _defaultExpression = defaultExpression;
        }

        public ExpressionMultiRepo(IEnumerable<RepoExpression> expressions)
        {
            _expressions = new Dictionary<Repository, Expression>();
            _defaultExpression = null;
            foreach (RepoExpression repoExpr in expressions)
            {
                if (repoExpr.Repo == null)
                {
                    if (_defaultExpression != null)
                    {
                        throw new InvalidDataException($"Duplicate default query defined.");
                    }
                    _defaultExpression = repoExpr.Expr;
                }
                else
                {
                    if (_expressions.ContainsKey(repoExpr.Repo))
                    {
                        throw new InvalidDataException($"Duplicate repo query defined for repo {repoExpr.Repo.RepoName}.");
                    }
                    _expressions[repoExpr.Repo] = repoExpr.Expr;
                }
            }
            if (_defaultExpression == null)
            {
                _defaultExpression = _filteredOutRepoExpression;
            }
        }

        private static readonly ExpressionFilteredOutRepo _filteredOutRepoExpression = new ExpressionFilteredOutRepo();

        public Expression GetExpression(Repository repo)
        {
            if (repo == null)
            {
                return _defaultExpression;
            }
            Expression expr;
            if (_expressions.TryGetValue(repo, out expr))
            {
                return expr;
            }
            return _defaultExpression;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return GetExpression(issue.Repo).Evaluate(issue);
        }

        public override string ToString()
        {
            return "{ " + string.Join(" / ", _expressions.Select(entry => $"[{entry.Key.RepoName}: {entry.Value}]")) + 
                (_expressions.Any() ? " / " : "") +
                $"[default: {_defaultExpression}] }}";
        }

        public override string GetGitHubQueryURL()
        {
            return null;
        }

        public override void Validate(IssueCollection collection)
        {
            foreach (Expression expr in _expressions.Values)
            {
                expr.Validate(collection);
            }
            _defaultExpression.Validate(collection);
        }

        internal override bool IsNormalized(NormalizedState minAllowedState)
        {
            return (minAllowedState == NormalizedState.MultiRepo) &&
                _expressions.Values.Where(e => !e.IsNormalized(NormalizedState.Or)).None() &&
                _defaultExpression.IsNormalized(NormalizedState.Or);
        }

        public override Expression Normalized
        {
            get
            {
                Dictionary<Repository, Expression> normalizedExpressions = new Dictionary<Repository, Expression>();
                foreach (KeyValuePair<Repository, Expression> entry in _expressions)
                {
                    Expression normalizedExpression = entry.Value.Normalized;
                    if (normalizedExpression is ExpressionMultiRepo)
                    {
                        normalizedExpression = ((ExpressionMultiRepo)normalizedExpression).GetExpression(entry.Key);
                    }
                    normalizedExpressions[entry.Key] = normalizedExpression;
                }
                Expression normalizedDefaultExpression = _defaultExpression.Normalized;

                if (normalizedDefaultExpression is ExpressionMultiRepo)
                {
                    ExpressionMultiRepo normalizedDefaultExpressionMultiRepo = (ExpressionMultiRepo)normalizedDefaultExpression;
                    foreach (RepoExpression repoExpression in normalizedDefaultExpressionMultiRepo.RepoExpressions
                        .Where(re => !normalizedExpressions.ContainsKey(re.Repo)))
                    {
                        Debug.Assert(!normalizedExpressions.ContainsKey(repoExpression.Repo));
                        Debug.Assert(repoExpression.Expr.IsNormalized());
                        normalizedExpressions[repoExpression.Repo] = repoExpression.Expr;
                    }
                    normalizedDefaultExpression = normalizedDefaultExpressionMultiRepo.GetExpression(null);
                }

                ExpressionMultiRepo newMultiRepoExpression = new ExpressionMultiRepo(
                    normalizedExpressions, 
                    normalizedDefaultExpression);
                Debug.Assert(newMultiRepoExpression.IsNormalized());
                return newMultiRepoExpression;
            }
        }

        // Represents 'false' expression - the repo has been filtered out by not having any applicable query
        public class ExpressionFilteredOutRepo : Expression
        {
            public ExpressionFilteredOutRepo()
            {
            }

            public override bool Evaluate(DataModelIssue issue)
            {
                return false;
            }

            public override void Validate(IssueCollection collection)
            {
            }

            public override string ToString()
            {
                return "false";
            }

            public override string GetGitHubQueryURL()
            {
                return null;
            }

            internal override bool IsNormalized(NormalizedState minAllowedState)
            {
                return true;
            }

            public override Expression Normalized
            {
                get => this;
            }
        }
    }
}
