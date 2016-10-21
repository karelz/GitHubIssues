using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BugReport.DataModel
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
        public bool HasLabel(string labelName)
        {
            return LabelsMap.ContainsKey(labelName);
        }

        public bool HasUser(string userName)
        {
            // TODO
            return true;
        }

        Dictionary<string, Milestone> MilestonesMap;
        public bool HasMilestone(string milestoneName)
        {
            return MilestonesMap.ContainsKey(milestoneName);
        }

        public IssueCollection(IEnumerable<Issue> issues)
        {
            Issues = issues;

            LabelsMap = new Dictionary<string, Label>();
            MilestonesMap = new Dictionary<string, Milestone>();

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
                    }
                }

                if (issue.Milestone != null)
                {
                    string milestoneName = issue.Milestone.Title;
                    if (MilestonesMap.ContainsKey(milestoneName))
                    {
                        // Milestone names should be unique - if not, we need to use Number as unique identifier
                        Debug.Assert(issue.Milestone.Number == MilestonesMap[milestoneName].Number);
                        issue.Milestone = MilestonesMap[milestoneName];
                    }
                    else
                    {
                        MilestonesMap[milestoneName] = issue.Milestone;
                    }
                }
            }
        }

        public static IssueCollection LoadFrom(string fileName, IssueKindFlags issueKind = IssueKindFlags.All)
        {
            JsonSerializer serializer = new JsonSerializer();
            using (StreamReader sr = new StreamReader(fileName))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                return new IssueCollection(serializer.Deserialize<List<Issue>>(reader).Where(i => i.IsIssueKind(issueKind)));
            }
        }
    }
}
