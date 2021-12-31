using System.Collections.Generic;
using System.Threading.Tasks;

namespace FormulaEngine.Core.Interfaces
{
    public interface IMetadataProvider
    {
        Task<ICollection<IFunction>> GetFunctions();
        ICollection<IFormulaOperator> GetOperators();
        Task<IColumn> FindColumn(string entityNameOrId, string columnNameTitleOrId);
        Task<ICalculatedField> GetCalculatedField(int calculatedFieldId);
        Task<bool> ReferencesAnotherCalculatedField(int calculatedFieldId);
    }
}