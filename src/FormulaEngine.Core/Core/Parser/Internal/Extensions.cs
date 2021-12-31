using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Models;
using FormulaEngine.Core.Validation;
using FormulaOperator = FormulaEngine.Core.Enums.FormulaOperator;

namespace FormulaEngine.Core.Parser.Internal
{
    internal static class Extensions
    {
        public static bool IsLiteral(this Token token) =>
            token.Type == TokenType.String ||
            token.Type == TokenType.Date ||
            token.Type == TokenType.Boolean ||
            token.Type == TokenType.Number ||
            token.Type == TokenType.Unknown;

        public static string ParamName(this IParameter parameter)
        {
            return !string.IsNullOrEmpty(parameter.Name) ? parameter.Name : $"{parameter.Index}";
        }

        public static void ForEachItem<T>(this IEnumerable<T> collection, Action<T> action)
        {
            if (collection == null)
                return;

            foreach (var item in collection)
                action(item);
        }

        internal static bool IsCalculatedField(this IColumn column) =>
            column.ColumnDataType == ColumnDataType.CalculatedField &&
            column.CalculatedFieldId.GetValueOrDefault() > 0;

        internal static bool IsColumn(this Token token) =>
            token.Type == TokenType.Column || token.Type == TokenType.CalculatedField;

        internal static bool IsOperator(this Token token, Predicate<IFormulaOperator> predicate)
        {
            return token.Type == TokenType.Operator && token.Value is IFormulaOperator op && predicate(op);
        }

        internal static bool IsColumn(this Token token, out IColumn column)
        {
            column = (token.Type == TokenType.Column || token.Type == TokenType.CalculatedField) ? token.Value as IColumn : null;
            return column != null;
        }

        internal static bool IsFunction(this Token token, out IFunction function)
        {
            function = (token.Type == TokenType.Function) ? token.Value as IFunction : null;
            return function != null;
        }

        internal static bool IsOperator(this Token token, out IFormulaOperator @operator)
        {
            @operator = (token.Type == TokenType.Operator) ? token.Value as IFormulaOperator : null;
            return @operator != null;
        }

        public static DataType? ToGenericType(this Token token, TokenType? tokenType = null)
        {
            if (token == null)
                return null;

            switch (tokenType ?? token.Type)
            {
                case TokenType.Boolean:
                    return DataType.Boolean;

                case TokenType.Column:
                case TokenType.CalculatedField:
                    return token.Value is IColumn column
                        ? column.CalculatedFieldType ?? ToGenericType(column.ColumnDisplayType, column.ColumnDataType)
                        : (DataType?)null;

                case TokenType.Comma:
                case TokenType.Unknown:
                case TokenType.ParenthesisOpen:
                case TokenType.ParenthesisClose:
                case TokenType.Operator:
                    return null;

                case TokenType.Date:
                    return DataType.AbsoluteDatetime;

                case TokenType.Function:
                    return (token.Value is IFunction func) ? func.ResultTypeId : (DataType?)null;

                case TokenType.Number:
                    return DataType.Number;

                case TokenType.String:
                    return DataType.String;

                default:
                    throw new ArgumentOutOfRangeException($"Invalid or unsupported token type: {token}");
            }
        }

        public static bool TryGetKnownValues(this IParameter parameter, out IValueDomain knownValues)
        {
            knownValues = ParameterKnownValues.GetKnownValues(parameter);
            return knownValues != null;
        }

        internal static OperatorKind OperatorType(this IFormulaOperator @operator)
        {
            var attribute = typeof(FormulaOperator).GetField(@operator.Operator.ToString())
                .GetCustomAttribute<FormulaOperatorAttribute>();
            return attribute.OperatorKind;
        }

        public static bool IsStartOfExpressionGroup(this Token token) =>
            token == null || token.Type == TokenType.Operator || token.Type == TokenType.Comma ||
            token.Type == TokenType.ParenthesisOpen;

        internal static DataType ToGenericType(this ColumnDisplayTypeEnum columnDisplayType, ColumnDataType columnDataType)
        {
            switch (columnDisplayType)
            {
                case ColumnDisplayTypeEnum.String:
                    // this handles the case of custom fields - 
                    //  which are for instance DataType=Decimal or other type, but whose DisplayType is String
                    return columnDataType == ColumnDataType.Decimal ? DataType.Number : DataType.String;

                case ColumnDisplayTypeEnum.BooleanBit:
                case ColumnDisplayTypeEnum.BooleanYesNo:
                case ColumnDisplayTypeEnum.BooleanTrueFalse:
                    return DataType.Boolean;

                case ColumnDisplayTypeEnum.Integer:
                case ColumnDisplayTypeEnum.Currency:
                case ColumnDisplayTypeEnum.Decimal:
                case ColumnDisplayTypeEnum.CompensationPercentage:
                case ColumnDisplayTypeEnum.AnnualEquivalents:
                case ColumnDisplayTypeEnum.EmployeeWageType:
                case ColumnDisplayTypeEnum.NonMonetary:                
                    return DataType.Number;

                case ColumnDisplayTypeEnum.DateTime:
                case ColumnDisplayTypeEnum.Time:
                    return DataType.Datetime;

                case ColumnDisplayTypeEnum.Date:
                    return DataType.Date;

                case ColumnDisplayTypeEnum.AbsoluteDateTime:
                    return DataType.AbsoluteDatetime;

                case ColumnDisplayTypeEnum.AbsoluteDate:
                    return DataType.AbsoluteDate;

                default:
                    return DataType.String; // should be Any type?
            }
        }

