using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class DiffReport
    {
        IssueCollection Issues1;
        IssueCollection Issues2;

        public DiffReport(IssueCollection issues1, IssueCollection issues2)
        {
            Issues1 = issues1;
            Issues2 = issues2;
        }

        public void Report(string configJson, string outputHtml)
        {
            Report("area-Meta");
            Console.WriteLine("===========================================");
            Report("area-System.Globalization");
            Console.WriteLine("===========================================");
            Report("area-System.Collections");
            Console.WriteLine("===========================================");
            Report("netstandard2.0");
        }

        public void Report(string areaLabelName)
        {
            Console.WriteLine("{0}:", areaLabelName);
            Report(CreateQuery(areaLabelName));
        }

        void Report(IssueQuery query)
        {
            IEnumerable<Issue> query1 = Issues1.Issues.Where(i => query(i));
            IEnumerable<Issue> query2 = Issues2.Issues.Where(i => query(i));

            Console.WriteLine("Created:");
            ReportIssues(query2.Except(Issues1));
            Console.WriteLine("New in query:");
            ReportIssues(query2.Intersect(Issues1).ExceptByNumber(query1));
            Console.WriteLine("Not in query:");
            ReportIssues(query1.Intersect(Issues2).ExceptByNumber(query2));
            Console.WriteLine("Closed:");
            ReportIssues(query1.Except(Issues2));
        }

        void ReportIssues(IEnumerable<Issue> issues)
        {
            foreach (Issue issue in issues)
            {
                Console.WriteLine("  {0} - {1}", issue.Number, issue.Title);
                Issue issue1 = Issues1.GetIssue(issue.Number);
                Issue issue2 = Issues2.GetIssue(issue.Number);

                if ((issue1 != null) && (issue2 != null))
                {
                    ReportIssue_LabelsDiff(issue1, Issues1.GetAreaLabels(), issue2, Issues2.GetAreaLabels());
                }
                else if (issue1 == null)
                {
                    Console.WriteLine("      Labels: {0}", GetLabelsText(issue2.Labels, "<none>"));
                    Console.WriteLine("      Created: {0}", Util.Format(issue2.CreatedAt));
                }
            }
        }

        void ReportIssue_LabelsDiff(Issue issue1, IEnumerable<Label> areaLabels1, Issue issue2, IEnumerable<Label> areaLabels2)
        {
            IEnumerable<Label> labels1Only = issue1.Labels.ExceptByName(issue2.Labels);
            IEnumerable<Label> labels2Only = issue2.Labels.ExceptByName(issue1.Labels);

            Console.WriteLine("      Area: {0} -> {1}", 
                GetLabelsText(labels1Only.Intersect(areaLabels1), "<none>"), 
                GetLabelsText(labels2Only.Intersect(areaLabels2)));
            if (labels1Only.Except(areaLabels1).Any() || labels2Only.Except(areaLabels2).Any())
            {
                Console.WriteLine("      Labels: {0} -> {1}",
                    GetLabelsText(labels1Only.Except(areaLabels1), "<none>"),
                    GetLabelsText(labels2Only.Except(areaLabels2), "<none>"));
            }
        }

        string GetLabelsText(IEnumerable<Label> labels, string textIfEmpty = "")
        {
            if (labels.Any())
            {
                return string.Join(", ", labels.Select(l => l.Name));
            }
            return textIfEmpty;
        }

        delegate bool IssueQuery(Issue issue);

        static IssueQuery CreateQuery(string label)
        {
            return new IssueQuery(i => i.Labels.Where(l => l.Name == label).Any());
        }
    }
}
