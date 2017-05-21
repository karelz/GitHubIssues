using System;

namespace BugReport.Query
{
    public class InvalidQueryException : Exception
    {
        public InvalidQueryException(string message, string queryString, int position)
            : base($"{message} at position {position}")
        {
        }
    }
}
