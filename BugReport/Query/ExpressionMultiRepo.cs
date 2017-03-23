using System.Collections.Generic;
using System.IO;
using System.Linq;
using BugReport.DataModel;
using BugReport.Util;
using System.Diagnostics;
using System;

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
                _defaultExpression = FilteredOutRepoExpression;
            }
        }

        private static readonly ExpressionConstant FilteredOutRepoExpression = ExpressionConstant.False;

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

        private static readonly string RepoQuerySeparator = "\r\n";
        public override string ToString()
        {
            if (!_expressions.Any())
            {
                return $"{{ {_defaultExpression} }}";
            }

            string text = "{ ";
            text += string.Join(RepoQuerySeparator, _expressions.Select(entry => $"[{entry.Key.Alias}] {entry.Value}"));
            if (_defaultExpression != FilteredOutRepoExpression)
            {
                if (_expressions.Any())
                {
                    text += RepoQuerySeparator;
                }
                text += $"[default] {_defaultExpression}";
            }
            text += " }";
            return text;
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

        protected override bool Equals(Expression e)
        {
            if (e is ExpressionMultiRepo)
            {
                ExpressionMultiRepo expr = (ExpressionMultiRepo)e;
                if ((_expressions.Count != expr._expressions.Count) ||
                    !_defaultExpression.Equals(expr._defaultExpression))
                {
                    return false;
                }
                foreach (Repository repo in _expressions.Keys)
                {
                    if (!expr._expressions.TryGetValue(repo, out Expression e2) ||
                        !_expressions[repo].Equals(e2))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
