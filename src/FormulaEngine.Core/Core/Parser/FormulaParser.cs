using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Models;
using FormulaEngine.Core.Parser.Internal;
using FormulaEngine.Core.Validation;
using static System.String;

namespace FormulaEngine.Core.Parser
{
    /// <summary> Parses textual formulas and generates Sql, Xml and other equivalents </summary>
    public class FormulaParser : IFormulaParser
    {
        public async Task<ParseResult> Parse(string formula, IParserContext context) =>
            await ParseFormula(formula, context);

        public async Task<ParseResult> Parse(Formula formula, IParserContext context) =>
            await ParseFormula(formula, context);

        protected virtual async Task<ParseResult> ParseFormula(Formula formula, IParserContext context)
        {
            _ = formula ?? throw new ArgumentNullException(nameof(context));

            _ = context ?? throw new ArgumentNullException(nameof(context));

            if (!ItHasFormula(formula))
                throw new ArgumentException("Formula text to parse isn't provided!", nameof(formula.Text));

            try
            {
                var parseResult = await TryParseFormula(formula, context);
                var outputType = formula.OutputType ?? DataType.Any;

                if (parseResult.IsSuccess && 
                    outputType.IsNot(DataType.Any) &&
                    outputType.IsNot(parseResult.ResultType.ToGenericType() ?? 0))
                {
                    parseResult.Errors = new[] {TypeMismatchError(outputType, parseResult.ResultType)};
                }

                return parseResult;
            }
            catch (Exception exception)
            {
                return ParseResult.Error(exception);
            }
        }

        private bool ItHasFormula(Formula formula) => !IsNullOrEmpty(formula?.Text);

        protected virtual async Task<ParseResult> TryParseFormula(Formula formula, IParserContext context)
        {
            var tokenizer = new Tokenizer(formula, context);
            var tokenConverter = new TokenConverter();
            var validator = new TokenValidator();
            var referencedColumns = new List<IColumn>();
            IList<Token> tokens = new List<Token>();
            (bool hasNextToken, Token token) nextTokenResult;

            while ((nextTokenResult = await tokenizer.TryGetNextToken()).hasNextToken)
            {
                var token = nextTokenResult.token;
                if (token.Type == TokenType.Function)
                {
                    var result1 = await ConsolidateFunctionToken(token, tokenizer);
                    if (!result1.IsSuccess)
                        return result1;
                }

                var validationResult = await validator.ValidateAsync(token, context);
                if (validationResult != null && !validationResult.IsValid)
                    return ParseResult.Error(validationResult.Errors);

                tokens.Add(token);

                tokenConverter.GetReferencedColumns(token, referencedColumns);
            }

            var outputType = context.Settings.OutputType;
            AnalyzeTokens(tokens, outputType, context);

            var isValidExpr = TypeValidator.Instance.IsValidExpression(tokens, outputType, out var expressionType);
            
            if(isValidExpr) UpdateUnaryBooleanTokens(tokens, expressionType, context);
            
            var xml = isValidExpr && context.Settings.GenerateXml ? tokenConverter.ToXml(tokens) : (string)null;

            var options = UpdateOptions(context.Settings.SqlOptions, expressionType == DataType.Number && context.Settings.DecimalPoints > 0);
            var sql = isValidExpr && context.Settings.GenerateSql ? tokenConverter.ToSql(tokens, outputType ?? expressionType, options) : (string)null;

            var result = new ParseResult
            {
                ResultType = expressionType,
                Xml = xml,
                Sql = sql,
                ReferencedColumns = referencedColumns,
                Errors = !isValidExpr ? new[] { TypeMismatchError(outputType, expressionType) } : null,
                ContainsAggregateFunction = await ContainsAggregateFunction(tokens, context),
                AggregationType = await GetAggregationType(tokens, context),
                Tokens = tokens.ConvertAll()
            };

            return result;
        }

