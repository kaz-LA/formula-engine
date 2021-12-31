using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormulaEngine.Core;
using FormulaEngine.Core.Parser;
using FormulaEngine.Contracts.Enums;
using FormulaEngine.Contracts.Interfaces;
using FormulaEngine.Contracts.Models;
using FormulaEngine.Data;
using Xunit;

namespace FormulaEngineTests.UnitTests
{
    [Trait("Category", "CalculatedFieldMetaTests")]
    public class ServiceTests : IClassFixture<CalculatedFieldsTestContextFixture>
    {
        private ICalculatedFieldMeta _sut;
        private TestUser _user;
        private readonly TestContext _testContext;
        private Mock<IResourceLocalizer> _localizerMock;
        private IFormulaParserContext _parserContext;

        public ServiceTests(CalculatedFieldsTestContextFixture fixture)
        {
            _testContext = fixture;
            _user = fixture.User;
            var utilsProvider = new Mock<IReportingUtilsProvider>();
            var repository = new CalculatedFieldRepository(fixture.ContextFactory, _user, utilsProvider.Object);
            _sut = new CalculatedFieldMeta(repository);
            _parserContext = new ParserContext(_sut, _user, Localizer());
        }

        private IFormulaParser Parser() => new FormulaParser(_parserContext);

        private IResourceLocalizer Localizer()
        {
            if (_localizerMock == null)
            {
                _localizerMock = new Mock<IResourceLocalizer>();
            }

            return _localizerMock.Object;
        }

        [Theory(Skip = "skipping - fails when run in VS Test Runner, but passes when run individually")]
        [InlineData("DateAdd(day, 2, \"10/12/2020 21:30\")", ColumnDisplayTypeEnum.AbsoluteDateTime)]
        //[InlineData("DateAdd(month, 1, [User].[Termination Date])", ColumnDisplayTypeEnum.AbsoluteDate)]
        //[InlineData("DateAdd(day, 1, [User Career Preferences].[Last Modified Date])", ColumnDisplayTypeEnum.DateTime)]
        public async Task ShouldConsiderArgumentType(string formula, ColumnDisplayTypeEnum expectedColumnDisplayType )
        {
            var model = new CalculatedFieldModel
            {
                Expression = formula,
                Name = "dateAdd",
                OutputTypeId =  DataType.Datetime
            };

            var actual = await _sut.CreateCalculatedField(model, Parser(), MyMapper());

            Assert.Equal(expectedColumnDisplayType, actual.Column.ColumnDisplayTypeId);
        }

