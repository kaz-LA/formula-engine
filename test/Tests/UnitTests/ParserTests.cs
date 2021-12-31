using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormulaEngine.Core.Parser;
using FormulaEngine.Contracts.Enums;
using FormulaEngine.Contracts.Interfaces;
using FormulaEngine.Contracts.Models;
using FormulaEngine.Data;
using Xunit;

namespace FormulaEngineTests.UnitTests
{
    

    [Trait("Category", "CalculatedFieldParserTests")]
    public class ParserTests
    {
        private readonly IFormulaParser _sut;
        private readonly IFormulaParserContext _context;

        public ParserTests()
        {
            MetaData.CalculatedFieldMeta.ClearCache();
            _context = new ParserContext(TestData.CalculatedFieldProvider(), null, null);
            _context.Options.SqlOptions = FormulaSqlOptions.None;
            _sut = new FormulaParser(_context);
        }

        [Theory]
        [MemberData(nameof(NullTestData))]
        public async Task ShouldThrowExceptionForNullFormula(string formula, IFormulaParserContext context)
        {
            var result = await _sut.Parse(formula, context);
            Assert.False(result.IsSuccess);
            Assert.IsType<ArgumentNullException>(result.Errors.First().Data["exception"]);
        }

        [Theory]
        [InlineData("1", "1", "<formula><number value=\"1\" /></formula>")]
        [InlineData("\"Kaaz\" + \" C# \" ", "'Kaaz' + ' C# '", "ignore")]
        [InlineData("\"Kaaz\" & \" C++ \" ", "'Kaaz' + ' C++ '", "ignore")]
        [InlineData("(1/0)", "(1 / 0)", "ignore")]
        [InlineData("Len(\"01/01/2020\")", "Len('01/01/2020')", "ignore")]
        [InlineData("\"a valid string literal()\"\" here we go!\"", "'a valid string literal()\" here we go!'", "ignore")]
        public async Task ShouldParseSimpleValidFormula(string formula, string expectedSql, string expectedXml)
        {
            var actual = await _sut.Parse(formula, _context);

            AssertValidParserResult(actual, expectedSql, expectedXml);
        }

