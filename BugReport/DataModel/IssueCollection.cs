using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using BugReport.Util;
using BugReport.Reports;

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
            Config config,
            IssueKindFlags issueKind = IssueKindFlags.All)
        {
            return LoadIssues(fileName.ToEnumerable(), config, issueKind);
        }

        public static IEnumerable<DataModelIssue> LoadIssues(
            IEnumerable<string> fileNames,
            Config config,
            IssueKindFlags issueKind = IssueKindFlags.All)
        {
            IEnumerable<DataModelIssue> issues = new DataModelIssue[] {};

            if (fileNames == null)
            {
                return issues;
            }

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
            
            // Process label/milestone aliases before repo filtering - its query might rely on the aliases
            foreach (DataModelIssue issue in issues)
            {
                issue.Labels = ApplyLabelAliases(issue.Labels, config.LabelAliases).ToArray();
                ApplyMilestoneAliases(issue.Milestone, config.MilestoneAliases);
            }
            
            // Process repo filters after label aliases, the filter query might depend on them
            foreach (Repository repo in Repository.Repositories)
            {
                issues = repo.Filter(issues);
            }

            return issues.ToArray();
        }

        private static IEnumerable<Label> ApplyLabelAliases(
            IEnumerable<Label> labels, 
            IDictionary<string, Label> labelAliases)
        {
            foreach (Label label in labels)
            {
                if (labelAliases.TryGetValue(label.Name, out Label newLabel))
                {
                    yield return newLabel;
                }
                else
                {
                    yield return label;
                }
            }
        }

        private static void ApplyMilestoneAliases(
            Milestone milestone,
            IDictionary<string, string> milestoneAliases)
        {
            if ((milestone != null) && 
                milestoneAliases.TryGetValue(milestone.Title, out string newMilestoneTitle))
            {
                milestone.Title = newMilestoneTitle;
            }
        }
    }
}
