using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BugReport.Reports
{
    public class Util
    {
        public static string Format(DateTimeOffset? dateNullable)
        {
            if (!dateNullable.HasValue)
            {
                return "";
            }
            DateTimeOffset date = dateNullable.Value;
            TimeSpan diff = DateTime.UtcNow.Subtract(date.UtcDateTime);
            if (diff.TotalDays < 1)
            {
                if (diff.TotalHours < 1)
                {
                    return String.Format("{0} minutes ago", 1 + (int)diff.TotalMinutes);
                }
                return String.Format("{0} hours ago", (int)diff.TotalHours);
            }
            return String.Format("{0} days ago", (int)diff.TotalDays);
        }
    }
}
