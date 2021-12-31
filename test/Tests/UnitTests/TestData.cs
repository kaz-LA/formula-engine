using System;
using System.Collections.Generic;
using System.Linq;

namespace FormulaEngineTests.UnitTests
{
    internal class TestData
    {
        static TestData()
        {
            Columns = GetTestColumns();
            Functions = GetTestFunctions();
            CalculatedFields = GetTestCalculatedFields();
        }

        public static ICollection<ColumnInfo> Columns { get; set; }

        public static ICollection<IFunction> Functions { get; set; }

        public static ICollection<ICalculatedField> CalculatedFields { get; set; }

        public static ICalculatedFieldProvider CalculatedFieldProvider()
            => new CalculatedFieldMeta(new FormulaRepository(Columns, Functions, CalculatedFields));

        public static IColumn GetColumn(string entity, string column) => FindColumnByTitle(entity, column);

        public static IFunction GetFunction(string name) => Functions.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public static bool ColumnExists(int? entityId, int columnId) 
            => Columns.Any(c => c.EntityId == entityId && c.ColumnId == columnId);

        public static ColumnInfo ColumnInfo(string entity, string name, int? entityId, int columnId, ColumnDataType type,
            ColumnDisplayTypeEnum? displayType = null,
           int? calculatedFieldId = null, DataType? calculatedFieldType = null) => new ColumnInfo
           {
               EntityName = entity,
               Name = name,
               DefaultTitle = name,
               ColumnDataType = type,
               ColumnDisplayType = displayType ?? ToColumnDisplayType(type, calculatedFieldType),
               ColumnId = columnId,
               EntityId = entityId,
               CalculatedFieldId = calculatedFieldId,
               CalculatedFieldType = calculatedFieldType,
               IsSelectable = true,
               IsActive = true, 
               CalculatedFieldIsActive = true, 
               CalculatedFieldIsPublic = true, 
               CalculatedFieldCreatedBy = 1
           };

        public static ColumnInfo AddColumn(string entity, string name, int? entityId, int columnId, ColumnDataType type,
           ColumnDisplayTypeEnum? displayType = null,
          int? calculatedFieldId = null, DataType? calculatedFieldType = null)
        {
            var col = ColumnInfo(entity, name, entityId, columnId, type, displayType, calculatedFieldId, calculatedFieldType);
            Columns.Add(col);
            return col;
        }

