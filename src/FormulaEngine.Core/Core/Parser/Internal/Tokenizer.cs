using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FormulaEngine.Core.Interfaces;
using static System.String;

namespace FormulaEngine.Core.Parser.Internal
{
    public interface ITokenizer
    {
        /// <summary> Returns the next valid, non-whitespace Token </summary>
        bool TryGetNextToken(out Token token);

        Token GetNextToken();
    }

    /// <summary>
    /// Generates Tokens from a textual formula expression that includes - literals, operators, column names and functions 
    /// </summary>
    internal class Tokenizer : ITokenizer
    {
        private readonly string _data;
        private readonly IParserContext _context;
        private int _index;
        private Token _previousToken;
        private readonly ITokenFactory _tokenFactory;
        
        public Tokenizer(string data, IParserContext context, ITokenFactory tokenFactory)
        {
            _data = data;
            _context = context;
            _tokenFactory = tokenFactory;
            _index = 0;
        }

        /// <summary> Returns the next valid, non-whitespace Token </summary>
        public virtual bool TryGetNextToken(out Token token)
        {
            token = _previousToken = GetNextToken();
            return !IsNullOrWhiteSpace(token?.Text) && token?.Type == TokenType.Unknown;
        }

        /// <summary>
        /// Returns the next Token which could be a literal, an operator, a column name, a space, or a function or even an invalid one
        /// </summary>
        public virtual Token GetNextToken()
        {
            var quoteStartIndex = -1;
            var columnStartIndex = -1;
            var startIndex = _index;
            var numberOfQuotes = 0;
            Token token = null;

            var (quoteChar, entityColumnDelimiter) = _context.Settings;
            const char nullChar = '\0';

            char PreviousChar() => _index > 0 ? _data[_index - 1] : nullChar;
            char NextChar() => _index < _data.Length - 1 ? _data[_index + 1] : nullChar;

            bool IsEndOfQuotedText(char c, bool quotesBalanced) =>
                c == quoteChar && NextChar() != quoteChar && quotesBalanced;

            while (_index < _data?.Length && token == null)
            {
                var currentChar = _data[_index];
                var isToken = false;
                var increment = 1;

                if (quoteStartIndex >= startIndex)
                {
                    // end of string literal in double-quotes?
                    if (IsEndOfQuotedText(currentChar, numberOfQuotes % 2 == 0))
                        token = _tokenFactory.StringLiteral(ExtractToken(startIndex), quoteStartIndex, _context);

                    // it's inside of a string literal
                    if (currentChar == quoteChar) numberOfQuotes++;
                    _index++;
                    continue;
                }

                if (columnStartIndex >= 0 && currentChar != _context.ColumnEndChar)
                {
                    _index++;
                    continue;
                }

                switch (currentChar)
                {
                    case ' ':
                    case '\t':
                    case '\n':
                    case '\r':
                        break;

                    case ',':
                        isToken = true;
                        token = _tokenFactory.Comma(_index);
                        break;

                    case _context.ColumnStartChar:
                        isToken = PreviousChar() != entityColumnDelimiter;
                        if (columnStartIndex == -1)
                            columnStartIndex = _index;
                        break;

                    case _context.ColumnEndChar:
                        if (columnStartIndex >= startIndex && NextChar() != entityColumnDelimiter)
                        {
                            var text = ExtractToken(columnStartIndex);
                            token = await _tokenFactory.ColumnToken(text, columnStartIndex, _context);
                        }

                        break;
                    case '(':
                        isToken = true;
                        createTokenFunc = _tokenFactory.CreateFunctionToken;
                        token = _tokenFactory.CreateToken(currentChar.ToString(), TokenType.ParenthesisOpen, _index);
                        break;

                    case ')':
                        isToken = true;
                        token = _tokenFactory.CreateToken(currentChar.ToString(), TokenType.ParenthesisClose, _index);
                        break;

                    case quoteChar:
                        isToken = true;
                        quoteStartIndex = _index;
                        break;

                    default:
                        var isNegativeSign = currentChar == '-' && char.IsDigit(NextChar()) &&
                                             !HasToken(startIndex, false, out var _) &&
                                             _previousToken.IsStartOfExpressionGroup();
                        isToken = !isNegativeSign && IsOperator(currentChar, NextChar(), out token);
                        increment = token?.Text?.Length ?? 1;

                        break;
                }

                if (isToken && HasToken(startIndex, false, out var value))
                    return await createTokenFunc(value, _context, startIndex);

                _index += increment;
            }

            return token ?? await _tokenFactory.CreateToken(ExtractToken(startIndex), _context, startIndex);
        }

        private bool HasToken(int startIndex, bool includeCurrent, out string str)
        {
            str = ExtractToken(startIndex, includeCurrent);
            return !IsNullOrEmpty(str);
        }

        private string ExtractToken(int startIndex, bool includeCurrent = true)
        {
            var length = _data.Length;
            var endIndex = _index >= length ? length - 1 : _index;
            var extra = includeCurrent && endIndex < length ? 1 : 0;

            return _data[startIndex..(endIndex - startIndex + extra)].Trim();
        }

        private bool IsOperator(char chr, char next, out Token token)
        {
            token = null;
            IFormulaOperator @currOperator = null;
            if (_context.IsOperator(ToString(chr, next), out var @operator) ||
                _context.IsOperator(ToString(chr), out @currOperator))
            {
                var operatorText = (@operator ?? @currOperator)?.Symbol;
                token = _tokenFactory.CreateToken(operatorText, TokenType.Operator, _index, @operator ?? @currOperator);
            }

            return token != null;
        }

        private static string ToString(params char[] chars) => new string(chars);
    }
}
