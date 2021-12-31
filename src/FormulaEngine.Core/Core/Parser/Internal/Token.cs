using System.Collections.Generic;
using System.Linq;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Validation;

namespace FormulaEngine.Core.Parser.Internal
{
    public class Token
    {
        public string Text { get; set; }
        public TokenType Type { get; set; }
        /// <summary>
        /// Secondary type of a Token a string literal token such as "01/01/2009" may represent a Datetime Token
        /// </summary>
        public TokenType? SecondaryType { get; set; }
        public object Value { get; set; }
        public int StartIndex { get; set; }

        public override string ToString() => $"{{{Text}}} - {Type} ({StartIndex})";
    }

    internal class Argument
    {
        private ExpressionType _resultType;

        public ExpressionType ResultType 
        {
            get => _resultType ??= TypeValidator.Instance.GetExpressionOutputType(Tokens);
            set => _resultType = value; 
        }

        public IList<Token> Tokens { get; set; } = new List<Token>();

        public override string ToString()
        {
            return string.Join(" ", Tokens.Select(t => t.Text));
        }
    }

    internal class FunctionToken : Token
    {
        public FunctionToken()
        {
            Type = TokenType.Function;
        }

        public IFunction Function { get; set; }
        public int Level { get; set; } // function nesting level
        public IList<Argument> Arguments { get; set; } = new List<Argument>();
        public string NestingHierarchy { get; set; }
    }

    public enum TokenType
    {
        Unknown,
        Boolean,
        CalculatedField,        // another calculated field
        Column,
        Comma,
        Date,
        Function,
        Number,
        Operator,
        ParenthesisOpen,
        ParenthesisClose,
        String
    }
}
