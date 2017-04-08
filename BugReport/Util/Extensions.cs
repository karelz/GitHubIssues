using System;
using System.Collections.Generic;
using System.Linq;

namespace BugReport.Util
{
    public static class Extensions
    {
        public static bool ContainsIgnoreCase(this IEnumerable<string> strs, string str)
        {
            return strs.Where(s => s.Equals(str, StringComparison.InvariantCultureIgnoreCase)).Any();
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
