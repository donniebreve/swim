﻿using Common.Serialization.Json;
using Newtonsoft.Json;

namespace Common.Configuration.Json
{
    /// <summary>
    /// Maps a source field to a different target field.
    /// </summary>
    public class FieldMapping : IFieldMapping
    {
        /// <summary>
        /// The source field name.
        /// </summary>
        [JsonProperty(PropertyName = "source", Required = Required.Always)]
        public string Source { get; set; }

        /// <summary>
        /// The target field name.
        /// </summary>
        [JsonProperty(PropertyName = "target", Required = Required.Always)]
        public string Target { get; set; }
    }
}
