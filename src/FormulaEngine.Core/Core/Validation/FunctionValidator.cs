using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Parser.Internal;

namespace FormulaEngine.Core.Validation
{
    /// <summary>
    /// Validates Function Tokens
    /// </summary>
    internal class FunctionValidator : TokenValidator
    {
        public static FunctionValidator Instance = new FunctionValidator();

        public override async Task<ValidationResult> ValidateAsync(Token token, IParserContext context)
        {
            return await ValidateFunction((FunctionToken)token, context);
        }

        private async Task<ValidationResult> ValidateFunction(FunctionToken token, IParserContext context)
        {
            var function = token.Function ?? await context.GetFunction(token.Text);
            if (function == null)
                return Error(ErrorCode.UnknownFunction, new { function = token.Text, Index = token.StartIndex });

            var errors = new List<ParseError>();
            Action<ParseError> onError = errors.Add;

            ValidateFunctionNestingLevel(token, function, context.Settings.MaxFunctionNesting, token.NestingHierarchy, onError);

            var functionParams = GetAllParameters(function, token.Arguments.Count);

            // number of arguments
            ValidateArgumentCount(token, function, functionParams, onError);

            // Type check on function arguments against the parameter definition of the function
            var index = 0;            
            var expectedType = ExpectedArgumentType(function);
            
            foreach (var argument in token.Arguments)
            {
                //if (index >= functionParams.Count && expectedType == null) break;
                var parameter = index < functionParams.Count
                    ? functionParams[index]
                    : GetSomeParameter(functionParams, index);

                if (parameter == null) break;
                await ValidateArgument(argument, function, parameter, expectedType, context, token, onError);
                index++;
            }
                        
            return errors;
        }

        private IParameter GetSomeParameter(ICollection<IParameter> parameters, int index)
        {
            if (!parameters.Any()) return null;

            var param = parameters.First();
            param = parameters.All(p => p.ValueTypeId == param.ValueTypeId) /* Example: CONCAT(str1, str2, ...)*/
                    ? param
                    : parameters.ElementAtOrDefault((index % 2 == 0 ? 2 : 1)) ?? param; /* Example: CASE(expr, value1, result1, ..., else)*/
            return CloneParameter(param, index);
        }

        private void ValidateArgumentCount(FunctionToken token, IFunction function, 
            IList<IParameter> functionParams, Action<ParseError> onError)
        {
            //var functionParams = function.Parameters.ToList();
            var paramCount = functionParams.Count(p => !p.IsOptional);

            // variable number of arguments function?
            if (function.IsVariableArguments && token.Arguments.Count < paramCount)
            {
                onError(Error(ErrorCode.MinimumNumberOfArgs, new { function = function.Name, minParams = paramCount }));
                return;
            }

            // too little arguments passed ?
            foreach(var param in functionParams.Skip(token.Arguments.Count).Where(p => !p.IsOptional))
            {
                onError(Error(ErrorCode.MissingValueForRequiredParameter,
                    new { function = function.Name, paramName = param.ParamName() }));
            }

            // too many arguments passed ?
            paramCount = functionParams.Count;
            if (token.Arguments.Count > paramCount && !function.IsVariableArguments)
                onError(Error(ErrorCode.TooManyArguments,
                    new
                    {
                        function = function.Name,
                        expectedParamCount = paramCount,
                        actualParamCount = token.Arguments.Count
                    }));
        }

        // Performs Type check on a function argument against the corresponding parameter definition
        private async Task ValidateArgument(Argument argument, IFunction function, IParameter parameter,
            ExpressionType expectedArgumentType, IParserContext context, FunctionToken token,
            Action<ParseError> onError)
        {
            if (parameter.TryGetKnownValues(out var knownValues))
            {
                var value = argument.Tokens.FirstOrDefault();
                if (!knownValues.IsValidValue(value, parameter.ValueTypeId))
                    onError(Error(ErrorCode.ValueIsOutsideRangeOfValidValuesForParameter,
                        new
                        {
                            function = function.Name,
                            parameter = parameter.ParamName(),
                            value = value?.Text,
                            values = knownValues.ToString()
                        }));
            }

            foreach (var argToken in argument.Tokens)
            {
                var result = await base.ValidateAsync(argToken, context, knownValues);
                Contracts.Extensions.ForEachItem(result?.Errors, onError);

                // validate nesting levels of functions from existing Formula Engine
                await ValidateFunctionNestingLevels(argToken, token.Level + 1, token.NestingHierarchy, context, onError);
            }

            var expectedType = expectedArgumentType ?? parameter.ValueType(token);
            var isValid = IsValidExpression(argument, expectedType, out var actualType);
            if (!isValid || (!argument.Is(TokenType.Unknown) && actualType == null))
            {
                onError(Error(!isValid ? ErrorCode.InvalidArgumentOrExpression : ErrorCode.UnexpectedToken,
                    new
                    {
                        function = function.Name,
                        parameter = parameter.ParamName(),
                        expectedType = $"EX.RPT.DataType.{expectedType}",
                        actualType,
                        token = argument.ToString()
                    }));
            }

            // Aggregate functions can't have a boolean filter like expression as an argument
            // Example: GCOUNT([Transcript].[Transcript Status] = 'Withdrawn') is bad
            if(function.CategoryId == FunctionCategory.Aggregate && 
                actualType == DataType.Boolean && 
                argument.Tokens.Count > 1)
            {
                onError(Error(ErrorCode.InvalidArgumentOrExpression,
                    new
                    {
                        function = function.Name,
                        parameter = parameter.ParamName(),
                        expectedType = "column or function",
                        actualType,
                        token = argument.ToString()
                    }));
            }
        }

