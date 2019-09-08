using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Api
{
    public class WorkItemTrackingApi
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<WorkItemTrackingApi>();

        private const int _retry = 5;

        private static RetryPolicy _retryPolicy = new RetryPolicy(
            new TransientErrorDetection(),
            new FixedInterval(name: "default", retryCount: 3, retryInterval: TimeSpan.FromSeconds(1), firstFastRetry: true));

        /// <summary>
        /// Creates an attachment.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="data">The attachment data.</param>
        /// <returns>A new AttachmentReference.</returns>
        public async static Task<AttachmentReference> CreateAttachmentAsync(WorkItemTrackingHttpClient client, Stream stream)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.CreateAttachmentAsync(stream));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { stream }, exception);
                return null;
            }
        }

        /// <summary>
        /// Gets the attachment data.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="guid">The attachment GUID.</param>
        /// <returns>A stream to the attachment data.</returns>
        public async static Task<Stream> GetAttachmentAsync(WorkItemTrackingHttpClient client, Guid guid)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetAttachmentContentAsync(guid));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { guid }, exception);
                throw;
            }
        }

        /// <summary>
        /// Gets the work item for the given ID.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item id.</param>
        /// <param name="fields">A comma-separated list of requested fields.</param>
        /// <returns>A WorkItem.</returns>
        public static async Task<WorkItem> GetWorkItemAsync(WorkItemTrackingHttpClient client, int id, IEnumerable<string> fields = null)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetWorkItemAsync(id, fields));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { id, fields }, exception);
                throw;
            }
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
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetWorkItemsAsync(ids, fields: fields, expand: expand));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { ids, fields, expand }, exception);
                throw;
            }
        }

        /// <summary>
        /// Gets the work items comments.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item ID.</param>
        /// <returns>A WorkItemComments object.</returns>
        public async static Task<WorkItemComments> GetCommentsAsync(WorkItemTrackingHttpClient client, int id)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetCommentsAsync(id));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { id }, exception);
                throw;
            }
        }

        /// <summary>
        /// Gets the revisions for a work item. Includes all fields of the work item.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item id.</param>
        /// <param name="top">The number of results to return.</param>
        /// <param name="skip">The number of results to skip.</param>
        /// <returns>A WorkItem.</returns>
        public static async Task<List<WorkItem>> GetRevisionsAsync(WorkItemTrackingHttpClient client, int id, int? top = null, int? skip = null, WorkItemExpand? expand = null)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetRevisionsAsync(id, top: top, skip: skip, expand: expand));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { id, top, skip, expand }, exception);
                throw;
            }
        }

        /// <summary>
        /// Gets the work item updates (history).
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item id.</param>
        /// <param name="top">The number of results to return.</param>
        /// <param name="skip">The number of results to skip.</param>
        /// <returns>A list of the work item updates.</returns>
        public async static Task<List<WorkItemUpdate>> GetUpdatesAsync(WorkItemTrackingHttpClient client, int id, int? top = null, int? skip = null)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetUpdatesAsync(id, top: top, skip: skip));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { id, top, skip }, exception);
                throw;
            }
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
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.GetQueryAsync(project, query, QueryExpand.Wiql));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { project, query, expand = QueryExpand.Wiql }, exception);
                throw;
            }
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
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.QueryByWiqlAsync(wiql, project, top: top));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { wiql, project, top }, exception);
                throw;
            }
        }

        /// <summary>
        /// Queries the API to find work items matching the given artifact uri query.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="artifactUriQuery">The artifact URI query.</param>
        /// <returns>An ArtifactUriQueryResult.</returns>
        public async static Task<ArtifactUriQueryResult> QueryWorkItemsForArtifactUrisAsync(WorkItemTrackingHttpClient client, ArtifactUriQuery artifactUriQuery)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.QueryWorkItemsForArtifactUrisAsync(artifactUriQuery));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { artifactUriQuery }, exception);
                throw;
            }
        }

        /// <summary>
        /// Gets the work items comments.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item ID.</param>
        /// <returns>A WorkItemComments object.</returns>
        public async static Task<WorkItem> UpdateWorkItemAsync(WorkItemTrackingHttpClient client, IEnumerable<JsonPatchOperation> patchOperations, int id)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();
            patchDocument.AddRange(patchOperations);
            return await UpdateWorkItemAsync(client, patchDocument, id);
        }

        /// <summary>
        /// Gets the work items comments.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item ID.</param>
        /// <returns>A WorkItemComments object.</returns>
        public async static Task<WorkItem> UpdateWorkItemAsync(WorkItemTrackingHttpClient client, JsonPatchDocument patchDocument, int id)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                    await client.UpdateWorkItemAsync(patchDocument, id));
            }
            catch (Exception exception)
            {
                LogExceptionToFile(new { id, patchDocument }, exception);
                return null;
            }
        }

        /// <summary>
        /// Executes a group of batch requests.
        /// </summary>
        /// <param name="client">The WorkItemTrackingHttpClient.</param>
        /// <param name="id">The work item ID.</param>
        /// <returns>A WorkItemComments object.</returns>
        public async static Task<IList<WitBatchResponse>> ExecuteBatchRequest(WorkItemTrackingHttpClient client, IList<WitBatchRequest> witBatchRequests)
        {
            // I have a feeling this will always succeed (no exception) which makes all the exception handling in the ApiWrapper classes really strange...
            // My guess is we have to check each response for errors
            return await client.ExecuteBatchRequest(witBatchRequests);
        }

        private static void LogExceptionToFile(object values, Exception exception, [CallerMemberName] string sourceMemberName = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"Api call {sourceMemberName} failed");
            sb.AppendLine($"Error: {exception.Message}: {exception.InnerException?.Message}");
            sb.AppendLine($"Parameters: {JsonConvert.SerializeObject(values, Formatting.Indented)}");
            Logger.LogError(LogDestination.File, sb.ToString());
        }
    }
}
