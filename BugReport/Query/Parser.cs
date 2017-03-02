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
                        throw new InvalidQueryException("Unexpected value in key-value pair, expected: [milestone|assignee]", queryString, token.Position);
                    }
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
}
