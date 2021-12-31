using System.Collections.Generic;
using System.Threading.Tasks;
using FormulaEngine.Core.Models;

namespace FormulaEngine.Core.Interfaces
{
    public interface IMetadataMeta : IMetadataProvider
    {
        ICollection<IFormulaOperator> GetOperators();
        Task<ICollection<IDataType>> GetDataTypes();
        Task<ICalculatedField> CreateCalculatedField(CalculatedFieldModel calculatedFieldModel,
            IFormulaParser parser, IMapper mapper);
        Task<ICollection<ICalculatedField>> GetCalculatedFields(IUser user);
        Task<ICalculatedField> CreateCalculatedField(ICalculatedField calculatedField);
        Task<ICalculatedField> UpdateCalculatedField(CalculatedFieldModel model, IFormulaParser parser, IMapper mapper);
        Task<ICalculatedField> UpdateCalculatedField(CalculatedFieldModel model, IFormulaParser parser,
            IMapper mapper, bool forceParseAgain);
        Task<ActionResult> DeleteCalculatedField(int calculatedFieldId);
        Task<int> GetCalculatedFieldId(string name);
    }
}
