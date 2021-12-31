using System;
using System.Collections.Generic;
using System.Linq;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Parser;

namespace FormulaEngine.Core.Models
{
    public class ParseResult
    {
        public ICollection<ParseError> Errors { get; set; }
        public bool IsSuccess => Errors == null || !Errors.Any();
        public ICollection<object> Transformations { get; }
        public DataType? ResultType { get; set; }
        public ICollection<IColumn> ReferencedColumns { get; set; } = new List<IColumn>();
        public bool ContainsAggregateFunction => AggregationType.HasValue;
        public AggregationType? AggregationType { get; set; }
        public IEnumerable<object> Tokens { get; set; }
        
        public static ParseResult Error(ICollection<ParseError> errors) =>
            new ParseResult { Errors = errors };

        public static ParseResult Error(Exception exception) =>
            new ParseResult {Errors = new[] {new ParseError("Error occurred while parsing formula", exception)}};

        public static ParseResult Error(ErrorCode parseError, params object[] args) =>
            new ParseResult {Errors = new[] {new ParseError(parseError, args)}};

        public static implicit operator ParseResult(ParseError error) => new ParseResult {Errors = new[] {error}};
    }
}
