namespace Common.Configuration
{
    /// <summary>
    /// A class that describes a field replacement using regular expressions.
    /// </summary>
    public interface IFieldReplacement
    {
        /// <summary>
        /// The field name.
        /// </summary>
        string Field { get; set; }

        /// <summary>
        /// The pattern to match.
        /// </summary>
        string Pattern { get; set; }

        /// <summary>
        /// The replacement string.
        /// </summary>
        string Replacement { get; set; }
    }
}
