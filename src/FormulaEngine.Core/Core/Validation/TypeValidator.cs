using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Parser.Internal;

namespace FormulaEngine.Core.Validation
{
    /// <summary>
    /// Validates the overall data type of an expression represented as a sequence of Tokens
    /// by evaluating the data type of each Token and subsequent binary operations
    /// </summary>
    internal class TypeValidator : TokenValidator
    {
        private static IDictionary<string, DataType> BinaryExpressionRules;

        static TypeValidator()
        {
            BinaryExpressionRules = new Dictionary<string, DataType>
            {
                [Key(DataType.Number, OperatorKind.Arithmetic, DataType.Number)] = DataType.Number,
                [Key(DataType.Number, OperatorKind.Relational, DataType.Number)] = DataType.Boolean,

                [Key(DataType.String, FormulaOperator.Addition, DataType.String)] = DataType.String,
                [Key(DataType.String, FormulaOperator.Concat, DataType.String)] = DataType.String,
                [Key(DataType.String, OperatorKind.Relational, DataType.String)] = DataType.Boolean,

                [Key(DataType.Number, FormulaOperator.Addition, DataType.Datetime)] = DataType.Datetime,
                [Key(DataType.Datetime, FormulaOperator.Addition, DataType.Number)] = DataType.Datetime,
                [Key(DataType.Datetime, FormulaOperator.Subtraction, DataType.Number)] = DataType.Datetime,
                [Key(DataType.Datetime, OperatorKind.Relational, DataType.Datetime)] = DataType.Boolean,

                [Key(DataType.Boolean, OperatorKind.Relational, DataType.Boolean)] = DataType.Boolean,
                [Key(DataType.Boolean, OperatorKind.Logical, DataType.Boolean)] = DataType.Boolean
            };
        }

        public static TypeValidator Instance = new TypeValidator();

        public override async Task<ValidationResult> ValidateAsync(Token token, IParserContext context)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Validates the data type of each of the operations, and the over all type of the result 
        /// in an expression represented using a sequence of Tokens
        /// </summary>
        /// <param name="tokens">List of tokens representing an expression or formula</param>
        /// <returns></returns>
        public bool IsValidExpression(IList<Token> tokens, ExpressionType expectedType, out DataType? actualType)
        {
            actualType = null;
            var type = GetExpressionType(tokens);
            return type.Parenthesis == 0 && type.Type.IsExpectedType(expectedType, out actualType);
        }

        public ExpressionType GetExpressionOutputType(IList<Token> tokens) => GetExpressionType(tokens).Type;

        private (ExpressionType Type, int Index, int Parenthesis) GetExpressionType(IList<Token> tokens, int start = 0,
            int parenthesis = 0, IFormulaOperator previousOperator = null)
        {
            ExpressionType left = null;
            IFormulaOperator @operator = null;
            var index = start;

            while (index < tokens.Count)
            {
                var token = tokens[index];
                if (token.Type == TokenType.ParenthesisClose)
                {
                    parenthesis--;
                    break;
                }

                ExpressionType right = null;
                var isFirst = index == start;
                var increment = 1;

                switch (token.Type)
                {
                    case TokenType.Operator:
                        if (@operator != null || !(token.Value is IFormulaOperator op))
                            return (null, index, parenthesis);

                        if (previousOperator != null && op.OperatorType() == OperatorKind.Logical)
                            return (left, index, parenthesis);

                        @operator = op;
                        if (@operator.OperatorType() != OperatorKind.Arithmetic)
                        {
                            (right, index, parenthesis) = GetExpressionType(tokens, index + 1, parenthesis, @op);
                            increment = 0; // current operator should be processed!
                        }

                        break;

                    case TokenType.ParenthesisOpen:
                        (right, index, parenthesis) = GetExpressionType(tokens, index + 1, parenthesis + 1);
                        break;
                    default:
                        right = token;
                        break;
                }

                if (isFirst) left = right; // very first iteration ?
                else if (right != null)
                {
                    left = EvaluateBinaryExpression(left, @operator, right);
                    @operator = null;
                }

                index += increment;
            }

            return (@operator == null ? left : null, index, parenthesis);
        }

        private ExpressionType EvaluateBinaryExpression(ExpressionType left, IFormulaOperator @operator, ExpressionType right)
        {
            var type = EvaluateBinaryExpression(left.Primary, @operator, right.Primary);

            // some tokens have a Primary and a secondary type. 
            // For example:
            //      a string literal that represents DateTime value
            //      a string literal that represents a boolean value: "yes", "no" etc
            if (type == null && (left.HasSecondaryType || right.HasSecondaryType))
            {
                type = EvaluateBinaryExpression(left.Secondary ?? left.Primary, @operator, right.Secondary ?? right.Primary);
                if(type != null)
                {
                    left.SourceToken.SetSecondaryTypeAsPrimary();
                    right.SourceToken.SetSecondaryTypeAsPrimary();
                }
            }

            if (type != null)
                return new ExpressionType {Primary = type, SourceToken = new BinaryToken(left, @operator, right)};

            return type;
        }

        private DataType? EvaluateBinaryExpression(DataType? left, IFormulaOperator @operator,
            DataType? right)
        {
            if (right == null && @operator == null) return left;
            if (left == null || right == null) return null; // invalid expression

            left = left.ToGenericType();
            right = right.ToGenericType();

            if (BinaryExpressionRules.TryGetValue(Key(left.Value, @operator?.Operator, right.Value), out var result))
                return result;

            var operatorType = @operator?.OperatorType();
            if (BinaryExpressionRules.TryGetValue(Key(left.Value, operatorType, right.Value), out var result2))
                return result2;

            return null;
        }

        private static string Key(DataType leftExpr, object anOperator, DataType rightExpr) =>
            $"{leftExpr}{anOperator}{rightExpr}";
    }
}
