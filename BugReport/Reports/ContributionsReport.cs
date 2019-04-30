using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BugReport.Util;
using BugReport.DataModel;
using BugReport.Query;
using System.Xml.Linq;

namespace BugReport.Reports
{
    public class ContributionsReport
    {
        private ContributionsConfig _config;

        public readonly IEnumerable<string> InputFiles;

        public readonly IEnumerable<DataModelIssue> Issues;

        public IEnumerable<Report> Reports => _config.Reports;

        public ContributionsReport(
            IEnumerable<string> configFiles,
            IEnumerable<string> inputFiles)
        {
            _config = new ContributionsConfig(configFiles);

            InputFiles = inputFiles;

            Issues = IssueCollection.LoadIssues(inputFiles, _config);
        }

        public class User
        {
            public string Name { get; private set; }
            public string Id { get; private set; }
            public Category Category { get; private set; }
            public string Subcategory { get; private set; }
            public Interval Interval { get; private set; }

            public User(string name, string id, Category category, string subcategory, Interval interval)
            {
                Name = name;
                Id = id;
                Category = category;
                Subcategory = subcategory;
                Interval = interval;
            }

            public bool DoesOverlap(User user)
            {
                if ((Name != user.Name) || (Id != user.Id))
                {
                    return false;
                }
                return Interval.DoesOverlap(user.Interval);
            }

            public bool IsAuthor(DataModelIssue issue)
            {
                if ((Name != issue.User.Login) && (Id != issue.User.Id))
                {
                    return false;
                }
                return Interval.Contains(issue.CreatedAt.Value);
            }
        }

        public class Category
        {
            public string Name { get; private set; }
            public IEnumerable<User> Users { get; private set; }

            public Category(string name, IEnumerable<User> users)
            {
                Name = name;
                Users = users;
            }

            public bool ContainsAuthor(DataModelIssue issue)
            {
                return Users.Where(user => user.IsAuthor(issue)).Any();
            }
        }

        public class Group
        {
            public string Name { get; private set; }
            public IEnumerable<Category> Categories { get; private set; }
            public Report Report { get; private set; }

            public Group(string name, IEnumerable<Category> categories, Report report)
            {
                Name = name;
                Categories = categories;
                Report = report;
            }

            public bool ContainsAuthor(DataModelIssue issue)
            {
                return Categories.Where(category => category.ContainsAuthor(issue)).Any();
            }
        }

        public class Report
        {
            public enum UnitKind
            {
                Day,
                Week,
                Month
            }

            public string Name { get; private set; }
            public DateTimeOffset StartTime { get; private set; }
            public DateTimeOffset StopTime { get; private set; }
            public UnitKind Unit { get; private set; }
            public IEnumerable<Group> Groups { get; private set; }
            public string DefaultGroupName { get; private set; }

            public Report(string name, DateTimeOffset startTime, DateTimeOffset? stopTime, UnitKind unit, IEnumerable<Group> groups, string defaultGroupName)
            {
                Name = name;
                StartTime = startTime;
                StopTime = (stopTime == null) ? DateTimeOffset.Now : stopTime.Value;
                Unit = unit;
                Groups = groups;
                DefaultGroupName = defaultGroupName;
            }

            public IEnumerable<Interval> EnumerateIntervals()
            {
                DateTimeOffset time = StartTime;
                while (time <= StopTime)
                {
                    DateTimeOffset oldTime = time;
                    switch (Unit)
                    {
                        case UnitKind.Day:
                            time = time.AddDays(1);
                            break;
                        case UnitKind.Week:
                            time = time.AddDays(7);
                            break;
                        case UnitKind.Month:
                            time = time.AddMonths(1);
                            break;
                    }
                    yield return new Interval(oldTime, time);
                }
            }

            /*
            public bool ContainsDefaultGroupAuthor(DataModelIssue issue)
            {
                return Groups.Where(group => group.ContainsAuthor(issue)).None();
            }
            */
        }

        public class Interval
        {
            public DateTimeOffset From { get; private set; }
            public DateTimeOffset To { get; private set; }

            public Interval(DateTimeOffset from, DateTimeOffset to)
            {
                From = from;
                To = to;
            }

            public bool Contains(DateTimeOffset time)
            {
                return (From <= time) && (time < To);
            }
            public bool DoesOverlap(Interval interval)
            {
                return Contains(interval.From) || Contains(interval.To) || interval.Contains(From) || interval.Contains(To);
            }

            public string GetLabel(bool includeDays)
            {
                if (includeDays)
                {
                    return $"{To.Year}/{To.Month}/{To.Day}";
                }
                else
                {
                    return $"{To.Year}/{To.Month}";
                }
            }
        }

        public class ContributionsConfig : Config
        {
            private List<User> _users = new List<User>();

            public IEnumerable<Category> Categories { get; private set; }
            public IEnumerable<Report> Reports { get; private set; }

            public ContributionsConfig(IEnumerable<string> configFiles) : base(configFiles)
            {
                Categories = LoadCategories();
                Reports = LoadReports();
            }

