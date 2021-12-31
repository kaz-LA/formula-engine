using System;
using System.Collections.Generic;
using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Interfaces
{
    public interface ICalculatedField
    {
        int Id { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        DataType OutputTypeId { get; set; }
        int? DecimalPoints { get; set; }
        int CreatedUserId { get; set; }
        DateTime CreatedDate { get; set; }
        DateTime? ModifiedDate { get; set; }
        string Expression { get; set; }
        string SqlExpression { get; set; }
        string Xml { get; set; }
        bool IsPublic { get; set; }
        bool IsChartable  { get; set; }
        int ColumnId { get; set; }
        IEnumerable<ICalculatedFieldColumn> ReferencedColumns { get; set; }
        IColumn Column { get; set; }
        bool IsActive { get; set; }
        bool IsAggregate { get; set; }
        AggregationType? AggregationTypeId { get; set; }
    }
}
