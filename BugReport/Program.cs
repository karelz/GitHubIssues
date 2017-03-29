using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BugReport.CommandLine;
using BugReport.DataModel;
using BugReport.Reports;
using BugReport.Reports.EmailReports;
using BugReport.Util;

class Program
{
    enum ErrorCode
    {
        Success = 0,
        InvalidCommand = -1,
        EmailSendFailure = -50,
        CatastrophicFailure = -100
    }

    enum ActionCommand
    {
        cache,
        report,
        query,
        alerts,
        history,
        untriaged,
        needsResponse
    }

    static readonly IEnumerable<string> _helpActions = new List<string>() { "?", "help", "h" };
    // Prefixed with option prefix /, -, or --, see code:Parser._optionPrefixes
    static readonly IEnumerable<string> _helpOptions = new List<string>() { "?", "help", "h" };

    static readonly OptionMultipleValues _configOption = new OptionMultipleValues("config");
    static readonly OptionSingleValue _prefixOption = new OptionSingleValue("prefix");
    static readonly OptionSingleValue _authenticationTokenOption = new OptionSingleValue("authToken", "authenticationToken", "gitHubToken", "token");
    static readonly OptionSingleValue _commentsPrefixOption = new OptionSingleValue("comments_prefix");
    static readonly OptionMultipleValues _inputOption = new OptionMultipleValues("in", "input");
    static readonly OptionSingleValue _outputOption = new OptionSingleValue("out", "output");
    static readonly OptionSingleValue _outputCsvOption = new OptionSingleValue("out_csv");
    static readonly OptionSingleValue _nameOption = new OptionSingleValue("name");
    static readonly OptionMultipleValues _beginOption = new OptionMultipleValues("begin");
    static readonly OptionMultipleValues _middleOption = new OptionMultipleValues("middle");
    static readonly OptionMultipleValues _endOption = new OptionMultipleValues("end");
    static readonly OptionSingleValue _templateOption = new OptionSingleValue("template");
    static readonly OptionMultipleValues _filterOption = new OptionMultipleValues("filter");
    static readonly OptionWithoutValue _skipEmailOption = new OptionWithoutValue("skipEmail", "noEmail", "skipMail", "noMail");
    static readonly OptionMultipleValues _commentsOption = new OptionMultipleValues("comments");

    static void PrintUsage()
    {
        Console.WriteLine(
@"Usage:
  cache /config <.xml> /prefix <name> [/comments_prefix <comments>] [/authToken <token>]
    * Will cache all GitHub issues into file <name>YYYY-MM-DD@HH-MM.json
    * If /comments is set, will also cache all GitHub comments into file <comments>YYY-MM-DD@HH-MM.json
  report [/begin <issues1.json> [...]] /end <issues1_end.json> [...] [/out <.html>] [/out_csv <file_prefix>] 
        [/name <report_name>] [/middle <issues.json> [...]] /config <.xml>
    * Creates report with alerts/areas as rows and queries as columns from cached .json file
  query /in <issues.json> [...] /out <.html> /config <.xml>
    * Creates query report (list of issues) from cached .json file
  alerts /begin <issues1.json> [...] /end <issues1_end.json> [...] /template <.html> /config <.xml> 
        [/filter <alert_name> [...]] [/skipEmail] [/out <out.html>]
    * Sends alert emails based on config.xml, optinally filtered to just alert_name
  history /in <summary.csv> [...] /out <.xlsx>
    * Creates history report from .csv files (produced by report command above)
  untriaged /in <issues.json> [...] /template <.html> /config <.xml> 
        [/filter <alert_name> [...]] [/skipEmail] [/out <out.html>]
    * Sends alert emails based on config.xml, optinally filtered to just alert_name
  needsResponse /in <issues.json> [...] /comments <.json> [...] /template <.html> /config <.xml> 
        [/fitler:<alert_name>] [/skipEmail] [/out <out.html>]
    * Sends digest emails based on config.xml, optinally filtered to just alert_name");
    }

    static void ReportError(string error)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();

        PrintUsage();
    }

    static int Main(string[] args) => (int)Main_Internal(args);