        private static ICollection<IFunction> GetTestFunctions() =>
            new IFunction[]
            {
                new Function
                {
                    Id = 1, Name = "Func1", ResultTypeId = DataType.String,
                    MaxNestingLevel = 1,
                    Parameters = new[] {CreateParameter("string", DataType.String)}
                },
                new Function
                {
                    Id = 2, Name = "Year", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 1,
                    Parameters = new[] {CreateParameter("date", DataType.Datetime)}
                },
                new Function
                {
                    Id = 222, Name = "Month", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 1,
                    Parameters = new[] {CreateParameter("date", DataType.Datetime)}
                },
                new Function
                {
                    Id = 9900, Name = "Day", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 5,
                    Parameters = new[] {CreateParameter("date", DataType.Datetime)}
                },
                new Function
                {
                    Id = 222, Name = "TEXT", ResultTypeId = DataType.String, SqlExpression="CAST({0} as nvarchar(max))",
                    MaxNestingLevel = 5,
                    Parameters = new[] {CreateParameter("value", DataType.Any)}
                },
                new Function
                {
                    Id = 3, Name = "Trim", ResultTypeId = DataType.String, SqlExpression = "RTRIM(LTRIM({0}))",
                    MaxNestingLevel = 1,
                    Parameters = new[] {CreateParameter("string", DataType.String)}
                },
                new Function
                {
                    Id = 4, Name = "DateDiff", ResultTypeId = DataType.Number, SqlExpression = "sqldatediff",
                    MaxNestingLevel = 1,
                    Parameters = new[]
                    {
                        CreateParameter("datepart", DataType.Any, "year,day,month"),
                        CreateParameter("date1", DataType.Datetime),
                        CreateParameter("date2", DataType.Datetime)
                    }
                },
                new Function
                    {Id = 5, Name = "Today", ResultTypeId = DataType.Datetime, SqlExpression = "GetUtcDate()"},
                new Function
                {
                    Id = 6, Name = "round", ResultTypeId = DataType.Number, MaxNestingLevel = 1,
                    Parameters = new[]
                    {
                        CreateParameter("number", DataType.Number),
                        CreateParameter("precision", DataType.Number).Optional(),
                    }
                },
                new Function
                {
                    Id = 7, Name = "Contains", ResultTypeId = DataType.Boolean, SqlExpression = "{0} LIKE '%{1}%'", MaxNestingLevel = 3,
                    Parameters = new[]
                    {
                        CreateParameter("in_string", DataType.String),
                        CreateParameter("some_text", DataType.String)
                    }
                },
                new Function
                {
                    Id = 777, Name = "Contains2", ResultTypeId = DataType.Boolean, SqlExpression = "{0} LIKE N'%{1}%'", MaxNestingLevel = 3,
                    Parameters = new[]
                    {
                        CreateParameter("in_string", DataType.String),
                        CreateParameter("some_text", DataType.String)
                    }
                },
                new Function
                {
                    Id = 8, Name = "Date", ResultTypeId = DataType.Datetime, SqlExpression = "CAST('{0}-{1}-{2}' as date)",
                    MaxNestingLevel = 1,
                    Parameters = new[]
                    {
                        CreateParameter("year", DataType.Number, "1-9999"),
                        CreateParameter("month", DataType.Number, "1,2,3,4,5,6,7,8,9,10,11,12"),
                        CreateParameter("day", DataType.Number, "1-31")
                    }
                },
                new Function
                {
                    Id = 9, Name = "If", ResultTypeId = DataType.Arg2, SqlExpression = "IIF", MaxNestingLevel = 1,
                    Parameters = new[]
                    {
                        CreateParameter("test", DataType.Boolean),
                        CreateParameter("true_value", DataType.Any),
                        CreateParameter("false_value", DataType.Arg2)
                    }
                },
                new Function
                {
                    Id = 10, Name = "Len", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 1,
                    Parameters = new[] {CreateParameter("string", DataType.String)}
                },
                new Function
                {
                    Id = 11, Name = "Abs", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 1,
                    Parameters = new[] {CreateParameter("number", DataType.Number)}
                },
                new Function
                {
                    Id = 12, Name = "Floor", ResultTypeId = DataType.Number,
                    Parameters = new[] {CreateParameter("number", DataType.Number)}
                },
                new Function
                {
                    Id = 13, Name = "Concat", ResultTypeId = DataType.String,
                    MaxNestingLevel = 1, IsVariableArguments = true ,
                    Parameters = new[] {CreateParameter("str1", DataType.Any),
                    CreateParameter("str2", DataType.Any)}
                },
                new Function
                {
                    Id = 14, Name = "Case", ResultTypeId = DataType.Arg3,
                    MaxNestingLevel = 0, IsVariableArguments = true ,
                    SqlExpression="CASE {0} [WHEN {i:1} THEN {i+1}] ELSE {n} END",
                    IsChartable = false ,
                    Parameters = new[] {
                        CreateParameter("expression", DataType.Any),
                    CreateParameter("value1", DataType.Arg1),
                    CreateParameter("result1", DataType.Any),
                    CreateParameter("value2", DataType.Arg1).Optional(),
                    CreateParameter("result2", DataType.Arg3).Optional(),
                    CreateParameter("elseResult", DataType.Arg3)}
                },
                new Function
                {
                    Id = 15, Name = "Count", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 0, CategoryId = FunctionCategoryEnum.Aggregate,
                    Parameters = new[] {CreateParameter("expression", DataType.Any)}
                },
                new Function
                {
                    Id = 155, Name = "GSUM", SqlExpression="SUM", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 0, CategoryId = FunctionCategoryEnum.Aggregate,
                    Parameters = new[] {CreateParameter("expression", DataType.Number)}
                },
                new Function
                {
                    Id = 655, Name = "GAVG", SqlExpression="AVG", ResultTypeId = DataType.Number,
                    MaxNestingLevel = 0, CategoryId = FunctionCategoryEnum.Aggregate,
                    Parameters = new[] {CreateParameter("expression", DataType.Number)}
                },
                new Function
                {
                    Id = 16, Name = "BlankValue", ResultTypeId = DataType.Arg1,
                    MaxNestingLevel = 0, CategoryId = FunctionCategoryEnum.Logical,
                    SqlExpression = "IsNull",
                    Parameters = new[]
                    {
                        CreateParameter("value", DataType.Any),
                        CreateParameter("value_if_null", DataType.Arg1)
                    }
                },
                new Function
                    {Id = 19, Name = "Now", ResultTypeId = DataType.Datetime, SqlExpression = "GetUtcDate()"},
                new Function
                {
                    Id = 17, Name = "Concat2", ResultTypeId = DataType.String,
                    MaxNestingLevel = 1, IsVariableArguments = true , SqlExpression="Concat",
                    Parameters = new[]
                    {
                        CreateParameter("expression1", DataType.Any),
                        CreateParameter("expression2", DataType.Any)
                    }
                },
                new Function
                {
                    Id = 18, Name = "Date2", ResultTypeId = DataType.AbsoluteDatetime,
                    SqlExpression = "Convert(date, Concat({0}, '-', {1}, '-', {2}))",
                    MaxNestingLevel = 1,
                    Parameters = new[]
                    {
                        CreateParameter("year", DataType.Number, "1-9999"),
                        CreateParameter("month", DataType.Number, "1,2,3,4,5,6,7,8,9,10,11,12"),
                        CreateParameter("day", DataType.Number, "1-31")
                    }
                },
                new Function
                {
                    Id = 99, Name = "IsBlank", ResultTypeId = DataType.Boolean, SqlExpression = "{0} IS NULL", MaxNestingLevel = 3,
                    Parameters = new[]
                    {
                        CreateParameter("expression", DataType.Any)
                    }
                },
                new Function
                {
                    Id = 999, Name = "IsNumber", ResultTypeId = DataType.Boolean, SqlExpression = "ISNUMERIC", MaxNestingLevel = 1,
                    Parameters = new[]
                    {
                        CreateParameter("expression", DataType.Any)
                    }
                },
                new Function
                    {Id = 55, Name = "Today2", ResultTypeId = DataType.Datetime, SqlExpression = "CONVERT(date, GetUtcDate())"},
                new Function
                {
                    Id = 37, Name = "DateAdd", ResultTypeId = DataType.Datetime, SqlExpression = "dateadd",
                    MaxNestingLevel = 5,
                    Parameters = new[]
                    {
                        CreateParameter("datepart", DataType.Any, "year,day,month"),
                        CreateParameter("number", DataType.Number),
                        CreateParameter("date", DataType.Datetime)
                    }
                },
            };

