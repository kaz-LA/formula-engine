using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FormulaEngine.Core.Interfaces;

namespace FormulaEngine.Core.Parser.Internal
{
    public interface ITokenFactory
    {
        Token CreateToken(string token, TokenType type, int index, object data);
        Token CreateToken(string text, int index, IParserContext context);
        Token StringLiteral(string text, int startIndex, IParserContext context);
        Token Comma(int index);
    }

    /// <summary>
    /// Creates and returns Token of specified Type and Content
    /// </summary>
    internal class TokenFactory : ITokenFactory
    {
        public Token CreateToken(string token, TokenType type, int index, object data = null) =>
           new Token { Text = token, Type = type, Value = data, StartIndex = index };

        public async Task<Token> CreateToken(string text, IParserContext context, int index)
        {
            var value = text.Trim();
            var tokenType = TokenType.Unknown;
            TokenType? secondaryType = null;
            object data = null;

            if (IsQuotedText(value, out value))
            {
                tokenType = TokenType.String;
                secondaryType = GetTokenSecondaryType(value, context, out data);
            }
            else if (double.TryParse(value, NumberStyles.Number, context.CultureInfo, out var _))
                tokenType = TokenType.Number;

            else if (IsDateTime(value, context.CultureInfo))
                tokenType = TokenType.Date;

            else if (context.IsBoolean(value, out var boolValue))
            {
                tokenType = TokenType.Boolean;
                data = boolValue;
            }

            else if (context.IsOperator(value, out var @operator))
            {
                data = @operator;
                tokenType = TokenType.Operator;
            }

            return new Token { Text = value, Type = tokenType, StartIndex = index, Value = data, SecondaryType = secondaryType };
        }

        public async Task<Token> CreateFunctionToken(string name, IParserContext context, int index)
        {
            var function = await context.GetFunction(name);
            return new FunctionToken
                {Text = name, Type = TokenType.Function, Value = function, StartIndex = index, Function = function};
        }

        public Token StringLiteral(string text, int startIndex, IParserContext context)
        {
            text = RemoveQuotes(text, context.QuoteChar);
            var secondaryType = GetTokenSecondaryType(text, context, out var data);

            return new Token
            {
                Text = text,
                Type = TokenType.String,
                SecondaryType = secondaryType,
                StartIndex = startIndex,
                Value = data
            };
        }
        
        public async Task<Token> ColumnToken(string value, IParserContext context, int index)
        {
            var (_, column) = await IsColumn(value, context);
            return new Token { Text = value, Type = TokenType.Column, Value = column, StartIndex = index };
        }

        public async Task<(bool, IColumn)> IsColumn(string text, IParserContext context)
        {
            IColumn column = null;
            if (string.IsNullOrEmpty(text))
                return (false, column);

            var values = text.Split(new[] {Constants.COLUMN_START_CHAR, Constants.COLUMN_END_CHAR},
                StringSplitOptions.RemoveEmptyEntries);

            string entityName = null;
            string columnName = null;

            if (values.Length >= 3) // [EntityName].[ColumnName]
            {
                entityName = values[0].Trim();
                columnName = values[2].Trim();
            }
            else
            {
                columnName = values[0].Trim(); // just [ColumnName] or ColumnName
                if (text[0] != Constants.COLUMN_START_CHAR && text[text.Length - 1] != Constants.COLUMN_END_CHAR)
                {
                    values = text.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length > 1) entityName = values.First();
                    columnName = values.Last();
                }
            }

            column = await context.GetColumnByTitle(context.UserCultureId, entityName, columnName);

            return (column != null, column);
        }

        public static bool IsQuotedText(string text, char quoteChar, out string withoutQuote)
        {
            var isQuoted = text.Length > 1 && text[0] == quoteChar && text[^1] == quoteChar;
            withoutQuote = isQuoted ? text.Substring(1, text.Length - 2) : text;
            return isQuoted;
        }

        public static string RemoveQuotes(string text, char quoteChar)
        {
            text = text?.Trim();

            var isQuoted = text?.Length > 1 && text[0] == quoteChar && text[^1] == quoteChar;
            var withoutQuote = isQuoted ? text[1..^1] : text;

            return withoutQuote;
        }

        public static bool IsDateTime(string value, CultureInfo cultureInfo) =>
            DateTime.TryParse(value, cultureInfo, DateTimeStyles.AssumeLocal, out var _);

        private static TokenType? GetTokenSecondaryType(string token, IParserContext context, out object value)
        {
            value = null;
            if (IsDateTime(token, context.CultureInfo))
                return TokenType.Date;

            if (context.IsBoolean(token, out var bln))
            {
                value = bln;
                return TokenType.Boolean;
            }

            return null;
        }
    }
}
