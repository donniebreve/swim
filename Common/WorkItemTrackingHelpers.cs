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
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace Common
{
    public class WorkItemTrackingHelper
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<WorkItemTrackingHelper>();

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

        public async static Task<List<WorkItemUpdate>> GetWorkItemUpdatesAsync(WorkItemTrackingHttpClient client, int id, int skip = 0)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                return await client.GetUpdatesAsync(id, Constants.PageSize, skip: skip);
            }, 5);
        }

        public async static Task<List<WorkItemRelationType>> GetRelationTypesAsync(WorkItemTrackingHttpClient client)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                return await client.GetRelationTypesAsync();
            }, 5);
        }

        public async static Task<AttachmentReference> CreateAttachmentAsync(WorkItemTrackingHttpClient client, MemoryStream uploadStream)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                // clone the stream since if upload fails it disploses the underlying stream
                using (var clonedStream = new MemoryStream())
                {
                    await uploadStream.CopyToAsync(clonedStream);

                    // reset position for both streams
                    uploadStream.Position = 0;
                    clonedStream.Position = 0;

                    return await client.CreateAttachmentAsync(clonedStream);
                }
            }, 5);
        }

        public async static Task<AttachmentReference> CreateAttachmentChunkedAsync(WorkItemTrackingHttpClient client, VssConnection connection, MemoryStream uploadStream, int chunkSizeInBytes)
        {
            // it's possible for the attachment to be empty, if so we can't used the chunked upload and need
            // to fallback to the normal upload path.
            if (uploadStream.Length == 0)
            {
                return await CreateAttachmentAsync(client, uploadStream);
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
                        var criticalException = JsonConvert.DeserializeObject<CriticalExceptionResponse>(responseContentAsString);
                        var exception = JsonConvert.DeserializeObject<ExceptionResponse>(responseContentAsString);

                        throw new Exception(criticalException.Value?.Message ?? exception.Message);
                    }

                    return chunkResponse.StatusCode;
                }, 5);
            }

            return attachmentReference;
        }

        public async static Task<Stream> GetAttachmentAsync(WorkItemTrackingHttpClient client, Guid id)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                return await client.GetAttachmentContentAsync(id);
            }, 5);
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
            var queryHierarchyItem = await WorkItemApi.GetQueryAsync(client, project, query);

            var baseQuery = ParseQueryForPaging(queryHierarchyItem.Wiql, postMoveTag);
            var wiql = new Wiql() { Query = queryHierarchyItem.Wiql };
            if (postMoveTag != null) wiql = wiql.AddWhereConstraint($"System.Tags NOT CONTAINS '{postMoveTag}'");

            while (true)
            {
                Logger.LogInformation(LogDestination.File, $"Querying work items for {client.BaseAddress.Host}, page {page++}, last id {lastID}");
                var pagedWiql = wiql.AddWhereConstraint($"((System.Watermark > {watermark}) OR (System.Watermark = {watermark} AND System.Id > {lastID}))").SetOrderBy("System.Watermark, System.Id");
                var result = await WorkItemApi.QueryByWiqlAsync(client, pagedWiql, project, queryPageSize);
                Logger.LogTrace(LogDestination.File, $"The query returned {result.WorkItems.Count()} results");

                // Check if there were any results
                if (!result.WorkItems.Any()) break;

                foreach (var workItemReference in result.WorkItems)
                {
                    if (!workItemMigrationStates.ContainsKey(workItemReference.Id))
                    {
                        workItemMigrationStates[workItemReference.Id] = new WorkItemMigrationState()
                        {
                            SourceId = workItemReference.Id,
                            SourceUri = new Uri(RemoveProjectGuidFromUrl(workItemReference.Url)),
                            MigrationAction = MigrationAction.Create
                        };
                    }
                    lastID = workItemReference.Id;
                }

                // Get the last work item's watermark
                var workItem = await WorkItemApi.GetWorkItemAsync(client, lastID, queryFields);

                watermark = Convert.ToInt32(workItem.Fields[FieldNames.Watermark]);
            }
            return workItemMigrationStates;
        }

        public async static Task GetSourceWorkItemData(IContext context, ISet<string> fields)
        {
            // Skip work items that will not be migrated
            var workItems = context.WorkItemMigrationStates.Where(item => item.MigrationAction != MigrationAction.None);
            if (!workItems.Any()) return;

            // Gather work item fields
            var stopwatch = Stopwatch.StartNew();
            Logger.LogInformation(LogDestination.File, "Querying source to retrieve work item fields");
            var numberOfBatches = ClientHelpers.GetBatchCount(workItems.Count(), Constants.BatchSize);
            await workItems.Batch(Constants.BatchSize).ForEachAsync(context.Configuration.Parallelism, async (workItemMigrationStates, batchId) =>
            {
                var results = await WorkItemApi.GetWorkItemsAsync(context.SourceClient.WorkItemTrackingHttpClient, workItemMigrationStates.Select(item => item.SourceId));
                foreach (var result in results)
                {
                    var workItemMigrationState = context.GetWorkItemMigrationState(result.Id.Value);
                    foreach (var field in fields)
                    {
                        workItemMigrationState.GetType().GetProperty(field).SetValue(
                            workItemMigrationState, result.GetType().GetProperty(field).GetValue(result), null);
                    }
                }
            });
        }

        /// <summary>
        /// Looks at all the work items in context.WorkItemMigrationStates and identifies work items that already exist in the target.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>An awaitable Task.</returns>
        public async static Task IdentifyMigratedWorkItems(IContext context)
        {
            // Skip work items that will not be migrated
            var workItems = context.WorkItemMigrationStates.Where(item => item.MigrationAction != MigrationAction.None);
            if (workItems.Any())
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation(LogDestination.File, "Querying target to find previously migrated work items");
                var numberOfBatches = ClientHelpers.GetBatchCount(workItems.Count(), Constants.BatchSize);
                await workItems.Batch(Constants.BatchSize).ForEachAsync(context.Configuration.Parallelism, async (workItemMigrationStates, batchId) =>
                {
                    Logger.LogInformation(LogDestination.File, $"Batch {batchId} of {numberOfBatches}: Started");
                    var batchStopwatch = Stopwatch.StartNew();

                    // Get target work items which contain the source uris in their links section
                    var sourceUris = workItemMigrationStates.Select(item => item.SourceUri.ToString());
                    var results = await WorkItemApi.QueryWorkItemsForArtifactUrisAsync(context.TargetClient.WorkItemTrackingHttpClient, new ArtifactUriQuery { ArtifactUris = sourceUris });

                    // See if source work items exist in the target and update the migration action
                    foreach (var workItem in workItemMigrationStates)
                    {
                        try
                        {
                            var workItemReferences = results.ArtifactUrisQueryResult[workItem.SourceUri.ToString()];
                            if (workItemReferences.Count() > 1)
                            {
                                throw new Exception($"Found more than one work item with artifact link {workItem.SourceUri} in the target.");
                            }
                            if (workItemReferences.Count() == 1)
                            {










                                // To do
                                ////get the source rev from the revision dictionary - populated by PostValidateWorkitems
                                //int sourceId = workItemMigrationState.SourceId;
                                //int sourceRev = ValidationContext.SourceWorkItemRevision[sourceId];
                                //string sourceUrl = ValidationContext.WorkItemIdsUris[sourceId];
                                //int targetRev = GetRev(this.ValidationContext, targetWorkItem, sourceId, hyperlinkToSourceRelation);

                                //if (IsDifferenceInRevNumbers(sourceId, targetWorkItem, hyperlinkToSourceRelation, targetRev))
                                //{
                                //    Logger.LogInformation(LogDestination.File, $"Source workItem {sourceId} Rev {sourceRev} Target workitem {targetWorkItem.Id} Rev {targetRev}");
                                //    this.sourceWorkItemIdsThatHaveBeenUpdated.Add(sourceId);
                                //    workItemMigrationState.Requirement |= WorkItemMigrationState.RequirementForExisting.UpdatePhase1;
                                //    workItemMigrationState.Requirement |= WorkItemMigrationState.RequirementForExisting.UpdatePhase2;
                                //}
                                //else if (IsPhase2UpdateRequired(workItemMigrationState, targetWorkItem))
                                //{
                                //    workItemMigrationState.Requirement |= WorkItemMigrationState.RequirementForExisting.UpdatePhase2;
                                //}
                                //else
                                //{
                                //    workItemMigrationState.Requirement |= WorkItemMigrationState.RequirementForExisting.None;
                                //}








                                workItem.MigrationAction = MigrationAction.Update;
                                workItem.TargetId = workItemReferences.First().Id;
                                workItem.TargetUri = new Uri(workItemReferences.First().Url);
                            }
                        }
                        catch (Exception e)
                        {
                            workItem.MigrationAction = MigrationAction.None;
                            workItem.FailureReason |= FailureReason.DuplicateSourceLinksOnTarget;
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
