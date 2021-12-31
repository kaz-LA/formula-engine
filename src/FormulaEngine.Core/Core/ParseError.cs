using System;
using System.Collections.Generic;
using System.Linq;
using FormulaEngine.Core.Parser;

namespace FormulaEngine.Core
{
    public class ParseError
    {
        public ParseError() { }

        public ParseError(string message)
        {
            Message = message;
        }

        public ParseError(ErrorCode errorCode, params object[] args)
        {
            Message = errorCode.GetMessage(args);
            ErrorCode = errorCode;
        }

        public ParseError(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }

        public ParseError(string message, IDictionary<string, object> data)
        {
            Message = message;
            Data = data;
        }

        public ParseError(string message, object data)
        {
            Message = message;
            Data = data?.GetType()?.GetProperties()
                ?.ToDictionary(p => p.Name, p => p.GetValue(data), StringComparer.OrdinalIgnoreCase);
        }

        public ErrorCode ErrorCode { get; set; }

        public string Message { get; set; }

        public Exception Exception { get; set; }

        public IDictionary<string, object> Data { get; set; }
        
        public override string ToString() => $"{Message} {Exception}";
    }
}