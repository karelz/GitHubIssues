using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BugReport.Util;

namespace BugReport.CommandLine
{
    public class Option
    {
        public IEnumerable<string> Names { get; private set; }
        public bool RequiresValue { get; private set; }
        public bool AllowMultipleValues { get; private set; }

        protected Option(IEnumerable<string> names, bool requiresValue = true, bool allowMultipleValues = false)
        {
            if (allowMultipleValues && !requiresValue)
            {
                throw new InvalidOperationException("Option with multiple values need to require value");
            }

            Names = names;
            RequiresValue = requiresValue;
            AllowMultipleValues = allowMultipleValues;
        }

        public bool Equals(string name)
        {
            return Names.ContainsIgnoreCase(name);
        }

        public static IEnumerable<Option> EmptyList
        {
            get
            {
                return new Option[] {};
            }
        }
    }

    public class OptionWithoutValue : Option
    {
        public OptionWithoutValue(IEnumerable<string> names) 
            : base(names, requiresValue: false, allowMultipleValues: false)
        {
        }
        public OptionWithoutValue(params string[] names)
            : base(names, requiresValue: false, allowMultipleValues: false)
        {
        }

        public bool IsDefined(Parser parser)
        {
            return parser.IsOptionWithoutValueDefined(this);
        }
    }

    public class OptionSingleValue : Option
    {
        public OptionSingleValue(IEnumerable<string> names) 
            : base(names, requiresValue: true, allowMultipleValues: false)
        {
        }
        public OptionSingleValue(params string[] names)
            : base(names, requiresValue: true, allowMultipleValues: false)
        {
        }

        public string GetValue(Parser parser)
        {
            return parser.GetOptionValue(this);
        }
    }

    public class OptionMultipleValues : Option
    {
        public OptionMultipleValues(IEnumerable<string> names) 
            : base(names, requiresValue: true, allowMultipleValues: true)
        {
        }
        public OptionMultipleValues(params string[] names)
            : base(names, requiresValue: true, allowMultipleValues: true)
        {
        }

        public IEnumerable<string> GetValues(Parser parser)
        {
            return parser.GetOptionValues(this);
        }
    }
}
