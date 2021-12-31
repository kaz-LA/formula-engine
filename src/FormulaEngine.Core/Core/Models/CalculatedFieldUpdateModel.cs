using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Models
{
    public class CalculatedFieldCreateModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Formula { get; set; }
        public DataType OutputTypeId { get; set; }
        public int? DecimalPoints { get; set; }
        public bool IsPublic { get; set; }

        public virtual CalculatedFieldModel ToCalculatedFieldModel() => 
            new CalculatedFieldModel 
            { 
                Name = Name, 
                Description = Description,
                OutputTypeId = OutputTypeId, 
                DecimalPoints = DecimalPoints, 
                IsPublic = IsPublic, 
                Formula = Formula
            };
    }

    public class CalculatedFieldUpdateModel : CalculatedFieldCreateModel 
    {
        public int Id { get; set; }

        public override CalculatedFieldModel ToCalculatedFieldModel()
        {
            var model = base.ToCalculatedFieldModel();
            model.Id = Id;
            return model;
        }
    }
}
