using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Common.Api;
using Common.Extensions;
using Common.Configuration;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Newtonsoft.Json;

namespace Common.Migration
{
    /// <summary>
    /// Adds an attachment to the target work item containing the history of the source work item.
    /// </summary>
    public class HistoryAttachmentProcessor : IPhase2Processor
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<HistoryAttachmentProcessor>();

        /// <summary>
        /// The name to use for logging.
        /// </summary>
        public string Name => "History Attachment Processor";

        /// <summary>
        /// Returns true if this processor should be invoked.
        /// </summary>
        /// <param name="configuration">The current configuration.</param>
        /// <returns>True or false.</returns>
        public bool IsEnabled(IConfiguration configuration)
        {
            return configuration.MigrateHistory;
        }

        /// <summary>
        /// Performs work necessary prior to processing the work item batch.
        /// </summary>
        /// <param name="migrationContext">The migration context.</param>
        /// <param name="batchContext">The batch context.</param>
        /// <param name="sourceWorkItems">The list of source work items.</param>
        /// <param name="targetWorkItems">The list of target work items.</param>
        /// <returns>An awaitable Task.</returns>
        public async Task Preprocess(IMigrationContext migrationContext, IBatchMigrationContext batchContext, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems)
        {
            // Nothing required
        }

        /// <summary>
        /// Process the work item batch.
        /// </summary>
        /// <param name="migrationContext">The migration context.</param>
        /// <param name="batchContext">The batch context.</param>
        /// <param name="sourceWorkItem">The source work item.</param>
        /// <param name="targetWorkItem">The target work item.</param>
        /// <returns>A enumerable of JsonPatchOperations.</returns>
        public async Task<IEnumerable<JsonPatchOperation>> Process(IMigrationContext context, IBatchMigrationContext batchContext, WorkItem sourceWorkItem, WorkItem targetWorkItem)
        {
            var jsonPatchOperations = new List<JsonPatchOperation>();
            // Get the work item history
            var workItemHistory = await GetWorkItemHistory(sourceWorkItem, context);
            // Convert to the desired format
            byte[] historyDocumentData = null;
            string fileName = $"history";
            if (context.Configuration.HistoryAttachmentFormat == ".json")
            {
                fileName += ".json";
                historyDocumentData = ConvertToJsonDocument(workItemHistory);
            }
            if (context.Configuration.HistoryAttachmentFormat == ".txt")
            {
                fileName += ".txt";
                historyDocumentData = ConvertToTextDocument(workItemHistory);
            }
            // The attachment does not already exist on the target instance, or the history has changed
            if (targetWorkItem.FindAttachment(fileName, historyDocumentData.LongLength) == null)
            {
                using (MemoryStream stream = new MemoryStream(historyDocumentData))
                {
                    // Upload the attachment
                    var attachmentReference = await WorkItemApi.CreateAttachmentAsync(context.TargetClient.WorkItemTrackingHttpClient, stream);
                    // Create the attachment link
                    var attachmentLink = new AttachmentLink(fileName, attachmentReference, historyDocumentData.LongLength);
                    // Return the patch operation
                    JsonPatchOperation revisionHistoryAttachmentAddOperation = MigrationHelpers.GetRevisionHistoryAttachmentAddOperation(attachmentLink, sourceWorkItem.Id.Value);
                    jsonPatchOperations.Add(revisionHistoryAttachmentAddOperation);
                }
            }
            return jsonPatchOperations;
        }

        /// <summary>
        /// Gets the history for a work item.
        /// </summary>
        /// <remarks>
        /// Work item links that are added or removed do not contain the work item title.
        /// To do: possibly query the instance for the work item title.
        /// </remarks>
        /// <param name="workItem">The work item.</param>
        /// <param name="context">The current context.</param>
        /// <returns>A list of WorkItemUpdate objects.</returns>
        private async Task<List<WorkItemUpdate>> GetWorkItemHistory(WorkItem workItem, IContext context)
        {
            int updateCount = 0;
            int updateLimit = context.Configuration.HistoryLimit;
            List<WorkItemUpdate> workItemHistory = new List<WorkItemUpdate>();
            while (updateCount < updateLimit)
            {
                var itemsToRetrieve = Math.Min(Constants.PageSize, updateLimit - updateCount);
                var results = await WorkItemApi.GetUpdatesAsync(context.SourceClient.WorkItemTrackingHttpClient, workItem.Id.Value, itemsToRetrieve, updateCount);
                updateCount += results.Count;
                workItemHistory.AddRange(results);
                // If the results are less than the page size, we are done
                if (results.Count < itemsToRetrieve)
                {
                    break;
                }
            }
            return workItemHistory;
        }

