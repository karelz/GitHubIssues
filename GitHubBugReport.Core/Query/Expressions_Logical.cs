using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GitHubBugReport.Core.DataModel;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Repositories.Models;
//using GitHubBugReport.Core.Repositories.Models;
using GitHubBugReport.Core.Util;
//using Octokit;

namespace GitHubBugReport.Core.Query
{
    public class ExpressionNot : Expression
    {
        readonly Expression _expr;

        public Expression Expression => _expr;

        public ExpressionNot(Expression expr)
        {
            _expr = expr;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return !_expr.Evaluate(issue);
        }

        public override void Validate(IssueCollection collection)
        {
            _expr.Validate(collection);
        }

        public override string ToString()
        {
            if ((_expr is ExpressionAnd) || (_expr is ExpressionOr) || (_expr is ExpressionMultiRepo))
            {
                return $"!({_expr})";
            }

            return $"-{_expr}";
        }

        public override string GetGitHubQueryURL()
        {
            if ((_expr is ExpressionLabel) || (_expr is ExpressionMilestone) || (_expr is ExpressionAssignee))
            {
                return "-" + _expr.GetGitHubQueryURL();
            }
            if (_expr == ExpressionConstant.False)
            {
                return "";
            }
            return null;
        }

        internal override bool IsNormalized(NormalizedState minAllowedState)
        {
            return (minAllowedState <= NormalizedState.Not) &&_expr.IsNormalized(NormalizedState.Leaf);
        }

        protected override Expression GetSimplified()
        {
            if (_expr is ExpressionNot not)
            {
                return not._expr.Simplified;
            }

            Expression simplifiedExpr = _expr.Simplified;
            switch (simplifiedExpr)
            {
                case ExpressionOr or:
                    return Query.Expression.And(or.Expressions.Select(e => Expression.Not(e))).Simplified;
                case ExpressionAnd and:
                    return Expression.Or(and.Expressions.Select(e => Expression.Not(e))).Simplified;
                case ExpressionMultiRepo multiRepo:
                    return new ExpressionMultiRepo(multiRepo.RepoExpressions.Select(repoExpr =>
                        new RepoExpression(repoExpr.Repo, Expression.Not(repoExpr.Expr).Simplified)));
                case ExpressionConstant constant:
                    Debug.Assert((constant == ExpressionConstant.True) || (constant == ExpressionConstant.False));
                    return (constant == ExpressionConstant.True) ? ExpressionConstant.False : ExpressionConstant.True;
            }
            return Expression.Not(simplifiedExpr);
        }

        public override bool Equals(Expression e)
        {
            return (e is ExpressionNot) &&
                ((ExpressionNot)e)._expr.Equals(_expr);
        }
    }

    public class ExpressionAnd : Expression
    {
        readonly IEnumerable<Expression> _expressions;

        public IEnumerable<Expression> Expressions => _expressions;

        public ExpressionAnd(IEnumerable<Expression> expressions)
        {
            _expressions = expressions;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            foreach (Expression expr in _expressions)
            {
                if (!expr.Evaluate(issue))
                {
                    return false;
                }
            }

            return true;
        }

        public override void Validate(IssueCollection collection)
        {
            foreach (Expression expr in _expressions)
            {
                expr.Validate(collection);
            }
        }

        public override string ToString()
        {
            return string.Join(" ", _expressions.Select(e => (e is ExpressionOr) ? $"({e})" : e.ToString()));
        }

        public override string GetGitHubQueryURL()
        {
            IEnumerable<string> subQueries = _expressions.Select(expr => expr.GetGitHubQueryURL());

            if (subQueries.Contains(null))
            {
                return null;
            }

            return string.Join(" ", subQueries);
        }

        internal override bool IsNormalized(NormalizedState minAllowedState)
        {
            return (minAllowedState <= NormalizedState.And) &&
                _expressions.Where(e => !e.IsNormalized(NormalizedState.And + 1)).None();
        }

        static readonly int const_SubExpressionsLimit = 100;

