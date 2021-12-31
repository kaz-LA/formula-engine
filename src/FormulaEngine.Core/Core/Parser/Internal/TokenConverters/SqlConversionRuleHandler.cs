using System;
using System.Collections.Generic;
using System.Linq;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;
using static System.Text.RegularExpressions.Regex;

namespace FormulaEngine.Core.Parser.Internal.TokenConverters
{
    internal partial class TokenSqlConverter
    {
        private const int SPECIAL_FUNCTION_ID = -999;

        private static Token CreateFunctionToken(IFunction function, params object[] argTokens)
        {
            var args = argTokens.Select(arg => new Argument { Tokens = Tokens(arg) }).ToList();
            return new FunctionToken { Function = function, Value = function, Arguments = args, Text = function.Name };

            IList<Token> Tokens(object tokenOrTokens)
            {
                if (tokenOrTokens is IList<Token> tokens) return tokens;
                if (tokenOrTokens is Token token) return new[] { token };

                throw new ArgumentException($"Unexpected value for {nameof(tokenOrTokens)}: {tokenOrTokens?.GetType()}");
            }
        }

        private static IList<Token> PreventDivisionByZero(IList<Token> tokens, Predicate<Token> predicate, Func<IList<Token>, Token> wrap)
        {
            var index = tokens.Count;
            var result = new List<Token>();

            while (--index >= 0)
            {
                var token = tokens[index];
                if (token.Type == TokenType.Function)
                {
                    // recursively process each argument's tokens
                    ((FunctionToken)token).Arguments.ForEachItem(arg =>
                        arg.Tokens = PreventDivisionByZero(arg.Tokens, predicate, wrap));
                }

                if (predicate(token))
                {
                    var denominator = GetOperand(result, 0).ToList();

                    // check if all the tokens are numeric literals - and skip
                    if (!AreAllNumericLiterals(denominator))
                    {
                        result.RemoveRange(0, denominator.Count);
                        result.Insert(0, wrap(denominator));
                    }
                }

                result.Insert(0, token);
            }

            return result;
        }

        private static IList<Token> PreventNullPropagation(IList<Token> tokens, Func<IList<Token>, Token> wrap)
        {
            var index = -1; // intentional!
            var result = new List<Token>();

            while (++index < tokens.Count)
            {
                var token = tokens[index];
                if (token.Type == TokenType.Function)
                {
                    // recursively process each argument's tokens
                    ((FunctionToken)token).Arguments.ForEachItem(arg => arg.Tokens = PreventNullPropagation(arg.Tokens, wrap));
                }

                if (token.IsOperator(out var op) &&
                    (op.Operator == FormulaOperator.Addition || op.Operator == FormulaOperator.Subtraction))
                {
                    IList<Token> operand = GetOperandReverse(result, result.Count - 1, op).ToList();
                    if (!AreAllNumericLiterals(operand) && !IsDateTimeColumn(operand))
                    {
                        result.RemoveRange(result.Count - operand.Count, operand.Count);
                        result.Add(wrap(operand.Reverse().ToList()));
                    }

                    result.Add(token);

                    operand = GetOperandForward(tokens, index + 1, op).ToList();
                    if (!AreAllNumericLiterals(operand) && !IsDateTimeColumn(operand))
                    {
                        result.Add(wrap(PreventNullPropagation(operand, wrap)));
                        index += operand.Count;
                    }

                    continue;
                }

                result.Add(token);
            }

            return result;
        }

        private static IEnumerable<Token> GetOperandForward(IList<Token> tokens, int index, IFormulaOperator @operator)
        {
            var openParenthesis = 0;
            var precedence = SqlPrecedenceLevel(@operator);

            do
            {
                var token = tokens[index];
                openParenthesis += ParenthesisType(token);

                if (openParenthesis == -1 || (openParenthesis == 0 && token.IsOperator(out var op) && SqlPrecedenceLevel(op) >= precedence))
                    yield break;

                yield return token;

            } while (++index < tokens.Count);
        }

        private static IEnumerable<Token> GetOperandReverse(IList<Token> tokens, int index, IFormulaOperator @operator)
        {
            var openParenthesis = 0;
            var precedence = SqlPrecedenceLevel(@operator);

            do
            {
                var token = tokens[index];
                openParenthesis += ParenthesisType(token);

                if (openParenthesis == 1 || (openParenthesis == 0 && token.IsOperator(out var op) && SqlPrecedenceLevel(op) >= precedence))
                    yield break;

                yield return token;

            } while (--index >= 0);
        }

        private static IEnumerable<Token> GetOperand(IList<Token> tokens, int index = 0)
        {
            var openParenthesis = 0;

            do
            {
                var token = tokens[index];
                openParenthesis += ParenthesisType(token);

                yield return token;

            } while (openParenthesis != 0 && ++index < tokens.Count);
        }

        private static int ParenthesisType(Token token)
                => (token.Type == TokenType.ParenthesisOpen) ? 1 : ((token.Type == TokenType.ParenthesisClose) ? -1 : 0);

