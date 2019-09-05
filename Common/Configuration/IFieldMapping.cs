﻿namespace Common.Configuration
{
    /// <summary>
    /// Maps a source field to a different target field.
    /// </summary>
    public interface IFieldMapping
    {
        /// <summary>
        /// The source field name.
        /// </summary>
        string Source { get; set; }

        /// <summary>
        /// The target field name.
        /// </summary>
        string Target { get; set; }
    }
}