using System;

namespace GitHubBugReport.Core.Query
{
    internal class QuerySyntaxParser : IDisposable
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
}