            private IEnumerable<Category> LoadCategories()
            {
                List<Category> categories = new List<Category>();

                foreach (ConfigFile configFile in _configFiles)
                {
                    foreach (XElement categoryNode in configFile.Root.Elements("category"))
                    {
                        string categoryName = categoryNode.Attribute("name").Value;
                        List<User> categoryUsers = new List<User>();
                        Category category = new Category(categoryName, categoryUsers);
                        categories.Add(category);

                        foreach (XElement userNode in categoryNode.Elements("user"))
                        {
                            string userName = userNode.Attribute("name")?.Value;
                            string id = userNode.Attribute("id")?.Value;
                            string subcategory = userNode.Attribute("subcategory")?.Value;
                            string toText = userNode.Attribute("to")?.Value;
                            string fromText = userNode.Attribute("from")?.Value;

                            if (userName == null && id == null)
                            {
                                throw new InvalidDataException($"User name or id has to be defined for user in category '{categoryName}'");
                            }
                            if (userName != null && id != null)
                            {
                                throw new InvalidDataException($"User name and id cannot be both defined '{userName}' / '{id}'");
                            }

                            DateTimeOffset from = DateTimeOffset.MinValue;
                            if (fromText != null)
                            {
                                if (!DateTimeOffset.TryParse(fromText, out from))
                                {
                                    throw new InvalidDataException($"User '{userName ?? id}' has invalid 'from' value '{fromText}'");
                                }
                            }
                            DateTimeOffset to = DateTimeOffset.MaxValue;
                            if (toText != null)
                            {
                                if (!DateTimeOffset.TryParse(toText, out to))
                                {
                                    throw new InvalidDataException($"User '{userName ?? id}' has invalid 'to' value '{toText}'");
                                }
                            }

                            User user = new User(userName, id, category, subcategory, new Interval(from, to));

                            if (IsOverlappingUser(user))
                            {
                                throw new InvalidDataException($"Overlapping user detected '{userName ?? id}'");
                            }
                            _users.Add(user);

                            categoryUsers.Add(user);
                        }
                    }
                }

                return categories;
            }

            private bool IsOverlappingUser(User user)
            {
                foreach (User other in _users)
                {
                    if (user.DoesOverlap(other))
                    {
                        return true;
                    }
                }
                return false;
            }

            private Category FindCategory(string name)
            {
                return Categories.Where(category => category.Name == name).FirstOrDefault();
            }

            private IEnumerable<Report> LoadReports()
            {
                List<Report> reports = new List<Report>();

                foreach (ConfigFile configFile in _configFiles)
                {
                    foreach (XElement reportNode in configFile.Root.Descendants("report"))
                    {
                        string reportName = reportNode.Attribute("name").Value;
                        string startTimeText = reportNode.Attribute("start").Value;
                        string stopTimeText = reportNode.Attribute("stop")?.Value;
                        string unitText = reportNode.Attribute("unit").Value;
                        string defaultGroupName = reportNode.Attribute("defaultGroupName").Value;

                        DateTimeOffset startTime;
                        if (!DateTimeOffset.TryParse(startTimeText, out startTime))
                        {
                            throw new InvalidDataException($"Report '{reportName}' has invalid 'start' value '{startTimeText}'");
                        }
                        if (startTime < new DateTimeOffset(2000, 1, 1, 0, 0, 0, new TimeSpan()))
                        {
                            throw new InvalidDataException($"Likely invalid date '{startTime}'");
                        }
                        DateTimeOffset? stopTime = null;
                        if (stopTimeText != null)
                        {
                            DateTimeOffset stopTimeValue;
                            if (!DateTimeOffset.TryParse(stopTimeText, out stopTimeValue))
                            {
                                throw new InvalidDataException($"Report '{reportName}' has invalid 'start' value '{startTimeText}'");
                            }
                            stopTime = stopTimeValue;
                        }
                        Report.UnitKind unit;
                        if (!Enum.TryParse<Report.UnitKind>(unitText, ignoreCase: true, out unit))
                        {
                            throw new InvalidDataException($"Report '{reportName}' has invalid 'unit' value '{unitText}'");
                        }

                        List<Group> groups = new List<Group>();

                        Report report = new Report(reportName, startTime, stopTime, unit, groups,defaultGroupName);
                        reports.Add(report);

                        foreach (XElement groupNode in reportNode.Descendants("group"))
                        {
                            string groupName = groupNode.Attribute("name")?.Value;
                            string categoriesText = groupNode.Attribute("categories")?.Value;

                            List<Category> categories = new List<Category>();
                            foreach (string categoryName in categoriesText.Split(';'))
                            {
                                Category category = FindCategory(categoryName);
                                if (category == null)
                                {
                                    throw new InvalidDataException($"Invalid category '{categoryName}' in report '{reportName}'");
                                }
                                categories.Add(category);
                            }

                            groups.Add(new Group(groupName, categories, report));
                        }
                    }
                }

                return reports;
            }
        }
    }
}
