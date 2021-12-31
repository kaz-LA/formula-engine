using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Parser.Internal;

namespace FormulaEngine.Core.Validation
{
    internal class ColumnValidator : AbstractValidator<IColumn>
    {
        private readonly IParserContext _context;
        private readonly Token _token;

        public ColumnValidator(Token theToken, IParserContext context)
        {
            _context = context;
            _token = theToken;

            RuleFor(x => x)
               .Must(ItIsAValidColumn)
               .WithMessage(ErrorCode.UnknownColumnOrCalculatedField,  new { value = _token.Text, Index = _token.StartIndex });

            When(ItIsAValidColumn, () =>
            {
                RuleFor(x => x.IsActive)
                    .Must(BeTrue)
                    .WithMessage(ErrorCode.InActiveColumn,  new { column = _token.Text, Index = _token.StartIndex });

                RuleFor(x => x.IsSelectable)
                    .Must(BeTrue)
                    .WithMessage(ErrorCode.UnSelectableColumn,  new { column = _token.Text, Index = _token.StartIndex });

                RuleFor(x => x.ColumnDisplayType)
                    .Must(BeUseableInCalculatedFields)
                    .WithMessage("EX.RPT.ColumnNotSupportedInCalcFields", new { column = _token.Text, Index = _token.StartIndex });
            });

            When(ItIsACalculatedField, () =>
            {
                RuleFor(c => c)
                    .MustAsync(async (c, ct) => await NotReferenceAnotherCalculatedField(c))
                    .WithMessage(ErrorCode.InvalidCalculatedFieldUsage,  new { calculatedField = _token.Text, Index = _token.StartIndex });

                RuleFor(c => c.CalculatedFieldIsActive)
                    .Must(BeTrue)
                    .WithMessage(ErrorCode.CantUseDeletedCalculatedField,  new { calculatedField = _token.Text, Index = _token.StartIndex });

                RuleFor(c => c)
                    .Must(BePublicOrOwnedByTheUser)
                    .WithMessage(ErrorCode.PrivateCalculatedField,  new { calculatedField = _token.Text, Index = _token.StartIndex });
            });
        }

        public override async Task<FluentValidation.Results.ValidationResult> ValidateAsync(ValidationContext<IColumn> context, CancellationToken cancellation)
        {
            if(context.InstanceToValidate == null)
            {
                return await base.ValidateAsync(new ColumnInfo());
            }

            return await base.ValidateAsync(context);
        }

        private bool ItIsAValidColumn(IColumn column) => column != null && column.ColumnId != 0;

        private bool BeTrue(bool value) => value;

        private bool BeUseableInCalculatedFields(ColumnDisplayTypeEnum displayType)
            => displayType != ColumnDisplayTypeEnum.LookupSet && displayType != ColumnDisplayTypeEnum.CSVLookup;

        private bool BeTrue(bool? value) => value.GetValueOrDefault();

        private bool ItIsACalculatedField(IColumn column) => column != null && column.IsCalculatedField();

        private async Task<bool> NotReferenceAnotherCalculatedField(IColumn column)
            => !await _context.ReferencesAnotherCalculatedField(column.CalculatedFieldId ?? 0);

        private bool BePublicOrOwnedByTheUser(IColumn column)
                => column.CalculatedFieldIsPublic == true || column.CalculatedFieldCreatedBy == _context.UserId;

    }
}
