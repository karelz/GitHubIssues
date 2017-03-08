using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BugReport.Query;
using BugReport.DataModel;
using BugReport.Reports;

class Program
{
    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  cache <config.xml> [<GithubToken>]- will cache all GitHub issues into file Issues_YYY-MM-DD@HH-MM.json");
        Console.WriteLine("  cacheWithComments <alerts.xml> [<GithubToken>] - will cache all GitHub issues into file Issues_YYY-MM-DD@HH-MM.json and Github comments into file Comments_YYY-MM-DD@HH-MM.json");
        Console.WriteLine("  report <input.json> <output.html> <config.xml> - Creates report of GitHub issues from cached .json file");
        Console.WriteLine("  alerts <input1.json> <input2.json> <emailTemplate.html> <config.xml> [<alert_name>] - Sends alert emails based on .xml config, optinally filtered to just alert_name");
        Console.WriteLine("      alerts_SkipEmail or set SEND_EMAIL=0 - Won't send any emails");
        Console.WriteLine("  untriaged <issues.json> <emailTemplate.html> <config.xml> [<alert_name>] - Sends alert emails based on .xml config, optinally filtered to just alert_name");
        Console.WriteLine("      untriaged_SkipEmail or set SEND_EMAIL=0 - Won't send any emails");
        Console.WriteLine("  needsMSResponse <issues.json> <comments.json> <emailTemplate.html> <config.xml> [<alert_name>] - Sends digest emails based on .xml config, optinally filtered to just alert_name");
        Console.WriteLine("     needsMSResponse_SkipEmail or set SEND_EMAIL=0 - Won't send any emails");
    }

    enum ErrorCode
    {
        Success = 0,
        InvalidCommand = -1,
        EmailSendFailure = -50,
        CatastrophicFailure = -100
    }

    static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 1)
            {
                if ((args[0].Equals("cache", StringComparison.OrdinalIgnoreCase) ||
                    args[0].Equals("cacheWithComments", StringComparison.OrdinalIgnoreCase))
                    && (args.Length == 2 || args.Length == 3))
                {
                    if (args.Length == 2)
                        CacheGitHubIssues(args[1], authenticationToken: null, includeComments: args[0].Equals("cacheWithComments"));
                    else
                        CacheGitHubIssues(args[1], authenticationToken: args[2], includeComments: args[0].Equals("cacheWithComments"));

                    return (int)ErrorCode.Success;
                }
                else if (args[0].Equals("report", StringComparison.OrdinalIgnoreCase) && (args.Length == 4))
                {
                    HtmlReport(args[1], args[2], args[3]);
                    return (int)ErrorCode.Success;
                }
                else if ((args[0].Equals("alerts", StringComparison.OrdinalIgnoreCase)
                     || args[0].Equals("alerts_SkipEmail", StringComparison.OrdinalIgnoreCase))
                     && ((args.Length == 5) || (args.Length == 6)))
                {
                    bool skipEmail = args[0].Equals("alerts_SkipEmail", StringComparison.OrdinalIgnoreCase);
                    string alertName = (args.Length == 6) ? args[5] : null;
                    bool isAllEmailSendSuccessful = SendAlerts_Diff(args[1], args[2], args[3], args[4], alertName, skipEmail);
                    return (int)(isAllEmailSendSuccessful ? ErrorCode.Success : ErrorCode.EmailSendFailure);
                }
                else if ((args[0].Equals("untriaged", StringComparison.OrdinalIgnoreCase)
                     || args[0].Equals("untriaged_SkipEmail", StringComparison.OrdinalIgnoreCase))
                     && ((args.Length == 4) || (args.Length == 5)))
                {
                    bool skipEmail = args[0].Equals("untriaged_SkipEmail", StringComparison.OrdinalIgnoreCase);
                    string alertName = (args.Length == 5) ? args[4] : null;
                    bool isAllEmailSendSuccessful = SendAlerts_Untriaged(args[1], args[2], args[3], alertName, skipEmail);
                    return (int)(isAllEmailSendSuccessful ? ErrorCode.Success : ErrorCode.EmailSendFailure);
                }
                else if ((args[0].Equals("needsMSResponse", StringComparison.OrdinalIgnoreCase)
                     || args[0].Equals("needsMSResponse_SkipEmail", StringComparison.OrdinalIgnoreCase))
                     && ((args.Length == 5) || (args.Length == 6)))
                {
                    bool skipEmail = args[0].Equals("needsMSResponse_SkipEmail", StringComparison.OrdinalIgnoreCase);
                    string alertName = (args.Length == 6) ? args[5] : null;
                    bool isAllEmailSendSuccessful = SendAlerts_NeedsMSResponse(args[1], args[2], args[3], args[4], alertName, skipEmail);
                    return (int)(isAllEmailSendSuccessful ? ErrorCode.Success : ErrorCode.EmailSendFailure);
                }
            }
            PrintUsage();
            return (int)ErrorCode.InvalidCommand;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Catastrophic failure:");
            Console.WriteLine(ex);
            return (int)ErrorCode.CatastrophicFailure;
        }
    }

    static void CacheGitHubIssues(string configFileName, string authenticationToken, bool includeComments)
    {
        Repository repo = new Repository(configFileName);
        DateTime currentTime = DateTime.Now;
        repo.AuthenticationToken = authenticationToken;
        repo.LoadIssues();
        repo.SerializeToFile(string.Format("Issues_{0:yyyy-MM-dd@HH-mm}.json", currentTime), repo.Issues);
        if (includeComments)
        {
            repo.LoadIssueComments();
            repo.SerializeToFile(string.Format("Comments_{0:yyyy-MM-dd@HH-mm}.json", currentTime), repo.IssueComments);
        }
    }

    static void HtmlReport(string inputJsonFileName, string outputHtmlFileName, string configFileName)
    {
        HtmlReport report = new HtmlReport(configFileName);
        report.Write(IssueCollection.LoadFrom(inputJsonFileName), outputHtmlFileName);
    }

    // Returns false if any of the emails failed to be sent
    static bool SendAlerts_Diff(string input1JsonFileName, string input2JsonFileName, string htmlTemplateFileName, string configFileName, string alertName, bool skipEmail)
    {
        AlertReporting report = new AlertReporting(configFileName, skipEmail, htmlTemplateFileName, AlertType.Diff);
        IssueCollection collection1 = IssueCollection.LoadFrom(input1JsonFileName, issueKind: IssueKindFlags.Issue);
        IssueCollection collection2 = IssueCollection.LoadFrom(input2JsonFileName, issueKind: IssueKindFlags.Issue);
        return report.SendEmails(collection1, collection2, alertName);
    }

    // Returns false if any of the emails failed to be sent
    static bool SendAlerts_Untriaged(string inputJsonFileName, string htmlTemplateFileName, string configFileName, string alertName, bool skipEmail)
    {
        AlertReporting report = new AlertReporting(configFileName, skipEmail, htmlTemplateFileName, AlertType.Untriaged);
        IssueCollection collection1 = IssueCollection.LoadFrom(inputJsonFileName, issueKind: IssueKindFlags.Issue);
        return report.SendEmails(collection1, null, alertName);
    }

    // Returns false if any of the emails failed to be sent
    static bool SendAlerts_NeedsMSResponse(string issueJsonFile, string commentJsonFile, string htmlTemplateFileName, string configFileName, string alertName, bool skipEmail)
    {
        AlertReporting report = new AlertReporting(configFileName, skipEmail, htmlTemplateFileName, AlertType.NeedsMSResponse);
        IssueCollection collection1 = IssueCollection.LoadFrom(issueJsonFile, issueKind: IssueKindFlags.Issue);
        IssueCollection collection2 = IssueCollection.LoadFrom(commentJsonFile, issueKind: IssueKindFlags.Comment);
        return report.SendEmails(collection1, collection2, alertName);
    }
}
