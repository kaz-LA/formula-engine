using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Interfaces
{
    public interface IColumn
    {
        int? EntityId { get; set; }
        int ColumnId { get; set; }
        ColumnDataType? ColumnDataType { get; set; }
        int? CalculatedFieldId { get; set; }
        DataType? CalculatedFieldType { get; set; }
        ColumnDataType? ColumnDisplayType { get; set; }
        bool IsSelectable { get; set; }
        bool IsActive { get; set; }
        bool? CalculatedFieldIsActive { get; set; }
        int? CalculatedFieldCreatedBy { get; set; }
        bool? CalculatedFieldIsPublic { get; set; }
    }
}