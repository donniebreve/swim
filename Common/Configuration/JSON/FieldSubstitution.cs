using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// A class that describes a field value substitution.
    /// </summary>
    public class FieldSubstitution : IFieldSubstitution
    {
        [JsonProperty(Required = Required.Always)]
        public string Field { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Value { get; set; }
    }
}
