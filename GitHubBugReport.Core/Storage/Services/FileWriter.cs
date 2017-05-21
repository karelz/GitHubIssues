using System.Collections.Generic;
using System.IO;
using GitHubBugReport.Core.Issues.Models;
using Newtonsoft.Json;
using Formatting = System.Xml.Formatting;

namespace GitHubBugReport.Core.Storage.Services
{
    public class FileWriter : IFileWriter
    {
        public void SerializeToFile(string fileName, IReadOnlyCollection<Octokit.Issue> issues)
        {
            SerializeToFile(fileName, (object)issues);
        }

        public void SerializeToFile(string fileName, IReadOnlyCollection<Octokit.IssueComment> issueComments)
        {
            SerializeToFile(fileName, (object)issueComments);
        }

        public void SerializeToFile(string fileName, IEnumerable<DataModelIssue> issues)
        {
            SerializeToFile(fileName, (object)issues);
        }

        private void SerializeToFile(string fileName, object objToSerialize)
        {
            JsonSerializer serializer =
                new JsonSerializer {Formatting = (Newtonsoft.Json.Formatting) Formatting.Indented};

            using (StreamWriter sw = new StreamWriter(fileName))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, objToSerialize);
            }
        }
    }
}