        protected override Expression GetSimplified()
        {
            List<Expression> andExpressions = new List<Expression>();

            // First flatten all ANDs
            Queue<Expression> simplifiedExpressionsQueue = new Queue<Expression>(_expressions);
            while (simplifiedExpressionsQueue.Count > 0)
            {
                Expression simplifiedExpression = simplifiedExpressionsQueue.Dequeue();
                if (simplifiedExpression is ExpressionAnd)
                {   // Fold all inner AND operands into this one
                    foreach (Expression expr2 in ((ExpressionAnd)simplifiedExpression)._expressions)
                    {
                        simplifiedExpressionsQueue.Enqueue(expr2);
                    }
                }
                else if (simplifiedExpression == ExpressionConstant.True)
                {   // Skip 'True' expressions in AND list
                }
                else
                {
                    andExpressions.Add(simplifiedExpression);
                }
            }
                
            // Now normalize the AND expressions (can't do it earlier, because AND(x,AND(y,OR(a,b))) would start to permutate too early
            {
                List<Expression> andExpressionsSimplified = new List<Expression>(andExpressions.Select(e => e.Simplified));
                andExpressions = andExpressionsSimplified;
            }

            // Handle multi-repo sub-expressions if present - bubble merge multi-repo expression up
            IEnumerable<ExpressionMultiRepo> multiRepoExpressions = andExpressions
                .Where(e => e is ExpressionMultiRepo)
                .Select(e => (ExpressionMultiRepo)e);

            if (multiRepoExpressions.Any())
            {
                IEnumerable<Repository> repos = multiRepoExpressions
                    .SelectMany(e => e.RepoExpressions.Select(re => re.Repo))
                    .Distinct();

                IEnumerable<RepoExpression> repoExpressions = repos
                    .Select(repo =>
                        new RepoExpression(repo,
                            Expression.And(andExpressions.Select(e =>
                                    (e is ExpressionMultiRepo)
                                        ? ((ExpressionMultiRepo)e).GetExpression(repo)
                                        : e))
                                // We need to normalize to bubble up all ORs on top
                                .Simplified
                        )
                    );
                Expression multiRepoExpr = new ExpressionMultiRepo(repoExpressions);
                return multiRepoExpr;
            }

            // Handle OR sub-expressions if present - make sub-expression permutations and bubble OR up
            IEnumerable<ExpressionOr> orSubExpressions = andExpressions
                .Where(e => e is ExpressionOr)
                .Select(e => (ExpressionOr)e).ToArray();

            if (orSubExpressions.Any())
            {
                IEnumerable<Expression> nonOrSubExpressions = andExpressions.Where(e => !(e is ExpressionOr)).ToArray();
                Debug.Assert(andExpressions.Count() == orSubExpressions.Count() + nonOrSubExpressions.Count());

                // Check for large expansions
                int combinationsCount = 1;
                foreach (ExpressionOr orSubExpression in orSubExpressions)
                {
                    combinationsCount *= orSubExpression.Expressions.Count();
                }
                if (combinationsCount < const_SubExpressionsLimit)
                {
                    List<Expression> orExpressions = new List<Expression>();

                    // (A || B) && Z ---> (A && Z) || (B && Z)
                    // (A || B) && (C || D) && Z ---> (A && C && Z) || (A && D && Z) || (B && C && Z) || (B && D && Z)
                    foreach (IEnumerable<Expression> orSubExpressionsAndPermutation in GeneratePermutations(orSubExpressions))
                    {
                        orExpressions.Add(Expression.And(orSubExpressionsAndPermutation.Concat(nonOrSubExpressions)).Simplified);
                    }
                    Expression orExpression = Expression.Or(orExpressions);
                    return orExpression;
                }
            }

            // Simplify the expression
            if (andExpressions.Contains(ExpressionConstant.False))
            {
                return ExpressionConstant.False;
            }

            RemoveDuplicates(andExpressions);

            if (ContainsNegatedExpressions(andExpressions))
            {
                return ExpressionConstant.False;
            }

            if (andExpressions.Count == 1)
            {
                return andExpressions[0];
            }

            // AND is on the top
            Expression andExpression = Expression.And(andExpressions);
            return andExpression;
        }

        private IEnumerable<IEnumerable<Expression>> GeneratePermutations(IEnumerable<ExpressionOr> orExpressions)
        {
            return GeneratePermutations(orExpressions, orExpressions.Count());
        }

