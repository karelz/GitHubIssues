using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GitHubBugReport.Core.DataModel;
using GitHubBugReport.Core.Issues.Models;
using GitHubBugReport.Core.Util;

namespace GitHubBugReport.Core.Query
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

        private Expression _simplified;
        // Try to normalize into DNF form = Dusjuncite normal form:
        //  [MultiRepo] -> [OR] -> [AND] -> [NOT] -> Leaf = Label|Milestone|IsIssue|IsOpen|Assignee|Untriaged
        // May return non-normalized, just simplified expression if the expression is too complex
        public Expression Simplified
        {
            get
            {
                if (_simplified == null)
                {
                    _simplified = IsNormalized() ? this : GetSimplified();
                }

                return _simplified;
            }
        }

        protected abstract Expression GetSimplified();

        internal enum NormalizedState
        {
            MultiRepo = 0,
            Or = 1,
            And = 2,
            Not = 3,
            Leaf = 4
        }

        bool? _isNormalized = null;
        internal bool IsNormalized()
        {
            if (_isNormalized == null)
            {
                _isNormalized = IsNormalized(NormalizedState.MultiRepo);
            }

            return _isNormalized.Value;
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

        public abstract override string ToString();

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

        public abstract bool Equals(Expression e);

        // Common logic for AND and OR
        protected static bool Equals(IEnumerable<Expression> expressions1, IEnumerable<Expression> expressions2)
        {
            List<Expression> remainingExpressions2 = expressions2.ToList();
            // They have to have same length
            if (remainingExpressions2.Count != expressions1.Count())
            {
                return false;
            }
            foreach (Expression expr1 in expressions1)
            {
                bool removedExpr = false;
                foreach (Expression expr2 in remainingExpressions2)
                {
                    if (expr1.Equals(expr2))
                    {
                        remainingExpressions2.Remove(expr2);
                        removedExpr = true;
                        break;
                    }
                }
                if (!removedExpr)
                {
                    return false;
                }
            }
            Debug.Assert(remainingExpressions2.None());
            return true;
        }

        // Common logic for AND and OR
        protected static void RemoveDuplicates(List<Expression> expressions)
        {
            for (int i = 0; i < expressions.Count; i++)
            {
                Expression expr = expressions[i];
                for (int j = i + 1; j < expressions.Count; )
                {
                    if (expr.Equals(expressions[j]))
                    {   // Duplicate found
                        expressions.RemoveAt(j);
                        // Process the same index as it contains the next item
                    }
                    else
                    {   // Not a dupe, skip to next one
                        j++;
                    }
                }
            }
        }

        // Returns true if the list contains an expression X and also !X
        // Common logic for AND and OR
        protected static bool ContainsNegatedExpressions(List<Expression> expressions)
        {
            for (int i = 0; i < expressions.Count; i++)
            {
                Expression expr = expressions[i];

                if (expr is ExpressionNot)
                {
                    Expression subExpr = ((ExpressionNot)expr).Expression;
                    for (int j = 0; j < expressions.Count; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }
                        if (subExpr.Equals(expressions[j]))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    public class ExpressionTooComplexException : Exception
    {
    }
}
