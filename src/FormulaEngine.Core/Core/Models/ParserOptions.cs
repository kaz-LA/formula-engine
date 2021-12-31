using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Models
{
    public class OutputOptions
    {
        public bool GenerateSql { get; set; } = true;

        public bool GenerateXml { get; set; } = true;

        public SqlTranslationOptions SqlOptions { get; set; } = SqlTranslationOptions.All;

        /// <summary>
        /// Default maximum function nesting level, if not specified at the function level
        /// </summary>
        public int MaxFunctionNesting { get; set; }
    }
}