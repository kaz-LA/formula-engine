using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;

namespace FormulaEngine.Data
{
    [Table(TableNames.RptCalculatedField, Schema = SchemaNames.Rpt)]
    public class CalculatedField : ICalculatedField
    {
        internal static class FieldNames
        {
            public const string Id = "id";
            public const string Name = "name";
            public const string Description = "description";
            public const string OutputTypeId = "output_type_id";
            public const string DecimalPoints = "decimal_points";
            public const string CreatedUser = "create_user_id";
            public const string CreatedDate = "create_date";
            public const string ModifiedDate = "modified_date";
            public const string Expression = "expression";
            public const string SqlExpression = "sql_expression";
            public const string Xml = "xml";
            public const string IsPublic = "is_public";
            public const string IsChartable = "is_chartable";
            public const string ColumnId = "column_id";
            public const string IsActive = "is_active";
            public const string IsAggregate = "is_aggregate";
            public const string AnalyticsCalcColumnId = "analytics_calc_column_id";
            public const string IsMigrated = "is_migrated";
            public const string AggregationType = "aggregation_type";
        }

        [Key, Column(FieldNames.Id), DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column(FieldNames.Name), MaxLength(250)]
        public string Name { get; set; }

        [Column(FieldNames.Description), DataType("nvarchar(max)")]
        public string Description { get; set; }

        [Column(FieldNames.OutputTypeId)]
        public DataType OutputTypeId { get; set; }

        [ForeignKey(nameof(OutputTypeId))]
        public FunctionDataType OutputType { get; set; }

        [Column(FieldNames.DecimalPoints)]
        public int? DecimalPoints { get; set; }

        [Column(FieldNames.CreatedUser)]
        public int CreatedUserId { get; set; }

        [Column(FieldNames.CreatedDate)]
        public DateTime CreatedDate { get; set; }

        [Column(FieldNames.ModifiedDate)]
        public DateTime? ModifiedDate { get; set; }

        [Column(FieldNames.Expression), DataType("nvarchar(max)")]
        public string Expression { get; set; }        

        [Column(FieldNames.SqlExpression), DataType("nvarchar(max)")]
        public string SqlExpression { get; set; }

        [Column(FieldNames.Xml), DataType("nvarchar(max)")]
        public string Xml { get; set; }

        [Column(FieldNames.IsPublic)]
        public bool IsPublic { get; set; }        

        [Column(FieldNames.IsChartable)]
        public bool IsChartable { get; set; }

        [Column(FieldNames.ColumnId)]
        public int ColumnId { get; set; }

        [Required, ForeignKey(nameof(ColumnId))]
        public Column Column { get; set; }

        [InverseProperty(nameof(CalculatedFieldColumn.CalculatedField))]
        public virtual ICollection<CalculatedFieldColumn> ReferencedColumns { get; set; } = new List<CalculatedFieldColumn>();

        IEnumerable<ICalculatedFieldColumn> ICalculatedField.ReferencedColumns
        {
            get => ReferencedColumns;
            set => ReferencedColumns = value?.Cast<CalculatedFieldColumn>().ToList();
        }
        
        [NotMapped]
        public IUsers CreatedUser { get; set; }

        IColumn ICalculatedField.Column 
        {
            get => Column;
            set => Column = value as Column;
        }

        [Column(FieldNames.IsActive)]
        public bool IsActive { get; set; }

        [Column(FieldNames.IsAggregate)]
        public bool IsAggregate { get; set; }

        [Column(FieldNames.AnalyticsCalcColumnId)]
        public int? AnalyticsCalcColumnId { get; set; }

        [Column(FieldNames.IsMigrated),DefaultValue("false")]
        public bool IsMigrated { get; set; }

        [Column(FieldNames.AggregationType)]
        public AggregationType? AggregationTypeId { get; set; }

        [ForeignKey(nameof(AggregationTypeId))]
        public ColumnAggregationType AggregationType { get; set; }

        public override string ToString() => $@"{Name} - {Expression}";
    }

}
