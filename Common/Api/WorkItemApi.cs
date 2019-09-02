using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Api
{
    public class WorkItemApi
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<WorkItemApi>();

        private const int _retry = 5;

        /// <summary>
        /// Gets the work item for the given ID.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item id.</param>
        /// <param name="fields">A comma-separated list of requested fields.</param>
        /// <returns>A WorkItem.</returns>
        public static async Task<WorkItem> GetWorkItemAsync(WorkItemTrackingHttpClient client, int id, IEnumerable<string> fields = null)
        {
            return await RetryAsync(async () =>
            {
                return await client.GetWorkItemAsync(id, fields);
            });
        }

        /// <summary>
        /// Gets the work items for the given IDs.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item id.</param>
        /// <param name="fields">A comma-separated list of requested fields.</param>
        /// <returns>A WorkItem.</returns>
        public static async Task<IList<WorkItem>> GetWorkItemsAsync(WorkItemTrackingHttpClient client, IEnumerable<int> ids, IEnumerable<string> fields = null, WorkItemExpand? expand = null)
        {
            return await RetryAsync(async () =>
            {
                return await client.GetWorkItemsAsync(ids, fields: fields, expand: expand);
            });
        }

        /// <summary>
        /// Retrieves the work item query.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="project">The project name.</param>
        /// <param name="query">The query path.</param>
        /// <returns>A QueryHierarchyItem.</returns>
        public static async Task<QueryHierarchyItem> GetQueryAsync(WorkItemTrackingHttpClient client, string project, string query)
        {
            return await RetryAsync(async () =>
            {
                return await client.GetQueryAsync(project, query, QueryExpand.Wiql);
            });
        }

        /// <summary>
        /// Queries the API using the given WIQL.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="wiql">The query containing the WIQL.</param>
        /// <param name="project">The project ID or project name.</param>
        /// <param name="top">The max number of results to return.</param>
        /// <returns>A WorkItemQueryResult.</returns>
        public static async Task<WorkItemQueryResult> QueryByWiqlAsync(WorkItemTrackingHttpClient client, Wiql wiql, string project, int? top = null)
        {
            return await RetryAsync(async () =>
            {
                return await client.QueryByWiqlAsync(wiql, project, top: top);
            });
        }

        /// <summary>
        /// Queries the API to find work items matching the given artifact uri query.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="artifactUriQuery">The artifact URI query.</param>
        /// <returns>An ArtifactUriQueryResult.</returns>
        public async static Task<ArtifactUriQueryResult> QueryWorkItemsForArtifactUrisAsync(WorkItemTrackingHttpClient client, ArtifactUriQuery artifactUriQuery)
        {
            return await RetryAsync(async () =>
            {
                return await client.QueryWorkItemsForArtifactUrisAsync(artifactUriQuery);
            });
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> function, Func<Guid, Exception, Task<Exception>> exceptionHandler = null, int retryCount = _retry, int secsDelay = 1)
        {
            Guid requestId = Guid.NewGuid();
            Exception exception = null;
            bool succeeded = true;
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    succeeded = true;
                    return await function();
                }
                catch (Exception ex)
                {
                    exception = TranslateException(requestId, ex);

                    if (exceptionHandler != null)
                    {
                        try
                        {
                            exception = await exceptionHandler(requestId, exception);
                        }
                        catch
                        {
                            // continue with the original exception handling process
                        }
                    }

                    if (exception is RetryPermanentException)
                    {
                        //exit the for loop as we are not retrying for anything considered permanent
                        break;
                    }

                    succeeded = false;
                    Logger.LogTrace(LogDestination.File, $"Sleeping for {secsDelay} seconds and retrying {requestId} again.");

                    await Task.Delay(secsDelay * 1000);

                    // add 1 second to delay so that each delay is slightly incrementing in wait time
                    secsDelay += 1;
                }
                finally
                {
                    if (succeeded && i >= 1)
                    {
                        Logger.LogSuccess(LogDestination.File, $"request {requestId} succeeded.");
                    }
                }
            }

            if (exception is null)
            {
                throw new RetryExhaustedException($"Retry count exhausted for {requestId}.");
            }
            else
            {
                throw exception;
            }
        }

        /// <summary>
        /// Translates the exception to a permanent exception if not retryable
        /// </summary>
        private static Exception TranslateException(Guid requestId, Exception e)
        {
            var ex = UnwrapIfAggregateException(e);
            if (ex is VssServiceException)
            {
                //Retry in following cases only
                //VS402335: QueryTimeoutException
                //VS402490: QueryTooManyConcurrentUsers
                //VS402491: QueryServerBusy
                //TF400733: The request has been canceled: Request was blocked due to exceeding usage of resource 'WorkItemTrackingResource' in namespace 'User.'
                if (ex.Message.Contains("VS402335")
                    || ex.Message.Contains("VS402490")
                    || ex.Message.Contains("VS402491")
                    || ex.Message.Contains("TF400733"))
                {
                    Logger.LogWarning(LogDestination.File, ex, $"VssServiceException exception caught for {requestId}:");
                }
                else
                {
                    //Specific TF or VS errors. No need to retry DO NOT THROW
                    return new RetryPermanentException($"Permanent error for {requestId}, not retrying", ex);
                }
            }
            else if (ex is HttpRequestException)
            {
                // all request exceptions should be considered retryable
                Logger.LogWarning(LogDestination.File, ex, $"HttpRequestException exception caught for {requestId}:");
            }
            // TF237082: The file you are trying to upload exceeds the supported file upload size
            else if (ex.Message.Contains("TF237082"))
            {
                return new RetryPermanentException($"Permanent error for {requestId}, not retrying", ex);
            }
            else
            {
                //Log and throw every other exception for now - example HttpServiceException for connection errors
                //Need to retry - in case of connection timeouts or server unreachable etc.
                Logger.LogWarning(LogDestination.File, ex, $"Exception caught for {requestId}:");
            }

            return ex;
        }

        private static Exception UnwrapIfAggregateException(Exception e)
        {
            Exception ex;
            //Async calls returns AggregateException
            //Sync calls returns exception
            if (e is AggregateException)
            {
                ex = e.InnerException;
            }
            else
            {
                ex = e;
            }

            return ex;
        }
    }
}