    static ErrorCode Main_Internal(string[] args)
    {
        try
        {
            if ((args.Length == 0)
                || Parser.IsOption(args[0], _helpOptions)
                || _helpActions.ContainsIgnoreCase(args[0]))
            {   // Print help
                PrintUsage();
                return (int)ErrorCode.Success;
            }

            // Parse first 'action' argument
            string actionArg = args[0];
            ActionCommand action = 0;
            {
                bool isValidActionArg = false;
                foreach (ActionCommand actionCommand in Enum.GetValues(typeof(ActionCommand)))
                {
                    if (actionArg.Equals(actionCommand.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        action = actionCommand;
                        isValidActionArg = true;
                        break;
                    }
                }
                if (!isValidActionArg)
                {
                    ReportError($"Error: unrecognized action '{actionArg}' - command line argument #1");
                    return ErrorCode.InvalidCommand;
                }
            }

            Parser optionsParser = new Parser(
                args.Skip(1).ToArray(),
                PrintUsage);

            switch (action)
            {
                case ActionCommand.cache:
                    {
                        if (!optionsParser.Parse(
                            new Option[] { _configOption, _prefixOption },
                            new Option[] { _authenticationTokenOption, _commentsPrefixOption }))
                        {
                            return ErrorCode.InvalidCommand;
                        }
                        IEnumerable<string> configFiles = _configOption.GetValues(optionsParser);
                        string filePrefix = _prefixOption.GetValue(optionsParser);
                        // Optional args
                        string authenticationToken = _authenticationTokenOption.GetValue(optionsParser);
                        string commentsFilePrefix = _commentsPrefixOption.GetValue(optionsParser);

                        return CacheGitHubIssues(configFiles, filePrefix, commentsFilePrefix, authenticationToken);
                    }
                case ActionCommand.query:
                    {
                        if (!optionsParser.Parse(
                            new Option[] { _configOption, _inputOption, _outputOption },
                            Option.EmptyList))
                        {
                            return ErrorCode.InvalidCommand;
                        }
                        IEnumerable<string> configFiles = _configOption.GetValues(optionsParser);
                        IEnumerable<string> inputFiles = _inputOption.GetValues(optionsParser);
                        string outputFile = _outputOption.GetValue(optionsParser);

                        Config config = new Config(configFiles);
                        QueryReport report = new QueryReport(config);
                        report.Write(IssueCollection.LoadIssues(inputFiles, config), outputFile);
                        return ErrorCode.Success;
                    }
                case ActionCommand.report:
                    {
                        if (!optionsParser.Parse(
                            new Option[] { _configOption, _endOption },
                            new Option[] { _beginOption, _middleOption, _outputOption, _outputCsvOption, _nameOption }))
                        {
                            return ErrorCode.InvalidCommand;
                        }
                        IEnumerable<string> configFiles = _configOption.GetValues(optionsParser);
                        IEnumerable<string> beginFiles = _beginOption.GetValues(optionsParser);
                        IEnumerable<string> middleFiles = _middleOption.GetValues(optionsParser);
                        IEnumerable<string> endFiles = _endOption.GetValues(optionsParser);

                        string outputFile = _outputOption.GetValue(optionsParser);
                        string csvFileNamePrefix = _outputCsvOption.GetValue(optionsParser);
                        string reportName = _nameOption.GetValue(optionsParser);

                        if ((outputFile == null) && (csvFileNamePrefix == null))
                        {
                            optionsParser.ReportError("Required at least one option: '/out' or '/out_csv'.");
                            return ErrorCode.InvalidCommand;
                        }
                        if ((csvFileNamePrefix != null) && 
                            csvFileNamePrefix.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            optionsParser.ReportError($"Option '/out_csv' takes file name prefix, not file name {csvFileNamePrefix}.");
                            return ErrorCode.InvalidCommand;
                        }

                        TableReport report = new TableReport(configFiles, beginFiles, middleFiles, endFiles);
                        if (outputFile != null)
                        {
                            HtmlTableReport.Write(report, outputFile, reportName);
                        }
                        // Note we can have both options
                        if (csvFileNamePrefix != null)
                        {
                            CsvTableReport.Write(report, csvFileNamePrefix, reportName);
                        }
                        return ErrorCode.Success;
                    }
                case ActionCommand.alerts:
                    {
                        if (!optionsParser.Parse(
                            new Option[] { _configOption, _beginOption, _endOption, _templateOption },
                            new Option[] { _filterOption, _skipEmailOption, _outputOption }))
                        {
                            return ErrorCode.InvalidCommand;
                        }
                        IEnumerable<string> configFiles = _configOption.GetValues(optionsParser);
                        IEnumerable<string> beginFiles = _beginOption.GetValues(optionsParser);
                        IEnumerable<string> endFiles = _endOption.GetValues(optionsParser);
                        string templateFile = _templateOption.GetValue(optionsParser);
                        // Optional args
                        IEnumerable<string> alertFilters = _filterOption.GetValues(optionsParser);
                        bool skipEmail = _skipEmailOption.IsDefined(optionsParser);
                        string outputFile = _outputOption.GetValue(optionsParser);

                        Config config = new Config(configFiles);
                        IEnumerable<DataModelIssue> beginIssues = IssueCollection.LoadIssues(
                            beginFiles, 
                            config, 
                            IssueKindFlags.Issue | IssueKindFlags.PullRequest);
                        IEnumerable<DataModelIssue> endIssues = IssueCollection.LoadIssues(
                            endFiles, 
                            config, 
                            IssueKindFlags.Issue | IssueKindFlags.PullRequest);

                        return GetSendEmailErrorCode(AlertReport_Diff.SendEmails(
                            config,
                            templateFile,
                            skipEmail,
                            outputFile,
                            alertFilters,
                            beginIssues,
                            endIssues));
                    }
                case ActionCommand.history:
                    {
                        if (!optionsParser.Parse(
                            new Option[] { _inputOption, _outputOption },
                            Option.EmptyList))
                        {
                            return ErrorCode.InvalidCommand;
                        }
                        IEnumerable<string> inputFiles = _inputOption.GetValues(optionsParser);
                        string outputFile = _outputOption.GetValue(optionsParser);

                        HistoryReport.Create(inputFiles, outputFile);
                        return ErrorCode.Success;
                    }
                case ActionCommand.untriaged:
                    {
                        if (!optionsParser.Parse(
                            new Option[] { _configOption, _inputOption, _templateOption },
                            new Option[] { _filterOption, _skipEmailOption, _outputOption }))
                        {
                            return ErrorCode.InvalidCommand;
                        }
                        IEnumerable<string> configFiles = _configOption.GetValues(optionsParser);
                        IEnumerable<string> inputFiles = _inputOption.GetValues(optionsParser);
                        string templateFile = _templateOption.GetValue(optionsParser);
                        // Optional args
                        IEnumerable<string> alertFilters = _filterOption.GetValues(optionsParser);
                        bool skipEmail = _skipEmailOption.IsDefined(optionsParser);
                        string outputFile = _outputOption.GetValue(optionsParser);

                        Config config = new Config(configFiles);
                        IEnumerable<DataModelIssue> issues = IssueCollection.LoadIssues(
                            inputFiles, 
                            config, 
                            IssueKindFlags.Issue);

                        return GetSendEmailErrorCode(AlertReport_Untriaged.SendEmails(
                            config,
                            templateFile,
                            skipEmail,
                            outputFile,
                            alertFilters,
                            issues));
                    }
                case ActionCommand.needsResponse:
                    {
                        if (!optionsParser.Parse(
                            new Option[] { _configOption, _inputOption, _commentsOption, _templateOption },
                            new Option[] { _filterOption, _skipEmailOption, _outputOption }))
                        {
                            return ErrorCode.InvalidCommand;
                        }
                        IEnumerable<string> configFiles = _configOption.GetValues(optionsParser);
                        IEnumerable<string> inputFiles = _inputOption.GetValues(optionsParser);
                        IEnumerable<string> commentsFiles = _commentsOption.GetValues(optionsParser);
                        string templateFile = _templateOption.GetValue(optionsParser);
                        // Optional args
                        IEnumerable<string> alertFilters = _filterOption.GetValues(optionsParser);
                        bool skipEmail = _skipEmailOption.IsDefined(optionsParser);
                        string outputFile = _outputOption.GetValue(optionsParser);

                        Config config = new Config(configFiles);
                        IEnumerable<DataModelIssue> issues = IssueCollection.LoadIssues(
                            inputFiles, 
                            config, 
                            IssueKindFlags.Issue);
                        IEnumerable<DataModelIssue> comments = IssueCollection.LoadIssues(
                            commentsFiles, 
                            config, 
                            IssueKindFlags.Comment);

                        return GetSendEmailErrorCode(AlertReport_NeedsResponse.SendEmails(
                            config,
                            templateFile,
                            skipEmail,
                            outputFile,
                            alertFilters,
                            issues,
                            comments));
                    }
                default:
                    Debug.Assert(false);
                    return ErrorCode.CatastrophicFailure;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine();
            Console.Error.WriteLine();
            Console.Error.WriteLine("Catastrophic failure:");
            Console.Error.WriteLine(ex);
            return ErrorCode.CatastrophicFailure;
        }
    }

    private static ErrorCode GetSendEmailErrorCode(bool isAllEmailSendSuccessful)
    {
        return isAllEmailSendSuccessful ? ErrorCode.Success : ErrorCode.EmailSendFailure;
    }

    private static ErrorCode CacheGitHubIssues(
        IEnumerable<string> configFiles, 
        string prefix, 
        string commentsPrefix, 
        string authenticationToken)
    {
        Config config = new Config(configFiles);
        if (config.Repositories.Count() != 1)
        {
            if (config.Repositories.Count() == 0)
            {
                ReportError("No repository definition found in config file(s).");
                return ErrorCode.InvalidCommand;
            }
            else
            {
                ReportError("Multiple repositories found in config file(s).");
                return ErrorCode.InvalidCommand;
            }
        }

        Repository repo = config.Repositories.First();
        repo.AuthenticationToken = authenticationToken;

        DateTime currentTime = DateTime.Now;
        repo.LoadIssues();
        repo.SerializeToFile(
            string.Format("{0}{1:yyyy-MM-dd@HH-mm}.json", prefix, currentTime), 
            repo.Issues);

        if (commentsPrefix != null)
        {
            repo.LoadIssueComments();
            repo.SerializeToFile(
                string.Format("{0}{1:yyyy-MM-dd@HH-mm}.json", commentsPrefix, currentTime), 
                repo.IssueComments);
        }

        return ErrorCode.Success;
    }
}
