using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Newtonsoft.Json;
using Common.ApiWrappers;
using Logging;
using Common.Api;

namespace Common.Migration
{
    public class Migrator
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<Migrator>();
        private IMigrationContext _context;
        private string queryString;

        public Migrator(IMigrationContext context)
        {
            this._context = context;
            bool bypassRules = true;
            bool suppressNotifications = true;
            this.queryString = $"bypassRules={bypassRules}&suppressNotifications={suppressNotifications}&api-version=4.0";
        }

        public async Task Migrate()
        {
            Logger.LogInformation("Migration option selected");

            await MigratePhase1();
            await MigratePhase2();
            await MigratePhase3();

            LogFinalStatus();
        }

        private async Task MigratePhase1()
        {
            var stopwatch = new Stopwatch();
            Logger.LogInformation("Starting migration phase 1");
            foreach (IPhase1Processor processor in ClientHelpers.GetProcessorInstances<IPhase1Processor>(this._context.Configuration))
            {
                Logger.LogInformation(LogDestination.File, $"Starting migration phase for: {processor.Name}");
                stopwatch.Start();
                await processor.Process(this._context);
                stopwatch.Reset();
                Logger.LogInformation(LogDestination.File, $"Completed migration phase for: {processor.Name} in {stopwatch.Elapsed.TotalSeconds}s");
            }
            Logger.LogInformation("Completed migration phase 1");
        }

        private async Task MigratePhase2()
        {
            Logger.LogInformation("Starting migration phase 2");

            IList<WorkItemMigrationState> workItems = new List<WorkItemMigrationState>();
            if (_context.Configuration.CreateNewWorkItems)
            {
                workItems.AddRange(_context.WorkItemMigrationStates.Where(item => item.MigrationAction == MigrationAction.Create));
            }
            if (_context.Configuration.UpdateModifiedWorkItems || _context.Configuration.OverwriteExistingWorkItems)
            {
                // To do: figure out why he wanted to only update items with the RequirementForExisting.UpdatePhase2
                workItems.AddRange(_context.WorkItemMigrationStates.Where(item => item.MigrationAction == MigrationAction.Update));
            }

            var phase2WorkItemsToUpdateCount = workItems.Count();
            var totalNumberOfBatches = ClientHelpers.GetBatchCount(phase2WorkItemsToUpdateCount, Constants.BatchSize);

            if (phase2WorkItemsToUpdateCount == 0)
            {
                Logger.LogInformation(LogDestination.File, "No work items to process for phase 2");
                return;
            }

            await workItems.Batch(Constants.BatchSize).ForEachAsync(_context.Configuration.Parallelism, async (workItemMigrationStateBatch, batchId) =>
            {
                Logger.LogTrace(LogDestination.File, $"Reading Phase 2 source and target work items for batch {batchId} of {totalNumberOfBatches}");
                // make web call to get source and target work items
                IList<WorkItem> sourceWorkItemsInBatch = await WorkItemApi.GetWorkItemsAsync(_context.SourceClient.WorkItemTrackingHttpClient, workItemMigrationStateBatch.Select(a => a.SourceId).ToList(), expand: WorkItemExpand.All);
                IList<WorkItem> targetWorkItemsInBatch = await WorkItemApi.GetWorkItemsAsync(_context.TargetClient.WorkItemTrackingHttpClient, workItemMigrationStateBatch.Select(a => a.TargetId.Value).ToList(), expand: WorkItemExpand.Relations);

                IBatchMigrationContext batchContext = new BatchMigrationContext(batchId, workItemMigrationStateBatch);
                batchContext.SourceWorkItemIdToTargetWorkItemIdMapping = workItemMigrationStateBatch.ToDictionary(key => key.SourceId, value => value.TargetId.Value);

                foreach (var sourceWorkItem in sourceWorkItemsInBatch)
                {
                    int targetId = Migrator.GetTargetId(sourceWorkItem.Id.Value, workItemMigrationStateBatch);
                    batchContext.TargetIdToSourceWorkItemMapping.Add(targetId, sourceWorkItem);
                }

                Logger.LogTrace(LogDestination.File, $"Generating Phase 2 json patch operations for batch {batchId} of {totalNumberOfBatches}");
                var sourceIdToWitBatchRequests = await GenerateWitBatchRequestsForPhase2Batch(batchContext, batchId, workItemMigrationStateBatch, sourceWorkItemsInBatch, targetWorkItemsInBatch);

                Logger.LogTrace(LogDestination.File, $"Saving Phase 2 json patch operations for batch {batchId} of {totalNumberOfBatches}");

                var phase2ApiWrapper = new Phase2ApiWrapper();
                await phase2ApiWrapper.ExecuteWitBatchRequests(sourceIdToWitBatchRequests, _context, batchContext);

                Logger.LogTrace(LogDestination.File, $"Completed Phase 2 for batch {batchId} of {totalNumberOfBatches}");
            });

            Logger.LogInformation("Completed migration phase 2");
        }

