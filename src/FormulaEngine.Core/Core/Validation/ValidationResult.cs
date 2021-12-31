using System.Collections.Generic;
using System.Linq;

namespace FormulaEngine.Core.Validation
{
    internal class ValidationResult
    {
        public ICollection<ParseError> Errors { get; set; }
        public bool IsValid => Errors == null || !Errors.Any();

        public static implicit operator ValidationResult(ParseError error) => error == null
            ? null
            : new ValidationResult
            {
                Errors = new List<ParseError>() {error}
            };

        public static implicit operator ValidationResult(List<ParseError> errors) => new ValidationResult
        {
            Errors = errors
        };

        public static implicit operator ValidationResult(FluentValidation.Results.ValidationResult result) =>
            new ValidationResult
            {
                Errors = result?.Errors?.Select(e => new ParseError(e.ErrorMessage, e.FormattedMessageArguments?.FirstOrDefault()))?.ToList()
            };
    }
}