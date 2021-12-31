
using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Interfaces
{
    public interface IParameter
    {
        int Id { get; set; }
        int FunctionId { get; set; }
        int Index { get; set; }
        string Name { get; set; }
        DataType ValueTypeId { get; set; }
        bool IsOptional { get; set; }
        string KnownValues { get; set; }
    }
}
