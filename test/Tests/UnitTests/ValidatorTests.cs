using System.Threading.Tasks;
using Xunit;

namespace FormulaEngineTests.UnitTests
{
    [Trait("Category", "CalculatedFieldValidatorTests")]
    public class ValidatorTests
    {
        private readonly CalculatedFieldValidator _sut;
        private readonly Moq.Mock<ICalculatedFieldMeta> _meta;

        public ValidatorTests()
        {
            _meta = new Moq.Mock<ICalculatedFieldMeta>();
            _meta.Setup(_ => _.GetCalculatedFieldId("cf1")).ReturnsAsync(1);

            _sut = new CalculatedFieldValidator(_meta.Object);
        }

        [Theory]
        [InlineData(null, "cf0", "", false, "EX.RPT.MissingFormula")]     
        [InlineData("X+Y", "", "", false, "EX.RPT.CalculatedFieldNameIsRequired")] 
        [InlineData("X+Y", "$LEN$", "", false, "EX.RPT.CalculatedFieldNameTooLong")] 
        [InlineData("X+Y", "cf1", null, false, "EX.RPT.CalculatedFieldUniqueName")]        
        [InlineData("X+Y", "cf1", "$LEN$", false, "EX.RPT.CalculatedFieldDescriptionTooLong")]
        [InlineData("X+Y", "_cf_[]_+)(*&^%$#@!~?|{}-';:.,", "descr':;[]_+)(*&^%$#@!~?|{}-.,", true, null)]
        [InlineData("X+Y", "_cf_[]_+)(*&^%$#@!~?|{}-';:.,", null, true, null)]
        public async Task ShouldValidateFormula(string formula, string name, string description, bool isValid, string expectedError)
        {
            if (name == "$LEN$") name = new string('x', 129);
            if (description == "$LEN$") description = new string('x', 601);

            var model = new CalculatedFieldModel() { Formula = formula, Name = name, Description = description };

            var result = await _sut.ValidateAsync(model);

            Assert.Equal(isValid, result.IsValid);
            if (!isValid)
                AssertContainsError(result, expectedError);
        }

        [Theory]
        [InlineData("X+Y", "cf0[]_+)(*&^%$#@!~?|{}-.,", true)]
        [InlineData("X+Y", "cf3<", false)]
        [InlineData("X+Y", "cf2>", false)]
        [InlineData("X+Y", "cf1/", false)]
        [InlineData("X+Y", "cf1\\", false)]
        public async Task ShouldValidateNameForInvalidChar(string formula, string name, bool isValid)
        {
            var model = new CalculatedFieldModel() { Formula = formula, Name = name};
            
            var result = await _sut.ValidateAsync(model);

            Assert.Equal(isValid, result.IsValid);
            if(!isValid)
                AssertContainsError(result, "EX.RPT.CalculatedFieldNameContainsInvalidChar");
        }

        [Theory]
        [InlineData("X+Y", "cf0_[]_+)(*&^%$#@!~?|{}-';:.,", "Hi, there_<", false)]
        [InlineData("X+Y", "cf0_[]_+)(*&^%$#@!~?|{}-';:.,", "Hi, there_>", false)]
        [InlineData("X+Y", "cf0_[]_+)(*&^%$#@!~?|{}-';:.,", "Hi, there_/", false)]
        [InlineData("X+Y", "cf0_[]_+)(*&^%$#@!~?|{}-';:.,", "Hi, there_\\", false)]
        public async Task ShouldValidateDescriptionForInvalidChar(string formula, string name, string description, bool isValid)
        {
            var model = new CalculatedFieldModel() { Formula = formula, Name = name, Description = description };

            var result = await _sut.ValidateAsync(model);

            Assert.Equal(isValid, result.IsValid);
            if (!isValid)
                AssertContainsError(result, "EX.RPT.CalculatedFieldDescriptionContainsInvalidChar");
        }

        private static void AssertContainsError(ValidationResult validationResult, string error)
        {
            Assert.Contains(validationResult.Errors, _ => _.ErrorMessage == error);
        }
    }
}