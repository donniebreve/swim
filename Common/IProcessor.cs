using Common.Configuration;

namespace Common
{
    public interface IProcessor
    {
        /// <summary>
        /// The name to use for logging
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns true if this processor should be invoked
        /// </summary>
        /// <param name="configuration">The current configuration.</param>
        /// <returns>True or false.</returns>
        bool IsEnabled(IConfiguration configuration);
    }
}