        /// <summary>
        /// Converts a list of WorkItemUpdate objects to a JSON document.
        /// </summary>
        /// <param name="workItemHistory">A list of WorkItemUpdate objects.</param>
        /// <returns>A JSON document.</returns>
        private byte[] ConvertToJsonDocument(List<WorkItemUpdate> workItemHistory)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(workItemHistory));
        }

        /// <summary>
        /// Converts a list of WorkItemUpdate objects to a text document.
        /// </summary>
        /// <remarks>
        /// Excludes some fields from the document, and potentially some revisions. Attempts to replicate the history view in TFS.
        /// </remarks>
        /// <param name="workItemHistory">A list of WorkItemUpdate objects.</param>
        /// <returns>A text document.</returns>
        private byte[] ConvertToTextDocument(List<WorkItemUpdate> workItemHistory)
        {
            StringBuilder documentBuilder = new StringBuilder();
            foreach (var revision in workItemHistory)
            {
                StringBuilder revisionDetailBuilder = new StringBuilder();
                if (revision.Fields != null)
                {
                    foreach (var field in revision.Fields)
                    {
                        if (field.Key == "System.AreaId"
                            || field.Key == "System.AreaLevel1"
                            || field.Key == "System.AreaLevel2"
                            || field.Key == "System.AreaLevel3"
                            || field.Key == "System.AuthorizedAs"
                            || field.Key == "System.AuthorizedDate"
                            || field.Key == "System.ChangedDate"
                            || field.Key == "System.IterationId"
                            || field.Key == "System.IterationLevel1"
                            || field.Key == "System.IterationLevel2"
                            || field.Key == "System.IterationLevel3"
                            || field.Key == "System.PersonId"
                            || field.Key == "System.Rev"
                            || field.Key == "System.RevisedDate"
                            || field.Key == "System.Watermark"
                            || field.Key == "Microsoft.VSTS.Common.StackRank")
                        {
                            // These fields are unwanted in text output
                            continue;
                        }
                        if (!Object.Equals(field.Value.OldValue, field.Value.NewValue))
                        {
                            revisionDetailBuilder.AppendLine($"Changed {field.Key} from '{Convert.ToString(field.Value.OldValue)}' to '{Convert.ToString(field.Value.NewValue)}'");
                        }
                    }
                }
                if (revision.Links != null)
                {
                    // Not sure yet
                }
                if (revision.Relations != null)
                {
                    if (revision.Relations.Added != null)
                    {
                        foreach (var relation in revision.Relations.Added)
                        {
                            revisionDetailBuilder.AppendLine($"Added a {relation.Rel} relation to {relation.Title}:{relation.Url}");
                        }
                    }
                    if (revision.Relations.Removed != null)
                    {
                        foreach (var relation in revision.Relations.Removed)
                        {
                            revisionDetailBuilder.AppendLine($"Removed a {relation.Rel} relation to {relation.Title}:{relation.Url}");
                        }
                    }
                    if (revision.Relations.Updated != null)
                    {
                        foreach (var relation in revision.Relations.Updated)
                        {
                            revisionDetailBuilder.AppendLine($"Updated a {relation.Rel} relation to {relation.Title}:{relation.Url}");
                        }
                    }
                }
                // If any information was added besides the header, add the revision to the document
                if (revisionDetailBuilder.Length > 0)
                {
                    documentBuilder.AppendLine($"Revision {revision.Rev}");
                    documentBuilder.AppendLine($"Revised By: {revision.RevisedBy.Name}");
                    documentBuilder.AppendLine($"Revision Date: {revision.RevisedDate}");
                    documentBuilder.AppendLine(revisionDetailBuilder.ToString());
                }
            }
            return Encoding.UTF8.GetBytes(documentBuilder.ToString());
        }
    }
}