        private static async Task<ParseResult> ConsolidateFunctionToken(Token funcToken, Tokenizer tokenizer, int nesting = 0, string hierarchy = null)
        {
            var args = new Queue<Token>();
            var parenthesis = 0;
            (bool hasNextToken, Token token) nextTokenResult;
            hierarchy = hierarchy ?? funcToken.Text;

            while ((nextTokenResult = await tokenizer.TryGetNextToken()).hasNextToken)
            {
                var token = nextTokenResult.token;
                args.Enqueue(token);
                if (token.Type == TokenType.Function)
                {
                    var result = await ConsolidateFunctionToken(token, tokenizer, nesting + 1, $"{hierarchy}->{token.Text}");
                    if (!result.IsSuccess)
                        return result;
                }
                else if (token.Type == TokenType.ParenthesisOpen) parenthesis++;
                else if (token.Type == TokenType.ParenthesisClose && --parenthesis <= 0) break;
            }

            // validate function syntax: FUNC_NAME([Args])

            // 1. minimum two - at least opening and closing parenthesis
            if (args.Count < 2 || parenthesis != 0)
                return Error(ErrorCode.InvalidFunctionSyntax,
                    new {function = funcToken.Text, Index = funcToken.StartIndex});

            if (args.First().Type != TokenType.ParenthesisOpen || args.Last().Type != TokenType.ParenthesisClose)
                return Error(ErrorCode.MissingOpeningOrClosingParenthesis,
                    new {function = funcToken.Text, Index = funcToken.StartIndex});

            // remove/skip first and last parenthesis
            args = new Queue<Token>(args.Skip(1).Take(args.Count - 2));
            var functionArgs = new List<Argument>();

            while (args.Any())
            {
                var tokens = new List<Token>();
                Token arg;
                while (args.Any() && (arg = args.Dequeue()) != null && (arg.Type != TokenType.Comma || parenthesis != 0))
                {
                    if (arg.Type == TokenType.ParenthesisOpen) parenthesis++;
                    else if (arg.Type == TokenType.ParenthesisClose) parenthesis--;

                    tokens.Add(arg);
                }

                functionArgs.Add(new Argument {Tokens = new List<Token>(tokens)});
            }

            var theToken = (FunctionToken) funcToken;
            theToken.Arguments = new List<Argument>(functionArgs);
            theToken.Level = nesting;
            theToken.NestingHierarchy = hierarchy;

            return true;
        }

        private static async Task<bool> ContainsAggregateFunction(ICollection<Token> tokens, IParserContext context)
        {
            // Aggregate functions are TOP level functions (not nested) 
            // so it's enough to check the top level tokens only
            foreach (var t in tokens)
            {
                if ((t.IsFunction(out var func) && func.CategoryId == FunctionCategory.Aggregate) ||
                    (t.IsColumn(out var column) && column.CalculatedFieldId != null && (await context.GetCalculatedField(column.CalculatedFieldId.Value))?.IsAggregate == true))
                    return true;
            }
            return false;
        }
        