        [Fact(Skip = "unreliable")]
        public async Task CreateCalculatedField_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateCalculatedField(new CalculatedFieldModel(), null, MyMapper()));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateCalculatedField(null, Parser(), MyMapper()));
        }

        [Fact(Skip = "unreliable")]
        public async Task CreateCalculatedField_ThrowsParserException()
        {
            var model = new CalculatedFieldModel
            {
                Expression = "UnknownFunction([User].[User First Name])",
                Name = "CF01",
                OutputTypeId = DataType.String
            };

            await Assert.ThrowsAsync<CalculatedFieldValidationException>(() => _sut.CreateCalculatedField(model, Parser(), MyMapper()));
        }

        [Theory(Skip = "unreliable")]
        [MemberData(nameof(TestDataForCreateCalculatedField))]
        public async Task ShouldCreateCalculatedField(CalculatedFieldModel model, bool hasAggregateFunction, ColumnDisplayTypeEnum expectedDisplayType)
        {
            RemoveTestData();
            var actual = await CreateCalculatedField(model);
            AssertCalculatedField(actual, hasAggregateFunction, model.IsPublic, expectedDisplayType);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldGetCalculatedFieldById()
        {
            RemoveTestData();
            var model = CreateCalculatedField("cf01", DataType.Datetime, "Today()");
            var calcField = await CreateCalculatedField(model);

            var actual = _sut.GetCalculatedField(calcField.Id);
            Assert.NotNull(actual);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldGetAllCalculatedFields()
        {
            RemoveTestData();
            var model = CreateCalculatedField("cf01", DataType.Datetime, "Today()");
            await CreateCalculatedField(model);

            var actual = await _sut.GetCalculatedFields(new TestUser("corp1", 999, 1));
            Assert.NotEmpty(actual);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldReturnNullWhenUpdatingNonExistentCalculatedField()
        {
            var model = CreateCalculatedField("cf01", DataType.Number, "DateDiff(day, [User].[Termination Date], Today())");
            model.Id = 99999;

            var actual = await _sut.UpdateCalculatedField(model, Parser(), MyMapper());
            Assert.Null(actual);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldUpdateCalculatedField()
        {
            RemoveTestData();
            var model = CreateCalculatedField("cf01", DataType.Number, "DateDiff(day, [User].[Termination Date], Today())");
            var actual = await CreateCalculatedField(model);

            model.Name = "cf02";
            model.Id = actual.Id;
            model.Formula = "LEN([User].[User First Name])";

            actual = await _sut.UpdateCalculatedField(model, Parser(), MyMapper());
            AssertCalculatedField(actual, false, model.IsPublic, ColumnDisplayTypeEnum.Decimal);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldNotUpdateGlobalFieldWithoutPermission()
        {
            RemoveTestData();
            var model = CreateCalculatedField("cf01", DataType.Number, "DateDiff(day, [User].[Termination Date], Today())");
            var actual = await CreateCalculatedField(model);

            model.Name = "cf02";
            model.Id = actual.Id;

            var newUser = new TestUser("test01", 9999, 1);
            var utilsProvider = new Mock<IReportingUtilsProvider>();
            var repository = new CalculatedFieldRepository(_testContext.ContextFactory, newUser, utilsProvider.Object);
            _sut = new CalculatedFieldMeta(repository);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateCalculatedField(model, Parser(), MyMapper()));
            Assert.Contains("EX.RPT.CalculatedFieldUpdateDenied", exception.Message);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldUpdateGlobalFieldWithPermission()
        {
            RemoveTestData();
            var model = CreateCalculatedField("cf01", DataType.Datetime, "Today()");
            var actual = await CreateCalculatedField(model);

            model.Name = "cf02";
            model.Id = actual.Id;

            var newUser = new TestUser("test01", 9999, 1);
            newUser.HasManageGlobalCalculatedFieldsAccess = true;
            var utilsProvider = new Mock<IReportingUtilsProvider>();
            var repository = new CalculatedFieldRepository(_testContext.ContextFactory, newUser, utilsProvider.Object);
            _sut = new CalculatedFieldMeta(repository);

            actual = await _sut.UpdateCalculatedField(model, Parser(), MyMapper());
            AssertCalculatedField(actual, false, model.IsPublic, ColumnDisplayTypeEnum.Date);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldReturnErrorWhenDeletingNonExistentCalculatedField()
        {
            var actual = await _sut.DeleteCalculatedField(-9900);
            Assert.Contains("EX.RPT.CalculatedFieldNotFound", actual.Errors);
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldDeleteCalculatedField()
        {
            RemoveTestData();
            var model = CreateCalculatedField("cf01", DataType.Number, "DateDiff(day, [User].[Termination Date], Today())");
            var calcField = await CreateCalculatedField(model);

            var actual = await _sut.DeleteCalculatedField(calcField.Id);
            Assert.True(actual.Succeeded);
        }

        [Fact(Skip = "unreliable")]
        public void ShouldReturnAllOperators()
        {
            var actual = _sut.GetOperators();
            Assert.Equal(FormulaEngine.Contracts.Models.FormulaOperator.GetAllOperators(), actual, new OperatorComparer());
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldReturnFunctions()
        {
            var actual = await _sut.GetFunctions();
            Assert.NotEmpty(actual);

            var ifFunc = actual.FirstOrDefault(f => f.Name == "IF");
            if (ifFunc != null)
            {
                Assert.False(ifFunc.IsVariableArguments);
                Assert.True(ifFunc.IsChartable);
            }
        }

        [Fact(Skip = "unreliable")]
        public async Task ShouldReturnDataTypes()
        {
            var actual = await _sut.GetDataTypes();
            Assert.Equal(Enum.GetValues(typeof(DataType)).Length, actual.Count);
        }

        private async Task<ICalculatedField> CreateCalculatedField(CalculatedFieldModel model)
            => await _sut.CreateCalculatedField(model, Parser(), MyMapper());

        private void AssertCalculatedField(ICalculatedField actual, bool hasAggregateFunction,
            bool isPublic, ColumnDisplayTypeEnum expectedDisplayType)
        {
            var context = _testContext.Context;

            Assert.NotNull(actual);
            Assert.True(actual.IsActive);
            Assert.Equal(hasAggregateFunction, actual.IsAggregate);
            ///TODO: if(decimalPoints != null) Assert.Equal(decimalPoints, actual.DecimalPoints);
            Assert.True(actual.IsChartable);
            Assert.False(actual.IsMigrated);
            Assert.Equal(isPublic, actual.IsPublic);
            Assert.NotNull(actual.SqlExpression);

            Assert.True(((Column)actual.Column).Active);
            Assert.True(actual.Column.IsSelectable);
            Assert.Equal(!hasAggregateFunction, actual.Column.IsFilterable);
            Assert.Equal(CalculatedFieldRepository.CalculatedFieldsTable, actual.Column.TableId);
            Assert.Equal(ColumnDataType.CalculatedField, actual.Column.ColumnDataTypeId);
            Assert.Equal(expectedDisplayType, actual.Column.ColumnDisplayTypeId);
            //Assert.Equal(Contracts.Enums.ColumnFilterTypeEnum.String, actual.Column.ColumnFilterTypeId);

            Assert.Contains(context.Columns, c => c.Name == actual.Name);
            Assert.True(actual.ReferencedColumns.All(rc => context.CalculatedFieldColumns.Any(cfc => cfc.ColumnId == rc.ColumnId)));
            Assert.Contains(context.UIGroupColumns, c => c.GroupId == CalculatedFieldRepository.CalculatedFieldsUIGroup);

            RemoveTestData();
        }

        private void RemoveTestData()
        {
            var context = _testContext.Context;
            context.Columns.RemoveRange(context.Columns.Where(_ => _.ColumnDataTypeId == ColumnDataType.CalculatedField));
            context.SaveChanges();
        }

        public static IEnumerable<object[]> TestDataForCreateCalculatedField =>
            new List<object[]>
            {
                new object[] { CreateCalculatedField("cf01", DataType.String, "CONCAT(1234, [User].[User First Name], \" \", [User].[User Last Name])"), false, ColumnDisplayTypeEnum.String },
                new object[] { CreateCalculatedField("cf02", DataType.Number, "GCOUNT([User].[User First Name])", true, 2), true, ColumnDisplayTypeEnum.Decimal },
                new object[] { CreateCalculatedField("cf03", DataType.Boolean, "ISNUMBER([User].[User First Name])"), false, ColumnDisplayTypeEnum.BooleanYesNo },
                new object[] { CreateCalculatedField("cf04", DataType.Datetime, "Today()"), false, ColumnDisplayTypeEnum.Date },
                new object[] { CreateCalculatedField("cf05", DataType.Datetime, "Now()"), false, ColumnDisplayTypeEnum.DateTime },
                new object[] { CreateCalculatedField("cf06", DataType.Datetime, "DATE(2020, 1, 1)"), false, ColumnDisplayTypeEnum.AbsoluteDate },
                new object[] { CreateCalculatedField("cf07", DataType.Datetime, "DATETIMEVALUE(\"2020-01-01 22:11:01\")"), false, ColumnDisplayTypeEnum.AbsoluteDateTime },
                ////TODO: new object[] { CreateCalculatedField("cf08", DataType.Boolean, "ISBLANK([AnotherCalculatedField])"), false, ColumnDisplayTypeEnum.BooleanYesNo },
            };

        private static CalculatedFieldModel CreateCalculatedField(string name, DataType outputType, string formula, bool isPublic = true, int? decimalPoints = null)
            => new CalculatedFieldModel
            {
                Expression = formula,
                Name = name,
                OutputTypeId = outputType,
                DecimalPoints = decimalPoints,
                IsPublic = isPublic
            };

        private static MapperConfiguration _mapperConfig;

        private static MapperConfiguration MapperConfig => _mapperConfig ?? (_mapperConfig =
            new MapperConfiguration(cfg =>
                cfg.CreateMap<CalculatedFieldModel, ICalculatedField>()
                    //.ForMember(c => c.OutputType, m => m.Ignore())
                    .ForMember(c => c.CreatedUserId, m => m.Ignore())
                    .ForMember(c => c.CreatedUser, m => m.Ignore())
                    .ForMember(c => c.CreatedDate, m => m.Ignore())
                    .ForMember(c => c.ModifiedDate, m => m.Ignore())
                    .ForMember(c => c.SqlExpression, m => m.Ignore())
                    .ForMember(c => c.Xml, m => m.Ignore())
                    .ForMember(c => c.ColumnId, m => m.Ignore())
                    .ForMember(c => c.Column, m => m.Ignore())
                    .ForMember(c => c.ReferencedColumns, m => m.Ignore())
                    .ForMember(c => c.IsActive, m => m.Ignore())
                    .ForMember(c => c.IsAggregate, m => m.Ignore())
                    .ForMember(c => c.IsChartable, m => m.Ignore())
                    .As<CalculatedField>()));

        private static IMapper MyMapper() => MapperConfig.CreateMapper();

        private class OperatorComparer : IEqualityComparer<IFormulaOperator>
        {
            public bool Equals(IFormulaOperator x, IFormulaOperator y) => x.Operator == y.Operator;

            public int GetHashCode(IFormulaOperator obj) => obj.GetHashCode();
        }
    }

    public class CalculatedFieldsTestContextFixture : TestContextFixture
    {
        protected override void PopulateAdditionalData(ReportingContext context)
        {           
            CalculatedFieldMeta.ClearCache();

            // functions and metadata
            context.Set<FunctionDataType>().AddRange(CsvDataReader.FunctionTypes());
            context.Set<FunctionCategory>().AddRange(CsvDataReader.FunctionCategories());
            context.Set<FunctionParameter>().AddRange(CsvDataReader.FunctionParameters());
            context.Functions.AddRange(CsvDataReader.Functions());

            context.Tables.Add(new Table { Id = -1, Name = "CalculatedFields" });
            context.ReportEntities.Add(new ReportEntity { Id = -1, Title = "CalculatedFields", TableId = -1, ModuleId = 8 });
            context.UIGroups.Add(new UIGroup { Id = -1, Name = "CalculatedFields", EntityId = -1 });
        }
    }

}
