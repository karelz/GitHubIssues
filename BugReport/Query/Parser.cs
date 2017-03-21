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
        public InvalidQueryException(string message, string queryString, int position)
            : base(String.Format("{0} at position {1}", message, position))
        {
        }
    }

    public class QueryParser : IDisposable
    {
        QuerySyntaxParser _parser;
        string _queryString;
        IReadOnlyDictionary<string, Expression> _customIsValues;

        public QueryParser(string queryString, IReadOnlyDictionary<string, Expression> customIsValues)
        {
            _queryString = queryString;
            _customIsValues = customIsValues ?? new Dictionary<string, Expression>();
            _parser = new QuerySyntaxParser(queryString);
        }

        public static Expression Parse(string queryString, IReadOnlyDictionary<string, Expression> customIsValues)
        {
            QueryParser queryParser = new QueryParser(queryString, customIsValues);
            return queryParser.Parse();
        }

        public Expression Parse()
        {
            Expression expr = ParseExpression_Or();
            if (expr == null)
            {
                throw new InvalidQueryException("The query is empty", _queryString, 0);
            }
            return expr;
        }

        Expression ParseExpression_Or()
        {
            Expression expr = ParseExpression_And();
            Token token = _parser.Peek();
            if (!token.IsOperatorOr())
            {
                return expr;
            }
            _parser.Skip();

            if (expr == null)
            {
                throw new InvalidQueryException("Expression expected before OR operator", _queryString, token.Position);
            }

            List<Expression> expressions = new List<Expression>();
            expressions.Add(expr);

            for (;;)
            {
                expr = ParseExpression_And();
                expressions.Add(expr);
                if (expr == null)
                {
                    throw new InvalidQueryException("Expression expected after OR operator", _queryString, token.Position);
                }
                token = _parser.Peek();
                if (!token.IsOperatorOr())
                {
                    return new ExpressionOr(expressions);
                }
                _parser.Skip();
            }
        }

        Expression ParseExpression_And()
        {
            Token token = _parser.Peek();
            if (token.IsEndOfQuery() || token.IsOperatorOr() || token.IsBracketRight())
            {
                return null;
            }
            if (token.IsOperatorAnd())
            {
                throw new InvalidQueryException("Expression expected before AND operator", _queryString, token.Position);
            }

            Expression expr = ParseExpression_Single();
            token = _parser.Peek();
            if (token.IsEndOfQuery() || token.IsOperatorOr() || token.IsBracketRight())
            {
                return expr;
            }

            List<Expression> expressions = new List<Expression>();
            expressions.Add(expr);

            for (;;)
            {
                bool hasOperatorAnd = false;
                token = _parser.Peek();
                if (token.IsOperatorAnd())
                {
                    hasOperatorAnd = true;
                    _parser.Skip();
                }

                expr = ParseExpression_Single();
                if (expr == null)
                {
                    if (hasOperatorAnd)
                    {
                        throw new InvalidQueryException("Expression expected after AND operator", _queryString, token.Position);
                    }
                    Debug.Assert(expressions.Count > 1);
                    return new ExpressionAnd(expressions);
                }
                expressions.Add(expr);
            }
        }

        Expression ParseExpression_Single()
        {
            Token token = _parser.Peek();

            if (token.IsEndOfQuery() || token.IsOperatorOr() || token.IsOperatorAnd() || token.IsBracketRight())
            {
                return null;
            }

            if (token.IsOperatorNot())
            {
                _parser.Skip();
                Expression expr = new ExpressionNot(ParseExpression_Single());
                if (expr == null)
                {
                    throw new InvalidQueryException("The sub-expression after NOT operator is empty", _queryString, token.Position);
                }
                return expr;
            }

            if (token.IsKeyValuePair())
            {
                Expression expr;
                if (token.IsKeyValuePair("label"))
                {
                    if (IsRegex(token.Word2))
                    {
                        expr = new ExpressionLabelPattern(token.Word2);
                    }
                    else
                    {
                        expr = new ExpressionLabel(token.Word2);
                    }
                }
                else if (token.IsKeyValuePair("-label"))
                {
                    if (IsRegex(token.Word2))
                    {
                        expr = new ExpressionNot(new ExpressionLabelPattern(token.Word2));
                    }
                    else
                    {
                        expr = new ExpressionNot(new ExpressionLabel(token.Word2));
                    }
                }
                else if (token.IsKeyValuePair("milestone"))
                {
                    if (IsRegex(token.Word2))
                    {
                        expr = new ExpressionMilestonePattern(token.Word2);
                    }
                    else
                    {
                        expr = new ExpressionMilestone(token.Word2);
                    }
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
                    else if (_customIsValues.TryGetValue(token.Word2, out expr))
                    {
                    }
                    else
                    {
                        throw new InvalidQueryException($"Unexpected value '{token}' in key-value pair, expected: [pr|issue|open|close]", _queryString, token.Position);
                    }
                }
                else if (token.IsKeyValuePair("assignee"))
                {
                    expr = new ExpressionAssignee(token.Word2);
                }
                else if (token.IsKeyValuePair("no"))
                {
                    if (token.IsKeyValuePair("no", "milestone"))
                    {
                        expr = new ExpressionMilestone(null);
                    }
                    else if (token.IsKeyValuePair("no", "assignee"))
                    {
                        expr = new ExpressionAssignee(null);
                    }
                    else
                    {
                        throw new InvalidQueryException($"Unexpected value '{token}' in key-value pair, expected: [milestone|assignee]", _queryString, token.Position);
                    }
                }
                else
                {
                    throw new InvalidQueryException($"Unexpected key '{token}' in key-value pair, expected: [label|-label|milestone|is|assignee]", _queryString, token.Position);
                }
                _parser.Skip();
                return expr;
            }

            if (token.IsBracketLeft())
            {
                _parser.Skip();
                Expression expr = Parse();
                if (expr == null)
                {
                    throw new InvalidQueryException("The sub-expression in brackets is empty", _queryString, token.Position);
                }

                Token rightBracketToken = _parser.Read();
                if (!rightBracketToken.IsBracketRight())
                {
                    throw new InvalidQueryException("Cannot find matching bracket ')'", _queryString, token.Position);
                }
                return expr;
            }

            throw new InvalidQueryException("Unexpected expression -- expected ! or ( or key-value pair", _queryString, token.Position);
        }

        public static bool IsRegex(string value)
        {
            return (value.Contains(".*") || value.Contains(".."));
        }

        public void Close()
        {
            if (_parser != null)
            {
                _parser.Close();
            }
        }
        public void Dispose()
        {
            if (_parser != null)
            {
                _parser.Dispose();
                _parser = null;
            }
        }
    }
}
