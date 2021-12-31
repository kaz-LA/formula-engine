using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Interfaces
{
    public interface IDataType
    {
        DataType Id { get; set; }
        string Name { get; set; }
        string ResourceKey { get; set; }
    }
}
