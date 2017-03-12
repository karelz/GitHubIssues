using System;
using System.Collections.Generic;
using System.Linq;

namespace BugReport.Util
{
    public static class Extensions
    {
        public static bool EqualsIgnoreCase(this string str1, string str2)
        {
            return str1.Equals(str2, StringComparison.OrdinalIgnoreCase);
        }
        public static bool ContainsIgnoreCase(this IEnumerable<string> strs, string str)
        {
            return strs.Where(s => s.EqualsIgnoreCase(str)).Any();
        }
    }
}
