using Common.Serialization.Json;
using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// A class that describes a field mapping to a different target field.
    /// </summary>
    public class FieldMapping : IFieldMapping
    {
        [JsonProperty(PropertyName = "source-field", Required = Required.Always)]
        public string SourceField { get; set; }

        [JsonProperty(PropertyName = "target-field", Required = Required.Always)]
        public string TargetField { get; set; }
    }
}
