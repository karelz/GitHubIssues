using System;

namespace GitHubBugReport.Core.Issues.Models
{
    public class Label
    {
        public string Name;

        public Label(string name)
        {
            if (String.IsNullOrEmpty(name)) { throw new ArgumentNullException(nameof(name)); }

            Name = name;
        }

        public bool Equals(string name)
        {
            return NameEqualityComparer.Equals(Name, name);
        }

        public static StringComparer NameEqualityComparer = StringComparer.InvariantCultureIgnoreCase;
    }
}
