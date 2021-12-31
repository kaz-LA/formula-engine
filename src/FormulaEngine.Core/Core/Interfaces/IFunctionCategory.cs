using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Interfaces
{
    public interface IFunctionCategory
    {
        FunctionCategory Id { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        string ResourceKey { get; set; }
    }
}
