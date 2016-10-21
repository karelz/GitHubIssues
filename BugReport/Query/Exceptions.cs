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
    public class InvalidQueryException : Exception
    {
        public InvalidQueryException(string message, string queryString, int position) :
            base(String.Format("{0} at position {1}", message, position))
        {
        }
    }

    public class QueryParser : IDisposable
    {
        QuerySyntaxParser parser;
        string queryString;

        public QueryParser(string queryString)
        {
            this.queryString = queryString;
            parser = new QuerySyntaxParser(queryString);
        }

        public Expression Parse()
        {
            Expression expr = ParseExpression_Or();
            if (expr == null)
            {
                throw new InvalidQueryException("The query is empty", queryString, 0);
            }
            return expr;
        }

        Expression ParseExpression_Or()
        {
            Expression expr = ParseExpression_And();
            Token token = parser.Peek();
            if (!token.IsOperatorOr())
            {
                return expr;
            }
            parser.Skip();

            if (expr == null)
            {
                throw new InvalidQueryException("Expression expected before OR operator", queryString, token.Position);
            }

            List<Expression> expressions = new List<Expression>();
            expressions.Add(expr);

            for (;;)
            {
                expr = ParseExpression_And();
                expressions.Add(expr);
                if (expr == null)
                {
                    throw new InvalidQueryException("Expression expected after OR operator", queryString, token.Position);
                }
                token = parser.Peek();
                if (!token.IsOperatorOr())
                {
                    return new ExpressionOr(expressions);
                }
                parser.Skip();
            }
        }

        Expression ParseExpression_And()
        {
            Token token = parser.Peek();
            if (token.IsEndOfQuery() || token.IsOperatorOr() || token.IsBracketRight())
            {
                return null;
            }
            if (token.IsOperatorAnd())
            {
                throw new InvalidQueryException("Expression expected before AND operator", queryString, token.Position);
            }

            Expression expr = ParseExpression_Single();
            token = parser.Peek();
            if (token.IsEndOfQuery() || token.IsOperatorOr() || token.IsBracketRight())
            {
                return expr;
            }

            List<Expression> expressions = new List<Expression>();
            expressions.Add(expr);

            for (;;)
            {
                bool hasOperatorAnd = false;
                token = parser.Peek();
                if (token.IsOperatorAnd())
                {
                    hasOperatorAnd = true;
                    parser.Skip();
                }

                expr = ParseExpression_Single();
                if (expr == null)
                {
                    if (hasOperatorAnd)
                    {
                        throw new InvalidQueryException("Expression expected after AND operator", queryString, token.Position);
                    }
                    Debug.Assert(expressions.Count > 1);
                    return new ExpressionAnd(expressions);
                }
                expressions.Add(expr);
            }
        }

        Expression ParseExpression_Single()
        {
            Token token = parser.Peek();

            if (token.IsEndOfQuery() || token.IsOperatorOr() || token.IsOperatorAnd() || token.IsBracketRight())
            {
                return null;
            }

            if (token.IsOperatorNot())
            {
                parser.Skip();
                Expression expr = new ExpressionNot(Parse());
                if (expr == null)
                {
                    throw new InvalidQueryException("The sub-expression after NOT operator is empty", queryString, token.Position);
                }
                return expr;
            }

            if (token.IsKeyValuePair())
            {
                Expression expr;
                if (token.IsKeyValuePair("label"))
                {
                    expr = new ExpressionLabel(token.Word2);
                }
                else if (token.IsKeyValuePair("-label"))
                {
                    expr = new ExpressionNot(new ExpressionLabel(token.Word2));
                }
                else if (token.IsKeyValuePair("milestone"))
                {
                    expr = new ExpressionMilestone(token.Word2);
                }
                else if (token.IsKeyValuePair("is"))
                {
                    if (token.IsKeyValuePair("is", "issue"))
                    {
                        expr = new ExpressionIsIssue(true);
                    }
                    else if (token.IsKeyValuePair("is", "pr"))
                    {
                        expr = new ExpressionIsIssue(false);
                    }
                    else if (token.IsKeyValuePair("is", "open"))
                    {
                        expr = new ExpressionIsOpen(true);
                    }
                    else if (token.IsKeyValuePair("is", "closed"))
                    {
                        expr = new ExpressionIsOpen(false);
                    }
                    else
                    {
                        throw new InvalidQueryException("Unexpected value in key-value pair, expected: [pr|issue|open|close]", queryString, token.Position);
                    }
                }
                else if (token.IsKeyValuePair("assignee"))
                {
                    expr = new ExpressionAssignee(token.Word2);
                }
                else
                {
                    throw new InvalidQueryException("Unexpected key in key-value pair, expected: [label|-label|milestone|is|assignee]", queryString, token.Position);
                }
                parser.Skip();
                return expr;
            }

            if (token.IsBracketLeft())
            {
                parser.Skip();
                Expression expr = Parse();
                if (expr == null)
                {
                    throw new InvalidQueryException("The sub-expression in brackets is empty", queryString, token.Position);
                }

                Token rightBracketToken = parser.Read();
                if (!rightBracketToken.IsBracketRight())
                {
                    throw new InvalidQueryException("Cannot find matching bracket ')'", queryString, token.Position);
                }
                return expr;
            }

            throw new InvalidQueryException("Unexpected expression -- expected ! or ( or key-value pair", queryString, token.Position);
        }

        public void Close()
        {
            if (parser != null)
            {
                parser.Close();
            }
        }
        public void Dispose()
        {
            if (parser != null)
            {
                parser.Dispose();
                parser = null;
            }
        }
    }

    public class QuerySyntaxParser : IDisposable
    {
        QueryTokenizer tokenizer;

        public QuerySyntaxParser(string queryString)
        {
            tokenizer = new QueryTokenizer(queryString);
            nextToken = new Token(Token.Type.Uninitialized);
        }

        public Token Peek()
        {
            EnsureNextToken();
            return nextToken;
        }

        public void Skip()
        {
            EnsureNextToken();
            nextToken = new Token(Token.Type.Uninitialized);
        }

        public Token Read()
        {
            EnsureNextToken();
            Token token = nextToken;
            nextToken = new Token(Token.Type.Uninitialized);
            return token;
        }

        Token nextToken;
        void EnsureNextToken()
        {
            if (!nextToken.IsInitialized())
            {
                nextToken = tokenizer.ReadNextToken();
                if (nextToken.IsWord())
                {
                    if (nextToken.IsWord("AND"))
                    {
                        nextToken = new Token(Token.Type.OperatorAnd, nextToken.Position);
                    }
                    if (nextToken.IsWord("OR"))
                    {
                        nextToken = new Token(Token.Type.OperatorOr, nextToken.Position);
                    }
                    if (nextToken.IsWord("NOT"))
                    {
                        nextToken = new Token(Token.Type.OperatorNot, nextToken.Position);
                    }
                }
            }
        }

        public void Close()
        {
            if (tokenizer != null)
            {
                tokenizer.Close();
            }
        }
        public void Dispose()
        {
            if (tokenizer != null)
            {
                tokenizer.Dispose();
                tokenizer = null;
            }
        }
    }

    public struct Token
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
                return String.Format("{0}", TokenType);
            if (Word2 == null)
                return String.Format("{0} {1}", TokenType, Word1);
            return String.Format("{0} {1}:{2}", TokenType, Word1, Word2);
        }
    }

}
