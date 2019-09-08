using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Microsoft.VisualStudio.Services.Common;
using System;

namespace Common.Api
{
    /// <summary>
    /// Detects retryable exceptions.
    /// </summary>
    public class TransientErrorDetection : ITransientErrorDetectionStrategy
    {
        /// <summary>
        /// If this exception is retryable.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <returns>True or false.</returns>
        public bool IsTransient(Exception exception)
        {
            if (exception is AggregateException)
            {
                exception = exception.InnerException;
            }
            if (exception is VssServiceException)
            {
                // Retry in the following cases only
                // VS402335: QueryTimeoutException
                // VS402490: QueryTooManyConcurrentUsers
                // VS402491: QueryServerBusy
                // TF400733: The request has been canceled: Request was blocked due to exceeding usage of resource 'WorkItemTrackingResource' in namespace 'User.'
                if (!(exception.Message.Contains("VS402335")
                    || exception.Message.Contains("VS402490")
                    || exception.Message.Contains("VS402491")
                    || exception.Message.Contains("TF400733")))
                {
                    return false;
                }
            }
            // TF237082: The file you are trying to upload exceeds the supported file upload size
            if (exception.Message.Contains("TF237082"))
            {
                return false;
            }
            return true;
        }
    }
}
