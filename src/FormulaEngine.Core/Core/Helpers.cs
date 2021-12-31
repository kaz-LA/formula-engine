using System;
using System.Text.RegularExpressions;
using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core
{
    public static class Helpers
    {
        public static bool IsBooleanExpression(string sql, DataType? resultType)
        {
            var functionMatches = Regex.Matches(sql.Trim(), @"^[A-Za-z]+\s*\(.+?\)$");
            var isFunction = functionMatches.Count == 1 && functionMatches[0].Value == sql; // exactly one sql function call
            var isBooleanLiteral = sql == "1" || sql == "0";
            var caseExpr = sql.StartsWith("CASE", StringComparison.OrdinalIgnoreCase);
            var hasMultipleTokens = sql.Contains(' ');

            return (resultType == DataType.Boolean &&
                    !string.IsNullOrEmpty(sql) &&
                    !isFunction &&
                    !isBooleanLiteral &&
                    hasMultipleTokens &&
                    !caseExpr);
        }

        public static bool IsNot<TEnum>(this TEnum value1, TEnum value2) where TEnum : Enum => value1.CompareTo(value2) != 0;
    }
}
