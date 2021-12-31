using System.Collections.Generic;
using System.Linq;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;

namespace FormulaEngine.Core.Parser.Internal
{
    /// <summary>
    /// Converts a Token (from a formula expression) - to Xml or Sql format
    /// </summary>
    internal class TokenConverter
    {
        public static string CreatePlaceholder(int entityId, int columnId) => $"[{entityId}:{columnId}]";

        public string ToXml(ICollection<Token> tokens) 
            => TokenConverters.TokenXmlConverter.Instance.ToXml(tokens);

        public string ToSql(ICollection<Token> tokens, DataType? outputType, SqlTranslationOptions options)
            => TokenConverters.TokenSqlConverter.Instance.ToSql(tokens, outputType, options);

        public void GetReferencedColumns(Token token, ICollection<IColumn> columns)
        {
            switch (token.Type)
            {
                case TokenType.Column:
                case TokenType.CalculatedField:
                    if (token.Value is IColumn column)
                        columns.Add(column);
                    break;

                case TokenType.Function:
                    ((FunctionToken)token).Arguments.SelectMany(arg => arg.Tokens)
                        .ForEachItem(argToken => GetReferencedColumns(argToken, columns));
                    break;
            }
        }               
    }
}
