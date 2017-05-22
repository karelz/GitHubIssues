using System;
using System.Collections.Generic;
using System.Linq;

namespace GitHubBugReport.Core.Util
{
    public static class Extensions
    {
        public static bool ContainsIgnoreCase(this IEnumerable<string> strs, string str)
        {
            return strs.Any(s => s.Equals(str, StringComparison.InvariantCultureIgnoreCase));
        }

        public static bool None<T>(this IEnumerable<T> items)
        {
            return !items.Any();
        }

        public static IEnumerable<T> ToEnumerable<T>(this T value)
        {
            yield return value;
        }
    }
}
