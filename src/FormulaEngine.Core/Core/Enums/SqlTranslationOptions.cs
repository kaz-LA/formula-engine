using System;

namespace FormulaEngine.Core.Enums
{
    /// <summary>
    /// Various options to automatically add to the SQL generated from calculated field formula 
    /// </summary>
    [Flags]
    public enum SqlTranslationOptions
    {
        /// <summary>
        /// No options
        /// </summary>
        None = 0,
        /// <summary>
        /// Add a guard to prevent Division by Zero errors
        /// </summary>
        DivisionByZero = 1,
        /// <summary>
        /// Add a guard to prevent null propagation in Arithmetic expressions
        /// </summary>
        NullPropagation = 2,
        /// <summary>
        /// Automatically transform integer (whole number) divisions into decimal division based on the user's intent
        /// </summary>
        DecimalDivision = 4,
        /// <summary>
        /// Add/Insert unicode character literal marker (N) before character literals (for Sql Server)
        /// </summary>
        UnicodeMarker = 8,
        /// <summary>
        /// All options listed above
        /// </summary>
        All = DivisionByZero | NullPropagation | DecimalDivision | UnicodeMarker
    }
}