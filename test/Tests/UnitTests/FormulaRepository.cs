using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FormulaEngineTests.UnitTests
{
    internal class FormulaRepository : ICalculatedFieldRepository
    {
        public FormulaRepository(
            ICollection<ColumnInfo> columns,             
            ICollection<IFunction> functions, 
            ICollection<ICalculatedField> calculatedFields)
        {
            Columns = columns;
            Functions = functions;
            CalculatedFields = calculatedFields;
        }

        public ICollection<ColumnInfo> Columns { get; set; }

        public ICollection<IFunction> Functions { get; set; }

        public ICollection<ICalculatedField> CalculatedFields { get; set; }

        public async Task<ICollection<IFunction>> GetFunctions() => Functions;

        public async Task<IColumn> GetColumnById(int columnId, int entityId) 
            => Columns.FirstOrDefault(c => c.ColumnId == columnId && (entityId == 0 || c.EntityId == entityId));

        public async Task<bool> ReferencesAnotherCalculatedField(int calculatedFieldId)
        {
            return calculatedFieldId == 3; // [calculated_field_num3] references [calculated_field_num2]
        }

        public async Task<IColumn> FindColumnByTitle(string entityTitle, string columnTitle)
        {
            var cmp = StringComparison.OrdinalIgnoreCase;

            var column = Columns.FirstOrDefault(c =>
            (string.Equals(c.Name, columnTitle, cmp) || string.Equals(c.DefaultTitle, columnTitle, cmp)) &&
            (string.IsNullOrEmpty(entityTitle) || string.Equals(c.EntityName, entityTitle, cmp)));

            return column;
        }

        public async Task<ICalculatedField> GetCalculatedField(int calculatedFieldId) 
            => CalculatedFields.FirstOrDefault(cc => cc.Id == calculatedFieldId);

        Task<ICalculatedField> ICalculatedFieldRepository.CreateCalculatedField(ICalculatedField calculatedField)
        {
            throw new NotImplementedException();
        }

        Task<ActionResult> ICalculatedFieldRepository.DeleteCalculatedField(int calculatedFieldId)
        {
            throw new NotImplementedException();
        }
                
        public Task<ICalculatedField> GetCalculatedFieldWithReferencedColumns(int calculatedFieldId)
        {
            throw new NotImplementedException();
        }

        public async Task<int> GetCalculatedFieldId(string name)
        {
            return this.CalculatedFields.FirstOrDefault(cf => cf.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        }

        public Task<ICollection<ICalculatedField>> GetCalculatedFields(IUser user)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<IDataType>> GetDataTypes()
        {
            throw new NotImplementedException();
        }
                
        public Task<ICalculatedField> UpdateCalculatedField(ICalculatedField updatedCalculatedField)
        {
            throw new NotImplementedException();
        }

        public Task UpdateRelatedCalculatedFields(int calculatedFieldId, Func<string, Task<ICalculatedField>> formulaParser) => throw new NotImplementedException();
    }
}
