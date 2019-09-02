using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common.Serialization.Json
{
    /// <summary>
    /// Converter that forces conversion to an implemented type.
    /// </summary>
    /// <typeparam name="TImplementation">The desired serialization type.</typeparam>
    public class ConcreteTypeConverter<TImplementation> : JsonConverter
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
        public override object ReadJson(JsonReader reader, Type type, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            return serializer.Deserialize<TImplementation>(reader);
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