        internal static IEnumerable<object> ConvertAll(this ICollection<Token> tokens)
        {
            Func<Token, string> type2 = (t) => (t.SecondaryType != null && t.SecondaryType != t.Type ? $"|{t.SecondaryType.ToString()}" : "");
            return tokens.Select(t => new { t.Text, Type = t.Type.ToString() + type2(t), Index = t.StartIndex, Value = t.ConvertValue() });
        }

        internal static object ConvertValue(this Token token)
        {
            switch (token.Type)
            {
                case TokenType.Function:
                    var func = token as FunctionToken;
                    if (token.Value is IFunction f)
                        return new
                        {
                            Function = new
                            {
                                func.Level,
                                f.Id,
                                f.Name,
                                Arguments = func.Arguments.Select((a, i) => new { Index = i, Tokens = a.Tokens.ConvertAll() })
                            }
                        };
                    break;
                case TokenType.Operator:
                    if (token.Value is IFormulaOperator op)
                        return op.Symbol;
                    break;

                case TokenType.Column:
                    if (token.Value is IColumn c)
                        return new { Column = new { c.ColumnId, c.EntityId, c.CalculatedFieldId } };
                    break;
            }

            return token.Value;
        }

        internal static bool IsExpectedType(this ExpressionType actual, ExpressionType expected, out DataType? actualType)
        {
            actualType = actual?.Primary;
            return (expected?.Primary == null || expected.Primary == DataType.Any) ||
                  (true == actual?.IsValid(expected, out actualType));
        }

        internal static ExpressionType ResultType(this FunctionToken token)
            => token.Value is IFunction func ? ToExpressionType(func.ResultTypeId, token) : null;

        internal static ExpressionType ValueType(this IParameter parameter, FunctionToken token)
            => ToExpressionType(parameter.ValueTypeId, token);

        internal static ExpressionType ToExpressionType(DataType type, FunctionToken token)
        {
            if (type > DataType.Arg1 || type < DataType.Arg3)
                return type;
            var argument = token.Arguments.ElementAtOrDefault(Math.Abs((int)type) - 1);
            return argument?.ResultType;
        }

        internal static DataType? ToGenericType(this DataType? type)
            => type <= DataType.AbsoluteDatetime ? DataType.Datetime : type;

        public static Token SetSecondaryTypeAsPrimary(this Token token)
        {
            if (token?.SecondaryType != null)
            {
                var type1 = token.Type;
                token.Type = token.SecondaryType.Value;
                token.SecondaryType = type1;
            }

            return token;
        }

        public static bool IsBoolean(this IParserContext context, string token, out bool value)
        {
            value = false;
            if (string.Equals(token, context.BooleanYesText, StringComparison.OrdinalIgnoreCase))
                return value = true;

            if (string.Equals(token, context.BooleanNoText, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        internal static bool Is(this Argument arg, TokenType type) => arg?.Tokens?.Count == 1 && arg.Tokens[0].Type == type;

        internal static bool Is(this string str, string other) => string.Compare(str, other, true) == 0;

        internal static IFunction Function(this Token token) => token is FunctionToken f ? f.Function : (IFunction)null;

        internal static bool Is(this Argument arg, DataType type) => arg?.ResultType?.Primary == type || arg?.ResultType?.Secondary == type;

        internal static bool IsOperator(this IParserContext context, string @operatorText, out IFormulaOperator @operator)
            => ((ParserContext)context).IsOperator(operatorText, out @operator);
        
        internal static bool IsBoolean(this IColumn col) => col.ColumnDataType == ColumnDataType.BooleanBit ||
                                                                col.ColumnDataType == ColumnDataType.BooleanYesNo;

        internal static bool IsUnaryBooleanExpression(this Token token)
        {
            return IsBooleanValuedSqlFunction() || IsBooleanLiteralOrColumn();

            bool IsBooleanValuedSqlFunction() =>
                TokenConverters.TokenSqlConverter.Instance.IsBooleanValuedSqlFunction(token);

            bool IsBooleanLiteralOrColumn() => /*token.Type == TokenType.Boolean ||
                                               token.SecondaryType == TokenType.Boolean ||*/
                                               (token.IsColumn(out var col) && col.IsBoolean());
        }

        internal static IFormulaOperator GetOperator(this IParserContext context, FormulaOperator operatorType) =>
            context.Operators.FirstOrDefault(o => o.Operator == operatorType);

        internal static bool IsLogical(this IFormulaOperator @operator) =>
            @operator.OperatorType() == OperatorKind.Logical;
    }
}
