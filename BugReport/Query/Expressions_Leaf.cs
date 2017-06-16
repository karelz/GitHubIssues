using System;
using System.Linq;
using System.Text.RegularExpressions;
using BugReport.DataModel;
using BugReport.Util;

namespace BugReport.Query
{
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

        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
        {
            return (e is ExpressionLabel) ?
                StringComparer.InvariantCultureIgnoreCase.Equals(_labelName, ((ExpressionLabel)e)._labelName) : 
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
            _labelRegex = new Regex("^" + labelPattern + "$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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

        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
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

        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
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
        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
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

        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
        {
            return (e is ExpressionMilestone) ?
                StringComparer.InvariantCultureIgnoreCase.Equals(_milestoneName, ((ExpressionMilestone)e)._milestoneName) :
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
            _milestoneRegex = new Regex("^" + milestonePattern + "$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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

        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
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
            return "assignee:" + _assigneeName;
        }

        internal override bool IsNormalized(NormalizedState minAllowedState)
        {
            return true;
        }

        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
        {
            return (e is ExpressionAssignee) ?
                StringComparer.InvariantCultureIgnoreCase.Equals(_assigneeName, ((ExpressionAssignee)e)._assigneeName) :
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

        protected override Expression GetSimplified()
        {
            return this;
        }

        public override bool Equals(Expression e)
        {
            // Only 2 static instances exist (True & False), so it is sufficient to compare object references
            return (this == e);
        }
    }
}
