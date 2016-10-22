using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

namespace BugReport.Reports
{
    public class LabelGroup
    {
        public string Name { get; private set; }
        public List<Label> Labels { get; private set; }

        public LabelGroup(string name)
        {
            Name = name;
            Labels = new List<Label>();
        }
        public LabelGroup(string name, IEnumerable<Label> labels)
        {
            Name = name;
            Labels = new List<Label>(labels);
        }
        public LabelGroup(Label label)
        {
            Name = label.Name;
            Labels = new List<Label>(1);
            Labels.Add(label);
        }

        public static void Add(List<LabelGroup> labelGroups, Label label)
        {
            if (label != null)
            {
                labelGroups.Add(new LabelGroup(label));
            }
        }

        public static LabelGroup Empty = new LabelGroup("<empty>", new Label[] { });
        public static IEnumerable<LabelGroup> EmptyList = new LabelGroup[] { };
    }
}
