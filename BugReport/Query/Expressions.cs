using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

namespace BugReport.Query
{
    public abstract class Expression
    {
        public abstract bool Evaluate(Issue issue);
        public abstract void Validate(IssueCollection collection);

        protected class Indentation
        {
            public static string Indent(string value)
            {
                return Indent("  ", value);
            }
            public static string Indent(string prefix, string value)
            {
                return prefix + value.Replace("\n", "\n" + prefix);
            }
        }
    }

    public class ExpressionNot : Expression
    {
        Expression expr;
        public ExpressionNot(Expression expr)
        {
            this.expr = expr;
        }
        public override bool Evaluate(Issue issue)
        {
            return !expr.Evaluate(issue);
        }
        public override void Validate(IssueCollection collection)
        {
            expr.Validate(collection);
        }
        public override string ToString()
        {
            return "NOT:\n" + Indentation.Indent(expr.ToString());
        }
    }

    public class ExpressionAnd : Expression
    {
        IEnumerable<Expression> expressions;

        public ExpressionAnd(IEnumerable<Expression> expressions)
        {
            this.expressions = expressions;
        }
        public override bool Evaluate(Issue issue)
        {
            foreach (Expression expr in expressions)
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
            foreach (Expression expr in expressions)
            {
                expr.Validate(collection);
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("AND:");
            foreach (Expression expr in expressions)
            {
                sb.AppendLine();
                sb.Append(Indentation.Indent(expr.ToString()));
            }
            return sb.ToString();
        }
    }

    public class ExpressionOr : Expression
    {
        IEnumerable<Expression> expressions;

        public ExpressionOr(IEnumerable<Expression> expressions)
        {
            this.expressions = expressions;
        }
        public override bool Evaluate(Issue issue)
        {
            foreach (Expression expr in expressions)
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
            foreach (Expression expr in expressions)
            {
                expr.Validate(collection);
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("OR:");
            foreach (Expression expr in expressions)
            {
                sb.AppendLine();
                sb.Append(Indentation.Indent(expr.ToString()));
            }
            return sb.ToString();
        }
    }

    public class ExpressionLabel : Expression
    {
        string labelName;
        public ExpressionLabel(string labelName)
        {
            this.labelName = labelName;
        }
        public override bool Evaluate(Issue issue)
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
        public override bool Evaluate(Issue issue)
        {
            return issue.IsIssue == isIssue;
        }
        public override void Validate(IssueCollection collection)
        {
        }
        public override string ToString()
        {
            return "is:" + (isIssue ? "issue" : "pr");
        }
    }

    public class ExpressionIsOpen : Expression
    {
        bool isOpen;
        public ExpressionIsOpen(bool isOpen)
        {
            this.isOpen = isOpen;
        }
        public override bool Evaluate(Issue issue)
        {
            return issue.IsOpen == isOpen;
        }
        public override void Validate(IssueCollection collection)
        {
        }
        public override string ToString()
        {
            return "is:" + (isOpen ? "open" : "closed");
        }
    }

    public class ExpressionMilestone : Expression
    {
        string milestoneName;
        public ExpressionMilestone(string milestoneName)
        {
            this.milestoneName = milestoneName;
        }
        public override bool Evaluate(Issue issue)
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
        public override bool Evaluate(Issue issue)
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
            return "assginee:" + assigneeName;
        }
    }
}
