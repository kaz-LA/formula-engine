using FormulaEngine.Core.Enums;

namespace FormulaEngine.Core.Models
{
    public readonly struct Operator
    {
        public Operator(int id, OperatorKind kind, string name, string text, string sqlSymbol) : this()
        {
            Id = id;
            Name = name;
            Text = text;
            SqlSymbol = sqlSymbol;
            Kind = kind;
        }
        
        public int Id { get; }
        public string Name { get; }
        public string Text { get; }
        public string SqlSymbol { get; }
        public OperatorKind Kind { get; }

        public override string ToString() => $"{Name}: {Text}";
    }
}