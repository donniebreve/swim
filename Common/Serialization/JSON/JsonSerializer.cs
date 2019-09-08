using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Common.Serialization.Json
{
    /// <summary>
    /// A class the serializes and deserializes JSON.
    /// </summary>
    public class JsonSerializer : ISerializer
    {
        private static JsonSerializerSettings _settings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        /// <summary>
        /// Serializes the object to a JSON string.
        /// </summary>
        /// <param name="o">The object.</param>
        /// <returns>A JSON string.</returns>
        public string Serialize(object o)
        {
            return JsonConvert.SerializeObject(o, Formatting.Indented);
        }

        /// <summary>
        /// Deserializes a JSON string into an object.
        /// </summary>
        /// <typeparam name="T">The type of the destination object.</typeparam>
        /// <param name="s">The JSON string.</param>
        /// <returns>An instantated object of type T.</returns>
        public T Deserialize<T>(string s)
        {
            return JsonConvert.DeserializeObject<T>(s, _settings);
        }
    }
}
