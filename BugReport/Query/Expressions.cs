using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;
using BugReport.Util;

namespace BugReport.Query
{
    public abstract class Expression
    {
        public abstract bool Evaluate(DataModelIssue issue);

        public abstract void Validate(IssueCollection collection);

        public IEnumerable<DataModelIssue> Evaluate(IEnumerable<DataModelIssue> issues)
        {
            return issues.Where(i => Evaluate(i));
        }

        public abstract string GetGitHubQueryURL();

        // Normalized form:
        //  [MultiRepo] -> [OR|AND] -> [NOT] -> Leaf = Label|Milestone|IsIssue|IsOpen|Assignee|Untriaged
        public abstract Expression Normalized
        {
            get;
        }

        internal enum NormalizedState
        {
            MultiRepo = 0,
            AndOr = 1,
            Not = 2,
            Leaf = 3
        }

        internal bool IsNormalized()
        {
            return IsNormalized(NormalizedState.MultiRepo);
        }

        internal abstract bool IsNormalized(NormalizedState minAllowedState);

        protected class Indentation
        {
            private static readonly string Prefix = "  ";

            public static string Indent(string value)
            {
                return Indent(Prefix, value);
            }
            public static string Indent(string prefix, string value)
            {
                return prefix + value.Replace("\n", "\n" + prefix);
            }
        }

        public static Expression And(params Expression[] expressions)
        {
            return new ExpressionAnd(expressions);
        }
        public static Expression And(IEnumerable<Expression> expressions)
        {
            return new ExpressionAnd(expressions);
        }
        public static Expression Or(params Expression[] expressions)
        {
            return new ExpressionOr(expressions);
        }
        public static Expression Or(IEnumerable<Expression> expressions)
        {
            return new ExpressionOr(expressions);
        }
        public static Expression Not(Expression ex)
        {
            return new ExpressionNot(ex);
        }
    }

    public class ExpressionNot : Expression
    {
        readonly Expression _expr;

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
            if (_expr is ExpressionMultiRepo.ExpressionFilteredOutRepo)
            {
                return "";
            }
            return null;
        }

        internal override bool IsNormalized(NormalizedState minAllowedState)
        {
            return (minAllowedState <= NormalizedState.Not) &&_expr.IsNormalized(NormalizedState.Leaf);
        }

