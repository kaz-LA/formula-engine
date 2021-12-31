using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FormulaEngine.Core.Interfaces
{
    public interface ICalculatedFieldRepository
    {
        Task<ICollection<IFunction>> GetFunctions();
        Task<ICollection<IDataType>> GetDataTypes();
        Task<IColumn> FindColumnByTitle(string entityTitle, string columnTitle);
        Task<ICalculatedField> GetCalculatedFieldWithReferencedColumns(int calculatedFieldId);
        Task<ICalculatedField> GetCalculatedField(int calculatedFieldId);
        Task<bool> ReferencesAnotherCalculatedField(int calculatedFieldId);
        Task<ICollection<ICalculatedField>> GetCalculatedFields(IUser user);
        Task<ICalculatedField> CreateCalculatedField(ICalculatedField calculatedField);
        Task<ICalculatedField> UpdateCalculatedField(ICalculatedField updatedCalculatedField);
        Task<ActionResult> DeleteCalculatedField(int calculatedFieldId);
        Task<int> GetCalculatedFieldId(string name);
        Task<IColumn> GetColumnById(int columnId, int entityId);
        Task UpdateRelatedCalculatedFields(int calculatedFieldId, Func<string, Task<ICalculatedField>> formulaParser);
    }
}
