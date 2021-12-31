using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Models;
using FormulaOperator = FormulaEngine.Core.Enums.FormulaOperator;

namespace FormulaEngine.Core.Parser.Internal.TokenConverters
{
    /// <summary>
    /// Converts a Token (from a formula expression) - to Sql format
    /// </summary>
    internal partial class TokenSqlConverter : ITokenConverter
    {
        public static TokenSqlConverter Instance => new TokenSqlConverter();

        public string ToSql(ICollection<Token> tokens, DataType? outputType, SqlTranslationOptions options)
        {
            var wrapBool = outputType == DataType.Boolean && tokens.Count > 1 &&
                           tokens.Any(t => t.Type == TokenType.Boolean) &&
                           tokens.Any(t => t.Type == TokenType.Function);

            tokens = ApplySqlRules(tokens.ToList(), options);

            var buffer = new StringBuilder();
            tokens.ForEachItem(token => ToSql(token, buffer, wrapBool, options.HasFlag(SqlTranslationOptions.UnicodeMarker)));

            return buffer.ToString();
        }

        public string ToSql(Token token, StringBuilder sql, bool wrapBoolExpression = false, bool addUnicodeMarker = false)
        {
            switch (token.Type)
            {
                case TokenType.CalculatedField:
                case TokenType.Column:
                    var column = token.Value as IColumn;
                    sql.Append($"[{column?.EntityId ?? 0}:{column?.ColumnId ?? 0}]");
                    break;

                case TokenType.ParenthesisOpen:
                case TokenType.ParenthesisClose:
                case TokenType.Unknown:
                case TokenType.Number:
                    sql.Append(token.Text);
                    break;

                case TokenType.Operator:
                    var @operator = token.Value as IFormulaOperator;
                    sql.Append($" {@operator?.SqlSymbol ?? @operator?.Symbol} ");
                    break;

                case TokenType.Boolean:
                    sql.Append(token.Value is bool b && b ? "1" : "0");
                    break;

                case TokenType.Function:

                    ConvertSimpleCaseToSearchedCase(token);

                    if (wrapBoolExpression && token.Value is IFunction func && func.ResultTypeId == DataType.Boolean)
                    {
                        var expr = FunctionToSql(token, new StringBuilder(), addUnicodeMarker);
                        if (ParseResult.IsBooleanExpression(expr, func.ResultTypeId)) expr = $"IIF({expr}, 1, 0)";
                        sql.Append(expr);
                    }
                    else FunctionToSql(token, sql, addUnicodeMarker);
                    break;

                case TokenType.Date:
                case TokenType.String:
                    var value = token.Text?.Replace("\"\"", "\"")?.Replace("'", "''");
                    var charLiteralMarker = addUnicodeMarker && ContainsUnicodeCharacter(value) ? "N" : string.Empty;
                    sql.Append($"{charLiteralMarker}'{value}'");
                    break;

                case TokenType.Comma:
                    // Ignore!
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Invalid token type: {token.Type}");
            }

            return sql.ToString();
        }

        /// <summary>
        /// Determines if a given FunctionToken represents an AVG function applied on an integer column
        /// </summary>
        private static bool IsAverageOverIntColumn(FunctionToken token)
        {
            if (FormulaParser.ToColumnAggregationType(token.Function.Name) != Contracts.Enums.ColumnAggregationTypeEnum.Avg || token.Arguments.Count != 1)
                return false;

            var argToken = token.Arguments[0].Tokens.FirstOrDefault(t => t.Type == TokenType.Column);
            return (argToken?.Value is IColumn column &&
                    column.ColumnDataType == ColumnDataType.Integer);
        }

        string ITokenConverter.Convert(ICollection<Token> tokens, DataType? outputType, params object[] args)
        => this.ToSql(tokens, outputType, SqlTranslationOptions.All);