        [Theory]
        [InlineData("Trim([User].[name_first])", DataType.String)]
        [InlineData("Len([User].[name_first])", DataType.Number)]
        [InlineData("DateDiff(year, [user].[date of birth], today()) / 100", DataType.Number)]
        [InlineData("date(year(today()),1,1)", DataType.Datetime)]
        [InlineData("Round([calculated_field_num1]/100 * 2, [calculated_field_num2]/2)", DataType.Number)]
        [InlineData("If(Len([user].[name_first]) <= 100 , [user].[name_first], \"N/A\")", DataType.String)]
        [InlineData("If(Year(Today()) >= 2020, Today(), \"01/01/2000\")", DataType.Datetime)]
        [InlineData("If(1 == 2, \"2001-12-31\", \"01/01/2000\")", DataType.Datetime)]
        [InlineData("If(2 != 3, \"2001-12-31\", \"01/01/2000\")", DataType.String)]
        [InlineData("BlankValue([User].[name_first], \"_NoName_\")", DataType.String)]
        [InlineData("IF(DATEDIFF(month, [User].[User Last Hire Date], TODAY()) =3, \"YES\", \"NO\")", DataType.String,
                    "IIF(sqldatediff(month,[70:-999],GetUtcDate()) = 3,'YES','NO')")]
        [InlineData("IF(DATEDIFF(month, [User].[User Last Hire Date], TODAY()) =3, \"YES\", \"NO\")", DataType.Boolean,
                    "IIF(sqldatediff(month,[70:-999],GetUtcDate()) = 3,1,0)")]
        [InlineData("IF(MONTH([User].[Date of Birth])=MONTH(TODAY()), \"YES\", \"NO\")", DataType.Boolean,
                    "IIF(Month([70:-99]) = Month(GetUtcDate()),1,0)")]
        [InlineData("DateAdd(year, 1, [user].[date of birth])", DataType.Datetime)]
        public async Task ShouldParseAndValidateFormula(string formula, DataType expectedResultType, string expectedSql = null)
        {
            _context.Options.OutputType = expectedResultType;
            var actual = await _sut.Parse(formula);

            Assert.True(actual.IsSuccess);
            Assert.True(expectedSql == null || expectedSql.Equals(actual.Sql, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("BlankValue([User].[name_first], 2233)")]
        [InlineData("BlankValue(2233, [User].[name_first])")]
        public async Task ValidationShouldFail(string formula)
        {
            var actual = await _sut.Parse(formula);

            Assert.False(actual.IsSuccess);
        }

        [Theory]
        [InlineData(null, null, "[User].[name_first]", "[70:1]", "ignore")]
        [InlineData(null, "some + column**", "[some + column**] + -10", "[44:88] + -10", "ignore")]
        [InlineData(null, null, "GSUM([Training].[Course Duration (Hours)])", "SUM([46:7777])", "ignore")]
        public async Task ShouldParseFormulaWithColumn(string entity, string column, string formula, string expectedSql, string expectedXml)
        {
            if(!string.IsNullOrEmpty(column))
                TestData.AddColumn(entity, column, 44, 88, ColumnDataType.Integer);

            var actual = await _sut.Parse(formula);

            AssertValidParserResult(actual, expectedSql, expectedXml);
        }

        [Theory]
        [InlineData("ካዞርላ - השבח לאל!! Archived - from Transcript (Curriculum)?")]
        [InlineData("Approver's State/Province - Successor")]
        [InlineData("Immediate Bonus %")]
        [InlineData("Screening Section % Score")]
        [InlineData("Referrer Name (Last Name, First Name)")]
        [InlineData("# Parts User Attended")]
        [InlineData("Tag: Featured")]
        [InlineData("Override Amount Currency/%")]
        [InlineData("ILT Facility Address#2")]
        [InlineData("Offer Letter Approval Offer Approver ? Comment")]
        [InlineData("RF-Fire Dept. Mult.Checkbox-01 (US)")]
        [InlineData("'XYZ")]
        [InlineData("API Custom Field QA05Core 8/18")]
        [InlineData("CSOD-61766 Hierarchy \"''\"")]
        [InlineData("CF-Scrolling Text box  05/23/2011")]
        [InlineData("!!test!!")]
        [InlineData("##NEWTARGET## Bonus on 4/27 D-306857'<' %")]
        [InlineData("管理部課長からの承認は得られていますか。")]
        public async Task ShouldParseColumnWithWeiredCharacter(string column)
        {
            if (!string.IsNullOrEmpty(column))
                TestData.AddColumn("An Entity", column, 33, 66, ColumnDataType.Integer);

            var actual = await _sut.Parse($"[An Entity].[{column}] / 2");

            AssertValidParserResult(actual, "[33:66] / 2", "ignore");
        }

        [Theory]
        [InlineData("Date(2,2,2)", true)]
        [InlineData("Date(9999, 1, 31)", true)]
        [InlineData("Date(1, 1, 1)", true)]
        [InlineData("Date(1, 1, 32)", false)]
        [InlineData("Date(2001, 14, 31)", false)]
        [InlineData("Date(0, 1, 31)", false)]
        [InlineData("DateDiff(day, Today(), Now())", true)]
        [InlineData("DateDiff([user].[name_first], Today(), Now())", false)]
        public async Task ShouldValidateKnownValues(string formula, bool isValid)
        {
            var actual = await _sut.Parse(formula);
            Assert.Equal(isValid, actual.IsSuccess);
        }

        [Theory]
        [InlineData("ABS(-1--9) -2 * -7", "ABS(-1 - -9) - 2 * -7", "ignore")]
        [InlineData("Func1([user].[name_first])", "Func1([70:1])", "ignore")]
        [InlineData("DateDiff(year, [user].[date of birth], today())",
            "sqlDateDiff(year,[70:-99],GetUtcDate())",
            "<formula><function id=\"4\" name=\"DateDiff\" level=\"0\"><args><arg i=\"0\"><unknown value=\"year\" /></arg><arg i=\"1\"><column id=\"-99\" entityId=\"70\" value=\"[user].[date of birth]\" /></arg><arg i=\"2\"><function id=\"5\" name=\"Today\" level=\"1\"><args></args></function></arg></args></function></formula>")]
        [InlineData("DateAdd(day, 1, [user].[date of birth])", "DateAdd(day,1,[70:-99])",
            "<formula><function id=\"37\" name=\"DateAdd\" level=\"0\"><args><arg i=\"0\"><unknown value=\"day\" /></arg><arg i=\"1\"><number value=\"1\" /></arg><arg i=\"2\"><column id=\"-99\" entityId=\"70\" value=\"[user].[date of birth]\" /></arg></args></function></formula>")]
        public async Task ShouldParseValidFormulaWithFunction(string formula, string expectedSql, string expectedXml)
        {
            var actual = await _sut.Parse(formula, _context);
            AssertValidParserResult(actual, expectedSql, expectedXml);
        }

        [Theory]
        [InlineData("DateDiff(year, [user].[date of birth], date(year(today()),1,1))",
            "sqldatediff(year,[70:-99],CAST('Year(GetUtcDate())-1-1' as date))",
            "<formula><function id=\"4\" name=\"DateDiff\" level=\"0\"><args><arg i=\"0\"><unknown value=\"year\" /></arg><arg i=\"1\"><column id=\"-99\" entityId=\"70\" value=\"[user].[date of birth]\" /></arg><arg i=\"2\"><function id=\"8\" name=\"Date\" level=\"1\"><args><arg i=\"0\"><function id=\"2\" name=\"Year\" level=\"2\"><args><arg i=\"0\"><function id=\"5\" name=\"Today\" level=\"3\"><args></args></function></arg></args></function></arg><arg i=\"1\"><number value=\"1\" /></arg><arg i=\"2\"><number value=\"1\" /></arg></args></function></arg></args></function></formula>"
        )]
        public async Task ShouldCorrectlyDetermineFunctionNestingLevels(string formula, string expectedSql,
            string expectedXml)
        {
            (await _context.GetFunction("Today")).MaxNestingLevel = 3;
            (await _context.GetFunction("Year")).MaxNestingLevel = 2;

            var actual = await _sut.Parse(formula, _context);
            AssertValidParserResult(actual, expectedSql, expectedXml);
        }

        [Theory]
        [InlineData("Round([calculated_field_num1]/100 * 2, [calculated_field_num2]/2)", DataType.Number, true)]
        [InlineData("(((([User].[Months of Service] / [User].[Months of Service]) * [User].[Months of Service]) + [User].[Months of Service]) * [User].[Months of Service])", DataType.Number, true)]
        [InlineData("(((([User].[Months of Service] / [User].[Months of Service]) * [User].[Months of Service] + [User].[Months of Service]) * [User].[Months of Service])", DataType.Number, false)]
        [InlineData("Round((44 + ([calculated_field_num1]/100) * 2), ([calculated_field_num2]/2))", DataType.Number, true)]
        [InlineData("Round((44 + ([calculated_field_num1]/100) * 2)), ([calculated_field_num2]/2))", DataType.Number, false)]
        public async Task ShouldParseComplexExpression(string formula, DataType? expressionType, bool beValid)
        {
            _context.Options.OutputType = expressionType;
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(beValid, actual.IsSuccess);
        }

        [Theory]
        [InlineData("Concat([user].[name_first], \",\", \"str1\", \"str2\")", "Concat([70:1],',','str1','str2')")]
        [InlineData("Case([calculated_field_num1],1,100,2,200,3,300,400)",
            "CASE [-1:1001] WHEN 1 THEN 100 WHEN 2 THEN 200 WHEN 3 THEN 300  ELSE 400 END")]
        [InlineData("Case([calculated_field_num1],1,100,2,200,10000)",
            "ignore")]
        [InlineData("Case([calculated_field_num1],1,100,2,200,3,300, 4,400, 5, 500, 6, 600, 100000)",
            "CASE [-1:1001] WHEN 1 THEN 100 WHEN 2 THEN 200 WHEN 3 THEN 300 WHEN 4 THEN 400 WHEN 5 THEN 500 WHEN 6 THEN 600  ELSE 100000 END")]
        [InlineData("Case([calculated_field_num1] == 100, yes, \"Gosh!\" , \"WHOAA!!\")",
            "IIF([-1:1001] = 100,'Gosh!','WHOAA!!')")]
        [InlineData("Concat(\"01/01/2020\", \":\", \"10-10-2001\")", "ignore")]
        public async Task ShouldParseValidFunctionWithVariableNumberOfArgs(string formula, string expectedSql)
        {
            var actual = await _sut.Parse(formula, _context);

            AssertValidParserResult(actual, expectedSql, "ignore");
        }

        [Theory]
        [InlineData("Concat([user].[name_first])", "Concat", 2)]
        [InlineData("Case([calculated_field_num1],1,100)", "Case", 4)]
        public async Task ShouldValidateFunctionWithVariableNumberOfArgs_LessArgs(string formula, string function, int minArgs)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(actual.Errors,
                e => e.Message == ParseErrors.MinimumNumberOfArgs &&
                     e.Data.Contains("function", function) &&
                     e.Data.Contains("minParams", minArgs.ToString()));
        }

        [Theory]
        [InlineData("Trim([user].[name_first])", "RTRIM(LTRIM([70:1]))", "ignore")]
        [InlineData("Date ( 2001,10, 01) ", "CAST('2001-10-01' as date)", "ignore")]
        [InlineData(" Contains ([training].[training title], \"text'\"\"()\")", "[46:-48] LIKE '%text''\"()%'", "ignore")]
        [InlineData("Date2(Year(Now()), 1, 01) ", "Convert(date, Concat(Year(GetUtcDate()), '-', 1, '-', 01))", "ignore")]
        [InlineData("Concat2(\"Kaaz\", 2020, \"2000-12-31\", yes)", "Concat('Kaaz',2020,'2000-12-31',1)", "ignore")]
        [InlineData(" Contains ([user].[user full name], [user].[name_first])", "[70:-1] LIKE '%' + [70:1] + '%'", "ignore")]
        [InlineData(" Contains ([user].[user full name], Concat(1,2,3))", "[70:-1] LIKE '%' + Concat(1,2,3) + '%'", "ignore")]
        [InlineData("IF(ISBLANK([training].[training title]) && [user].[name_first] =\"Not Activated\", 0, IF(ISBLANK([training].[training title]), 50, 100))  ",
                    "IIF([46:-48] IS NULL AND [70:1] = 'Not Activated',0,IIF([46:-48] IS NULL,50,100))", "ignore")]
        public async Task ShouldBuildSqlExpression(string formula, string expectedSql, string expectedXml)
        {
            var actual = await _sut.Parse(formula, _context);

            AssertValidParserResult(actual, expectedSql, expectedXml);
        }

        [Theory]
        [MemberData(nameof(ReferencedColumnsTestData))]
        public async Task ShouldReturnReferencedColumns(string formula, ICollection<IColumn> expectedColumns)
        {
            var actual = await _sut.Parse(formula, _context);

            AssertValidParserResult(actual, expectedColumns);
        }
                
        [Theory]
        [InlineData("Count([user].[name_first])", true)]
        [InlineData("Trim([user].[name_first])", false)]
        [InlineData("[aggregate_calculated_field] + 2", true)]
        [InlineData("[calculated_field_num2] * 2", false)]
        [InlineData("[calculated_field_num2] / [aggregate_calculated_field] + 2", true)]
        [InlineData("GSUM([calculated_field_num2])", true)]
        public async Task ShouldParseExpressionWithAggregateFunction(string formula, bool containsAggregateFunction)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
            Assert.Equal(containsAggregateFunction, actual.ContainsAggregateFunction);
        }