        private IEnumerable<IEnumerable<Expression>> GeneratePermutations(
            IEnumerable<ExpressionOr> orExpressions, 
            int orExpressionsCount)
        {
            foreach (ExpressionOr orExpression in orExpressions)
            {
                foreach (Expression expr in orExpression.Expressions)
                {
                    if (orExpressionsCount == 1)
                    {
                        yield return expr.ToEnumerable();
                    }
                    else
                    {
                        foreach (IEnumerable<Expression> tailPermutation in GeneratePermutations(orExpressions.Skip(1)))
                        {
                            yield return Enumerable.Concat(expr.ToEnumerable(), tailPermutation);
                        }
                    }
                }
            }
        }

        public override bool Equals(Expression e)
        {
            return (e is ExpressionAnd) ? 
                Equals(_expressions, ((ExpressionAnd)e).Expressions) : 
                false;
        }
    }

    public class ExpressionOr : Expression
    {
        readonly IEnumerable<Expression> _expressions;

        public IEnumerable<Expression> Expressions => _expressions;

        public ExpressionOr(IEnumerable<Expression> expressions)
        {
            _expressions = expressions;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            foreach (Expression expr in _expressions)
            {
                if (expr.Evaluate(issue))
                {
                    return true;
                }
            }

            return false;
        }

        public override void Validate(IssueCollection collection)
        {
            foreach (Expression expr in _expressions)
            {
                expr.Validate(collection);
            }
        }

        public override string ToString()
        {
            return string.Join(" OR ", _expressions.Select(e => (e is ExpressionAnd) ? $"({e})" : e.ToString()));
        }

        public override string GetGitHubQueryURL()
        {
            return null;
        }

        internal override bool IsNormalized(NormalizedState minAllowedState)
        {
            return (minAllowedState <= NormalizedState.Or) &&
                _expressions.Where(e => !e.IsNormalized(NormalizedState.Or + 1)).None();
        }

        protected override Expression GetSimplified()
        {
            List<Expression> orExpressions = new List<Expression>();

            Queue<Expression> simplifiedExpressionsQueue = new Queue<Expression>(_expressions.Select(e => e.Simplified));
            while (simplifiedExpressionsQueue.Count > 0)
            {
                Expression simplifiedExpression = simplifiedExpressionsQueue.Dequeue();
                if (simplifiedExpression is ExpressionOr)
                {   // Fold all inner OR operands into this one
                    foreach (Expression expr2 in ((ExpressionOr)simplifiedExpression)._expressions)
                    {
                        simplifiedExpressionsQueue.Enqueue(expr2);
                    }
                }
                else if (simplifiedExpression == ExpressionConstant.False)
                {   // Skip 'False' expression in OR list
                }
                else
                {
                    orExpressions.Add(simplifiedExpression);
                }
            }

            IEnumerable<ExpressionMultiRepo> multiRepoExpressions = orExpressions
                .Where(e => e is ExpressionMultiRepo)
                .Select(e => (ExpressionMultiRepo)e);

            if (multiRepoExpressions.None())
            {
                if (orExpressions.Contains(ExpressionConstant.True))
                {
                    return ExpressionConstant.True;
                }
                RemoveDuplicates(orExpressions);
                if (ContainsNegatedExpressions(orExpressions))
                {
                    return ExpressionConstant.True;
                }

                if (orExpressions.Count == 1)
                {
                    return orExpressions[0];
                }

                Expression orExpr = Expression.Or(orExpressions.Distinct());
                return orExpr;
            }

            IEnumerable<Repository> repos = multiRepoExpressions
                .SelectMany(e => e.RepoExpressions.Select(re => re.Repo))
                .Distinct();

            IEnumerable<RepoExpression> repoExpressions = repos
                .Select(repo =>
                    new RepoExpression(repo,
                        Expression.Or(orExpressions.Select(e =>
                            (e is ExpressionMultiRepo)
                                ? ((ExpressionMultiRepo)e).GetExpression(repo)
                                : e)).Simplified
                    )
                );
            Expression expr = new ExpressionMultiRepo(repoExpressions);
            return expr;
        }

        public override bool Equals(Expression e)
        {
            return (e is ExpressionOr) ? 
                Equals(_expressions, ((ExpressionOr)e).Expressions) : 
                false;
        }
    }
}
