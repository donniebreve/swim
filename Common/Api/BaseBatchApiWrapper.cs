using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Migration;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Common.ApiWrappers
{
    public abstract class BaseBatchApiWrapper
    {
        protected abstract ILogger Logger { get; }


        public async Task ExecuteWitBatchRequests(
             IList<(int SourceId, WitBatchRequest WitBatchRequest)> sourceIdToWitBatchRequests,
             IMigrationContext migrationContext,
             IBatchMigrationContext batchContext,
             bool bypassRules = true,
             bool verifyOnFailure = false)
        {
            if (sourceIdToWitBatchRequests == null || sourceIdToWitBatchRequests.Count == 0)
            {
                Logger.LogError(LogDestination.All, $"Expected a non empty request list for batch {batchContext.BatchId}");
                return;
            }

            IEnumerable<int> sourceIds = sourceIdToWitBatchRequests.Select(w => w.SourceId);
            IList<WitBatchRequest> witBatchRequests = sourceIdToWitBatchRequests.Select(w => w.WitBatchRequest).ToList();
            IList<WitBatchResponse> witBatchResponses = null;

            try
            {
                var client = this.GetWorkItemTrackingHttpClient(migrationContext);
                witBatchResponses = await RetryHelper.RetryAsync(
                    async () =>
                    {
                        return await ApiWrapperHelpers.ExecuteBatchRequest(client, witBatchRequests);
                    },
                    async (requestId, exception) =>
                    {
                        if (verifyOnFailure)
                        {
                            return await ApiWrapperHelpers.HandleBatchException(requestId, exception, migrationContext, batchContext, sourceIds);
                        }
                        else
                        {
                            return exception;
                        }
                    },
                    5);
            }
            catch (Exception e)
            {
                Logger.LogError(LogDestination.All, e, $"Exception caught while calling ExecuteWitBatchRequests() in batch with batchId {batchContext.BatchId}:");
            }

            // This seems like a good idea, but the ExecuteBatchRequest method does not return the same number of responses as requests, and the responses contain no identifying information
            // Mapping a reponse back to a request does not seem like it is possible at this time
            HandleBatchResponses(sourceIdToWitBatchRequests, witBatchResponses, migrationContext, batchContext);
        }

        protected abstract WorkItemTrackingHttpClient GetWorkItemTrackingHttpClient(IMigrationContext migrationContext);

        protected abstract void UpdateWorkItemMigrationStatus(IBatchMigrationContext batchContext, int sourceId, WorkItem targetWorkItem);

        protected abstract void BatchCompleted(IMigrationContext migrationContext, IBatchMigrationContext batchContext);

        private void HandleBatchResponses(
            IList<(int SourceId, WitBatchRequest WitBatchRequest)> sourceIdToWitBatchRequests,
            IList<WitBatchResponse> witBatchResponses,
            IMigrationContext migrationContext,
            IBatchMigrationContext batchContext)
        {
            int statusCode = 200;

            // For some reason we got a witbatchresponse null or empty - could be due to http errors.  
            if (ApiWrapperHelpers.ResponsesLackExpectedData(witBatchResponses, sourceIdToWitBatchRequests))
            {
                Logger.LogInformation(LogDestination.File, $"WitBatchResponses contains no responses. Marking all the work items in batch {batchContext.BatchId} as NotMigrated");
                ApiWrapperHelpers.MarkBatchAsFailed(batchContext, sourceIdToWitBatchRequests.Select(r => r.SourceId), FailureReason.CriticalError);
                statusCode = 500;
            }
            // Check if we got one response for the entire batch. This can happen in case of status code 400 - critical error
            else if (witBatchResponses.Count == 1 && sourceIdToWitBatchRequests.Count > 1)
            {
                ApiWrapperHelpers.HandleCriticalError(witBatchResponses.First(), sourceIdToWitBatchRequests.Select(r => r.SourceId), batchContext);
                statusCode = 500;
            }
            // The number of responses has to match...
            else if (witBatchResponses.Count != sourceIdToWitBatchRequests.Count)
            {
                throw new Exception("The number of WitBatchResponses does not match the number of WitBatchRequests sent.");
            }
            // All is good, we got the expected number of responses, process accordingly
            else
            {
                for (int i = 0; i < witBatchResponses.Count; i++)
                {
                    int sourceId = sourceIdToWitBatchRequests[i].SourceId;
                    int statusCodeForWorkItem = witBatchResponses[i].Code;
                    if (statusCodeForWorkItem != 200)
                    {
                        statusCode = statusCodeForWorkItem;
                    }

                    switch ((HttpStatusCode)statusCodeForWorkItem)
                    {
                        case HttpStatusCode.OK:
                            {
                                // Deserialize the reponse
                                WorkItem workItem = witBatchResponses[i].ParseBody<WorkItem>();
                                // Get the migration state
                                var workItemMigrationState = migrationContext.GetWorkItemMigrationState(sourceId);
                                // Update the target work item
                                workItemMigrationState.TargetId = workItem.Id.Value;
                                workItemMigrationState.TargetUrl = workItem.Url;
                                workItemMigrationState.TargetWorkItem = workItem;
                                break;
                            }
                        case HttpStatusCode.BadRequest:
                            SaveFailureStatusInWorkItemsMigrationState(batchContext, sourceId, FailureReason.BadRequest);
                            ApiWrapperHelpers.HandleUnsuccessfulWitBatchResponse(witBatchResponses[i], sourceIdToWitBatchRequests[i], batchContext, statusCodeForWorkItem, FailureReason.BadRequest);
                            break;
                        default:
                            SaveFailureStatusInWorkItemsMigrationState(batchContext, sourceId, FailureReason.UnexpectedError);
                            ApiWrapperHelpers.HandleUnsuccessfulWitBatchResponse(witBatchResponses[i], sourceIdToWitBatchRequests[i], batchContext, statusCodeForWorkItem, FailureReason.UnexpectedError);
                            break;
                    }
                }

                BatchCompleted(migrationContext, batchContext);
            }

            if (statusCode != 200)
            {
                WitBatchRequestLogger.Log(sourceIdToWitBatchRequests.Select(r => r.WitBatchRequest).ToList(), witBatchResponses, batchContext.BatchId);
            }
        }

        private void SaveFailureStatusInWorkItemsMigrationState(IBatchMigrationContext batchContext, int sourceWorkItemId, FailureReason failureReason)
        {
            WorkItemMigrationState state = batchContext.WorkItemMigrationState.First(a => a.SourceId == sourceWorkItemId);
            state.FailureReason |= failureReason;
            //if (state.RevAndPhaseStatus != null && state.RevAndPhaseStatus.PhaseStatus != null)
            //{
            //    state.RevAndPhaseStatus.PhaseStatus.Clear();
            //}
        }
    }
}
