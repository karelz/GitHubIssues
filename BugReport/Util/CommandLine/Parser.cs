using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BugReport.Util;

namespace BugReport.CommandLine
{
    public class Parser
    {
        private static readonly IEnumerable<string> _optionPrefixes = new List<string>() { "/", "--", "-" };

        private string[] _args;
        private Action _printUsage;
        public string Action { get; private set; }

        public Parser(string[] args, Action printUsage)
        {
            _args = args;
            _printUsage = printUsage;
        }

        // 'value' is:
        //    * null - if it is option without additional value
        //    * string - if it is option with single value
        //    * List<string> - if it is option with multiple values
        private Dictionary<Option, object> _options;

        internal IEnumerable<string> GetOptionValues(Option option)
        {
            Debug.Assert(option.RequiresValue && option.AllowMultipleValues);
            Debug.Assert(_options != null);
            object optionValues;
            if (!_options.TryGetValue(option, out optionValues))
            {
                return null;
            }
            return (IEnumerable<string>)optionValues;
        }

        internal string GetOptionValue(Option option)
        {
            Debug.Assert(option.RequiresValue && !option.AllowMultipleValues);
            Debug.Assert(_options != null);
            object optionValue;
            if (!_options.TryGetValue(option, out optionValue))
            {
                return null;
            }
            return (string)optionValue;
        }

        internal bool IsOptionWithoutValueDefined(Option option)
        {
            Debug.Assert(!option.RequiresValue && !option.AllowMultipleValues);
            Debug.Assert(_options != null);
            return _options.TryGetValue(option, out _);
        }

        private bool IsOptionValueSet(Option option)
        {
            return _options.TryGetValue(option, out _);
        }

        private void AddOptionValue(Option option, string value)
        {
            object optionValues;
            if (!_options.TryGetValue(option, out optionValues))
            {
                optionValues = new List<string>();
                _options[option] = optionValues;
            }
            ((List<string>)optionValues).Add(value);
        }
        private void SetOptionValue(Option option, string value)
        {
            Debug.Assert(!IsOptionValueSet(option));
            _options[option] = value;
        }

        public bool Parse(IEnumerable<Option> requiredOptions, IEnumerable<Option> optionalOptions)
        {
            if (_options != null)
            {
                throw new InvalidOperationException("Parse cannot be called twice");
            }
            _options = new Dictionary<Option, object>();

            IEnumerable<Option> allOptions = requiredOptions.Concat(optionalOptions);

            for (int i = 0; i < _args.Length; i++)
            {
                string optionArg = _args[i];
                Option option = FindOption(optionArg, allOptions);
                if (option == null)
                {
                    ReportError($"Unrecognized option '{optionArg}'.");
                    return false;
                }

                if (option.RequiresValue)
                {
                    if (i + 1 >= _args.Length)
                    {
                        ReportError($"Missing value for last option '{optionArg}'.");
                        return false;
                    }
                    i++;
                    string optionValue = _args[i];

                    if (option.AllowMultipleValues)
                    {
                        AddOptionValue(option, optionValue);
                    }
                    else
                    {   // single-value option
                        if (IsOptionValueSet(option))
                        {
                            ReportError($"Option '{optionArg}' is not allowed to have multiple values.");
                            return false;
                        }
                        SetOptionValue(option, optionValue);
                    }
                }
                else
                {   // option without value
                    if (IsOptionValueSet(option))
                    {
                        ReportError($"Option '{optionArg}' is defined more than once.");
                        return false;
                    }
                    SetOptionValue(option, null);
                }
            }

            foreach (Option option in requiredOptions)
            {
                if (!IsOptionValueSet(option))
                {
                    ReportError($"Required option '{option.Names.First()}' not set.");
                    return false;
                }
            }

            return true;
        }

        public void ReportError(string error)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();

            // Discard any partially parsed options
            _options = new Dictionary<Option, object>();

            _printUsage();
        }

        private static Option FindOption(string value, IEnumerable<Option> options)
        {
            string optionPrefix = _optionPrefixes
                .Where(p => value.StartsWith(p))    // match prefixes
                .OrderByDescending(p => p.Length)   // pick the longest if multiple choices (e.g. '--' and '-')
                .FirstOrDefault();
            if (optionPrefix == null)
            {
                return null;
            }
            string optionName = value.Substring(optionPrefix.Length);
            foreach (Option option in options)
            {
                if (option.Equals(optionName))
                {
                    return option;
                }
            }
            return null;
        }
        
        public static bool IsOption(string value, IEnumerable<string> options)
        {
            string optionPrefix = _optionPrefixes
                .Where(p => value.StartsWith(p))    // match prefixes
                .OrderByDescending(p => p.Length)   // pick the longest if multiple choices (e.g. '--' and '-')
                .FirstOrDefault();
            if (optionPrefix == null)
            {
                return false;
            }
            string optionName = value.Substring(optionPrefix.Length);
            return options.ContainsIgnoreCase(optionName);
        }
    }
}
