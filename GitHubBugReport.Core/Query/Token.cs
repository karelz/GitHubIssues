using System;
using System.Diagnostics;

namespace GitHubBugReport.Core.Query
{
    internal struct Token
    {
        public Token(Type type, int position = -1, string word1 = null, string word2 = null)
        {
            switch (type)
            {
                case Type.Uninitialized:
                case Type.EndOfQuery:
                case Type.BracketLeft:
                case Type.BracketRight:
                case Type.OperatorNot:
                case Type.OperatorAnd:
                case Type.OperatorOr:
                    Debug.Assert((word1 == null) || (word2 == null));
                    break;
                case Type.Word:
                    Debug.Assert((word1 != null) || (word2 == null));
                    break;
                case Type.KeyValuePair:
                    Debug.Assert((word1 != null) || (word2 != null));
                    break;
                default:
                    Debug.Assert(false, "Invalid 'type' value");
                    break;

            }

            TokenType = type;
            Position = position;
            Word1 = word1;
            Word2 = word2;
        }

        public enum Type
        {
            Uninitialized,      // Internal only, never leaks out of the type

            EndOfQuery,     // no words
            BracketLeft,    // (
            BracketRight,   // )
            Word,           // word1 = AND, OR, NOT
            OperatorNot,    // !
            OperatorAnd,    // &&
            OperatorOr,     // ||
            KeyValuePair    // word1:word2
        }

        public Type TokenType { get; private set; }
        public string Word1 { get; private set; }
        public string Word2 { get; private set; }
        public int Position { get; private set; }

        public bool IsEndOfQuery()
        {
            return (TokenType == Type.EndOfQuery);
        }

        public bool IsInitialized()
        {
            return (TokenType != Type.Uninitialized);
        }

        public bool IsOperatorNot()
        {
            return (TokenType == Type.OperatorNot);
        }

        public bool IsOperatorAnd()
        {
            return (TokenType == Type.OperatorAnd);
        }

        public bool IsOperatorOr()
        {
            return (TokenType == Type.OperatorOr);
        }

        public bool IsBracketLeft()
        {
            return (TokenType == Type.BracketLeft);
        }

        public bool IsBracketRight()
        {
            return (TokenType == Type.BracketRight);
        }

        public bool IsWord()
        {
            return (TokenType == Type.Word);
        }

        public bool IsWord(string word, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            return ((TokenType == Type.Word) &&
                    Word1.Equals(word, comparisonType));
        }

        public bool IsKeyValuePair()
        {
            return (TokenType == Type.KeyValuePair);
        }

        public bool IsKeyValuePair(string key, StringComparison comparisonType = StringComparison.Ordinal)
        {
            return ((TokenType == Type.KeyValuePair) &&
                    Word1.Equals(key, comparisonType));
        }

        public bool IsKeyValuePair(string key, string value, StringComparison comparisonType = StringComparison.Ordinal)
        {
            return ((TokenType == Type.KeyValuePair) &&
                    Word1.Equals(key, comparisonType) &&
                    Word2.Equals(value, comparisonType));
        }

        public override string ToString()
        {
            if ((Word1 == null) && (Word2 == null))
                return $"{TokenType}";
            if (Word2 == null)
                return $"{Word1}";
            return $"{Word1}:{Word2}";
        }
    }
}
