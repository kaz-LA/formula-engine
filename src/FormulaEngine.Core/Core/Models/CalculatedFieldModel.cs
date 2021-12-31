using System;
using System.Collections.Generic;
using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Models
{
    public class CalculatedFieldModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Expression { get; set; }
        public DataType OutputTypeId { get; set; }
        public int? DecimalPoints { get; set; }
        public bool IsPublic { get; set; }
        public bool IsChartable  { get; set; }
        public int ColumnId { get; set; }
        public string Xml { get; set; }

        public string Formula
        {
            get => Expression;
            set => Expression = value;
        }

        public int CreatedUserId { get; set; }
        public string CreatedUser { get; set; }
        public int? ModifiedUserId { get; set; }
        public string ModifiedUser { get; set; }
        public DateTime CreateDate { get; set; } 
        public DateTime? ModifiedDate { get; set; }
        public IColumnModel Column { get; set; }
        public bool IsActive { get; set; }
        public bool IsAggregate { get; set; }
        public int? AnalyticsCalcColumnId { get; set; }
        public bool IsMigrated { get; set; }
        public List<int> ReferencedEntityIds { get; set; }
        public int? AggregationTypeId { get; set; }
    }
}