        private string FunctionToSql(Token token, StringBuilder sql, bool addUnicodeMarker = false)
        {
            var func = (FunctionToken)token;
            var sqlExpr = ExpandTemplate(func.Function?.SqlExpression, func.Arguments.Count);
            var sqlFunc = sqlExpr ?? func.Function?.Name ?? token.Text;
            var hasParentheses = Regex.IsMatch(sqlFunc, "^.+?\\(.*?\\)$");
            var isSqlExpression = Regex.IsMatch(sqlExpr ?? string.Empty, "\\{\\d+\\}");
            var addParenthesis = !hasParentheses && !isSqlExpression;
            var args = new List<object>();
            var index = 0;
            var patternPlaceholders = Regex.Matches(sqlFunc, @"'[%]?(\{\d\})[%]?'").Cast<Match>().Select(m => m.Groups[1].Value); // e.g. {0} LIKE '%{1}%'

            sql.Append(isSqlExpression ? "" : sqlFunc).Append(addParenthesis ? "(" : "");

            foreach (var arg in func.Arguments)
            {
                var argValue = new StringBuilder();
                foreach (var argToken in arg.Tokens)
                    ToSql(argToken, argValue, addUnicodeMarker: addUnicodeMarker);

                argValue.Append(CompleteBooleanExpression(func.Function.Parameters.ElementAtOrDefault(index), arg));

                var value = argValue.ToString();
                if (patternPlaceholders.Contains($"{{{index}}}"))
                    value = IsSqlStringLiteral(value, out var strValue) ? strValue : $"' + {value} + '";

                args.Add(value);
                index++;
            }

            if (isSqlExpression && args.Count > 0)
                sql.AppendFormat(sqlFunc, args.ToArray());
            else
            {
                var strArgs = string.Join(",", args);
                if (IsAverageOverIntColumn(func)) strArgs = $"CAST({strArgs} AS FLOAT)";
                sql.Append(strArgs);
            }

            sql.Append(addParenthesis ? ")" : "");

            return sql.ToString();
        }

        /// <summary>
        /// 'Completes' an argument expression of a boolean function: e.g. IF(ISNUMBER(x), true, false) => IF(ISNUMBER(x) == 1, true, false)
        /// </summary>
        private string CompleteBooleanExpression(IParameter parameter, Argument arg)
        {
            if (parameter?.ValueTypeId == DataType.Boolean &&
                arg.Tokens.Count == 1 &&
                (IsBooleanValuedSqlFunction(arg.Tokens) || IsBooleanValued(arg)))
            {
                return " = 1";
            }

            return string.Empty;

            bool IsBooleanValuedSqlFunction(ICollection<Token> tokens)
            {
                return tokens.First().IsFunction(out var function) &&
                       function.ResultTypeId == DataType.Boolean &&
                       IsFunction(ToSql(tokens.First(), new StringBuilder()));
            }

            bool IsBooleanValued(Argument argument) =>
                !argument.Is(TokenType.Function) &&
                argument.ResultType.Primary == DataType.Boolean;
        }

        // Expands a templated Sql Expression  
        // Example: "CASE {0} [WHEN {i:1} THEN {i+1}] ELSE {n} END"        
        private static string ExpandTemplate(string sqlExpr, int count)
        {
            if (string.IsNullOrEmpty(sqlExpr) || !Regex.IsMatch(sqlExpr, @"{i(:\d+)?}"))
                return sqlExpr;

            // extract the starting index: e.g. {i:1} 
            var match = Regex.Match(sqlExpr, @"\{i:(\d+)\}");
            var startIndex = match.Success ? int.Parse(match.Groups[1].Value) : 0;

            // extract the index step value: e.g. {i+2} etc
            var matches = Regex.Matches(sqlExpr, @"\{i\+(\d+)\}");
            match = matches.Count > 0 ? matches[matches.Count - 1] : null;
            var step = match != null && int.TryParse(match.Groups[match.Groups.Count - 1].Value, out var num)
                ? num + 1
                : 1;

            // extract end index
            var endIndex = count - 1;
            if (Regex.IsMatch(sqlExpr, @"\{n\}")) endIndex--;

            // extract repeating section - i.e. the "template" - presumably inside square brackets [...]
            match = Regex.Match(sqlExpr, @"^(.*?)\[(.*?)\](.*)$");
            var groups = match.Groups;

            // [0]=entire text, [1]=left side, [2]=repeating group, [3]=right side
            var template = groups.Count >= 4 ? groups[2].Value : "";
            var sb = new StringBuilder();

            for (var index = startIndex; index <= endIndex; index += step)
            {
                var str = Regex.Replace(template, @"{i(:\d+)?}", $"{{{index}}}");
                for (var i = 1; i < step; i++)
                    str = str.Replace("{i+" + i + "}", $"{{{i + index}}}");

                sb.Append(str + " ");
            }

            sb.Insert(0, groups.Count > 1 ? groups[1].Value : "")
                .Append(groups.Count >= 4 ? groups[3].Value : "")
                .Replace("{n}", $"{{{count - 1}}}");

            return sb.ToString();
        }

