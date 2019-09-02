using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Common.Serialization.Json
{
    /// <summary>
    /// Converter that forces conversion to an implemented type for a list.
    /// </summary>
    /// <typeparam name="TInterface">The desired return type.</typeparam>
    /// <typeparam name="TImplementation">The desired deserialization type.</typeparam>
    public class ConcreteListConverter<TInterface, TImplementation> : JsonConverter where TImplementation : TInterface
    {
        /// <summary>
        /// If this converter can convert the object.
        /// </summary>
        /// <param name="objectType">The object type.</param>
        /// <returns>True</returns>
        public override bool CanConvert(Type objectType) => true;

        /// <summary>
        /// Deserializes the JSON string.
        /// </summary>
        /// <param name="reader">The JsonReader.</param>
        /// <param name="type">The object type.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <param name="serializer">The JsonSerializer.</param>
        /// <returns>The deserialized object.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var list = serializer.Deserialize<List<TImplementation>>(reader);
            return list.ConvertAll(x => (TInterface)x);
        }

        /// <summary>
        /// Serializes the object to a JSON string.
        /// </summary>
        /// <param name="writer">The JsonWriter.</param>
        /// <param name="value">The object.</param>
        /// <param name="serializer">The JsonSerializer.</param>
        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}