        [Theory]
        [InlineData("Count([Transcript].[Transcript Status] == \"Withdrawn\")", false, ParseErrors.InvalidArgumentOrExpression)]
        [InlineData("Count(IF([Transcript].[Transcript Status] == \"Withdrawn\", 1, 0) + 10 == 100))", false, ParseErrors.InvalidArgumentOrExpression)]
        [InlineData("Count([Transcript].[Transcript Status] = \"Withdrawn\")", false, ParseErrors.InvalidArgumentOrExpression)]
        [InlineData("Count(IF([Transcript].[Transcript Status] = \"Withdrawn\", 1, 0))", true, null)]
        [InlineData("GSUM(IF([Transcript].[Transcript Status] = \"Withdrawn\", 1, 0))", true, null)]
        public async Task ShouldValidateExpressionInAggregateFunction(string formula, bool isSuccess, string expectedError)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(isSuccess, actual.IsSuccess);
            Assert.True(expectedError == null || actual.Errors.Any(e => e.Message == expectedError));
        }

        [Theory]
        [InlineData("GSUM([Training].[user_lo_rating])", "SUM([46:-728])")]
        [InlineData("GAVG([Training].[user_lo_rating])", "AVG(CAST([46:-728] AS FLOAT))")]
        [InlineData("GAVG([Training].[Training Hours])", "AVG([46:-56])")]
        public async Task ShouldGenerateCorrectSqlForAggregateFunction(string formula, string expectedSql)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
            Assert.Equal(expectedSql, actual.Sql);
        }

        [Theory]
        [InlineData("GSUM([calculated_field_num2])", ColumnAggregationTypeEnum.Sum)]
        [InlineData("GAVG([calculated_field_num2])", ColumnAggregationTypeEnum.Avg)]
        [InlineData("GSUM([calculated_field_num2]) / 2", null)]
        [InlineData("GSUM([calculated_field_num2] / 2)", ColumnAggregationTypeEnum.Sum)]
        [InlineData("Trim([user].[name_first])", null)]
        [InlineData("2 + 2", null)]
        [InlineData("[aggregate_calculated_field]", ColumnAggregationTypeEnum.Sum)]
        [InlineData("[aggregate_calculated_field] + 2", null)]
        public async Task ShouldDetermineAggregationType(string formula, ColumnAggregationTypeEnum? aggregationType)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
            Assert.Equal(aggregationType, actual.AggregationType);
        }

        [Theory]
        [InlineData("[calculated_field_num2] * [calculated_field_num3] * 2")] // formula for [calculated_field_num3] references [calculated_field_num2]
        public async Task ShouldFailForNestedCalculatedFieldReferences(string formula)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(actual.Errors,
                e => e.Message == ParseErrors.InvalidCalculatedFieldUsage &&
                     e.Data.Contains("calculatedField", "[calculated_field_num3]"));
        }

        [Theory]
        [InlineData("Floor([calculated_field_num2])")] // Floor(Abs([training].[Total Session Cost]))
        public async Task ShouldValidateMaxNestingLevelForFunctionsFromAnotherCalculatedField(string formula)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
        }

        // Added by Kaz to verify RPT-13619
        [Fact]
        public async Task ShouldValidateNestedCalculatedFields()
        {
            // create two test Formula Engine
            TestData.Columns.Add(TestData.ColumnInfo("User", "User Last Access", 70, -28, ColumnDataType.DateTime));
            TestData.GetFunction("Len").MaxNestingLevel = 5;
            TestData.GetFunction("Concat").MaxNestingLevel = 5;

            var formula = "CASE(MONTH([User].[User Last Access]),1,\"Jan\",2,\"Feb\",3,\"Mar\",4,\"Apr\",5,\"May\",6,\"Jun\",7,\"Jul\",8,\"Aug\",9,\"Sep\",10,\"Oct\", 11,\"Nov\", 12,\"Dec\",\"NOT SET\")";
            var parsed = await _sut.Parse(formula, _context);
            Assert.True(parsed.IsSuccess);

            var cf1 = new CalculatedField()
            {
                Id = 101,
                IsActive = true,
                Name = "MON GDP month last access date",
                ColumnId = 101,
                Expression = formula,
                SqlExpression = parsed.Sql,
                Xml = parsed.Xml, 
                OutputTypeId = parsed.ResultType ?? DataType.Any, 
            };
                        
            TestData.CalculatedFields.Add(cf1);
            TestData.Columns.Add(TestData.ColumnInfo("Formula Engine", cf1.Name, -1, cf1.ColumnId, ColumnDataType.CalculatedField, calculatedFieldId: cf1.Id));

            formula = @"IF(LEN(TEXT(DAY([User].[User Last Access]))) == 1, CONCAT(0,DAY([User].[User Last Access])), TEXT(DAY([User].[User Last Access])))";
            parsed = await _sut.Parse(formula, _context);
            Assert.True(parsed.IsSuccess);

            var cf2 = new CalculatedField()
            {
                Id = 102,
                IsActive = true,
                Name = "2 char day GDP last access date",
                ColumnId = 102,
                Expression = formula,
                SqlExpression = parsed.Sql,
                Xml = parsed.Xml,
                OutputTypeId = parsed.ResultType ?? DataType.Any,
            };

            TestData.CalculatedFields.Add(cf2);
            TestData.Columns.Add(TestData.ColumnInfo("Formula Engine", cf2.Name, -1, cf2.ColumnId, ColumnDataType.CalculatedField, calculatedFieldId: cf2.Id));

            await ParseAndAssert("CONCAT([Formula Engine].[2 char day GDP last access date],\" \", [Formula Engine].[MON GDP month last access date] ,\" \",YEAR([User].[User Last Access]))");
            
            await ParseAndAssert("CONCAT([Formula Engine].[MON GDP month last access date],\" \",[Formula Engine].[2 char day GDP last access date] , \" \",YEAR([User].[User Last Access]))");

            await ParseAndAssert("CONCAT(YEAR([User].[User Last Access]), \" \", [Formula Engine].[MON GDP month last access date], \" \", [Formula Engine].[2 char day GDP last access date])");

            await ParseAndAssert("CONCAT(YEAR([User].[User Last Access]), \" \", [Formula Engine].[2 char day GDP last access date], \" \", [Formula Engine].[MON GDP month last access date])");

            async Task ParseAndAssert(string aformula)
            {
                var actual = await _sut.Parse(aformula, _context);

                Assert.False(actual.IsSuccess);
                
                Assert.Contains(actual.Errors, e => e.Message == ParseErrors.FunctionCantBeNested && e.Data.Contains("function", "Case") && e.Data.Contains("nestingHierarchy", "(CONCAT->Case)"));
            }
        }

        [Theory]
        [InlineData("Floor(Round([calculated_field_num2],2))")] // Floor(Round(Abs([training].[Total Session Cost]), 2))
        [InlineData("Floor([calculated_field_num4])")]
        public async Task ShouldValidateMaxNestingLevelForFunctionsFromAnotherCalculatedField2(string formula)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(actual.Errors,
                e => e.Message == ParseErrors.MaxFunctionNestingExceeded && e.Data.Contains("function", "Abs"));
        }

        [Theory]
        [InlineData("[user].[doesnt_exist]")]
        [InlineData(" ([doesnt_exist] + 1) ")]
        [InlineData("func1([user].[not_a_column])")]
        public async Task ShouldReturnParseErrorForUnknownColumn(string formula)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(ParseErrors.UnknownColumnOrCalculatedField, actual.Errors.Select(e => e.Message));
        }

        [Theory]
        [InlineData("x+1")]
        [InlineData("func1(what?)")]
        [InlineData("DateDiff(week,\"2020-01-01\", \"2021-12-31\")")]
        public async Task ShouldReturnParseErrorForUnknownToken(string formula)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(ParseErrors.UnexpectedToken, actual.Errors.Select(e => e.Message));
        }

        [Theory]
        [InlineData("trim99([some_column])")]
        [InlineData("func1(yearly ())")]
        public async Task ShouldReturnParseErrorForUnknownFunction(string formula)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(ParseErrors.UnknownFunction, actual.Errors.Select(e => e.Message));
        }

        [Theory]
        [InlineData("func1(12345)")]
        [InlineData("func1([calculated_field_num1])")]
        [InlineData("func1(\"Hi Kaz!\" + 200)")]
        [InlineData("Len(1234)")]
        [InlineData("Case([calculated_field_num1],1,100, yes,200,3,300,400)")]
        [InlineData("Case([calculated_field_num1],1,100,2,200,\"invalid\")")]
        [InlineData("Case([calculated_field_num1],1,100,2,200,3,300, 4,400, 5, 500, 6, -1, 600, 100000)")]
        [InlineData("Case([calculated_field_num1] == 100, yes, \"Gosh!\" , Today())")]
        [InlineData("COUNT([User].[user_id][User].[user_id])", ParseErrors.UnexpectedToken)]
        [InlineData("BlankValue(([user].[name_first], \"NA\"))")]
        [InlineData("BlankValue(([user].[name_first], \"NA\")", ParseErrors.InvalidFunctionSyntax)]
        [InlineData("BlankValue(([user].[name_first])), \"NA\")")]
        public async Task ShouldReturnParseErrorForInvalidArgument(string formula, string error = null)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            var errors = actual.Errors.Select(e => e.Message);
            Assert.True((error != null && errors.Contains(error)) || 
                        errors.Contains(ParseErrors.InvalidArgumentOrExpression) ||
                        errors.Contains(ParseErrors.MissingValueForRequiredParameter));
        }

        [Theory]
        [InlineData("func1(\"''\", 2)")]
        [InlineData("func1([user].[name_first], \"hi! i'm extra arg!\")")]
        [InlineData("today(whatever())")]
        [InlineData("IF([User].[user_has_photo],\"Yes\",\"1\",\"0\")")]
        public async Task ShouldReturnParseErrorForTooManyArguments(string formula)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(ParseErrors.TooManyArguments, actual.Errors.Select(e => e.Message));
        }

        [Theory]
        [InlineData("Trim([UnknownColumn])", ParseErrors.UnknownColumnOrCalculatedField)]
        [InlineData("Trim([InActiveColumn])", ParseErrors.InActiveColumn)]
        [InlineData("Trim([UnSelectableColumn])", ParseErrors.UnSelectableColumn)]
        [InlineData("Trim([CalculatedFieldNoActive])", ParseErrors.CantUseDeletedCalculatedField)]
        [InlineData("Trim([CalculatedFieldNotPublic])", ParseErrors.PrivateCalculatedField)]
        public async Task ShouldReturnParseErrorForInvalidColumn(string formula, string error)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);

            if (!string.IsNullOrEmpty(error))
                Assert.Contains(error, actual.Errors.Select(e => e.Message));
        }

        [Theory]
        [InlineData("If([user].[user_id] == -1 || trim([training].[training title]) == \"\" , [user].[name_first], \"N/A\")", "ignore")]
        [InlineData("If(Len([user].[name_first]) <= 100 , [user].[name_first], \"N/A\")", "ignore")]
        [InlineData("If((22 + 100) * [calculated_field_num1] <= 100 && [calculated_field_num2] == 4, 100, 200)", "ignore")]
        [InlineData("If(DATEDIFF(day,[Transcript].[Transcript_Due_Date],TODAY()) < 7, [Transcript].[Transcript_Due_Date], Today())", "ignore")]
        [InlineData("CASE([Training].[Training Hours] >= 1, Yes, No, Yes)", "IIF([46:-56] >= 1,0,1)")]
        [InlineData("CASE([Training].[Training Hours] >= 1, No, \"no_value\", \"yes_value\")", "IIF([46:-56] >= 1,'yes_value','no_value')")]
        [InlineData("CASE([Training].[Training Hours] >= 1, No, \"no_value\", Yes, \"yes_value\", \"otherwise\")",
                    "CASE  WHEN NOT([46:-56] >= 1) THEN 'no_value' WHEN [46:-56] >= 1 THEN 'yes_value'  ELSE 'otherwise' END")]
        [InlineData("CASE(IsNumber([Training].[Training Hours]), Yes, No, Yes)", "CASE ISNUMERIC([46:-56]) WHEN 1 THEN 0  ELSE 1 END")]
        [InlineData("CASE(IsNumber([Training].[Training Hours]), Yes, \"num\", No, \"not_num\", \"uknown\")",
                    "CASE ISNUMERIC([46:-56]) WHEN 1 THEN 'num' WHEN 0 THEN 'not_num'  ELSE 'uknown' END")]
        [InlineData("IF(ISBLANK([Certifications].[Certification Period User Completion Date]) && [Certifications].[Certification Training Not Activated]=\"Not Activated\", 0, IF(ISBLANK([Certifications].[Certification Period User Completion Date]), 50, 100))",
                    "IIF([50:-488] IS NULL AND [50:-730] = 'Not Activated',0,IIF([50:-488] IS NULL,50,100))")]
        [InlineData("IF(Contains(\"something\", \"value\"), 1, 0)", "IIF('something' LIKE '%value%',1,0)")]
        [InlineData("IF(IsNumber(\"value\"), 1, 0)", "IIF(ISNUMERIC('value') = 1,1,0)")]
        [InlineData("CASE(Yes, [Training].[Training Hours] > 1, \"value1\", [Training].[Training Hours] == 1, \"value2\", \"otherwise\")",
                    "CASE  WHEN [46:-56] > 1 THEN 'value1' WHEN [46:-56] = 1 THEN 'value2'  ELSE 'otherwise' END")]
        [InlineData("CASE(No, [Training].[Training Hours] > 1, \"value1\", [Training].[Training Hours] == 1, \"value2\", \"otherwise\")",
                    "CASE  WHEN NOT([46:-56] > 1) THEN 'value1' WHEN NOT([46:-56] = 1) THEN 'value2'  ELSE 'otherwise' END")]
        public async Task ShouldParseValidLogicalExpression(string formula, string expected)
        {
            var actual = await _sut.Parse(formula, _context);
             
            Assert.True(actual.IsSuccess);
            Assert.True(expected == "ignore" || actual.Sql == expected);
        }

        [Theory]
        [InlineData("If(Len([user].[user_id]) + 100 , [user].[name_first], \"N/A\")", "InvalidArgumentOrExpression")]
        [InlineData("If((22 + 100) * [calculated_field_num1] <= 100 && [calculated_field_num2] + 4, 100, 200)", "InvalidArgumentOrExpression")]
        [InlineData("If(DATEDIFF(day,[Transcript].[Transcript_Due_Date],TODAY()) < 7 || 1 + 8, [Transcript].[Transcript_Due_Date], Today())", "InvalidArgumentOrExpression")]
        [InlineData("CASE([Training].[Training Hours]>=1&&<=25, 25, 0,0,0000)", ParseErrors.UnexpectedToken)]
        public async Task ShouldReturnErrorForInvalidLogicalExpression(string formula, string expectedError)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(actual.Errors, e => e.Message == expectedError);
        }

        [Theory]
        [InlineData("Round(Len(Trim([user].[name_first])),2)", ParseErrors.MaxFunctionNestingExceeded, "Trim", 2, "(Round->Len->Trim)")]
        [InlineData("Round(GSUM([user].[user_id]),2)", ParseErrors.FunctionCantBeNested, "GSUM", 1, "(Round->GSUM)")]
        public async Task ShouldReturnErrorForInvalidFunctionNestingLevel(string formula, string expectedError, string function, int level, string nestingHierarchy)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.False(actual.IsSuccess);
            Assert.Contains(actual.Errors,
                e => e.Message == expectedError && e.Data["function"]?.ToString() == function &&
                     (int)e.Data["Level"] == level && 
                     (string)e.Data["nestingHierarchy"] == nestingHierarchy);
        }

        [Theory]
        [InlineData("If([user_has_photo] == \"yes\", 1, 2)", true, null)]
        [InlineData("If(\"NO\" != [user_has_photo], 1, 2)", true, null)]
        [InlineData("If([User].[date of birth] <= \"1990-01-01\", \"NOOOO Way!\", \"Mature Enough!\")", true, null)]
        [InlineData("If([user_has_photo] == 1, 1, 2)", false, "InvalidArgumentOrExpression")]
        [InlineData("Year([training_version_start_dt]) - 2", true, null)]
        [InlineData("Year([training_version_end_dt]) - 2", true, null)]
        [InlineData("Trim([Test Report].[Question Correct Response])", false, null)] // CSVLookup is disabled
        [InlineData("Concat(\"Language is: \", Language)", false, null)] // LookupSet is disabled
        [InlineData("Trim([User Succession Metrics].[M-24: Metric Grid Text Box])", true, null)]
        [InlineData("[Compensation Task].[New Target NQO %] * 100", true, null)]
        public async Task ShouldValidateFormulaUsingColumnDisplayType(string formula, bool shouldSucceed, string expectedError)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(shouldSucceed, actual.IsSuccess);

            if (actual.Errors != null && !string.IsNullOrEmpty(expectedError))
                Assert.Contains(actual.Errors, e => e.Message == expectedError);
        }

        [Theory]
        [InlineData("Round([1001]/100 * 2, [1002]/2)", true, "Round([-1:1001] / 100 * 2,[-1:1002] / 2)", null)]
        [InlineData("Round([Bad].[1001]/100 * 2, [1002]/2)", false, null, null)]
        [InlineData("If([70].[-1095] == yes, 1, 2)", true, "IIf([70:-1095] = 1,1,2)", null)]
        [InlineData("If([-1095] == yes, 1, 2)", true, "IIf([70:-1095] = 1,1,2)", null)]
        public async Task ShouldParseFormulaWithColumnIdInsteadOfTitle(string formula, bool shouldSucceed, string expectedSql, string expectedXml)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(shouldSucceed, actual.IsSuccess);
            Assert.True(expectedSql == null || expectedSql.Equals(actual.Sql, StringComparison.OrdinalIgnoreCase));
            Assert.True(expectedXml == null || expectedXml.Equals(actual.Xml, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData(" Contains ([training].[training title], \"text'\"\"()\")", "[46:-48] LIKE '%text''\"()%'", "ignore")]
        [InlineData("[training].[training title] == \"Title1\"", "[46:-48] = 'Title1'", "ignore")]
        [InlineData("yes", "1", "ignore")]
        [InlineData("\"yes\" == IsBlank([training].[training title])", "1 = IIF([46:-48] IS NULL, 1, 0)", "ignore")]
        [InlineData("\"2020-01-01\" != Today2()", "'2020-01-01' <> CONVERT(date, GetUtcDate())", "ignore")]
        [InlineData("ISNUMBER([User].[name_first]) && ISBLANK([user].[date of birth])", "ISNUMERIC([70:1]) = 1 AND [70:-99] IS NULL", "ignore")]
        public async Task ShouldNotWrapSqlForBooleanResults(string formula, string expectedSql, string expectedXml)
        {
            var actual = await _sut.Parse(formula, _context);

            AssertValidParserResult(actual, expectedSql, expectedXml);
        }

        [Theory]
        [InlineData("yes != \"no\"", true, "1 <> 0")]
        [InlineData("yes + 1", false, null)]        
        [InlineData("[User].[user_has_photo]+Count([Training].[training_version_start_dt])  *  12", false, null)]
        public async Task NoBooleanArithmetic(string formula, bool shouldSucceed, string expectedSql)
        {
            _context.Options.OutputType = DataType.Boolean;
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(shouldSucceed, actual.IsSuccess);
            Assert.True(expectedSql == null || expectedSql.Equals(actual.Sql, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("1/(100-100)*200", true, true, "1 / (100 - 100) * 200", "")]
        [InlineData("[Training].[Training Price]/([Training].[Training Hours])", true, true, "[46:-53] / NULLIF(([46:-56]),0)", "")]
        [InlineData("([Training].[Training Price]/(2*[Training].[Training Hours]/[calculated_field_num1]+2))", true, true,
                    "([46:-53] / NULLIF((2 * [46:-56] / NULLIF([-1:1001],0) + 2),0))",
                    "([46:-53] / NULLIF((ISNULL(2 * [46:-56] / NULLIF([-1:1001],0),0) + 2),0))")]
        [InlineData("([Training].[Training Price]/(2*8/[calculated_field_num1]+2))", true, true,
                    "([46:-53] / (2 * 8 / NULLIF([-1:1001],0) + 2))", "")]
        [InlineData("((( GSUM([Transcript].[Training Point Value]) + GSUM([Transcript].[Transcript Score])) / GSUM([Transcript].[Transcript Score]))  * 100 ) ", true, true,
            "(((SUM([42:-1430]) + SUM([46:-77])) / NULLIF(SUM([46:-77]),0)) * 100)",
            "(((ISNULL(SUM([42:-1430]),0) + ISNULL(SUM([46:-77]),0)) / NULLIF(SUM([46:-77]),0)) * 100)")]
        [InlineData("[Transcript].[Training Point Value] * ([Transcript].[Transcript Score] + 1)", true, true,
                    "[42:-1430] * ([46:-77] + 1)", "[42:-1430] * (ISNULL([46:-77],0) + 1)")]
        [InlineData("[Transcript].[Training Point Value] / (1 / [Transcript].[Transcript Score] * 2)", true, true,
                    "[42:-1430] / (1 / NULLIF([46:-77],0) * 2)", "")]
        [InlineData("[Transcript].[Training Point Value] / ((1 - [Transcript].[Transcript Score] * 2)) + 2", true, true,
                    "[42:-1430] / NULLIF(((1 - [46:-77] * 2)),0) + 2",
                    "ISNULL([42:-1430] / NULLIF(((1 - ISNULL([46:-77] * 2,0))),0),0) + 2")]
        [InlineData("([Training].[Training Price] + (2 * [Training].[Training Hours] - [calculated_field_num1] + 2))", true, true,
                    "(ISNULL([46:-53],0) + ISNULL((ISNULL(2 * [46:-56],0) - ISNULL([-1:1001],0) + 2),0))", "")]
        [InlineData("IF([46].[-2084] + [46].[-2083] >= 0 && [46].[-460] + 2 <= 2000, \"R1\", \"R2\")", true, true,
                    "IIF(ISNULL([46:-2084],0) + ISNULL([46:-2083],0) >= 0 AND ISNULL([46:-460],0) + 2 <= 2000,'R1','R2')", "")]
        [InlineData("[46].[-2084] + [46].[-2083]", true, true, "ISNULL([46:-2084],0) + ISNULL([46:-2083],0)", "")]
        [InlineData("[46].[-2084]", true, true, "[46:-2084]", "")]
        [InlineData("[46].[-2084] + [46].[-2083] * [46].[-460]", true, true, "ISNULL([46:-2084],0) + ISNULL([46:-2083] * [46:-460],0)", "")]
        public async Task ShouldApplySqlRules(string formula, bool applySqlRules, bool shouldSucceed, string expectedSql, string sql2)
        {
            if (!TestData.ColumnExists(46, -2084))
                TestData.AddColumn("Training", "event_max_enrollment", 46, -2084, ColumnDataType.Integer, ColumnDisplayTypeEnum.Integer);
            if (!TestData.ColumnExists(46, -2083))
                TestData.AddColumn("Training", "event_min_enrollment", 46, -2083, ColumnDataType.Integer, ColumnDisplayTypeEnum.Integer);
            if (!TestData.ColumnExists(46, -460))
                TestData.AddColumn("Training", "Minimum_part_attendance", 46, -460, ColumnDataType.Integer, ColumnDisplayTypeEnum.Integer);

            if (applySqlRules) _context.Options.SqlOptions = FormulaSqlOptions.All;
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(shouldSucceed, actual.IsSuccess);
            Assert.True(expectedSql == null || expectedSql.Equals(actual.Sql) || sql2.Equals(actual.Sql));
        }

        // RPT-9431
        [Theory]
        [InlineData("IF([Training].[Training Title]=\"Passport to Safety Curriculum\", [Transcript].[Transcript_Due_Date]-180,[Transcript].[Transcript_Due_Date]-30)", true, true,
            /*without null propagation guard applied*/ "IIF([46:-48] = 'Passport to Safety Curriculum',[42:-45] - 180,[42:-45] - 30)",
            /*with null propagation guard incorrectly applied*/ "IIF([46:-48] = 'Passport to Safety Curriculum',ISNULL([42:-45],0) - 180,ISNULL([42:-45],0) - 30)")]
        public async Task ShouldNotApplyNullPropagationGuardForNonNumericArithmetic(string formula, bool applySqlRules, bool shouldSucceed, string expectedSql, string sql2)
        {
            if (applySqlRules) _context.Options.SqlOptions = FormulaSqlOptions.All;
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(shouldSucceed, actual.IsSuccess);
            Assert.True(expectedSql == null || expectedSql.Equals(actual.Sql) || sql2.Equals(actual.Sql));
        }

         // RPT-9506 -- CASE function with AbsoluteDate and Datetime arguments validation
        [Theory]
        [InlineData("CASE([Training].[Training provider], \"SQI International\",  [Training record].[Certified Date], \"St John Singapore\",  [Training record].[Certified Date],\"BCA Academy\",  [Training record].[Certified Date], [Training record].[Training record completed date])",
                    "CASE [46:-52] WHEN 'SQI International' THEN [42:870] WHEN 'St John Singapore' THEN [42:870] WHEN 'BCA Academy' THEN [42:870]  ELSE [42:-76] END")]
        public async Task CaseFunctionShouldValidateForAbsoluteDateColumns(string formula, string expectedSql)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
            Assert.Equal(expectedSql, actual.Sql);
        }

        [Theory]
        [InlineData("IF(ISNUMBER([Transcript].[Transcript_Due_Date]),1,0)", "IIF(ISNUMERIC([42:-45]) = 1,1,0)")]
        [InlineData("ISNUMBER([Transcript].[Transcript_Due_Date])", "ISNUMERIC([42:-45])")]
        [InlineData("CASE(ISNUMBER([42].[-45]), yes, \"YESSSS\", no, \"NOOOOO\", \"Oops!\")", 
                    "CASE ISNUMERIC([42:-45]) WHEN 1 THEN 'YESSSS' WHEN 0 THEN 'NOOOOO'  ELSE 'Oops!' END")]
        public async Task ShouldTranslateLogicalFunctionExpression(string formula, string expected)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
            Assert.Equal(expected, actual.Sql);
        }

        [Theory]
        [InlineData("DateDiff(day, [User].[date of birth], \"1/1/2020\") / 365", true, 3, "sqldatediff(day,[70:-99],'1/1/2020') / 365.0")]
        [InlineData("100 / [Transcript].[Training Point Value]", true, null, "100 / NULLIF([42:-1430],0)")]
        [InlineData("100 / [Transcript].[Training Point Value]", true, 2, "100.0 / NULLIF([42:-1430],0)")]
        [InlineData("100 / [Transcript].[Training Point Value]", false, 2, "100 / NULLIF([42:-1430],0)")]
        [InlineData("[Training].[Training Price] / [Training].[Training Hours]", true, 2, "[46:-53] / NULLIF([46:-56],0)")]
        [InlineData("55*(75 + [Transcript].[Training Point Value]) / (2 + [Transcript].[Transcript Score])", true, 2, "55 * ((75 + ISNULL([42:-1430],0))) * 1.0 / NULLIF((2 + ISNULL([46:-77],0)),0)")]
        public async Task ShouldConvertIntegerDivisionToDecimalDivision(string formula, bool applyDecimalDivision, int? decimalPoints, string expectedSql)
        {
            _context.Options.SqlOptions = FormulaSqlOptions.All;
            _context.Options.OutputType = DataType.Number;
            _context.Options.DecimalPoints = decimalPoints;

            if (!applyDecimalDivision)
            {
                _context.Options.SqlOptions = FormulaSqlOptions.All & ~FormulaSqlOptions.DecimalDivision;
            }

            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
            Assert.Equal(expectedSql, actual.Sql);
        }

        [Theory]
        [InlineData("CASE([トレーニング受講リスト].[カリキュラム タイトル (トレーニング受講リスト)], \"空調基礎\", \"空基\", \"Full Courses in English\", \"EngFull\", \"その他\" )",
                    "CASE [42:-1563] WHEN N'空調基礎' THEN N'空基' WHEN 'Full Courses in English' THEN 'EngFull'  ELSE N'その他' END")]
        [InlineData(" Contains2 ([training].[training title], \"text'\"\"()\")", "[46:-48] LIKE N'%text''\"()%'")]
        [InlineData(" Contains2 ([training].[training title], \"text'\"\"(ጫላ ጩቤ ጨበጠ)\")", "[46:-48] LIKE N'%text''\"(ጫላ ጩቤ ጨበጠ)%'")]
        [InlineData("Concat2(\"Kaaz\", 2020, \"2000-12-31\", yes)", "Concat('Kaaz',2020,'2000-12-31',1)")]
        [InlineData("Concat2(\"Kaaz\", 2020, \"2000-12-31\", \"你好！\")", "Concat('Kaaz',2020,'2000-12-31',N'你好！')")]
        [InlineData(" Contains2 ([user].[user full name], [user].[name_first])", "[70:-1] LIKE N'%' + [70:1] + '%'")]
        [InlineData(" Contains2 ([user].[user full name], Concat(1,2,3))", "[70:-1] LIKE N'%' + Concat(1,2,3) + '%'")]
        public async Task ShouldAddSqlUnicodeLiteralMarker(string formula, string expected)
        {            
            _context.Options.SqlOptions = FormulaSqlOptions.All;
            var actual = await _sut.Parse(formula, _context);

            Assert.True(actual.IsSuccess);
            Assert.Equal(expected, actual.Sql);
        }

        [Theory]
        [InlineData("[70].[-1095]", "[70:-1095]")]
        [InlineData("[70].[-1095] == no", "[70:-1095] = 0")]
        [InlineData("[70].[-1095] == yes", "[70:-1095] = 1")]
        [InlineData("[70].[-1095] != yes", "[70:-1095] <> 1")]
        [InlineData("If([70].[-1095], 1, 2)", "IIF([70:-1095] = 1,1,2)", DataType.Number)]
        [InlineData("If([70].[-1095] == no, 1, 2)", "IIF([70:-1095] = 0,1,2)", DataType.Number)]
        [InlineData("If([70].[-1095] == yes, 1, 2)", "IIF([70:-1095] = 1,1,2)", DataType.Number)]
        [InlineData("If([70].[-1095] != yes, 1, 2)", "IIF([70:-1095] <> 1,1,2)", DataType.Number)]
        [InlineData("[70].[-1095] && [70].[-1095]", "[70:-1095] = 1 AND [70:-1095] = 1")]
        [InlineData("[70].[-1095] || [70].[-1095] == No", "[70:-1095] = 1 OR [70:-1095] = 0")]
        //TODO: [InlineData("If(If([70].[-1095], no, yes), 1, 2)", "IIF(IIF([70:-1095] = 1,0,1) = 1,1,2)", DataType.Number)]
        public async Task ShouldConvertBooleanExpression(string formula, string expectedSql,
            DataType resultType = DataType.Boolean)
        {
            var actual = await _sut.Parse(formula, _context);

            Assert.Equal(resultType, actual.ResultType);
            Assert.Equal(expectedSql, actual.Sql);
        }

        public static IEnumerable<object[]> NullTestData =>
            new List<object[]>
            {
                new object[] { null, new ParserContext(null, null, null)  },
                new object[] { "", new ParserContext(null, null, null) },
                new object[] { "some_formula", null  }
            };

        public static IEnumerable<object[]> ReferencedColumnsTestData =>
            new List<object[]>
            {
                new object[]
                    {"func1([user].[name_first])", new[] {TestData.GetColumn("user", "name_first") }},
                new object[]
                {
                    "round([calculated_field_num1] + [calculated_field_num2], 2)",
                    new[]
                    {
                        TestData.GetColumn(null, "calculated_field_num1"),
                       TestData.GetColumn(null, "calculated_field_num2")
                    }
                },
                new object[] {"today()", new IColumn[0]}
            };



        private static void AssertValidParserResult(ParseResult actual, string expectedSql, string expectedXml)
        {
            Assert.True(actual.IsSuccess);

            if (expectedSql != "ignore")
                actual.Sql.ShouldBeEqualTo(expectedSql);

            if (expectedXml != "ignore")
                actual.Xml.ShouldBeEqualTo(expectedXml);

            Assert.True(actual.Errors == null || !actual.Errors.Any());
        }

        private static void AssertValidParserResult(ParseResult actual, ICollection<IColumn> expectedColumns)
        {
            Assert.True(actual.IsSuccess);
            Assert.True(actual.Errors == null || !actual.Errors.Any());
            Assert.Equal(actual.ReferencedColumns, expectedColumns, new ColumnComparer());
        }

        class ColumnComparer : IEqualityComparer<IColumn>
        {
            public bool Equals(IColumn x, IColumn y)
            {
                return x.EntityId == y.EntityId && x.ColumnId == y.ColumnId;
            }

            public int GetHashCode(IColumn obj)
            {
                return $"{obj.EntityId}-{obj.ColumnId}".GetHashCode();
            }
        }

    }

    internal static class FormulaParserExtensions
    {
        public static Task<ParseResult> Parse(this IFormulaParser parser, string formula) => parser.Parse(formula, default(FormulaParserOptions));
    }
}
