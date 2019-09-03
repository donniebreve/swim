using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// Describes a connection to a TFS instance.
    /// </summary>
    public class Connection : IConnection
    {
        /// <summary>
        /// The connection URI.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Uri { get; set; }

        /// <summary>
        /// The default project.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Project { get; set; }

        /// <summary>
        /// The access token.
        /// </summary>
        [JsonProperty(PropertyName = "access-token", Required = Required.DisallowNull)]
        public string AccessToken { get; set; }

        /// <summary>
        /// If this connection should use integrated authentication.
        /// </summary>
        [JsonProperty(PropertyName = "use-integrated-auth", Required = Required.DisallowNull)]
        public bool UseIntegratedAuth { get; set; }

        /// <summary>
        /// If the field should be serialized.
        /// </summary>
        /// <returns>False</returns>
        public bool ShouldSerializeAccessToken()
        {
            return false;
        }
    }
}
