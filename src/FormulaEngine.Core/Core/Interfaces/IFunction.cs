using System.Collections.Generic;
using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Interfaces
{
    public interface IFunction
    {
        int Id { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        string SqlExpression { get; set; }
        FunctionCategory CategoryId { get; set; }
        DataType ResultTypeId { get; set; }
        bool IsVariableArguments { get; set; }
        bool IsChartable  { get; set; }
        int MaxNestingLevel { get; set; }
        string NameResourceKey { get; set; }
        string DescriptionResourceKey { get; set; }
        IEnumerable<IParameter> Parameters { get; set; }
        bool IsActive { get; set; }
    }
}
