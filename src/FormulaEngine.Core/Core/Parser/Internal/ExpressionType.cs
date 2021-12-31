using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Parser.Internal
{
    internal class ExpressionType
    {
        public DataType? Primary { get; set; }
        public DataType? Secondary { get; set; }
        public Token SourceToken { get; set; }
        public bool HasSecondaryType => Secondary != null;

        public bool IsValid(ExpressionType expected, out DataType? actualType)
        {
            actualType = this.Primary;
            var actual = this.Primary.ToGenericType();
            if (actual > 0 && (expected?.Primary == actual || expected?.Secondary == actual || actual == expected?.Primary.ToGenericType()))
                return true;

            if (this.Secondary == null) 
                return false;

            actualType = this.Secondary;
            actual = this.Secondary.ToGenericType();
            return (actual > 0 && (expected?.Primary == actual || expected?.Secondary == actual));
        }

        private ExpressionType SetSourceToken(Token token)
        {
            this.SourceToken = token;
            return this;
        }

        public override string ToString() => $"{Primary}";
                
        public static implicit operator ExpressionType(DataType? type) 
            => new ExpressionType {  Primary = type};

        public static implicit operator ExpressionType(Token token)
        {
            if (token is FunctionToken func)
                return func.ResultType()?.SetSourceToken(token);

            return new ExpressionType
            {
                SourceToken = token,
                Primary = token.ToGenericType(),
                Secondary = token.SecondaryType != null ? token.ToGenericType(token.SecondaryType) : null
            };
        }
    }
}