        private async Task MigratePhase3()
        {
            IEnumerable<IPhase3Processor> phase3Processors = ClientHelpers.GetProcessorInstances<IPhase3Processor>(_context.Configuration);
            if (phase3Processors != null && !phase3Processors.Any())
            {
                // nothing to do if no phase 3 processors are enabled
                return;
            }

            // Phase1 or Phase2 have completed, and FailureReason == None
            IEnumerable<WorkItemMigrationState> successfullyMigratedWorkItemMigrationStates = _context.WorkItemMigrationStates.Where(w => (w.MigrationCompleted.HasFlag(WorkItemMigrationState.MigrationCompletionStatus.Phase1) || w.MigrationCompleted.HasFlag(WorkItemMigrationState.MigrationCompletionStatus.Phase2)) && w.FailureReason == FailureReason.None);
            var phase3WorkItemsToUpdateCount = successfullyMigratedWorkItemMigrationStates.Count();
            var totalNumberOfBatches = ClientHelpers.GetBatchCount(phase3WorkItemsToUpdateCount, Constants.BatchSize);

            if (phase3WorkItemsToUpdateCount == 0)
            {
                return;
            }

            await successfullyMigratedWorkItemMigrationStates.Batch(Constants.BatchSize).ForEachAsync(_context.Configuration.Parallelism, async (workItemMigrationStateBatch, batchId) =>
            {
                IBatchMigrationContext batchContext = new BatchMigrationContext(batchId, workItemMigrationStateBatch);
                IList<(int SourceId, WitBatchRequest WitBatchRequest)> sourceIdToWitBatchRequests = new List<(int SourceId, WitBatchRequest WitBatchRequest)>();
                IList<WorkItem> sourceWorkItemsInBatch = await WorkItemApi.GetWorkItemsAsync(_context.SourceClient.WorkItemTrackingHttpClient, workItemMigrationStateBatch.Select(a => a.SourceId).ToList(), expand: WorkItemExpand.All);

                foreach (WorkItem sourceWorkItem in sourceWorkItemsInBatch)
                {
                    IList<JsonPatchOperation> jsonPatchOperations = new List<JsonPatchOperation>();
                    foreach (IPhase3Processor processor in phase3Processors)
                    {
                        IEnumerable<JsonPatchOperation> processorJsonPatchOperations = await processor.Process(_context, null, sourceWorkItem, null);
                        jsonPatchOperations.AddRange(processorJsonPatchOperations);
                    }

                    if (jsonPatchOperations.Any())
                    {
                        WitBatchRequest witBatchRequest = GenerateWitBatchRequestFromJsonPatchOperations(jsonPatchOperations, sourceWorkItem.Id.Value);
                        sourceIdToWitBatchRequests.Add((sourceWorkItem.Id.Value, witBatchRequest));
                    }
                }

                var phase3ApiWrapper = new Phase3ApiWrapper();
                await phase3ApiWrapper.ExecuteWitBatchRequests(sourceIdToWitBatchRequests, _context, batchContext);
            });
        }

        private WitBatchRequest GenerateWitBatchRequestFromJsonPatchOperations(IList<JsonPatchOperation> jsonPatchOperations, int workItemId)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Content-Type", "application/json-patch+json");

            JsonPatchDocument jsonPatchDocument = new JsonPatchDocument();
            jsonPatchDocument.AddRange(jsonPatchOperations);

            WitBatchRequest witBatchRequest = null;
            if (jsonPatchDocument.Any())
            {
                witBatchRequest = new WitBatchRequest();
                string json = JsonConvert.SerializeObject(jsonPatchDocument);
                witBatchRequest.Method = "PATCH";
                witBatchRequest.Headers = headers;
                witBatchRequest.Uri = $"/_apis/wit/workItems/{workItemId}?{this.queryString}";
                witBatchRequest.Body = json;
            }

