using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;

namespace FormulaEngine.Core.Parser
{
    public static class ParserConfiguration
    {
        public static FormulaSettings DefaultSettings { get; } = new FormulaSettings();
    }
    
    public class ParserContext : IParserContext
    {
        private readonly IMetadataProvider _provider;
        private readonly ILocalizer _localizer;

        private IDictionary<string, IFunction> _functions;
        private IDictionary<string, IFormulaOperator> _operators;
        private FormulaSettings _settings;
        private readonly Lazy<string> _localizedNoString;
        private readonly Lazy<string> _localizedYesString;

        public ParserContext(IMetadataProvider provider, IUser user, ILocalizer localizer)
        {
            _provider = provider;
            _localizer = localizer;

            if (_localizer != null)
            {
                _localizedNoString = new Lazy<string>(() => _localizer.Localize("EX.RPT.NO"));
                _localizedYesString = new Lazy<string>(() => _localizer.Localize("EX.RPT.YES"));
            }

            if (user != null)
            {
                CultureId = user.CultureId;
                if (string.IsNullOrEmpty(user.Locale))
                    CultureInfo = CultureInfo.GetCultureInfo(user.Locale);
                UserId = user.UserId;
            }
        }

        public FormulaSettings Settings
        {
            get => _settings ??= ParserConfiguration.DefaultSettings.Clone();
            set => _settings = value;
        }
        
        public CultureInfo CultureInfo { get; set; } = CultureInfo.CurrentCulture;

        public async Task<ICollection<IFunction>> Functions() => await _provider.GetFunctions();

        public ICollection<IFormulaOperator> Operators => _provider?.GetOperators();
        
        public string BooleanYesText => _localizedYesString?.Value ?? "Yes";

        public string BooleanNoText => _localizedNoString?.Value ?? "No";

        /// <summary>
        /// Apply Sql rules - such as division by zero and null propagation
        /// </summary>
        public SqlTranslationOptions ApplySqlRules { get => Settings.SqlOptions; set => Settings.SqlOptions = value; }

        /// <summary>
        /// A method that retrieves columnId and entityId given culture specific entityName and columnName
        /// parameters:
        /// cultureId, entityName, columnName
        /// </summary>
        public async Task<IColumn> GetColumnByTitle(int cultureId, string entityTitle, string columnTitle) =>
            await _provider.FindColumn(entityTitle, columnTitle);

        public async Task<ICalculatedField> GetCalculatedField(int calculatedFieldId) =>
            await _provider.GetCalculatedField(calculatedFieldId);

        public async Task<bool> ReferencesAnotherCalculatedField(int calculatedFieldId) =>
            await _provider.ReferencesAnotherCalculatedField(calculatedFieldId);

        internal async Task<(bool, IFunction)> IsFunction(string name)
        {
            if (_functions == null)
            {
                _functions = (await Functions())?.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            }

            IFunction function = null;
            var isFunction = _functions?.TryGetValue(name, out function) ?? false;
            return (isFunction, function);
        }

        internal bool IsOperator(string @operatorText, out IFormulaOperator @operator)
        {
            if (_operators == null)
            {
                _operators = Operators?.ToDictionary(o => o.Symbol, StringComparer.OrdinalIgnoreCase);
            }

            @operator = null;
            return _operators?.TryGetValue(@operatorText, out @operator) ?? false;
        }

        public async Task<IFunction> GetFunction(string name)
        {
            var (_, func) = await IsFunction(name);
            return func;
        }                
    }   
}