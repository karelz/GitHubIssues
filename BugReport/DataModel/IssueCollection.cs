using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BugReport
{
    public class IssueCollection
    {
        public IEnumerable<Issue> Issues { get; private set; }

        public Issue GetIssue(int id)
        {
            foreach (Issue issue in Issues)
            {
                if (issue.Number == id)
                {
                    return issue;
                }
            }
            return null;
        }

        public bool HasIssue(int id)
        {
            return GetIssue(id) != null;
        }

        Dictionary<string, Label> LabelsMap;
        public IEnumerable<Label> Labels
        {
            get { return LabelsMap.Values; }
        }
        public Label GetLabel(string name)
        {
            Label label;
            if (LabelsMap.TryGetValue(name, out label))
            {
                return label;
            }
            return null;
        }

        public IEnumerable<Label> AreaLabels
        {
            get
            {
                foreach (Label label in Labels)
                {
                    if (label.Name.StartsWith("area-") || (label.Name == "Infrastructure"))
                    {
                        yield return label;
                    }
                }
            }
        }

        public IEnumerable<Milestone> Milestones
        {
            get { return MilestonesMap.Values; }
        }

        Dictionary<int, Milestone> MilestonesMap;

        public IssueCollection(IEnumerable<Issue> issues)
        {
            Issues = issues;

            LabelsMap = new Dictionary<string, Label>();
            MilestonesMap = new Dictionary<int, Milestone>();

            foreach (Issue issue in issues)
            {
                for (int i = 0; i < issue.Labels.Length; i++)
                {
                    string labelName = issue.Labels[i].Name;
                    if (LabelsMap.ContainsKey(labelName))
                    {
                        issue.Labels[i] = LabelsMap[labelName];
                    }
                    else
                    {
                        LabelsMap[labelName] = issue.Labels[i];
                        Debug.Assert(issue.Labels[i].Issues == null);
                        LabelsMap[labelName].Issues = new List<Issue>();
                    }
                    issue.Labels[i].Issues.Add(issue);
                }

                if (issue.Milestone != null)
                {
                    int milestoneNumber = issue.Milestone.Number;
                    if (MilestonesMap.ContainsKey(milestoneNumber))
                    {
                        issue.Milestone = MilestonesMap[milestoneNumber];
                    }
                    else
                    {
                        MilestonesMap[milestoneNumber] = issue.Milestone;
                    }
                }
            }
        }

        public class FilteredLabel
        {
            public Label Label { get; private set; }
            public List<Issue> Issues { get; private set; }

            public FilteredLabel(Label label)
            {
                Label = label;
                Issues = new List<Issue>();
            }
        }

        public static IEnumerable<FilteredLabel> FilterLabels(IEnumerable<Issue> issues)
        {
            Dictionary<Label, FilteredLabel> map = new Dictionary<Label, FilteredLabel>();

            foreach (Issue issue in issues)
            {
                foreach (Label label in issue.Labels)
                {
                    if (!map.ContainsKey(label))
                    {
                        map[label] = new FilteredLabel(label);
                    }
                    map[label].Issues.Add(issue);
                }
            }

            return map.Values;
        }
    }
}
