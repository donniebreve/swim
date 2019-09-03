namespace Common.Configuration
{
    /// <summary>
    /// A class that describes a field value substitution.
    /// </summary>
    public interface IFieldSubstitution
    {
        /// <summary>
        /// The field name.
        /// </summary>
        string Field { get; set; }

        /// <summary>
        /// The substituted value.
        /// </summary>
        string Value { get; set; }
    }
}
