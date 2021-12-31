using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Parser.Internal;

namespace FormulaEngine.Core.Validation
{
    internal static class ParameterKnownValues
    {
        private static ConcurrentDictionary<IParameter, IValueDomain> 
            _knownValues = new ConcurrentDictionary<IParameter, IValueDomain>();

        public static IValueDomain GetKnownValues(IParameter parameter)
        {
            return _knownValues.GetOrAdd(parameter, ParseKnownValues);
        }

        public static IValueDomain ParseKnownValues(IParameter parameter)
        {
            IValueDomain knownValues = null;
            if (string.IsNullOrEmpty(parameter.KnownValues?.Trim()))
                return knownValues;

            var values = parameter.KnownValues.Split(',').ToList();
            var index = values.Count == 1 ? values.First().IndexOf('-', 1) : -1;

            if (index < 0)
                knownValues = new KnownValuesValueDomain(values);
            else
            {
                // A range of values in the form: 1-12 etc, A-Z etc
                var start = values.First().Substring(0, index).Trim();
                var end = values.First().Substring(index + 1).Trim();
                switch (parameter.ValueTypeId)
                {
                    case DataType.Datetime:
                        knownValues =
                            new RangeValueDomain<DateTime>(DateTime.Parse(start), DateTime.Parse(end),
                                Comparer<DateTime>.Default);
                        break;
                    case DataType.Number:
                        knownValues =
                            new RangeValueDomain<int>(int.Parse(start), int.Parse(end), Comparer<int>.Default);
                        break;
                    default:
                        knownValues = new RangeValueDomain<string>(start, end, StringComparer.OrdinalIgnoreCase);
                        break;
                }
            }

            return knownValues;
        }
    }

    /// <summary>
    /// Represents a list of valid values for a Token (e.g. a function parameter), validates
    /// a given token value against the specified valid values
    /// </summary>
    internal class KnownValuesValueDomain : IValueDomain
    {
        private readonly IEnumerable<string> _knownValues;

        public KnownValuesValueDomain(IEnumerable<string> knownValues)
        {
            _knownValues = knownValues;
        }

        public bool IsValidValue(Token token, DataType? valueType)
            => (!token.IsLiteral() && valueType == token.ToGenericType()) || 
                _knownValues.Contains(token.Text, StringComparer.OrdinalIgnoreCase);

        public override string ToString() => _knownValues?.Join(",");
    }

    /// <summary>
    /// Represents a range of valid values for a Token (e.g. a function parameter), validates
    /// a given token value against the values in the range
    /// </summary>
    internal class RangeValueDomain<T> : IValueDomain
    {
        private readonly T _startValue;
        private readonly T _endValue;
        private readonly IComparer<T> _comparer;

        public RangeValueDomain(T startValue, T endValue, IComparer<T> comparer)
        {
            _startValue = startValue;
            _endValue = endValue;
            _comparer = comparer;
        }

        bool IValueDomain.IsValidValue(Token token, DataType? valueType)
            => (!token.IsLiteral() && valueType == token.ToGenericType()) ||
                IsValidValue((T)Convert.ChangeType(token.Text, typeof(T)));

        public bool IsValidValue(T value) =>
            _comparer.Compare(value, _startValue) >= 0 && _comparer.Compare(value, _endValue) <= 0;

        public override string ToString() => $"{_startValue}-{_endValue}";
    }

    /// <summary>
    /// Represents a list or range of valid values for a Token (e.g. a function parameter), validates
    /// a given token value against the valid values in the domain
    /// </summary>
    internal interface IValueDomain
    {
        bool IsValidValue(Token token, DataType? valueType);
    }
}