        /// <summary>
        /// Applies certain Sql rules to the formula:
        /// <list  type="bullet">
        ///   <item> guarding against division by zero errors - wrap denominator in Sql <c>NULLIF()</c> function </item>
        ///   <item> [partial!] prevent null propagation in Arithmetic operations </item>
        /// </list>
        /// </summary>
        private static IList<Token> ApplySqlRules(IList<Token> tokens, SqlTranslationOptions options)
        {
            try
            {
                var replacement = new Token { Text = "0", Type = TokenType.Number, Value = 0 };
                
                // Apply decimal division
                if (options.HasFlag(SqlTranslationOptions.DecimalDivision) &&
                    tokens.Any(t => t.IsOperator(out var op) && op.Operator == FormulaOperator.Division))
                {
                    tokens = ConvertIntegerDivisionToDecimalDivision(tokens);
                }

                // prevent division by zero
                if (options.HasFlag(SqlTranslationOptions.DivisionByZero))
                {
                    var nullIf = new Function { Id = SPECIAL_FUNCTION_ID, Name = "NULLIF", ResultTypeId = DataType.Number };

                    tokens = PreventDivisionByZero(tokens,
                        token => token.IsOperator(op => op.Operator == FormulaOperator.Division),
                        argTokens => CreateFunctionToken(nullIf, argTokens, replacement));
                }

                //prevent null propagation
                if (options.HasFlag(SqlTranslationOptions.NullPropagation))
                {
                    var isNull = new Function { Id = SPECIAL_FUNCTION_ID, Name = "ISNULL", ResultTypeId = DataType.Number };

                    tokens = PreventNullPropagation(tokens, argTokens => CreateFunctionToken(isNull, argTokens, replacement));
                }
            }
            catch (Exception ex)
            {
                //TODO: why why ouch! please log error....
            }

            return tokens;
        }

        private static bool IsSqlStringLiteral(string value, out string stringValue)
        {
            var match = Regex.Match(value, "^[N]?'(.*)'$");
            stringValue = match.Success ? match.Groups[1].Value : null;
            return match.Success;
        }

        private static void ConvertSimpleCaseToSearchedCase(Token someToken)
        {
            // special handling for CASE function -> if the CASE function's condition is Bool then move 
            //   the condition to the value expression with 'True' value
            // e.g. 
            //   CASE([Training].[Training Hours] >= 1, true, "value1", "otherwise") => CASE WHEN [46:-56] >= 1 THEN "value1" ELSE "otherwise" END
            //   CASE([Training].[Training Hours] >= 1, false, "value1", "otherwise") => CASE WHEN NOT([46:-56] >= 1) THEN "value1" ELSE "otherwise" END
            //   CASE(true, [Training].[Training Hours] >= 1, "value1", "otherwise") => CASE WHEN [46:-56] >= 1 THEN "value1" ELSE "otherwise" END
            //   CASE(false , [Training].[Training Hours] >= 1, "value1", "otherwise") => CASE WHEN NOT([46:-56] >= 1) THEN "value1" ELSE "otherwise" END

            if (!(someToken is FunctionToken token &&
                token.Function.Name.Is(Constants.FUNC_CASE) &&
                token.Arguments.Count >= 4 /* 0=inputExpression, 1=value1, 2=result1, 3=elseResult */ &&
                token.Arguments.First().Is(DataType.Boolean) &&
                !token.Arguments.First().Is(TokenType.Function)))
            {
                return;
            }

            var inputExpression = token.Arguments.First();
            var inputExprFirstToken = inputExpression.Tokens.FirstOrDefault();
            var isInputExprBooleanLiteral = inputExpression.Tokens.Count() == 1 && inputExprFirstToken.Type == TokenType.Boolean;
            for (var i=1; i < token.Arguments.Count-1; i+=2)
            {
                var arg = token.Arguments[i];
                if ((isInputExprBooleanLiteral && (bool)inputExprFirstToken.Value) || (arg.Tokens.FirstOrDefault()?.Value is bool bln && bln))
                {
                    if (!isInputExprBooleanLiteral)
                        arg.Tokens = new List<Token>(inputExpression.Tokens);
                }
                else
                {
                    if (!isInputExprBooleanLiteral)
                        arg.Tokens = new List<Token>(inputExpression.Tokens);
                    arg.Tokens.Insert(0, new Token { Type = TokenType.Unknown, Text = "NOT" });
                    arg.Tokens.Insert(1, new Token { Type = TokenType.ParenthesisOpen, Text = "(" });
                    arg.Tokens.Add(new Token { Type = TokenType.ParenthesisClose, Text = ")" });
                }
            }

            // remove the inputExpression
            inputExpression.Tokens = new Token[0];
        }

        internal static bool IsFunction(string expr)
        {
            var functionMatches = Regex.Matches(expr.Trim(), @"^[A-Za-z]+\s*\(.+?\)$");
            return functionMatches.Count == 1 && functionMatches[0].Value == expr; // exactly one sql function call
        }

        public bool IsBooleanValuedSqlFunction(Token token)
        {
            return token.IsFunction(out var function) &&
                   function.ResultTypeId == DataType.Boolean &&
                   IsFunction(ToSql(token, new StringBuilder()));
        }

        private static bool ContainsUnicodeCharacter(string input)
        {
            const int MaxAnsiCode = 255;

            return input.Any(c => c > MaxAnsiCode);
        }
    }
}
