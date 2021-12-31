using System.Threading.Tasks;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Models;

namespace FormulaEngine.Core.Interfaces
{
    public interface IFormulaParser
    {
        Task<ParseResult> Parse(string formula, IParserContext options);

        Task<ParseResult> Parse(Formula formula, IParserContext context);
    }

    public class Formula
    {
        public string Text { get; set; }
        public DataType? OutputType { get; set; }
        public int? DecimalPoints { get; set; }
        public override string ToString() => Text;

        public static implicit operator Formula(string text) => new Formula {Text = text};
    }
}
