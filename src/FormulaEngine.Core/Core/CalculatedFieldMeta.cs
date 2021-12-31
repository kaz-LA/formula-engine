using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;
using FormulaEngine.Core.Models;
using FormulaEngine.Core.Parser.Internal;
using Operator = FormulaEngine.Core.Models.Operator;

namespace FormulaEngine.Core
{
    public class CalculatedFieldMeta : IMetadataMeta
    {
        private static ICollection<IFunction> _functions;
        private static ICollection<IDataType> _dataTypes;

        private readonly ICalculatedFieldRepository _repository;

        public CalculatedFieldMeta(ICalculatedFieldRepository repository)
        {
            _repository = repository;
        }

        public async Task<ICollection<IFunction>> GetFunctions()
        {
            return _functions ??= UpdateDetails(await _repository.GetFunctions());
        }

        public async Task<ICollection<IDataType>> GetDataTypes()
        {
            return _dataTypes ??= await _repository.GetDataTypes();
        }

        public ICollection<IFormulaOperator> GetOperators() => Operator.GetAllOperators();

        public async Task<IColumn> FindColumn(string entityNameOrId, string columnNameTitleOrId)
        {
            // entityTitle and columnTitle args may be corresponding ids as well
            var entityId = 0;
            IColumn column = null;
            
            if (int.TryParse(columnNameTitleOrId, out var columnId) &&
                (string.IsNullOrEmpty(entityNameOrId) || int.TryParse(entityNameOrId, out entityId)))
                column = await _repository.GetColumnById(columnId, entityId);

            return  column ?? await _repository.FindColumnByTitle(entityNameOrId, columnNameTitleOrId);
        }

        public async Task<ICalculatedField> GetCalculatedField(int calculatedFieldId)
        {
            return await _repository.GetCalculatedField(calculatedFieldId);
        }

        public async Task<ICollection<ICalculatedField>> GetCalculatedFields(IUser user)
        {
            return await _repository.GetCalculatedFields(user);
        }

        public async Task<ICalculatedField> CreateCalculatedField(CalculatedFieldModel model,
            IFormulaParser parser, IMapper mapper)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            var parseResult = await parser.Parse(model.Formula, model);
            if (!parseResult.IsSuccess)
                throw new CalculatedFieldValidationException(parseResult.Errors);

            var calculatedField = mapper.Map<CalculatedField>(model);
            SetValuesFromParseResult(calculatedField, parseResult);

            return await _repository.CreateCalculatedField(calculatedField);
        }

        public async Task<ICalculatedField> CreateCalculatedField(ICalculatedField calculatedField) =>
            await _repository.CreateCalculatedField(calculatedField);

        public async Task<ICalculatedField> UpdateCalculatedField(CalculatedFieldModel model, IFormulaParser parser,
            IMapper mapper) =>
            await UpdateCalculatedField(model, parser, mapper, false);

        public async Task<ICalculatedField> UpdateCalculatedField(CalculatedFieldModel model, IFormulaParser parser,
            IMapper mapper, bool forceParseAgain)
        {
            var calculatedField = await _repository.GetCalculatedFieldWithReferencedColumns(model.Id);

            if (calculatedField == null)
                return null;

            // if the formula has changed, then we have to parse and validate again
            var shouldValidateAgain = calculatedField.Expression != model.Formula ||
                                      calculatedField.OutputTypeId != model.OutputTypeId;

            mapper.Map(model, calculatedField);

            if (shouldValidateAgain || forceParseAgain)
            {
                var parseResult = await parser.Parse(model.Expression, model);
                if (!parseResult.IsSuccess)
                    throw new CalculatedFieldValidationException(parseResult.Errors);

                SetValuesFromParseResult(calculatedField, parseResult);
            }
                        
            var updated = await _repository.UpdateCalculatedField(calculatedField);
            await _repository.UpdateRelatedCalculatedFields(updated.Id, async formula => ConvertParseResult(await parser.Parse(formula, default(ParserOptions))));

            return updated;
        }

        public async Task<ActionResult> DeleteCalculatedField(int calculatedFieldId) =>
            await _repository.DeleteCalculatedField(calculatedFieldId);

        public async Task<bool> ReferencesAnotherCalculatedField(int calculatedFieldId) =>
            await _repository.ReferencesAnotherCalculatedField(calculatedFieldId);

        public async Task<int> GetCalculatedFieldId(string name) => await _repository.GetCalculatedFieldId(name);

        private static void SetValuesFromParseResult(ICalculatedField calculatedField, ParseResult parseResult)
        {
            calculatedField.Xml = parseResult.Xml;
            calculatedField.SqlExpression = parseResult.Sql;
            calculatedField.IsAggregate = parseResult.ContainsAggregateFunction;
            calculatedField.ReferencedColumns = new List<CalculatedFieldColumn>();
            calculatedField.OutputTypeId = parseResult.ResultType ?? calculatedField.OutputTypeId;
            calculatedField.AggregationTypeId = parseResult.AggregationType;

            if (parseResult.ReferencedColumns != null && parseResult.ReferencedColumns.Any())
            {
                calculatedField.ReferencedColumns = parseResult.ReferencedColumns.Select(c =>
                        CalculatedFieldColumn.Create(calculatedField.Id, c.ColumnId, c.EntityId, c.CalculatedFieldId)
                    ).ToList();
            }
        }

        private static ICalculatedField ConvertParseResult(ParseResult parseResult)
        {
            if (parseResult.IsSuccess)
            {
                var calculatedField = new CalculatedField();
                SetValuesFromParseResult(calculatedField, parseResult);
                return calculatedField;
            }

            return null;
        }

        // do some massaging on functions
        public static ICollection<IFunction> UpdateDetails(ICollection<IFunction> functions)
        {
            functions.ForEachItem(f =>
            {
                // Aggregate functions can't be nested
                if (f.CategoryId == FunctionCategory.Aggregate)
                    f.MaxNestingLevel = 0;

                if (f.Parameters == null || !Enumerable.Any<IParameter>(f.Parameters) && f.MaxNestingLevel < 2)
                    f.MaxNestingLevel = 2;  // level two

                // re-order function parameters
                f.Parameters = Enumerable.OrderBy<IParameter, int>(f.Parameters, p => p.Index);
            });

            return functions;
        }

        public static void ClearCache() => _functions = null;
    }
}