using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// A class that describes a field value substitution.
    /// </summary>
    public class FieldSubstitution : IFieldSubstitution
    {
        /// <summary>
        /// The field name.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Field { get; set; }

        /// <summary>
        /// The substituted value.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Value { get; set; }
    }
}