        private static bool AreAllNumericLiterals(IEnumerable<Token> tokens, bool treatSpecialWrapperFunctionAsLiteral = true)
        {
            return tokens.All(token => token.Type == TokenType.Operator ||
                                    token.Type == TokenType.ParenthesisOpen ||
                                    token.Type == TokenType.ParenthesisClose ||
                                    token.Type == TokenType.Number ||
                                    (!treatSpecialWrapperFunctionAsLiteral || (token.IsFunction(out var f) && f.Id == SPECIAL_FUNCTION_ID)));
        }

        private static int SqlPrecedenceLevel(IFormulaOperator op)
        {
            // t-sql operator precedence
            // https://docs.microsoft.com/en-us/sql/t-sql/language-elements/operator-precedence-transact-sql?view=sql-server-ver15

            if (op.OperatorType() == OperatorKind.Relational)
                return 4;

            switch (op.Operator)
            {
                case FormulaOperator.Multiplication:
                case FormulaOperator.Division:
                    return 2;
                case FormulaOperator.Addition:
                case FormulaOperator.Concat:
                case FormulaOperator.Subtraction:
                    return 3;
                case FormulaOperator.And:
                    return 6;
                case FormulaOperator.Or:
                    return 7;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid operator type: {op.Operator.ToString()}");
            }
        }

        private static bool IsDateTimeColumn(ICollection<Token> tokens)
        {
            return (tokens.Count == 1 &&
                    tokens.First().IsColumn(out var column) &&
                    (column.ColumnDataType == ColumnDataType.Date ||
                        column.ColumnDataType == ColumnDataType.DateTime)
                    );
        }

        /// <summary>
        /// converts whole number (integer) divisions to decimal division.
        /// Example:  
        ///     "100 / Training.some_int_column" ==> "100.0 / Training.some_int_column"
        ///     "int_expr1 / int_expr2" ==> "int_expr1 x 1.0 / int_expr2"
        /// </summary>
        /// <returns></returns>
        private static IList<Token> ConvertIntegerDivisionToDecimalDivision(IList<Token> tokens, bool recursive = false)
        {
            var index = -1; // intentional!
            var result = new List<Token>();

            while (++index < tokens.Count)
            {
                var token = tokens[index];
                if (recursive && token.Type == TokenType.Function)
                {
                    // recursively process each argument's tokens
                    ((FunctionToken)token).Arguments.ForEachItem(arg => arg.Tokens = ConvertIntegerDivisionToDecimalDivision(arg.Tokens, recursive));
                }

                if (!(token.IsOperator(out var op) && op.Operator == FormulaOperator.Division))
                {
                    result.Add(token);
                    continue;
                }

                var operand = GetOperandReverse(result, result.Count - 1, op).ToList();
                var denominator = GetOperand(tokens, index + 1).ToList();

                if (!(IsIntegral(operand) && IsIntegral(denominator)))
                {
                    result.Add(token);
                    continue;
                }

                // if either one of the first operand or the denominator is just a numeric/integer literal - then make it a decimal and continue
                var literalToken = (operand.Count == 1 && IsWholeNumberLiteral(operand.First()))
                                ? operand.First()
                                : ((denominator.Count == 1 && IsWholeNumberLiteral(denominator.First())) ? denominator.First() : null);

                if (literalToken != null)
                    literalToken.Text += ".0"; // make it decimal
                else
                {
                    if (operand.Count > 1)
                    {
                        result.Insert(result.Count - operand.Count, new Token { Type = TokenType.ParenthesisOpen, Text = "(" });
                        result.Add(new Token { Type = TokenType.ParenthesisClose, Text = ")" });
                    }
                    // simply multiply the first operand by 1.0 to make it's result a 'decimal' value
                    result.Add(new Token { Type = TokenType.Operator, Text = "*", Value = Models.Operator.GetOperator(FormulaOperator.Multiplication) }); // multiply
                    result.Add(new Token { Type = TokenType.Number, Text = "1.0", Value = 1.0m }); // multiply
                }

                result.Add(token);
                result.AddRange(denominator);
                index += denominator.Count;  // we've already processed the next few token(s)
            }

            return result;
        }

        /// <summary>
        /// Determines if a given sequence of tokens represents an Integral (Integer) valued expression
        /// </summary>
        private static bool IsIntegral(IList<Token> tokens) =>
            tokens.All(t => !(t.Type == TokenType.Number || t.Type == TokenType.Column || t.Type == TokenType.Function) || IsIntegral(t));

        /// <summary>
        /// Determines if a given token represents an Integral (Integer) valued expression
        /// </summary>
        private static bool IsIntegral(Token t)
        {
            return (t is FunctionToken ft && ft.Function.ResultTypeId == DataType.Number) ||
                   IsWholeNumberLiteral(t) ||
                   (t.Type == TokenType.Column && ((IColumn)t.Value).ColumnDataType == ColumnDataType.Integer);
        }

        private static bool IsWholeNumberLiteral(Token t) => t.Type == TokenType.Number && IsMatch(t.Text.Trim(), @"^\d+$");
    }
}
