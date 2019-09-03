using Common.Serialization.Json;
using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// Maps a source identity to a target identity..
    /// </summary>
    public class IdentityMapping : IIdentityMapping
    {
        /// <summary>
        /// The source identity.
        /// </summary>
        [JsonProperty(PropertyName = "source", Required = Required.Always)]
        public string Source { get; set; }

        /// <summary>
        /// The target identity.
        /// </summary>
        [JsonProperty(PropertyName = "target", Required = Required.Always)]
        public string Target { get; set; }
    }
}
