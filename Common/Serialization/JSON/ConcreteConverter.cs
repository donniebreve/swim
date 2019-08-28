using Newtonsoft.Json;
using System;

namespace Common.Serialization.Json
{
    /// <summary>
    /// Converter that forces conversion to a concrete type.
    /// </summary>
    /// <typeparam name="T">The desired type.</typeparam>
    public class ConcreteConverter<T> : JsonConverter
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
        /// <param name="objectType">The object type.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <param name="serializer">The JsonSerializer.</param>
        /// <returns>The deserialized object.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            return serializer.Deserialize<T>(reader);
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