        public override Expression Normalized
        {
            get
            {
                if (_expr is ExpressionNot)
                {
                    return ((ExpressionNot)_expr)._expr.Normalized;
                }

                Expression normalizedExpr = _expr.Normalized;
                if (normalizedExpr is ExpressionOr)
                {
                    return Expression.And(((ExpressionOr)normalizedExpr).Expressions.Select(e => Expression.Not(e))).Normalized;
                }
                if (normalizedExpr is ExpressionAnd)
                {
                    return Expression.Or(((ExpressionAnd)normalizedExpr).Expressions.Select(e => Expression.Not(e))).Normalized;
                }
                if (normalizedExpr is ExpressionMultiRepo)
                {
                    return new ExpressionMultiRepo(((ExpressionMultiRepo)normalizedExpr).RepoExpressions.Select(repoExpr =>
                        new RepoExpression(repoExpr.Repo, Expression.Not(repoExpr.Expr).Normalized)));
                }
                return Expression.Not(normalizedExpr);
            }
        }
    }

    public class ExpressionAnd : Expression
    {
        readonly IEnumerable<Expression> _expressions;

        public IEnumerable<Expression> Expressions
        {
            get => _expressions;
        }

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
            return (minAllowedState <= NormalizedState.AndOr) &&
                _expressions.Where(e => (e is ExpressionAnd) || !e.IsNormalized(NormalizedState.AndOr)).None();
        }

        public override Expression Normalized
        {
            get
            {
                List<Expression> andExpressions = new List<Expression>();

                Queue<Expression> normalizedExpressionsQueue = new Queue<Expression>(_expressions.Select(e => e.Normalized));
                while (normalizedExpressionsQueue.Count > 0)
                {
                    Expression normalizedExpression = normalizedExpressionsQueue.Dequeue();
                    if (normalizedExpression is ExpressionAnd)
                    {   // Fold all inner AND operands into this one
                        foreach (Expression expr2 in ((ExpressionAnd)normalizedExpression)._expressions)
                        {
                            normalizedExpressionsQueue.Enqueue(expr2);
                        }
                    }
                    else
                    {
                        andExpressions.Add(normalizedExpression);
                    }
                }

                IEnumerable<ExpressionMultiRepo> multiRepoExpressions = andExpressions
                    .Where(e => e is ExpressionMultiRepo)
                    .Select(e => (ExpressionMultiRepo)e);
                if (multiRepoExpressions.None())
                {
                    return Expression.And(andExpressions);
                }

                IEnumerable<Repository> repos = multiRepoExpressions
                    .SelectMany(e => e.RepoExpressions.Select(re => re.Repo))
                    .Distinct();
                IEnumerable<RepoExpression> repoExpressions = repos
                    .Select(repo =>
                        new RepoExpression(repo,
                            Expression.And(andExpressions.Select(e => 
                                (e is ExpressionMultiRepo)
                                    ? ((ExpressionMultiRepo)e).GetExpression(repo) 
                                    : e)).Normalized
                        )
                    );
                return new ExpressionMultiRepo(repoExpressions);
            }
        }
    }

    public class ExpressionOr : Expression
    {
        readonly IEnumerable<Expression> _expressions;

        public IEnumerable<Expression> Expressions
        {
            get => _expressions;
        }

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
            return (minAllowedState <= NormalizedState.AndOr) &&
                _expressions.Where(e => (e is ExpressionOr) || !e.IsNormalized(NormalizedState.AndOr)).None();
        }

        public override Expression Normalized
        {
            get
            {
                List<Expression> orExpressions = new List<Expression>();

                Queue<Expression> normalizedExpressionsQueue = new Queue<Expression>(_expressions.Select(e => e.Normalized));
                while (normalizedExpressionsQueue.Count > 0)
                {
                    Expression normalizedExpression = normalizedExpressionsQueue.Dequeue();
                    if (normalizedExpression is ExpressionOr)
                    {   // Fold all inner OR operands into this one
                        foreach (Expression expr2 in ((ExpressionOr)normalizedExpression)._expressions)
                        {
                            normalizedExpressionsQueue.Enqueue(expr2);
                        }
                    }
                    else
                    {
                        orExpressions.Add(normalizedExpression);
                    }
                }

                IEnumerable<ExpressionMultiRepo> multiRepoExpressions = orExpressions
                    .Where(e => e is ExpressionMultiRepo)
                    .Select(e => (ExpressionMultiRepo)e);
                if (multiRepoExpressions.None())
                {
                    return Expression.Or(orExpressions);
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
                                    : e)).Normalized
                        )
                    );
                return new ExpressionMultiRepo(repoExpressions);
            }
        }
    }

    public class ExpressionLabel : Expression
    {
        readonly string _labelName;

        public ExpressionLabel(string labelName)
        {
            _labelName = labelName;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.HasLabel(_labelName);
        }

        public override void Validate(IssueCollection collection)
        {
            if (!collection.HasLabel(_labelName))
            {
                Console.WriteLine("WARNING: Label does not exist: {0}", _labelName);
            }
        }

        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            if (_labelName.Contains(' '))
            {
                return $"label:\"{_labelName}\"";
            }
            return "label:" + _labelName;
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

    public class ExpressionIsIssue : Expression
    {
        readonly bool _isIssue;

        public ExpressionIsIssue(bool isIssue)
        {
            _isIssue = isIssue;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.IsIssueOrComment == _isIssue;
        }

        public override void Validate(IssueCollection collection)
        {
        }

        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            return _isIssue ? "is:issue" : "is:pr";
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

    public class ExpressionIsOpen : Expression
    {
        readonly bool _isOpen;

        public ExpressionIsOpen(bool isOpen)
        {
            _isOpen = isOpen;
        }
        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.IsOpen == _isOpen;
        }
        public override void Validate(IssueCollection collection)
        {
        }
        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            return _isOpen ? "is:open" : "is:closed";
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

    public class ExpressionMilestone : Expression
    {
        readonly string _milestoneName;

        public ExpressionMilestone(string milestoneName)
        {
            _milestoneName = milestoneName;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.IsMilestone(_milestoneName);
        }

        public override void Validate(IssueCollection collection)
        {
            if (!collection.HasMilestone(_milestoneName))
            {
                Console.WriteLine("WARNING: Milestone does not exist: {0}", _milestoneName);
            }
        }

        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            return "milestone:" + _milestoneName;
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

    public class ExpressionAssignee : Expression
    {
        readonly string _assigneeName;

        public ExpressionAssignee(string assigneeName)
        {
            _assigneeName = assigneeName;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.HasAssignee(_assigneeName);
        }

        public override void Validate(IssueCollection collection)
        {
            if (!collection.HasUser(_assigneeName))
            {
                Console.WriteLine("WARNING: Assignee does not exist: {0}", _assigneeName);
            }
        }

        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            return "assginee:" + _assigneeName;
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
