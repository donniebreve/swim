using Common.Configuration;

namespace Common
{
    public interface IProcessor
    {
        /// <summary>
        /// The name of to use for logging
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns true if this processor should be invoked
        /// </summary>
        bool IsEnabled(IConfiguration configuration);
    }
}