            return witBatchRequest;
        }

        private async Task<IList<(int SourceId, WitBatchRequest WitBatchRequest)>> GenerateWitBatchRequestsForPhase2Batch(IBatchMigrationContext batchContext, int batchId, IList<WorkItemMigrationState> workItemMigrationState, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems)
        {
            IList<(int SourceId, WitBatchRequest WitBatchRequest)> result = new List<(int SourceId, WitBatchRequest WitBatchRequest)>();
            IEnumerable<IPhase2Processor> phase2Processors = ClientHelpers.GetProcessorInstances<IPhase2Processor>(_context.Configuration);
            foreach (IPhase2Processor processor in phase2Processors)
            {
                Logger.LogInformation(LogDestination.File, $"Starting preprocessing of phase 2 step {processor.Name} for batch {batchId}");
                await processor.Preprocess(_context, batchContext, sourceWorkItems, targetWorkItems);
                Logger.LogInformation(LogDestination.File, $"Completed preprocessing of phase 2 step {processor.Name} for batch {batchId}");
            }

            foreach (var sourceToTarget in batchContext.SourceWorkItemIdToTargetWorkItemIdMapping)
            {
                int sourceId = sourceToTarget.Key;
                int targetId = sourceToTarget.Value;

                WorkItem sourceWorkItem = sourceWorkItems.First(a => a.Id == sourceId);
                WorkItem targetWorkItem = targetWorkItems.First(a => a.Id == targetId);

                IList<JsonPatchOperation> jsonPatchOperations = new List<JsonPatchOperation>();

                WorkItemMigrationState state = workItemMigrationState.First(a => a.SourceId == sourceId);
                state.RevAndPhaseStatus = GetRevAndPhaseStatus(targetWorkItem, sourceId);
                ISet<string> enabledPhaseStatuses = System.Linq.Enumerable.ToHashSet(phase2Processors.Where(a => a.IsEnabled(_context.Configuration)).Select(b => b.Name));
                enabledPhaseStatuses.Remove(Constants.RelationPhaseClearAllRelations);

                foreach (IPhase2Processor processor in phase2Processors)
                {
                    IEnumerable<JsonPatchOperation> processorJsonPatchOperations = await processor.Process(_context, batchContext, sourceWorkItem, targetWorkItem);
                    jsonPatchOperations.AddRange(processorJsonPatchOperations);
                }

                jsonPatchOperations.Add(GetAddHyperlinkWithCommentOperation(targetWorkItems, state, sourceId, targetId, sourceWorkItem, enabledPhaseStatuses));

                if (jsonPatchOperations.Any())
                {
                    WitBatchRequest witBatchRequest = GenerateWitBatchRequestFromJsonPatchOperations(jsonPatchOperations, targetId);
                    result.Add((sourceId, witBatchRequest));
                }
            }

            return result;
        }

        private JsonPatchOperation GetAddHyperlinkWithCommentOperation(IList<WorkItem> targetWorkItems, WorkItemMigrationState state, int sourceId, int targetId, WorkItem sourceWorkItem, ISet<string> enabledPhaseStatuses)
        {
            IList<WorkItemRelation> targetRelations = targetWorkItems.First(a => a.Id == targetId).Relations;

            foreach (WorkItemRelation targetRelation in targetRelations)
            {
                if (RelationHelpers.IsRelationHyperlinkToSourceWorkItem(_context, targetRelation, sourceId))
                {
                    // only store the enabled phase statuses
                    RevAndPhaseStatus newRevAndPhaseStatus = new RevAndPhaseStatus();
                    newRevAndPhaseStatus.Rev = sourceWorkItem.Rev.Value;
                    newRevAndPhaseStatus.PhaseStatus = enabledPhaseStatuses;
                    state.RevAndPhaseStatus = newRevAndPhaseStatus;

                    // get the key even if its letter case is different but it matches otherwise
                    string idKeyFromFields = targetRelation.Attributes.GetKeyIgnoringCase(Constants.RelationAttributeId);
                    object attributeId = targetRelation.Attributes[idKeyFromFields];

                    JsonPatchOperation addHyperlinkWithCommentOperation = MigrationHelpers.GetHyperlinkAddOperation(
                        state.SourceUri.ToString(),
                        newRevAndPhaseStatus.GetCommentRepresentation(),
                        attributeId);

                    return addHyperlinkWithCommentOperation;
                }
            }

            throw new Exception($"Could not find hyperlink to source work item on target work item with id: {targetId}. Expected source work item id: {sourceId}");
        }

        private RevAndPhaseStatus GetRevAndPhaseStatus(WorkItem targetWorkItem, int sourceWorkItemId)
        {
            if (targetWorkItem.Relations != null)
            {
                foreach (WorkItemRelation relation in targetWorkItem.Relations)
                {
                    if (RelationHelpers.IsRelationHyperlinkToSourceWorkItem(_context, relation, sourceWorkItemId))
                    {
                        // get the key even if its letter case is different but it matches otherwise
                        string keyFromFields = relation.Attributes.GetKeyIgnoringCase(Constants.RelationAttributeComment);
                        string relationRevAndPhaseStatusComment = relation.Attributes[keyFromFields].ToString();

                        RevAndPhaseStatus revAndPhaseStatus = new RevAndPhaseStatus(relationRevAndPhaseStatusComment);
                        return revAndPhaseStatus;
                    }
                }
            }

            throw new Exception($"Could not find comment in relation hyperlink to source work item on target work item with id: {targetWorkItem.Id.Value}. Expected source work item id: {sourceWorkItemId}");
        }

        private void LogFinalStatus()
        {
            var createdWorkItems = this._context.WorkItemMigrationStates.Where(w => w.MigrationAction == MigrationAction.Create);
            if (createdWorkItems.Any())
            {
                Logger.LogSuccess(LogDestination.All, $"Created {createdWorkItems.Count()} work item(s)");
                Logger.LogInformation(LogDestination.File, "Created WorkItems");
                Logger.LogInformation(LogDestination.File, "Source Id   :: Target Id");
                foreach (var item in createdWorkItems)
                {
                    Logger.LogInformation(LogDestination.File, $"{item.SourceId} :: {item.TargetId}");
                }
            }

            var updatedWorkItems = this._context.WorkItemMigrationStates.Where(w => w.MigrationAction == MigrationAction.Update && w.Requirement.HasFlag(WorkItemMigrationState.RequirementForExisting.UpdatePhase1));
            if (updatedWorkItems.Any())
            {
                Logger.LogSuccess(LogDestination.All, $"Updated {updatedWorkItems.Count()} work item(s)");
                Logger.LogInformation(LogDestination.File, "Updated WorkItems");
                Logger.LogInformation(LogDestination.File, "Source Id   :: Target Id");
                foreach (var item in updatedWorkItems)
                {
                    Logger.LogInformation(LogDestination.File, $"{item.SourceId} :: {item.TargetId}");
                }
            }

            // To do: fix
            Dictionary<int, FailureReason> notMigratedWorkItems = ClientHelpers.GetNotMigratedWorkItemsFromWorkItemsMigrationState(_context.WorkItemMigrationStates);

            if (notMigratedWorkItems.Any())
            {
                //Log breakdown of not migrated work items by FailureReason
                Logger.LogError(LogDestination.All, $"{notMigratedWorkItems.Count} total work item(s) failed.");

                FailureReason[] failureReasons = (FailureReason[])Enum.GetValues(typeof(FailureReason));
                FailureReason[] failureReasonsWithoutNone = failureReasons.SubArray(1, failureReasons.Length - 1);

                foreach (FailureReason failureReason in failureReasonsWithoutNone)
                {
                    int failureCount = notMigratedWorkItems.Where(a => a.Value.HasFlag(failureReason)).Count();
                    if (failureCount > 0)
                    {
                        Logger.LogError(LogDestination.All, $"   {failureCount} work item(s) failed for this reason: {failureReason}.");
                    }
                }

                //Log all the not migrated work items to both console and file 
                foreach (var item in notMigratedWorkItems)
                {
                    Logger.LogInformation(LogDestination.File, $"{item.Key} :: {item.Value}");
                }
            }

            Logger.LogInformation(LogDestination.All, "Migration complete");
        }

        /// <summary>
        /// Populates batchContext.WorkItems
        /// </summary>
        /// <param name="migrationContext"></param>
        /// <param name="workItemIds"></param>
        /// <param name="batchContext"></param>
        /// <param name="expand"></param>
        /// <returns></returns>
        public static async Task ReadSourceWorkItems(IMigrationContext migrationContext, IEnumerable<int> workItemIds, IBatchMigrationContext batchContext, WorkItemExpand? expand = WorkItemExpand.All)
        {
            batchContext.SourceWorkItems = await WorkItemApi.GetWorkItemsAsync(migrationContext.SourceClient.WorkItemTrackingHttpClient, workItemIds, expand: expand);
        }

        public static int GetTargetId(int sourceId, IEnumerable<WorkItemMigrationState> workItemMigrationStates)
        {
            return workItemMigrationStates.First(a => a.SourceId == sourceId).TargetId.Value;
        }
    }
}
