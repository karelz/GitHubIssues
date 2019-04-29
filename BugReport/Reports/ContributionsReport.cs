using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BugReport.Util;
using BugReport.DataModel;
using BugReport.Query;

namespace BugReport.Reports
{
    public class ContributionsReport
    {
        private Config _config;

        public readonly IEnumerable<string> InputFiles;

        public readonly IEnumerable<DataModelIssue> Issues;

        public ContributionsReport(
            IEnumerable<string> configFiles, 
            IEnumerable<string> inputFiles)
        {
            _config = new Config(configFiles);

            InputFiles = inputFiles;

            IEnumerable<DataModelIssue> issues = IssueCollection.LoadIssues(inputFiles, _config);
        }
    }
}
