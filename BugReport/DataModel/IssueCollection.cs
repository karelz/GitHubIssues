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
        private Dictionary<string, Label> LabelsMap;
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

        private Dictionary<string, Milestone> MilestonesMap;
        public bool HasMilestone(string milestoneName)
        {
            return MilestonesMap.ContainsKey(milestoneName);
        }

        public IssueCollection(IEnumerable<DataModelIssue> issues)
        {
            LabelsMap = new Dictionary<string, Label>();
            MilestonesMap = new Dictionary<string, Milestone>();

            foreach (DataModelIssue issue in issues)
            {
                if (issue.Labels != null)
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

        public static IEnumerable<DataModelIssue> LoadIssues(
            string fileName,
            IssueKindFlags issueKind = IssueKindFlags.All)
        {
            return LoadIssues(new string[] { fileName }, issueKind);
        }

        public static IEnumerable<DataModelIssue> LoadIssues(
            IEnumerable<string> fileNames, 
            IssueKindFlags issueKind = IssueKindFlags.All)
        {
            IEnumerable<DataModelIssue> issues = new DataModelIssue[] {};
            foreach (string fileName in fileNames)
            {
                JsonSerializer serializer = new JsonSerializer();
                using (StreamReader sr = new StreamReader(fileName))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    issues = issues.Concat(serializer.Deserialize<List<DataModelIssue>>(reader)
                                                        .Where(i => i.IsIssueKind(issueKind)));
                }
            }
            return issues;
        }
    }
}
