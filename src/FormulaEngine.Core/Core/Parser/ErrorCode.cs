using System.Collections.Generic;

namespace FormulaEngine.Core.Parser
{
    public enum ErrorCode
    {
        OtherError,
        InvalidExpression,
        InvalidFunctionSyntax,
        MissingOpeningOrClosingParenthesis,
        UnexpectedToken,
        UnknownColumnOrCalculatedField,
        UnknownFunction,
        MissingValueForRequiredParameter,
        TooManyArguments,
        ValueIsOutsideRangeOfValidValuesForParameter,
        InvalidArgumentOrExpression,
        MaxFunctionNestingExceeded,
        InvalidCalculatedFieldUsage,
        MinimumNumberOfArgs,
        ExpectedOutputTypeNotFulfilled,
        InActiveColumn,
        UnSelectableColumn,
        CantUseDeletedCalculatedField,
        PrivateCalculatedField,
        FunctionCantBeNested
    }

    public static class ErrorHelper
    {
        private static readonly IDictionary<ErrorCode, string> ErrorMessages = new Dictionary<ErrorCode, string>
        {
            [ErrorCode.OtherError] = "Error occurred while parsing!",
            [ErrorCode.InvalidExpression] = "Invalid formula!",
            [ErrorCode.InvalidFunctionSyntax] = "Invalid function syntax",
            [ErrorCode.MissingOpeningOrClosingParenthesis] = "Missing Opening or Closing Parenthesis",
            [ErrorCode.UnexpectedToken] = "Unexpected Token",
            [ErrorCode.UnknownColumnOrCalculatedField] = "Unknown Column or Calculated Field",
            [ErrorCode.UnknownFunction] = "Unknown Function",
            [ErrorCode.MissingValueForRequiredParameter] = "Missing Value for a Required Parameter",
            [ErrorCode.TooManyArguments] = "Too Many Arguments",
            [ErrorCode.ValueIsOutsideRangeOfValidValuesForParameter] = "Value is Outside Range Of Valid Values for a Parameter",
            [ErrorCode.InvalidArgumentOrExpression] = "Invalid Argument or Expression",
            [ErrorCode.MaxFunctionNestingExceeded] = "Maximum Function Nesting has exceeded",
            [ErrorCode.InvalidCalculatedFieldUsage] = "Invalid Calculated Field Usage",
            [ErrorCode.MinimumNumberOfArgs] = "Minimum number of arguments",
            [ErrorCode.ExpectedOutputTypeNotFulfilled] = "Expected Output Type not Fulfilled",
            [ErrorCode.InActiveColumn] = "In-active Column",
            [ErrorCode.UnSelectableColumn] = "Un-selectable column",
            [ErrorCode.CantUseDeletedCalculatedField] = "Deleted Calculated Field can't be used",
            [ErrorCode.PrivateCalculatedField] = "Private Calculated Field",
            [ErrorCode.FunctionCantBeNested] = "Function doesn't support nesting under another function",
        };

        public static string GetMessage(this ErrorCode errorCode, params object[] args)
        {
            if (ErrorMessages.TryGetValue(errorCode, out var message))
            {
                return args?.Length > 0 ? string.Format(message, args) : message;
            }

            return errorCode.ToString();
        }
    }
}