        private static FunctionParameter CreateParameter(string name, DataType type, string knownValues = null)
        {
            return new FunctionParameter() { Name = name, ValueTypeId = type, KnownValues = knownValues };
        }

        private static ICollection<ICalculatedField> GetTestCalculatedFields()
        {
            return new List<ICalculatedField>(new [] {
                new CalculatedField()
                {
                    Id = 1,
                    IsActive = true,
                    Name = "calculated_field_num1",
                    ColumnId = 1001,
                    Expression = "[training].[Total Session Cost] * 100",
                    SqlExpression = "[entity_46_column_-820] * 100",
                    Xml = "<formula>" +
                             "<column id=\"-820\" entityId=\"46\" value=\"[training].[Total Session Cost]\" />" +
                             "<operator value=\"*\" />" +
                             "<number value=\"100\" />" +
                             "</formula>"
                },
                new CalculatedField()
                {
                    Id = 2,
                    IsActive = true,
                    Name = "calculated_field_num2",
                    ColumnId = 1002,
                    Expression = "Abs([training].[Total Session Cost])",
                    SqlExpression = "ABS([entity_46_column_-820])",
                    Xml = "<formula>" +
                          "     <function id=\"16\" name=\"ABS\" level=\"0\"><args>" +
                          "     <arg i=\"0\"><column id=\"-820\" entityId=\"46\" value=\"[training].[Total Session Cost]\" /></arg></args>" +
                          " </function>" +
                          "</formula>"
                },
                new CalculatedField()
                {
                    Id = 3,
                    IsActive = true,
                    Name = "calculated_field_num3",
                    ColumnId = 1003,
                    Expression = "Round([calculated_field_num2]),2)",
                    SqlExpression = "ROUND([entity_0_column_1002],2)",
                    Xml = "<formula>" +
                          " <function id=\"21\" name=\"ROUND\" level=\"0\"><args>" +
                          "     <arg i=\"0\"><column id=\"1002\" entityId=\"0\" value=\"[calculated_field_num2]\" /></arg></args>" +
                          " </function>" +
                          "</formula>"
                },
                new CalculatedField()
                {
                    Id = 4,
                    IsActive = true,
                    Name = "calculated_field_num4",
                    ColumnId = 1004,
                    Expression = "Round(Abs([training].[Total Session Cost]), 2)",
                    SqlExpression = "ROUND(ABS([entity_46_column_-820]), 2)",
                    Xml = "<formula>" +
                          " <function id=\"21\" name=\"ROUND\" level=\"0\"><args>" +
                          " <arg i=\"0\">" +
                          "     <function id=\"16\" name=\"ABS\" level=\"1\"><args>" +
                          "     <arg i=\"0\"><column id=\"-820\" entityId=\"46\" value=\"[training].[Total Session Cost]\" /></arg></args>" +
                          "     </function></arg>" +
                          " <arg i=\"1\"><number value=\"2\" /></arg></args>" +
                          " </function>" +
                          "</formula>"
                },
            new CalculatedField()
                {
                    Id = 5,
                    IsActive = true,
                    Name = "aggregate_calculated_field",
                    ColumnId = 1005,
                    Expression = "GSUM([training].[Total Session Cost])",
                    SqlExpression = "Sum([46].[-820])",
                    Xml = "<xml>", AggregationTypeId = ColumnAggregationTypeEnum.Sum, IsAggregate = true, OutputTypeId = DataType.Number,
                    ReferencedColumns = new []{ new CalculatedFieldColumn { ColumnId = -820, EntityId = 46, Id = 1, CalculatedFieldId = 5 } }
                }});
        }

