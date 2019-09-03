using Newtonsoft.Json;

namespace Common.Configuration
{
    /// <summary>
    /// Describes a connection to a TFS instance.
    /// </summary>
    public interface IConnection
    {
        /// <summary>
        /// The connection URI.
        /// </summary>
        string Uri { get; set; }

        /// <summary>
        /// The default project.
        /// </summary>
        string Project { get; set; }

        /// <summary>
        /// The access token.
        /// </summary>
        string AccessToken { get; set; }

        /// <summary>
        /// If this connection should use integrated authentication.
        /// </summary>
        bool UseIntegratedAuth { get; set; }
    }
}
