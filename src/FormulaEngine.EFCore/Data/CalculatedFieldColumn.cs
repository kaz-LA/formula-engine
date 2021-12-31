using FormulaEngine.Core.Interfaces;

namespace FormulaEngine.Data
{
    [Table(TableNames.RptCalculatedFieldColumn, Schema = SchemaNames.Rpt)]
    public class CalculatedFieldColumn : ICalculatedFieldColumn
    {
        internal static class FieldNames
        {
            public const string Id = "id";
            public const string CalculatedFieldId = "calculated_field_id";
            public const string ColumnId = "column_id";
            public const string EntityId = "entity_id";
            public const string ParentId = "parent_id";
        }

        [Key, Column(FieldNames.Id), DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column(FieldNames.CalculatedFieldId)]
        public int CalculatedFieldId { get; set; }

        [Column(FieldNames.ColumnId)]
        public int ColumnId { get; set; }

        [ForeignKey(nameof(ColumnId))]
        public Column Column { get; set; }

        [ForeignKey(nameof(CalculatedFieldId))]
        public CalculatedField CalculatedField { get; set; }

        [Column(FieldNames.EntityId)]
        public int EntityId { get; set; }

        [ForeignKey(nameof(EntityId))]
        public ReportEntity Entity { get; set; }

        IReportEntity ICalculatedFieldColumn.Entity
        {
            get => Entity;
            set => Entity = value as ReportEntity;
        }

        IColumn ICalculatedFieldColumn.Column
        {
            get => Column;
            set => Column = value as Column;
        }

        public string Placeholder => $"[{EntityId}:{ColumnId}]";

        [Column(FieldNames.ParentId)]
        public int? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public CalculatedField Parent { get; set; }

        ICalculatedField ICalculatedFieldColumn.Parent => this.Parent;

        public override string ToString()
        {
            return $@"{CalculatedFieldId}-{EntityId}.{ColumnId}";
        }

        public static CalculatedFieldColumn Create(int calculatedFieldId, int columnId, int? entityId, int? parentId) =>
            new CalculatedFieldColumn 
            { 
                ColumnId = columnId, 
                EntityId = entityId ?? 0, 
                CalculatedFieldId = calculatedFieldId, 
                ParentId = parentId 
            };

        public static CalculatedFieldColumn Create(int calculatedFieldId, ICalculatedFieldColumn other) =>
            new CalculatedFieldColumn
            {
                ColumnId = other.ColumnId,
                EntityId = other.EntityId,
                CalculatedFieldId = calculatedFieldId,
                ParentId = other.CalculatedFieldId
            };
    }

}