        private static ICollection<ColumnInfo> GetTestColumns() =>
            new List<ColumnInfo>(new[] {
                ColumnInfo("user", "user_id", 70, -1792, ColumnDataType.Integer), // input: [user].[user_id]
                ColumnInfo("User", "Months of Service", 70, -430, ColumnDataType.Integer),
                ColumnInfo("user", "name_first",70, 1, ColumnDataType.String), // input: [user].[name_first]
                ColumnInfo("user", "user full name",70, -1, ColumnDataType.String), // input: [user].[user full name]
                ColumnInfo("user", "date of birth",70, -99, ColumnDataType.DateTime), //input: [user].[date of birth]
                ColumnInfo("user", "User Last Hire Date",70, -999, ColumnDataType.DateTime),
                ColumnInfo("training", "training title",46, -48, ColumnDataType.String),// input: [training].[training title]
                ColumnInfo(null, "calculated_field_num1", -1, 1001, ColumnDataType.CalculatedField, null, 1, DataType.Number),// input: [calculated_field_num1]
                ColumnInfo(null, "calculated_field_num2", -1, 1002, ColumnDataType.CalculatedField, null, 2, DataType.Number),// input: [calculated_field_num2]
                ColumnInfo(null, "calculated_field_num3", -1, 1003, ColumnDataType.CalculatedField, null, 3, DataType.Number),// input: [calculated_field_num3]
                ColumnInfo(null, "calculated_field_num4", -1, 1004, ColumnDataType.CalculatedField, null, 4, DataType.Number),// input: [calculated_field_num4]
                ColumnInfo("Transcript", "Transcript_Due_Date", 42, -45, ColumnDataType.DateTime), // input: [Transcript].[Transcript_Due_Date]
                ColumnInfo("User", "user_has_photo", 70, -1095, ColumnDataType.BooleanBit, ColumnDisplayTypeEnum.BooleanYesNo),
                ColumnInfo("Training", "training_version_end_dt", 46, -1304, ColumnDataType.DateTime, ColumnDisplayTypeEnum.AbsoluteDateTime),
                ColumnInfo("Training", "training_version_start_dt", 46, -1303, ColumnDataType.DateTime, ColumnDisplayTypeEnum.AbsoluteDate),
                ColumnInfo("Applications", "Application Flags", 31, -972, ColumnDataType.String, ColumnDisplayTypeEnum.CSVLookup),
                ColumnInfo("Training", "Language", 46, -228, ColumnDataType.Guid, ColumnDisplayTypeEnum.LookupSet),
                ColumnInfo("Test Report", "Question Correct Response", 47, -181, ColumnDataType.Integer, ColumnDisplayTypeEnum.CSVLookup),
                ColumnInfo("User Succession Metrics", "M-24: Metric Grid Text Box", 93, 10730, ColumnDataType.String, ColumnDisplayTypeEnum.SMPGrid),
                ColumnInfo("Compensation Task", "New Target NQO %", 83, -445, ColumnDataType.Decimal, ColumnDisplayTypeEnum.CompensationPercentage),
                ColumnInfo(null, "InActiveColumn", 1, 1, ColumnDataType.String, ColumnDisplayTypeEnum.String).Set("IsActive", false),
                ColumnInfo(null, "UnSelectableColumn", 1, 1, ColumnDataType.String, ColumnDisplayTypeEnum.String).Set("IsSelectable", false),
                ColumnInfo(null, "Month", -1, 13889, ColumnDataType.CalculatedField, ColumnDisplayTypeEnum.Integer, 888, DataType.Number).Set("CalculatedFieldIsPublic", false),
                ColumnInfo(null, "CalculatedFieldNoActive", 1, 1, ColumnDataType.CalculatedField, ColumnDisplayTypeEnum.String, 99, DataType.String).Set("CalculatedFieldIsActive", false),
                ColumnInfo(null, "CalculatedFieldNotPublic", 1, 1, ColumnDataType.CalculatedField, ColumnDisplayTypeEnum.String, 999, DataType.String).Set("CalculatedFieldIsPublic", false),
                ColumnInfo("Training", "Training Hours", 46, -56, ColumnDataType.Decimal, ColumnDisplayTypeEnum.Decimal),
                ColumnInfo("Training", "Training Price", 46, -53, ColumnDataType.Decimal, ColumnDisplayTypeEnum.Decimal),
                ColumnInfo("Transcript", "Training Point Value", 42, -1430, ColumnDataType.Integer, ColumnDisplayTypeEnum.Integer),
                ColumnInfo("Transcript", "Transcript Score", 46, -77, ColumnDataType.Integer, ColumnDisplayTypeEnum.Integer),
                ColumnInfo("Training", "Course Duration (Hours)", 46, 7777, ColumnDataType.Decimal, ColumnDisplayTypeEnum.String), // these are weired columns
                ColumnInfo(null, "aggregate_calculated_field", -1, 1005, ColumnDataType.CalculatedField, null, 5, DataType.Number),
                ColumnInfo("Training", "Training provider", 46, -52, ColumnDataType.String, ColumnDisplayTypeEnum.StripHtml),
                ColumnInfo("Training record", "Certified Date", 42, 870, ColumnDataType.String, ColumnDisplayTypeEnum.AbsoluteDate),
                ColumnInfo("Training record", "Training record completed date", 42, -76, ColumnDataType.String, ColumnDisplayTypeEnum.DateTime),
                ColumnInfo("Certifications", "Certification Period User Completion Date", 50, -488, ColumnDataType.DateTime, ColumnDisplayTypeEnum.AbsoluteDateTime),
                ColumnInfo("Certifications", "Certification Training Not Activated", 50, -730, ColumnDataType.Integer, ColumnDisplayTypeEnum.String),
                ColumnInfo("トレーニング受講リスト", "カリキュラム タイトル (トレーニング受講リスト)", 42, -1563, ColumnDataType.Guid, ColumnDisplayTypeEnum.String),
                ColumnInfo("Training", "user_lo_rating", 46, -728, ColumnDataType.Integer, ColumnDisplayTypeEnum.Integer),
                ColumnInfo("Transcript", "Transcript Status", 42, -74, ColumnDataType.Integer, ColumnDisplayTypeEnum.String),
            });

