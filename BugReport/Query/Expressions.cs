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
            get { return _expressions; }
        }

        public ExpressionAnd(IEnumerable<Expression> expressions)
        {
            this._expressions = expressions;
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
            get { return _expressions; }
        }

        public ExpressionOr(IEnumerable<Expression> expressions)
        {
            this._expressions = expressions;
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
        string labelName;
        public ExpressionLabel(string labelName)
        {
            this.labelName = labelName;
        }
        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.HasLabel(labelName);
        }
        public override void Validate(IssueCollection collection)
        {
            if (!collection.HasLabel(labelName))
            {
                Console.WriteLine("WARNING: Label does not exist: {0}", labelName);
            }
        }
        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            if (labelName.Contains(' '))
            {
                return $"label:\"{labelName}\"";
            }
            return "label:" + labelName;
        }
    }

    public class ExpressionIsIssue : Expression
    {
        bool isIssue;
        public ExpressionIsIssue(bool isIssue)
        {
            this.isIssue = isIssue;
        }
        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.IsIssueOrComment == isIssue;
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
            return isIssue ? "is:issue" : "is:pr";
        }
    }

    public class ExpressionIsOpen : Expression
    {
        bool isOpen;
        public ExpressionIsOpen(bool isOpen)
        {
            this.isOpen = isOpen;
        }
        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.IsOpen == isOpen;
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
            return isOpen ? "is:open" : "is:closed";
        }
    }

    public class ExpressionMilestone : Expression
    {
        string milestoneName;
        public ExpressionMilestone(string milestoneName)
        {
            this.milestoneName = milestoneName;
        }
        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.IsMilestone(milestoneName);
        }
        public override void Validate(IssueCollection collection)
        {
            if (!collection.HasMilestone(milestoneName))
            {
                Console.WriteLine("WARNING: Milestone does not exist: {0}", milestoneName);
            }
        }
        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            return "milestone:" + milestoneName;
        }
    }

    public class ExpressionAssignee : Expression
    {
        string assigneeName;
        public ExpressionAssignee(string assigneeName)
        {
            this.assigneeName = assigneeName;
        }
        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.HasAssignee(assigneeName);
        }
        public override void Validate(IssueCollection collection)
        {
            if (!collection.HasUser(assigneeName))
            {
                Console.WriteLine("WARNING: Assignee does not exist: {0}", assigneeName);
            }
        }
        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            return "assginee:" + assigneeName;
        }
    }
}
