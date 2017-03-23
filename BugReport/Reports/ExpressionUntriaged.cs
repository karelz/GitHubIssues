using System;
using System.Collections.Generic;
using System.Linq;
using BugReport.Query;
using BugReport.DataModel;
using System.Diagnostics;

namespace BugReport.Reports
{
    public class ExpressionUntriaged : Expression
    {
        private readonly IEnumerable<Label> _issueTypeLabels;
        private readonly IEnumerable<Label> _areaLabels;
        private readonly IEnumerable<Label> _untriagedLabels;

        public ExpressionUntriaged(
            IEnumerable<Label> issueTypeLabels, 
            IEnumerable<Label> areaLabels, 
            IEnumerable<Label> untriagedLabels)
        {
            _issueTypeLabels = issueTypeLabels;
            _areaLabels = areaLabels;
            _untriagedLabels = untriagedLabels;
        }

        [Flags]
        public enum Flags
        {
            UntriagedLabel = 0x1,
            MissingMilestone = 0x2,
            MissingAreaLabel = 0x4,
            MissingIssueTypeLabel = 0x8,
            MultipleIssueTypeLabels = 0x10,
            MultipleAreaLabels = 0x20
        }

        public static IEnumerable<Flags> EnumerateFlags(Flags flags)
        {
            foreach (Flags flag in Enum.GetValues(typeof(Flags)))
            {
                if ((flags & flag) != 0)
                {
                    yield return flag;
                }
            }
        }

        public Flags GetUntriagedFlags(DataModelIssue issue)
        {
            Flags triageFlags = 0;

            // Check if this issue is marked as 'untriaged'
            if (issue.Labels.Intersect_ByName(_untriagedLabels).Any())
            {
                triageFlags |= Flags.UntriagedLabel;
            }

            // check if this issue has a Milestone
            if (issue.Milestone == null)
            {
                triageFlags |= Flags.MissingMilestone;
            }

            // Count area labels
            int areaLabelsCount = issue.Labels.Intersect_ByName(_areaLabels).Count();
            if (areaLabelsCount == 0)
            {
                triageFlags |= Flags.MissingAreaLabel;
            }
            else if (areaLabelsCount > 1)
            {
                triageFlags |= Flags.MultipleAreaLabels;
            }

            // Count issue labels
            int issueTypeLabelsCount = issue.Labels.Intersect_ByName(_issueTypeLabels).Count();
            if (issueTypeLabelsCount == 0)
            {
                triageFlags |= Flags.MissingIssueTypeLabel;
            }
            else if (issueTypeLabelsCount > 1)
            {
                triageFlags |= Flags.MultipleIssueTypeLabels;
            }

            return triageFlags;
        }

        public override bool Evaluate(DataModelIssue issue)
        {
            return GetUntriagedFlags(issue) != 0;
        }

        public override void Validate(IssueCollection collection)
        {
            // Nothing to validate
        }

        public override string ToString()
        {
            return "is:untriaged";
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

        protected override bool Equals(Expression e)
        {
            // ExpressionUntriaged is unique (only one instance created in config)
            // We might need to rethink it if we support instances per repo / per team in future
            return (this == e);
        }
    }
}
