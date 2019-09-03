using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// A class that describes a field replacement using regular expressions.
    /// </summary>
    public class FieldReplacement : IFieldReplacement
    {
        /// <summary>
        /// The field name.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Field { get; set; }

        /// <summary>
        /// The pattern to match.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Pattern { get; set; }

        /// <summary>
        /// The replacement string.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Replacement { get; set; }
    }
}
