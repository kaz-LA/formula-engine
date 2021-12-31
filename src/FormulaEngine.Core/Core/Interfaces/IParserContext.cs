using System;
using System.Globalization;

namespace FormulaEngine.Core.Interfaces
{
    public interface IParserContext
    {
        CultureInfo CultureInfo { get; set; }
        string BooleanNoText { get; }
        string BooleanYesText { get; }
        FormulaSettings Settings { get; set; }
    }

    public class FormulaSettings
    {
        public char QuoteChar { get; set; } = '"';
        public char EntityColumnDelimiter { get; set; } = '.';
        public char ColumnStartChar { get; set; } = '[';
        public char ColumnEndChar { get; set; } = ']';

        /// <summary>
        /// Default maximum function nesting level, if not specified at the function level
        /// </summary>
        public int MaxFunctionNestingLevel { get; set; }

        internal FormulaSettings Clone() => (FormulaSettings) MemberwiseClone();

        public void Destructure(out char quoteChar, out char entityColumnDelimiter)
        {
            quoteChar = QuoteChar;
            entityColumnDelimiter = EntityColumnDelimiter;
        }
    }
}