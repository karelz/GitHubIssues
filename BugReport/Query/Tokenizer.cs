using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BugReport.DataModel;

namespace BugReport.Query
{
    internal class QueryTokenizer : IDisposable
    {
        string queryString;

        public QueryTokenizer(string queryString)
        {
            this.queryString = queryString;
            query = new StringReader(queryString);
            position = 0;
        }

        public Token ReadNextToken()
        {
            SkipWhitespaces();

            int nextChar = ReadNextChar();
            int startPosition = position;
            switch (nextChar)
            {
                case -1:
                    return new Token(Token.Type.EndOfQuery, startPosition);
                case '(':
                    return new Token(Token.Type.BracketLeft, startPosition);
                case ')':
                    return new Token(Token.Type.BracketRight, startPosition);
                case '!':
                    return new Token(Token.Type.OperatorNot, startPosition);
                case '&':
                    if (ReadNextChar() != '&')
                    {
                        throw new InvalidQueryException("& expected after &", queryString, position);
                    }
                    return new Token(Token.Type.OperatorAnd, startPosition);
                case '|':
                    if (ReadNextChar() != '|')
                    {
                        throw new InvalidQueryException("| expected after |", queryString, position);
                    }
                    return new Token(Token.Type.OperatorOr, startPosition);
            }

            StringBuilder word1 = new StringBuilder();

            bool startsWithMinus = false;
            if (nextChar == '-')
            {
                startsWithMinus = true;
                word1.Append((char)nextChar);
                nextChar = ReadNextChar();
            }
            if (!IsValidWord1FirstChar(nextChar))
            {
                throw new InvalidQueryException("Expected [a-zA-Z] character", queryString, position);
            }
            word1.Append((char)nextChar);
            for (;;)
            {
                nextChar = PeekNextChar();
                if (!IsValidWord1NextChar(nextChar))
                {
                    break;
                }
                ReadNextChar();
                word1.Append((char)nextChar);
            }
            if (char.IsWhiteSpace((char)nextChar) ||
                (nextChar == ')') ||
                (nextChar == -1))
            {
                if (startsWithMinus)
                {
                    throw new InvalidQueryException("Expected ':' separator for words starting with -", queryString, startPosition);
                }
                return new Token(Token.Type.Word, startPosition, word1.ToString());
            }
            if (nextChar != ':')
            {
                throw new InvalidQueryException("Expected ')' or ':' or whitespace after the word", queryString, startPosition);
            }
            // nextChar == ':'
            ReadNextChar();

            StringBuilder word2 = new StringBuilder();
            nextChar = ReadNextChar();

            if (nextChar == '"')
            {
                int start2Position = position;
                for (;;)
                {
                    nextChar = ReadNextChar();
                    if (nextChar == '"')
                    {
                        break;
                    }
                    if (nextChar == -1)
                    {
                        throw new InvalidQueryException("Unable to find matching '\"' character", queryString, start2Position);
                    }
                    word2.Append((char)nextChar);
                }
                nextChar = PeekNextChar();
                if (char.IsWhiteSpace((char)nextChar) ||
                    (nextChar == ')') ||
                    (nextChar == -1))
                {
                    return new Token(Token.Type.KeyValuePair, startPosition, word1.ToString(), word2.ToString());
                }
                throw new InvalidQueryException("Expected ')' or whitespace after the word", queryString, startPosition);
            }

            if (!IsValidWord2Char(nextChar))
            {
                throw new InvalidQueryException("Expected [-_.a-zA-Z0-9] character after ':'", queryString, position);
            }
            word2.Append((char)nextChar);
            for (;;)
            {
                nextChar = PeekNextChar();
                if (!IsValidWord2Char(nextChar))
                {
                    break;
                }
                ReadNextChar();
                word2.Append((char)nextChar);
            }
            if (char.IsWhiteSpace((char)nextChar) ||
                (nextChar == ')') ||
                (nextChar == -1))
            {
                return new Token(Token.Type.KeyValuePair, startPosition, word1.ToString(), word2.ToString());
            }
            throw new InvalidQueryException("Expected ')' or whitespace after the word", queryString, startPosition);
        }

        static bool IsValidWord1FirstChar(int c)
        {
            return char.IsLetter((char)c);
        }
        static bool IsValidWord1NextChar(int c)
        {
            return (char.IsLetter((char)c) ||
                    (c == '-') ||
                    (c == '_') ||
                    (c == '.'));
        }
        static bool IsValidWord2Char(int c)
        {
            return (char.IsLetterOrDigit((char)c) ||
                    (c == '-') ||
                    (c == '_') ||
                    (c == '.') ||
                    (c == '/'));
        }

        void SkipWhitespaces()
        {
            for (;;)
            {
                int nextChar = PeekNextChar();
                if (!char.IsWhiteSpace((char)nextChar))
                {
                    break;
                }
                ReadNextChar();
            }
        }

        StringReader query;
        int position;
        int PeekNextChar()
        {
            return query.Peek();
        }
        int ReadNextChar()
        {
            int nextChar = query.Read();
            if (nextChar != -1)
            {
                position++;
            }
            return nextChar;
        }

        public void Close()
        {
            if (query != null)
            {
                query.Close();
            }
        }

        public void Dispose()
        {
            if (query != null)
            {
                query.Dispose();
                query = null;
            }
        }
    }
}
