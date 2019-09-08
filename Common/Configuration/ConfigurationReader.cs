using Common.Serialization;
using Logging;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Common.Configuration
{
    /// <summary>
    /// A class that reads configuration files.
    /// </summary>
    public class ConfigurationReader
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<ConfigurationReader>();

        /// <summary>
        /// Loads the configuration from a file.
        /// </summary>
        /// <typeparam name="TConfiguration">The configuration object type.</typeparam>
        /// <typeparam name="TSerializer">The serializer object type.</typeparam>
        /// <param name="path">The file path.</param>
        /// <returns>An instantiated TConfiguration object.</returns>
        public static TConfiguration LoadFromFile<TConfiguration, TSerializer>(string path) where TSerializer : new()
        {
            try
            {
                ISerializer serializer = (ISerializer)new TSerializer();
                return serializer.Deserialize<TConfiguration>(File.ReadAllText(path));
            }
            catch (FileNotFoundException)
            {
                Logger.LogError($"The configuration file was not found: {path}");
                throw;
            }
            catch (PathTooLongException)
            {
                Logger.LogError("The configuration file could not be accessed because the file path is too long. Please store the files for this application in a location with a shorter path.");
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogError("Cannot read the configuration file because you are not authorized to access it. Please try running this application as administrator or moving it to a folder location that does not require special access.");
                throw;
            }
            catch (Exception)
            {
                Logger.LogError("Cannot read the configuration file. Please ensure it is formatted properly.");
                throw;
            }
        }
    }
}
