using System;
using System.Collections.Generic;

namespace FormulaEngine.Core
{
    public class CalculatedFieldValidationException: Exception
    {
        public CalculatedFieldValidationException(ICollection<ParseError> errors)
        {
            ParseErrors = errors;
        }

        public ICollection<ParseError> ParseErrors { get; set; }
    }
}
