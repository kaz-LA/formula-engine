namespace FormulaEngine.Core.Interfaces
{
    public interface ICalculatedFieldColumn : IColumnEntity
    {
        int Id { get; set; }
        int CalculatedFieldId { get; set; }        
        int ColumnId { get; set; }
        int EntityId { get; set; }
        IColumn Column { get; set; }
        IReportEntity Entity { get; set; }
        string Placeholder { get; }
        ICalculatedField Parent { get; }
        int? ParentId { get; set; }
    }
}
