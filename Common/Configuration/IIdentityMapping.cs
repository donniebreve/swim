namespace Common.Configuration
{
    /// <summary>
    /// Maps a source identity to a target identity.
    /// </summary>
    public interface IIdentityMapping
    {
        /// <summary>
        /// The source identity.
        /// </summary>
        string Source { get; set; }

        /// <summary>
        /// The target identity.
        /// </summary>
        string Target { get; set; }
    }
}
