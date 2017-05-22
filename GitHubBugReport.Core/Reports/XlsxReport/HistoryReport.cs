using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GitHubBugReport.Core.Reports.XlsxReport
{
    public class HistoryReport
    {
        public static void Create(IEnumerable<string> inputFiles, string outputFileName)
        {
            Debug.Assert(inputFiles.Count() > 0);

            List<Table> tables = new List<Table>();
            foreach (string inputFile in inputFiles)
            {
                tables.Add(Table.Parse(inputFile));
            }
            // Sort tables by name
            tables.Sort((t1, t2) => string.Compare(t1.Name, t2.Name));

            // Validate schema of all tables - they have to all match
            Table firstTable = tables.First();
            foreach (Table table in tables.Skip(1))
            {
                table.ValidateSchema(firstTable);
            }

            using (ExcelPackage package = new ExcelPackage(new FileStream(outputFileName, FileMode.Create)))
            {
                string[] columnHeaders = firstTable.ColumnHeaders;
                for (int i = 0; i < columnHeaders.Length; i++)
                {
                    string columnHeader = columnHeaders[i];
                    CreateWorksheet(package, columnHeader, tables, (row => row.Values[i].Issues));
                    CreateWorksheet(package, $"{columnHeader} - New", tables, (row => row.Values[i].NewIssues));
                    CreateWorksheet(package, $"{columnHeader} - Gone", tables, (row => row.Values[i].GoneIssues));
                }
                package.Save();
            }
        }

        private delegate int RowValueSelector(Row row);

        private static void CreateWorksheet(
            ExcelPackage package,
            string name,
            List<Table> tables,
            RowValueSelector rowValueSelector)
        {
            ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(name);
            for (int i = 0; i < tables.Count; i++)
            {
                worksheet.Cells[1, i + 2].Value = tables[i].Name;
            }

            int rowIndex = 2;
            foreach (string rowName in tables.First().Rows.Select(row => row.Name))
            {
                worksheet.Cells[rowIndex, 1].Value = rowName;
                rowIndex++;
            }

            int rowsCount = tables.First().Rows.Count;

            for (int i = 0; i < tables.Count; i++)
            {
                int colIndex = i + 2;
                for (int j = 0; j < rowsCount; j++)
                {
                    worksheet.Cells[j + 2, colIndex].Value = rowValueSelector(tables[i].Rows[j]);
                }
            }
        }

        private class Table
        {
            public string Name { get; private set; }
            public string[] ColumnHeaders { get; private set; }
            public List<Row> Rows { get; private set; }
            
            // For better errors from advanced validations
            public string FileName { get; private set; }

            private Table(string name, IEnumerable<string> columnHeaders, IEnumerable<Row> rows, string fileName)
            {
                Name = name;
                ColumnHeaders = columnHeaders.ToArray();
                Rows = rows.ToList();
                FileName = fileName;

                foreach (Row row in rows)
                {
                    Debug.Assert(row.Values.Length == columnHeaders.Count());
                }
            }

            public static Table Parse(string inputFileName)
            {
                TextFieldParser csvParser = new TextFieldParser(inputFileName);
                csvParser.TextFieldType = FieldType.Delimited;
                csvParser.Delimiters = new string[] { "," };

                string[] allHeaders = csvParser.ReadFields();
                if ((allHeaders.Length % 3) != 1)
                {
                    throw new InvalidDataException($"Unexpected number of columns {allHeaders.Length} in file '{inputFileName}'.");
                }

                string name = ConvertToDate(allHeaders[0]);
                string[] columnHeaders = new string[allHeaders.Length / 3];
                for (int i = 0; i < columnHeaders.Length; i++)
                {
                    int startIndex = 3 * i + 1;
                    columnHeaders[i] = allHeaders[startIndex];
                    if (allHeaders[startIndex + 1] != "new")
                    {
                        throw new InvalidDataException($"Header #{1 + startIndex + 1} in file '{inputFileName}' should be 'new'");
                    }
                    if (allHeaders[startIndex + 2] != "gone")
                    {
                        throw new InvalidDataException($"Header #{1 + startIndex + 2} in file '{inputFileName}' should be 'gone'");
                    }
                }

                List<Row> rows = new List<Row>();

                while (!csvParser.EndOfData)
                {
                    string[] fields = csvParser.ReadFields();
                    if (fields.Length != allHeaders.Length)
                    {
                        throw new InvalidDataException(
                            $"Inconsistent number of fields in file '{inputFileName}' on line {csvParser.LineNumber}, expected {allHeaders.Length} fields");
                    }
                    rows.Add(Row.Parse(fields, inputFileName, csvParser.LineNumber));
                }

                return new Table(name, columnHeaders, rows, inputFileName);
            }

            public void ValidateSchema(Table table)
            {
                if (ColumnHeaders.Length != table.ColumnHeaders.Length)
                {
                    throw new InvalidDataException($"Inconsistent number of column headers between files '{table.FileName}' and '{FileName}'");
                }
                for (int i = 0; i < ColumnHeaders.Length; i++)
                {
                    if (table.ColumnHeaders[i] != ColumnHeaders[i])
                    {
                        throw new InvalidDataException($"Inconsistent header #{i + 1} name '{table.ColumnHeaders[i]}' (in file '{table.FileName}') and '{ColumnHeaders[i]}' (in file '{FileName}')");
                    }
                }

                if (Rows.Count() != table.Rows.Count())
                {
                    throw new InvalidDataException($"Inconsistent number of rows between files '{table.FileName}' and '{FileName}'");
                }
                for (int i = 0; i < Rows.Count(); i++)
                {
                    if (table.Rows[i].Name != Rows[i].Name)
                    {
                        throw new InvalidDataException($"Inconsistent row #{i + 1} name '{table.Rows[i].Name}' (in file '{table.FileName}') and '{Rows[i].Name}' (in file '{FileName}')");
                    }
                }
            }
        }

        private class Entry
        {
            public int Issues { get; private set; }
            public int NewIssues { get; private set; }
            public int GoneIssues { get; private set; }

            public Entry(int issues, int newIssues, int goneIssues)
            {
                Issues = issues;
                NewIssues = newIssues;
                GoneIssues = goneIssues;
            }
        }

        private class Row
        {
            public string Name { get; private set; }
            public Entry[] Values { get; private set; }

            // For better errors from advanced validations
            public long LineNumber { get; private set; }

            public Row(string name, Entry[] values, long lineNumber)
            {
                Name = name;
                Values = values;
                LineNumber = lineNumber;
            }

            public static Row Parse(string[] lineValues, string fileName, long lineNumber)
            {
                string name = lineValues[0];

                int[] values = new int[lineValues.Length - 1];
                for (int i = 1; i < lineValues.Length; i++)
                {
                    if (!int.TryParse(lineValues[i], out values[i - 1]))
                    {
                        throw new InvalidDataException($"Invalid non-number value '{lineValues[i]}' in file '{fileName}', line {lineNumber}");
                    }
                }

                Debug.Assert((values.Length % 3) == 0);
                Entry[] entries = new Entry[values.Length / 3];
                for (int i = 0; i < entries.Length; i++)
                {
                    int startIndex = 3 * i;
                    entries[i] = new Entry(values[startIndex], values[startIndex + 1], values[startIndex + 2]);
                }

                return new Row(name, entries, lineNumber);
            }
        }

        private static string ConvertToDate(string value)
        {
            Regex regex = new Regex("^[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]$");
            if (regex.IsMatch(value))
            {
                return value.Replace('-', '/');
            }
            return value;
        }
    }
}
