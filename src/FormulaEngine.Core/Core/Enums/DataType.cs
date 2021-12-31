namespace FormulaEngine.Core.Enums
{
    /// <summary> Generic Data Type </summary>
    public enum DataType
    {
        Any,
        Boolean,
        Datetime,
        Number,
        String,
        Guid,        
        /// <summary>
        /// This is a contextual type: It means "the same type as the type of the first argument"
        /// </summary>
        Arg1 = -1,
        /// <summary>
        /// This is a contextual type: It means "the same type as the type of the second argument"
        /// </summary>
        Arg2 = -2,
        /// <summary>
        /// This is a contextual type: It means "the same type as the type of the third argument"
        /// </summary>
        Arg3 = -3, 
        AbsoluteDatetime = -4,
        AbsoluteDate = -5,
        Date = -6
    }
}
