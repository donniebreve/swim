using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Common.Api;
using Common.Extensions;
using Common.Migration;
using Common.Serialization;
using Common.Serialization.Json;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Common
{
    public class WorkItemTrackingHelper
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<WorkItemTrackingHelper>();
        private static ISerializer _serializer = new JsonSerializer();

        public static Task<List<WorkItemField>> GetFields(WorkItemTrackingHttpClient client)
        {
            Logger.LogInformation(LogDestination.File, $"Getting fields for {client.BaseAddress.Host}");
            return RetryHelper.RetryAsync(async () =>
            {
                return await client.GetFieldsAsync();
            }, 5);
        }

        public static Task<List<WorkItemType>> GetWorkItemTypes(WorkItemTrackingHttpClient client, string project)
        {
            Logger.LogInformation(LogDestination.File, $"Getting work item types for {client.BaseAddress.Host}");
            return RetryHelper.RetryAsync(async () =>
            {
                return await client.GetWorkItemTypesAsync(project);
            }, 5);
        }

        public async static Task<List<WorkItemRelationType>> GetRelationTypesAsync(WorkItemTrackingHttpClient client)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                return await client.GetRelationTypesAsync();
            }, 5);
        }

        public async static Task<AttachmentReference> CreateAttachmentChunkedAsync(WorkItemTrackingHttpClient client, VssConnection connection, MemoryStream uploadStream, int chunkSizeInBytes)
        {
            // To do: Cleanup

            // Why would there be an empty attachment?

            // it's possible for the attachment to be empty, if so we can't used the chunked upload and need
            // to fallback to the normal upload path.
            if (uploadStream.Length == 0)
            {
                return await WorkItemTrackingApi.CreateAttachmentAsync(client, null);
            }

            var requestSettings = new VssHttpRequestSettings
            {
                SendTimeout = TimeSpan.FromMinutes(5)
            };
            var httpClient = new HttpClient(new VssHttpMessageHandler(connection.Credentials, requestSettings));

            // first create the attachment reference.  
            // can't use the WorkItemTrackingHttpClient since it expects either a file or a stream.
            var attachmentReference = await RetryHelper.RetryAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{connection.Uri}/_apis/wit/attachments?uploadType=chunked&api-version=3.2");
                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsAsync<AttachmentReference>();
                }
                else
                {
                    var exceptionResponse = await response.Content.ReadAsAsync<ExceptionResponse>();
                    throw new Exception(exceptionResponse.Message);
                }
            }, 5);

            // now send up each chunk
            var totalNumberOfBytes = uploadStream.Length;

            // if number of chunks divides evenly, no need to add an extra chunk
            var numberOfChunks = ClientHelpers.GetBatchCount(totalNumberOfBytes, chunkSizeInBytes);
            for (var i = 0; i < numberOfChunks; i++)
            {
                var chunkBytes = new byte[chunkSizeInBytes];
                // offset is always 0 since read moves position forward
                var chunkLength = uploadStream.Read(chunkBytes, 0, chunkSizeInBytes);

                var result = await RetryHelper.RetryAsync(async () =>
                {
                    // manually create the request since the WorkItemTrackingHttpClient does not support chunking
                    var content = new ByteArrayContent(chunkBytes, 0, chunkLength);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Headers.ContentLength = chunkLength;
                    content.Headers.ContentRange = new ContentRangeHeaderValue(i * chunkSizeInBytes, i * chunkSizeInBytes + chunkLength - 1, totalNumberOfBytes);

                    var chunkRequest = new HttpRequestMessage(HttpMethod.Put, $"{connection.Uri}/_apis/wit/attachments/" + attachmentReference.Id + "?uploadType=chunked&api-version=3.2") { Content = content };
                    var chunkResponse = await httpClient.SendAsync(chunkRequest);
                    if (!chunkResponse.IsSuccessStatusCode)
                    {
                        // there are two formats for the exception, so detect both.
                        var responseContentAsString = await chunkResponse.Content.ReadAsStringAsync();

                        //var criticalException = JsonConvert.DeserializeObject<CriticalExceptionResponse>(responseContentAsString);
                        //var exception = JsonConvert.DeserializeObject<ExceptionResponse>(responseContentAsString);
                        var criticalException = _serializer.Deserialize<CriticalExceptionResponse>(responseContentAsString);
                        var exception = _serializer.Deserialize<ExceptionResponse>(responseContentAsString);

                        throw new Exception(criticalException.Value?.Message ?? exception.Message);
                    }

                    return chunkResponse.StatusCode;
                }, 5);
            }

            return attachmentReference;
        }



        



        /// <summary>
        /// Strips the ORDER BY from the query so we can append our own order by clause
        /// and injects the tag into the where clause to skip any work items that have
        /// been completely migrated.
        /// </summary>
        public static string ParseQueryForPaging(string query, string postMoveTag)
        {
            var lastOrderByIndex = query.LastIndexOf(" ORDER BY ", StringComparison.OrdinalIgnoreCase);
            var queryWithNoOrderByClause = query.Substring(0, lastOrderByIndex > 0 ? lastOrderByIndex : query.Length);

            if (!string.IsNullOrEmpty(postMoveTag))
            {
                var postMoveTagClause = !string.IsNullOrEmpty(postMoveTag) ? $"System.Tags NOT CONTAINS '{postMoveTag}'" : string.Empty;
                return $"{InjectWhereClause(queryWithNoOrderByClause, postMoveTagClause)}";
            }
            else
            {
                return queryWithNoOrderByClause;
            }
        }

        /// <summary>
        /// Adds the watermark and id filter and order clauses
        /// </summary>
        public static string GetPageableQuery(string query, int watermark, int id)
        {
            var pageableClause = $"((System.Watermark > {watermark}) OR (System.Watermark = {watermark} AND System.Id > {id}))";
            return $"{InjectWhereClause(query, pageableClause)} ORDER BY System.Watermark, System.Id";
        }

        private static string InjectWhereClause(string query, string clause) 
        {
            var lastWhereClauseIndex = query.LastIndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);
            if (lastWhereClauseIndex > 0)
            {
                query = $"{query.Substring(0, lastWhereClauseIndex)} WHERE ({query.Substring(lastWhereClauseIndex + " WHERE ".Length)}) AND ";
            }
            else
            {
                query = $"{query} WHERE ";
            }

            return $"{query}{clause}";
        }

        /// <summary>
        /// In M133 the work item URL format changed to include project.  This breaks
        /// vsts-work-item-migrator so for now stripping out the project from the URL
        /// since the collection scoped URL is still valid.
        /// </summary>
        private static string RemoveProjectGuidFromUrl(string url)
        {
            var parts = url.Split("/", StringSplitOptions.None);
            return string.Join("/", parts.Where(p => !Guid.TryParse(p, out Guid _)));
        }

        public async static Task<WorkItemClassificationNode> GetClassificationNode(WorkItemTrackingHttpClient client, string project, TreeStructureGroup structureGroup)
        {
            Logger.LogInformation(LogDestination.File, $"Getting classification node for {client.BaseAddress.Host}");
            return await RetryHelper.RetryAsync(async () =>
            {
                return await client.GetClassificationNodeAsync(project, structureGroup, depth: int.MaxValue);
            }, 5);
        }

        public async static Task<List<WorkItemClassificationNode>> GetClassificationNodes(WorkItemTrackingHttpClient client, string project)
        {
            Logger.LogInformation(LogDestination.File, $"Getting all classification nodes for {client.BaseAddress.Host}");
            return await RetryHelper.RetryAsync(async () =>
            {
                return await client.GetRootNodesAsync(project, depth: int.MaxValue);
            }, 5);
        }

        /// <summary>
        /// Gathers all the work item IDs and Uris from the query, with special handling for the case when the query results can exceed the result cap.
        /// </summary>
        public async static Task<ConcurrentDictionary<int, WorkItemMigrationState>> GetInitialWorkItemList(WorkItemTrackingHttpClient client, string project, string query, string postMoveTag, int queryPageSize)
        {
            Logger.LogInformation(LogDestination.File, $"Getting work item ids for {client.BaseAddress.Host}");

            string[] queryFields = new string[] { FieldNames.Watermark };
            queryPageSize--; // Have to subtract 1 from the page size due to a bug in how query interprets page size
            var watermark = 0;
            var lastID = 0;
            var page = 0;

            var workItemMigrationStates = new ConcurrentDictionary<int, WorkItemMigrationState>();
            var queryHierarchyItem = await WorkItemTrackingApi.GetQueryAsync(client, project, query);

            var baseQuery = ParseQueryForPaging(queryHierarchyItem.Wiql, postMoveTag);
            var wiql = new Wiql() { Query = queryHierarchyItem.Wiql };
            if (postMoveTag != null) wiql = wiql.AddWhereConstraint($"System.Tags NOT CONTAINS '{postMoveTag}'");

            while (true)
            {
                var pagedWiql = wiql.AddWhereConstraint($"((System.Watermark > {watermark}) OR (System.Watermark = {watermark} AND System.Id > {lastID}))").SetOrderBy("System.Watermark, System.Id");
                var queryResult = await WorkItemTrackingApi.QueryByWiqlAsync(client, pagedWiql, project, queryPageSize);

                // Check if there were any results
                if (!queryResult.WorkItems.Any()) break;

                // Get the source work items
                var workItems = await WorkItemTrackingApi.GetWorkItemsAsync(client, queryResult.WorkItems.Select(item => item.Id), expand: WorkItemExpand.All);

                foreach (var workItemReference in queryResult.WorkItems)
                {
                    if (!workItemMigrationStates.ContainsKey(workItemReference.Id))
                    {
                        workItemMigrationStates[workItemReference.Id] = new WorkItemMigrationState()
                        {
                            SourceId = workItemReference.Id,
                            SourceUrl = workItemReference.Url,
                            SourceWorkItem = workItems.FirstOrDefault(item => item.Id.Value == workItemReference.Id),
                            MigrationAction = MigrationAction.Create
                        };
                    }
                    lastID = workItemReference.Id;
                    watermark = Convert.ToInt32(workItemMigrationStates[workItemReference.Id].SourceWorkItem.Fields[FieldNames.Watermark]);
                }
            }
            return workItemMigrationStates;
        }

        /// <summary>
        /// Looks at all the work items in context.WorkItemMigrationStates and identifies work items that already exist in the target.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>An awaitable Task.</returns>
        public async static Task IdentifyMigratedWorkItems(IContext context)
        {
            // Skip work items that will not be migrated
            // To do: look over validation again 
            //var workItems = context.WorkItemMigrationStates.Where(item => item.MigrationAction != MigrationAction.None);

            // To do: batch this function
            if (context.WorkItemMigrationStates.Any())
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation(LogDestination.File, "Querying target to find previously migrated work items");
                var numberOfBatches = ClientHelpers.GetBatchCount(context.WorkItemMigrationStates.Count(), Constants.BatchSize);
                await context.WorkItemMigrationStates.Batch(Constants.BatchSize).ForEachAsync(context.Configuration.Parallelism, async (workItemMigrationStates, batchId) =>
                {
                    Logger.LogInformation(LogDestination.File, $"Batch {batchId} of {numberOfBatches}: Started");
                    var batchStopwatch = Stopwatch.StartNew();

                    // Get target work items which contain the source uris in their links section
                    var sourceUris = workItemMigrationStates.Select(item => item.SourceUrl);
                    var queryResults = await WorkItemTrackingApi.QueryWorkItemsForArtifactUrisAsync(context.TargetClient.WorkItemTrackingHttpClient, new ArtifactUriQuery { ArtifactUris = sourceUris });

                    // Get target work items
                    List<int> targetIDs = new List<int>();
                    foreach (var workItemReferences in queryResults.ArtifactUrisQueryResult.Values)
                    {
                        foreach (var workItemReference in workItemReferences)
                        {
                            targetIDs.Add(workItemReference.Id);
                        }
                    }
                    if (targetIDs.Count <= 0)
                    {
                        // No existing items
                        return;
                    }
                    var targetWorkItems = await WorkItemTrackingApi.GetWorkItemsAsync(context.TargetClient.WorkItemTrackingHttpClient, targetIDs, expand: WorkItemExpand.All);

                    // See if source work items exist in the target and update the migration action
                    foreach (var workItemMigrationState in workItemMigrationStates)
                    {
                        try
                        {
                            var workItemReferences = queryResults.ArtifactUrisQueryResult[workItemMigrationState.SourceUrl];
                            if (workItemReferences.Count() > 1)
                            {
                                throw new Exception($"Found more than one work item with artifact link {workItemMigrationState.SourceUrl} in the target.");
                            }
                            if (workItemReferences.Count() == 0)
                            {
                                workItemMigrationState.MigrationAction = MigrationAction.Create;
                            }
                            if (workItemReferences.Count() == 1)
                            {
                                var workItemReference = workItemReferences.First();
                                workItemMigrationState.TargetId = workItemReference.Id;
                                workItemMigrationState.TargetUrl = workItemReference.Url;
                                workItemMigrationState.TargetWorkItem = targetWorkItems.First(item => item.Id.Value == workItemReference.Id);
                                workItemMigrationState.MigrationAction = MigrationAction.None;
                                if (context.Configuration.OverwriteExistingWorkItems)
                                {
                                    workItemMigrationState.MigrationAction = MigrationAction.Update;
                                }
                                else if (context.Configuration.UpdateModifiedWorkItems)
                                {
                                    // Get the stored source revision
                                    var workItemRelation = workItemMigrationState.TargetWorkItem.FindHyperlink(workItemMigrationState.SourceUrl);
                                    if (workItemRelation != null)
                                    {
                                        var sourceHyperlinkComment = _serializer.Deserialize<SourceHyperlinkComment>((string)workItemRelation.Attributes.GetValue(Constants.RelationAttributeComment));
                                        if (sourceHyperlinkComment.SourceRev != workItemMigrationState.SourceWorkItem.Rev)
                                        {
                                            workItemMigrationState.MigrationAction = MigrationAction.Update;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            workItemMigrationState.MigrationAction = MigrationAction.None;
                            workItemMigrationState.AddFailureReason(FailureReason.DuplicateSourceLinksOnTarget);
                            Logger.LogError(LogDestination.File, e, e.Message);
                        }
                    }

                    batchStopwatch.Stop();
                    Logger.LogInformation(LogDestination.File, $"Batch {batchId} of {numberOfBatches} completed in {batchStopwatch.Elapsed.TotalSeconds}s");
                });

                stopwatch.Stop();
                Logger.LogInformation(LogDestination.File, $"Completed querying target to find previously migrated work items in {stopwatch.Elapsed.TotalSeconds}s");
            }
        }
    }
}
