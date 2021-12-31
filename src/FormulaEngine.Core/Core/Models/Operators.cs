using System.Collections.Generic;
using FormulaEngine.Core.Enums;
using System.Linq;

namespace FormulaEngine.Core.Models
{
    public static class Operators
    {
        private static readonly IDictionary<string, Operator> _allOperators = new Dictionary<string, Operator>()
        {
            ["+"] = new Operator(1, OperatorKind.Arithmetic, "Addition", "+", "+"),
            ["-"] = new Operator(2, OperatorKind.Arithmetic, "Subtraction", "-", "-"),
            ["*"] = new Operator(3, OperatorKind.Arithmetic, "Multiplication", "*", "*"),
            ["/"] = new Operator(4, OperatorKind.Arithmetic, "Division", "/", "/"),
            ["="] = new Operator(5, OperatorKind.Relational, "Equals", "=", "="),
            ["=="] = new Operator(6, OperatorKind.Relational, "Equal2", "==", "="),
            ["<>"] = new Operator(7, OperatorKind.Relational, "NotEqual", "<>", "<>"),
            ["!="] = new Operator(8, OperatorKind.Relational, "NotEqual2", "!=", "<>"),
            ["<"] = new Operator(9, OperatorKind.Relational, "LessThan", "<", "<"),
            ["<="] = new Operator(10, OperatorKind.Relational, "LessOrEqual", "<=", "<="),
            [">"] = new Operator(11, OperatorKind.Relational, "GreaterThan", ">", ">"),
            [">="] = new Operator(12, OperatorKind.Relational, "GreaterOrEqual", ">=", ">="),
            ["&&"] = new Operator(13, OperatorKind.Logical, "And", "&&", "AND"),
            ["||"] = new Operator(14, OperatorKind.Logical, "Or", "||", "OR"),
            ["&"] = new Operator(15, OperatorKind.Arithmetic, "Concat", "&", "+")
        };

        public static ICollection<Operator> AllOperators => _allOperators.Values;

        public static bool IsValidOperator(string text) => _allOperators.ContainsKey(text);

        public static bool TryGetOperator(string text, out Operator @operator) =>
            _allOperators.TryGetValue(text, out @operator);
    }
}