        private static ParseError TypeMismatchError(DataType? expectedType, DataType? actualType)
        {
            if (expectedType == null || expectedType == DataType.Any || actualType == null)
                return new ParseError(ErrorCode.InvalidExpression);

            return new ParseError(ErrorCode.ExpectedOutputTypeNotFulfilled,
                new
                {
                    expectedType = expectedType?.ToString(),
                    suggestedOutputType = actualType?.ToString()
                };
        }

        private static void AnalyzeTokens(ICollection<Token> tokens, DataType? outputType, IParserContext context)
        {
            foreach(var token in tokens)
            {
                if (token is FunctionToken ft)
                {
                    TryConvertSimpleCaseFunctionToIf(ft, context);
                    ft.Arguments.ForEachItem(a => AnalyzeTokens(a.Tokens, outputType, context));
                }
                else if (outputType == DataType.Boolean &&
                    token.Type == TokenType.String &&
                    token.SecondaryType == TokenType.Boolean &&
                    context.IsBoolean(token.Text, out var value))
                {
                    token.SetSecondaryTypeAsPrimary();
                    token.Value = value;
                }
            }
        }

        private static async Task<AggregationType?> GetAggregationType(ICollection<Token> tokens, IParserContext context)
        {
            if (tokens.Count != 1)
                return null;

            var token = tokens.First(); // we care about simple top level aggregations

            if (token.IsFunction(out var func) && func.CategoryId == FunctionCategory.Aggregate)
                return ToColumnAggregationType(func.Name);

            if (token.IsColumn(out var column) && column.CalculatedFieldId != null)
                return (await context.GetCalculatedField(column.CalculatedFieldId.Value))?.AggregationTypeId;

            return null;
        }

        internal static AggregationType? ToColumnAggregationType(string function)
        {
            if (string.IsNullOrEmpty(function))
                return null;

            return function.ToUpper() switch
            {
                "GSUM" => AggregationType.Sum,
                "GAVG" => AggregationType.Avg,
                "GCOUNT" => AggregationType.Count,
                "GCOUNTUNIQUE" => AggregationType.CountUnique,
                "GMIN" => AggregationType.Min,
                "GMAX" => AggregationType.Max,
                _ => null,
            };
        }

        /// <summary>
        /// Convert a simple CASE function with boolean check expression in to an IF.
        /// <code>
        /// e.g. CASE(bool_expr, value1, result1, elseResult) ==> 
        ///     if value1 is 'True': IF(bool_expr, result1, elseResult) 
        ///     otherwise: IF(bool_expr, elseResult, result1) 
        /// </code> 
        /// </summary>
        private static void TryConvertSimpleCaseFunctionToIf(FunctionToken token, IParserContext context)
        {
            if (token.Function.Name.Is(Constants.FUNC_CASE) &&
                token.Arguments.Count == 4 &&
                token.Arguments.First().Is(DataType.Boolean) &&
                !token.Arguments.First().Is(TokenType.Function))
            {
                token.Function = context.GetFunction(Constants.FUNC_IF).GetAwaiter().GetResult();
                if (token.Arguments[1].Tokens.FirstOrDefault()?.Value is bool bln && bln)
                {
                    token.Arguments.RemoveAt(1);
                }
                else
                {
                    token.Arguments[1] = token.Arguments[3];
                    token.Arguments.RemoveAt(3);
                }
            }
        }

        private void UpdateUnaryBooleanTokens(IList<Token> tokens, DataType? outputType,
            IParserContext context, Token currentToken = null)
        {
            var isBooleanResult = outputType == DataType.Boolean;
            if (!isBooleanResult || tokens.Count <= 1) return;

            var token = currentToken ?? TypeValidator.Instance.GetExpressionOutputType(tokens)?.SourceToken;
            if (token is BinaryToken binaryToken && binaryToken.Operator.IsLogical())
            {
                UpdateUnaryBooleanTokens(tokens, outputType, context, binaryToken.LeftToken);
                UpdateUnaryBooleanTokens(tokens, outputType, context, binaryToken.RightToken);
            }

            if (token.IsUnaryBooleanExpression())
            {
                var index = tokens.IndexOf(token);
                var newToken = new Token
                {
                    Type = TokenType.Operator,
                    Text = "=",
                    Value = context.GetOperator(FormulaOperator.Equals)
                };
                tokens.Insert(index + 1, newToken);

                index = tokens.IndexOf(newToken);
                newToken = new Token
                {
                    Type = TokenType.Number,
                    Text = "1",
                    Value = 1
                };
                tokens.Insert(index + 1, newToken);
            }
        }

        private SqlTranslationOptions UpdateOptions(SqlTranslationOptions options, bool addDecimalDivision) =>
                !addDecimalDivision && options.HasFlag(SqlTranslationOptions.DecimalDivision) ? (options & ~SqlTranslationOptions.DecimalDivision) : options;
    }
}