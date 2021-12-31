using System;
using System.Collections.Generic;
using System.Text;
using FormulaEngine.Core.Interfaces;

namespace FormulaEngine.Core.Parser.Internal.TokenConverters
{
    internal class TokenXmlConverter : ITokenConverter
    {
        public static TokenXmlConverter Instance => new TokenXmlConverter();

        public string ToXml(ICollection<Token> tokens)
        {
            var buffer = new StringBuilder();
            tokens.ForEachItem(token => ToXml(token, buffer));

            return $"<formula>{buffer}</formula>";
        }

        public void ToXml(Token token, StringBuilder xml)
        {
            switch (token.Type)
            {
                case TokenType.CalculatedField:
                case TokenType.Column:
                    var column = token.Value as IColumn;
                    xml.Append($"<column id=\"{column?.ColumnId}\" entityId=\"{column?.EntityId}\" value=\"{XmlEscape(token.Text)}\" />");
                    break;

                case TokenType.ParenthesisOpen:
                case TokenType.ParenthesisClose:
                case TokenType.Unknown:
                case TokenType.Number:
                case TokenType.Operator:
                case TokenType.Boolean:
                    xml.Append($"<{token.Type.ToString().ToLower()} value=\"{XmlEscape(token.Text)}\" />");
                    break;

                case TokenType.Function:
                    FunctionToXml(token, xml);
                    break;

                case TokenType.Date:
                case TokenType.String:
                    var value = XmlEscape(token.Text);
                    xml.Append($"<{token.Type.ToString().ToLower()} value=\"{value}\" />");
                    break;

                case TokenType.Comma:
                    // Ignore!
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Invalid token type: {token.Type}");
            }
        }

        private void FunctionToXml(Token token, StringBuilder xml)
        {
            var func = (FunctionToken)token;
            xml.Append($"<function id=\"{func.Function?.Id}\" name=\"{func.Function?.Name ?? token.Text}\" level=\"{func.Level}\">")
               .Append("<args>");
            var index = 0;

            foreach (var arg in func.Arguments)
            {
                xml.Append($"<arg i=\"{index++}\">");
                foreach (var argToken in arg.Tokens)
                    ToXml(argToken, xml);
                xml.Append("</arg>");
            }

            xml.Append("</args>").Append("</function>");
        }

        private static string XmlEscape(string text) => System.Security.SecurityElement.Escape(text);

        string ITokenConverter.Convert(ICollection<Token> tokens, DataType? outputType, params object[] args) => this.ToXml(tokens);
    }
}