        // Determines if all arguments/parameters should have the same type? E.g. Concat(str1, str2, ...)
        private ExpressionType ExpectedArgumentType(IFunction function)
        {
            if (function.IsVariableArguments && function.Parameters.Any())
            {
                var functionParams = function.Parameters.ToList();
                var firstArgType = functionParams.First().ValueTypeId;

                if (firstArgType != DataType.Any &&
                    functionParams.All(p => p.ValueTypeId == firstArgType))
                    return firstArgType;
            }

            return null;
        }

        private bool IsValidExpression(Argument argument, ExpressionType expectedType, out DataType? actualType)
            => argument.ResultType.IsExpectedType(expectedType, out actualType);

        private void ValidateFunctionNestingLevel(FunctionToken token, IFunction function, int defaultMaxNestingLevel,
            string nestingHierarchy, Action<ParseError> onError)
        {
            var maxNesting = Math.Max(function.MaxNestingLevel, defaultMaxNestingLevel);
            if (token.Level > maxNesting)
                onError(Error(maxNesting == 0 ? ErrorCode.FunctionCantBeNested : ErrorCode.MaxFunctionNestingExceeded,
                    new { function = token.Text, maxNesting, token.Level, nestingHierarchy = $"({nestingHierarchy})"}));
        }

        // validates the maximum nesting levels of each function referenced in an existing Calculated Field
        private async Task ValidateFunctionNestingLevels(Token token, int currentLevel, string nestingHierarchy, IParserContext context, Action<ParseError> onError)
        {
            if (!(token.IsColumn() && token.Value is IColumn column && column.IsCalculatedField()))
                return;

            var calculatedField = await context.GetCalculatedField(column.CalculatedFieldId ?? 0);
            if (string.IsNullOrEmpty(calculatedField?.Xml))
                return;

            // parse the functions from the Xml
            try
            {
                var xml = XElement.Parse(calculatedField.Xml, LoadOptions.None);
                var functions = xml.Descendants("function")
                    .Select(x => new
                    {
                        Name = x.Attribute("name")?.Value,
                        Level = x.Attribute("level")?.Value.ToInt() ?? 0,
                        nesting = x.AncestorsAndSelf("function").Attributes("name").Select(a => a.Value).Reverse().Join("->")
                    });

                foreach (var func in functions)
                {
                    var funcToken = new FunctionToken { Text = func.Name, Level = currentLevel + func.Level };
                    ValidateFunctionNestingLevel(funcToken, await context.GetFunction(func.Name), context.Settings.MaxFunctionNesting,
                         $"{nestingHierarchy}->{func.nesting}", onError);
                }
            }
            catch (Exception ex)
            {
                var msg =
                    $"Error occured while validating nesting levels of functions in an existing calculated field: {ex.Message}";
                onError(Error(msg, new { Exception = ex }));
            }
        }

        private IList<IParameter> GetAllParameters(IFunction function, int argumentCount)
        {
            var parameters = function.Parameters.ToList();
            if (function.IsVariableArguments &&
                parameters.Any(p => p.IsOptional) &&
                !parameters.Last().IsOptional &&
                parameters.Count != argumentCount)
            {
                parameters = new List<IParameter>(parameters);
                if (argumentCount < parameters.Count) // remove the optional ones in the middle
                    parameters.RemoveAll(p => p.IsOptional);
                else // create new ones
                {
                    var optionals = parameters.Where(p => p.IsOptional).ToArray();
                    var index = parameters.FindLastIndex(p => p.IsOptional);
                    var remaining = argumentCount - parameters.Count;
                    do
                    {
                        foreach (var param in optionals)
                            parameters.Insert(++index, CloneParameter(param, index));
                        remaining -= optionals.Length;
                    } while (remaining > 0);
                }
            }

            return parameters;
        }

        private static IParameter CloneParameter(IParameter from, int index)
        {            
            var name = Regex.Replace(from.Name, @"\d+", index.ToString());
            return new FunctionParameter { Index = index, Name = name, ValueTypeId = from.ValueTypeId };
        }
    }
}
