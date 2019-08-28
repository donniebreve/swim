using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// A class that describes a field replacement using regular expressions.
    /// </summary>
    public class FieldReplacement : IFieldReplacement
    {
        [JsonProperty(Required = Required.Always)]
        public string Field { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Pattern { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Replacement { get; set; }
    }
}
