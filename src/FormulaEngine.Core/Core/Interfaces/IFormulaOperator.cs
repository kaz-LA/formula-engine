using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Interfaces
{
    public interface IFormulaOperator
    {
        FormulaOperator Operator { get; set; }
        string Symbol { get; set; }
        string SqlSymbol { get; set; }
    }
}