        public static ColumnDisplayTypeEnum ToColumnDisplayType(ColumnDataType type, DataType? calculatedFieldType)
        {
            switch (type)
            {
                case ColumnDataType.Integer:
                    return ColumnDisplayTypeEnum.Integer;
                case ColumnDataType.String:
                    return ColumnDisplayTypeEnum.String;
                case ColumnDataType.DateTime:
                    return ColumnDisplayTypeEnum.DateTime;
                case ColumnDataType.CalculatedField:
                    switch(calculatedFieldType ?? DataType.Any)
                    {
                        case DataType.Number: return ColumnDisplayTypeEnum.Decimal;
                        case DataType.String: return ColumnDisplayTypeEnum.String;
                        case DataType.Datetime: return ColumnDisplayTypeEnum.DateTime;
                        default: return ColumnDisplayTypeEnum.None;
                    }                    
                default:
                    throw new InvalidOperationException($"ColumnDataType.{type} is not mapped to ColumnDataType.");
            }                        
        }

        public static ColumnInfo FindColumnByTitle(string entityTitle, string columnTitle)
        {
            var cmp = StringComparison.OrdinalIgnoreCase;

            var column = Columns.FirstOrDefault(c =>
            (string.Equals(c.Name, columnTitle, cmp) || string.Equals(c.DefaultTitle, columnTitle, cmp)) &&
            (string.IsNullOrEmpty(entityTitle) || string.Equals(c.EntityName, entityTitle, cmp)));

            return column;
        }

    }
}
