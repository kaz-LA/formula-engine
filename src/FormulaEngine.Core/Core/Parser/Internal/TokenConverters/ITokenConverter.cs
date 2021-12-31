using System.Collections.Generic;
using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Parser.Internal.TokenConverters
{
    internal interface ITokenConverter
    {
        string Convert(ICollection<Token> tokens, DataType? outputType, params object[] args);        
    }
}
