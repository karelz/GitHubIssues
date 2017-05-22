using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BugReport.Reports
{
    public class CsvWriter : IDisposable
    {
        private StreamWriter _file;
        private bool _isLineStart = true;

        public CsvWriter(string fileName)
        {
            _file = new StreamWriter(fileName);
        }

        public void Write(string value)
        {
            if (_isLineStart)
            {
                _isLineStart = false;
            }
            else
            {
                _file.Write(",");
            }

            if (value.Contains('"') || value.Contains(','))
            {
                _file.Write($"\"{value.Replace("\"", "\"\"")}\"");
            }
            else
            {
                _file.Write(value);
            }
        }

        public void WriteLine(IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                Write(value);
            }
            _file.WriteLine();
            _isLineStart = true;
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool isDisposing)
        {
            if (_file == null)
            {
                return;
            }

            if (isDisposing)
            {
                _file.Dispose();
                _file = null;
            }
        }
    }
}
