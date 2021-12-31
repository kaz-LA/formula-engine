namespace FormulaEngine.Core
{
    public interface IMapper
    {
        T Map<T>(object @object);
        T Map<T>(object @object, T result);
    }
}
