using System.Threading.Tasks;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Parser.Internal;

namespace FormulaEngine.Core.Validation
{
    internal class TokenValidator
    {
        public virtual async Task<ValidationResult> ValidateAsync(Token token, IParserContext context)
        {
            return await ValidateAsync(token, context, null);
        }

        public async Task<ValidationResult> ValidateAsync(Token token, IParserContext context, IValueDomain knownValues)
        {
            switch (token.Type)
            {
                case TokenType.ParenthesisOpen:
                case TokenType.ParenthesisClose:
                case TokenType.String:
                case TokenType.Date:
                case TokenType.Boolean:
                case TokenType.Operator:
                case TokenType.Number:
                    return null; // assume valid 

                case TokenType.Function:
                    return await FunctionValidator.Instance.ValidateAsync(token, context);

                case TokenType.Column:
                case TokenType.CalculatedField:
                    return await new ColumnValidator(token, context).ValidateAsync(token.Value as IColumn);

                case TokenType.Unknown:
                    return knownValues?.IsValidValue(token, null) == true
                        ? null
                        : Error(ErrorCode.UnexpectedToken, new {token = token.Text, Index = token.StartIndex});

                default:
                    return Error(ErrorCode.UnexpectedToken, new {token = token.Text, Index = token.StartIndex});
            }
        }

        protected static ParseError Error(string message, object data = null) =>
            new ParseError(message, data);
    }
}