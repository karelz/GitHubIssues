using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

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

        public virtual Expression Simplify()
        {
            return this;
        }

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
        public static Expression Or(params Expression[] expressions)
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
        Expression _expr;
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
            if ((_expr is ExpressionAnd) || (_expr is ExpressionOr))
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
            return null;
        }

        public override Expression Simplify()
        {
            if (_expr is ExpressionNot)
            {
                return ((ExpressionNot)_expr)._expr.Simplify();
            }
            return this;
        }
    }

    public class ExpressionAnd : Expression
    {
        IEnumerable<Expression> _expressions;

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

        public override Expression Simplify()
        {
            List<Expression> expressions = new List<Expression>();
            foreach (Expression expr in _expressions)
            {
                if (expr is ExpressionAnd)
                {   // Fold all inner AND operands into this one
                    foreach (Expression expr2 in ((ExpressionAnd)expr)._expressions)
                    {
                        expressions.Add(expr2.Simplify());
                    }
                }
                else
                {
                    expressions.Add(expr.Simplify());
                }
            }
            _expressions = expressions;
            return this;
        }
    }

    public class ExpressionOr : Expression
    {
        IEnumerable<Expression> _expressions;

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

        public override Expression Simplify()
        {
            List<Expression> expressions = new List<Expression>();
            foreach (Expression expr in _expressions)
            {
                if (expr is ExpressionOr)
                {   // Fold all inner OR operands into this one
                    foreach (Expression expr2 in ((ExpressionOr)expr)._expressions)
                    {
                        expressions.Add(expr2.Simplify());
                    }
                }
                else
                {
                    expressions.Add(expr.Simplify());
                }
            }
            _expressions = expressions;
            return this;
        }
    }

    public class ExpressionLabel : Expression
    {
        string _labelName;
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
    }

    public class ExpressionIsIssue : Expression
    {
        bool _isIssue;
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
    }

    public class ExpressionIsOpen : Expression
    {
        bool _isOpen;
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
    }

    public class ExpressionMilestone : Expression
    {
        string _milestoneName;
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
    }

    public class ExpressionAssignee : Expression
    {
        string _assigneeName;
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
    }

    public struct RepoExpression
    {
        public Repository Repo;
        public Expression Expr;
        public RepoExpression(Repository repo, Expression expr)
        {
            Repo = repo;
            Expr = expr;
        }
    }

    public class ExpressionMultiRepo : Expression
    {
        Dictionary<Repository, Expression> _expressions;
        Expression _defaultExpression;

        public IEnumerable<Expression> Expressions
        {
            get
            {
                foreach (Expression expr in _expressions.Values)
                {
                    yield return expr;
                }
                if (_defaultExpression != null)
                {
                    yield return _defaultExpression;
                }
            }
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
                        throw new InvalidDataException($"Duplicate no-repo query defined.");
                    }
                    _defaultExpression = repoExpr.Expr;
                    continue;
                }
                if (_expressions.ContainsKey(repoExpr.Repo))
                {
                    throw new InvalidDataException($"Duplicate repo query defined for repo {repoExpr.Repo.RepoName}.");
                }
                _expressions[repoExpr.Repo] = repoExpr.Expr;
            }
        }

        public Expression GetExpression(Repository repo)
        {
            Expression expr;
            if (_expressions.TryGetValue(repo, out expr))
            {
                return expr;
            }
            return _defaultExpression;
        }

        public override Expression Simplify()
        {
            if (_defaultExpression != null)
            {
                _defaultExpression = _defaultExpression.Simplify();
            }
            Repository[] repos = _expressions.Keys.ToArray();
            foreach (Repository repo in repos)
            {
                _expressions[repo] = _expressions[repo].Simplify();
            }
            return this;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            Expression expr = GetExpression(issue.Repo);
            return expr != null ? expr.Evaluate(issue) : false;
        }

        public override string GetGitHubQueryURL()
        {
            return null;
        }

        public override void Validate(IssueCollection collection)
        {
            if (_defaultExpression != null)
            {
                _defaultExpression.Validate(collection);
            }
            foreach (Expression expr in _expressions.Values)
            {
                expr.Validate(collection);
            }
        }
    }
}
