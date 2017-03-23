using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
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

        // Normalized DNF form = Dusjuncite normal form:
        //  [MultiRepo] -> [OR] -> [AND] -> [NOT] -> Leaf = Label|Milestone|IsIssue|IsOpen|Assignee|Untriaged
        public abstract Expression Normalized
        {
            get;
        }

        internal enum NormalizedState
        {
            MultiRepo = 0,
            Or = 1,
            And = 2,
            Not = 3,
            Leaf = 4
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

        protected abstract bool Equals(Expression e);

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

    public class ExpressionNot : Expression
    {
        readonly Expression _expr;

        public Expression Expression
        {
            get => _expr;
        }

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
                if (_expr is ExpressionConstant)
                {
                    Debug.Assert((_expr == ExpressionConstant.True) || (_expr == ExpressionConstant.False));
                    return (_expr == ExpressionConstant.True) ? ExpressionConstant.False : ExpressionConstant.True;
                }
                return Expression.Not(normalizedExpr);
            }
        }

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionNot) &&
                ((ExpressionNot)e)._expr.Equals(_expr);
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
            return (minAllowedState <= NormalizedState.And) &&
                _expressions.Where(e => !e.IsNormalized(NormalizedState.And + 1)).None();
        }

        public override Expression Normalized
        {
            get
            {
                List<Expression> andExpressions = new List<Expression>();

                // First flatten all ANDs
                Queue<Expression> normalizedExpressionsQueue = new Queue<Expression>(_expressions);
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
                    else if (normalizedExpression == ExpressionConstant.True)
                    {   // Skip 'True' expressions in AND list
                    }
                    else
                    {
                        andExpressions.Add(normalizedExpression);
                    }
                }
                
                // Now normalize the AND expressions (can't do it earlier, because AND(x,AND(y,OR(a,b))) would start to permutate too early
                {
                    List<Expression> andExpressionsNormalized = new List<Expression>(andExpressions.Select(e => e.Normalized));
                    andExpressions = andExpressionsNormalized;
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
                                    .Normalized
                            )
                        );
                    Expression multiRepoExpr = new ExpressionMultiRepo(repoExpressions);
                    Debug.Assert(multiRepoExpr.IsNormalized());
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

                    List<Expression> orExpressions = new List<Expression>();

                    // (A || B) && Z ---> (A && Z) || (B && Z)
                    // (A || B) && (C || D) && Z ---> (A && C && Z) || (A && D && Z) || (B && C && Z) || (B && D && Z)
                    foreach (IEnumerable<Expression> orSubExpressionsAndPermutation in GeneratePermutations(orSubExpressions))
                    {
                        orExpressions.Add(Expression.And(orSubExpressionsAndPermutation.Concat(nonOrSubExpressions)).Normalized);
                    }
                    Expression orExpression = Expression.Or(orExpressions);
                    Debug.Assert(orExpression.IsNormalized());
                    return orExpression;
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
                Debug.Assert(andExpression.IsNormalized());
                return andExpression;
            }
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
                            yield return expr.ToEnumerable().Concat(tailPermutation);
                        }
                    }
                }
            }
        }

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionAnd) ? 
                Equals(_expressions, ((ExpressionAnd)e).Expressions) : 
                false;
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
            return (minAllowedState <= NormalizedState.Or) &&
                _expressions.Where(e => !e.IsNormalized(NormalizedState.Or + 1)).None();
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
                    else if (normalizedExpression == ExpressionConstant.False)
                    {   // Skip 'False' expression in OR list
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
                    Debug.Assert(orExpr.IsNormalized());
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
                                    : e)).Normalized
                        )
                    );
                Expression expr = new ExpressionMultiRepo(repoExpressions);
                Debug.Assert(expr.IsNormalized());
                return expr;
            }
        }

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionOr) ? 
                Equals(_expressions, ((ExpressionOr)e).Expressions) : 
                false;
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
                Console.WriteLine($"WARNING: Label does not exist: {_labelName}");
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

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionLabel) ? 
                _labelName == ((ExpressionLabel)e)._labelName : 
                false;
        }
    }

    public class ExpressionLabelPattern : Expression
    {
        readonly string _labelPattern;
        readonly Regex _labelRegex;

        public ExpressionLabelPattern(string labelPattern)
        {
            _labelPattern = labelPattern;
            _labelRegex = new Regex("^" + labelPattern + "$");
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return issue.Labels.Where(label => _labelRegex.IsMatch(label.Name)).Any();
        }

        public override void Validate(IssueCollection collection)
        {
            if (collection.Labels.Where(label => _labelRegex.IsMatch(label.Name)).None())
            {
                Console.WriteLine($"WARNING: Label pattern does not match any label: {_labelRegex.ToString()}");
            }
        }

        public override string ToString()
        {
            if (_labelPattern.Contains(' '))
            {
                return $"label:\"{_labelPattern}\"";
            }
            return "label:" + _labelPattern;
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

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionLabelPattern) ?
                _labelPattern == ((ExpressionLabelPattern)e)._labelPattern :
                false;
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

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionIsIssue) ?
                _isIssue == ((ExpressionIsIssue)e)._isIssue :
                false;
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

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionIsOpen) ?
                _isOpen == ((ExpressionIsOpen)e)._isOpen :
                false;
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
                Console.WriteLine($"WARNING: Milestone does not exist: {_milestoneName}");
            }
        }

        public override string ToString()
        {
            return GetGitHubQueryURL();
        }

        public override string GetGitHubQueryURL()
        {
            if (_milestoneName == null)
            {
                return "no:milestone";
            }
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

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionMilestone) ?
                _milestoneName == ((ExpressionMilestone)e)._milestoneName :
                false;
        }
    }

    public class ExpressionMilestonePattern : Expression
    {
        readonly string _milestonePattern;
        readonly Regex _milestoneRegex;

        public ExpressionMilestonePattern(string milestonePattern)
        {
            _milestonePattern = milestonePattern;
            _milestoneRegex = new Regex("^" + milestonePattern + "$");
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return ((issue.Milestone != null) && 
                _milestoneRegex.IsMatch(issue.Milestone.Title));
        }

        public override void Validate(IssueCollection collection)
        {
            // TODO: We could enumerate all Milestones on collection - very low pri
        }

        public override string ToString()
        {
            if (_milestonePattern.Contains(' '))
            {
                return $"milestone:\"{_milestonePattern}\"";
            }
            return "milestone:" + _milestonePattern;
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

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionMilestonePattern) ?
                _milestonePattern == ((ExpressionMilestonePattern)e)._milestonePattern :
                false;
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
                Console.WriteLine($"WARNING: Assignee does not exist: {_assigneeName}");
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

        protected override bool Equals(Expression e)
        {
            return (e is ExpressionAssignee) ?
                _assigneeName == ((ExpressionAssignee)e)._assigneeName :
                false;
        }
    }

    public class ExpressionConstant : Expression
    {
        readonly bool _value;

        public static readonly ExpressionConstant True = new ExpressionConstant(true);
        public static readonly ExpressionConstant False = new ExpressionConstant(false);

        private ExpressionConstant(bool value)
        {
            _value = value;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return _value;
        }

        public override void Validate(IssueCollection collection)
        {
        }

        public override string ToString()
        {
            return _value.ToString();
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

        protected override bool Equals(Expression e)
        {
            // Only 2 static instances exist (True & False), so it is sufficient to compare object references
            return (this == e);
        }
    }
}
