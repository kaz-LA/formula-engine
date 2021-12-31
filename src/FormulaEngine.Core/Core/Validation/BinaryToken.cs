using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Parser.Internal;

namespace FormulaEngine.Core.Validation
{
    internal class BinaryToken : Token
    {
        public BinaryToken(ExpressionType left, IFormulaOperator @operator, ExpressionType right)
        {
            Left = left;
            Operator = @operator;
            Right = right;
        }

        public Token LeftToken => Left?.SourceToken;
        public Token RightToken => Right?.SourceToken;

        public ExpressionType Left { get; set; }
        public ExpressionType Right { get; set; }
        public IFormulaOperator Operator { get; set; }
    }
}