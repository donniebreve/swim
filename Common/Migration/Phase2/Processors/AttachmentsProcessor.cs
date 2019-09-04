using Common.Api;
using Common.Configuration;
using Common.Extensions;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common.Migration
{
    /// <summary>
    /// Migrates attachments from the source work item to the target work item.
    /// </summary>
    public class AttachmentsProcessor : IPhase2Processor
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<AttachmentsProcessor>();

        private static readonly Regex GuidRegex = new Regex(@"([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})");

        /// <summary>
        /// The name to use for logging.
        /// </summary>
        public string Name => Constants.RelationPhaseAttachments;

        /// <summary>
        /// Returns true if this processor should be invoked.
        /// </summary>
        /// <param name="configuration">The current configuration.</param>
        /// <returns>True or false.</returns>
        public bool IsEnabled(IConfiguration configuration)
        {
            return configuration.MigrateAttachments;
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
        /// <param name="context">The migration context.</param>
        /// <param name="batchContext">The batch context.</param>
        /// <param name="sourceWorkItem">The source work item.</param>
        /// <param name="targetWorkItem">The target work item.</param>
        /// <returns>A enumerable of JsonPatchOperations.</returns>
        public async Task<IEnumerable<JsonPatchOperation>> Process(IMigrationContext context, IBatchMigrationContext batchContext, WorkItem sourceWorkItem, WorkItem targetWorkItem)
        {
            IList<JsonPatchOperation> jsonPatchOperations = new List<JsonPatchOperation>();
            if (sourceWorkItem.Relations == null)
            {
                // If the source work item has no attachments, simply return
                return jsonPatchOperations;
            }
            // Process the attachment relation objects
            foreach (var relation in sourceWorkItem.Relations)
            {
                if (!relation.IsAttachment())
                {
                    continue;
                }
                // The attachment does not already exist on the target instance
                if (targetWorkItem.FindAttachment(relation) == null)
                {
                    // Upload the attachment to the target
                    AttachmentLink attachmentLink = await MigrateAttachment(context, sourceWorkItem, relation);
                    if (attachmentLink != null)
                    {
                        // Add the attachment link to the target work item
                        WorkItemRelation workItemRelation = new WorkItemRelation();
                        workItemRelation.Rel = relation.Rel;
                        workItemRelation.Url = attachmentLink.AttachmentReference.Url;
                        workItemRelation.Attributes = new Dictionary<string, object>();
                        workItemRelation.Attributes[Constants.RelationAttributeName] = relation.Attributes[Constants.RelationAttributeName];
                        workItemRelation.Attributes[Constants.RelationAttributeResourceSize] = relation.Attributes[Constants.RelationAttributeResourceSize];
                        workItemRelation.Attributes[Constants.RelationAttributeResourceModifiedDate] = relation.Attributes[Constants.RelationAttributeResourceModifiedDate];
                        if (attachmentLink.Comment != null)
                            workItemRelation.Attributes[Constants.RelationAttributeComment] = attachmentLink.Comment;
                        if (relation.Attributes.ContainsKey(Constants.RelationAttributeComment))
                            workItemRelation.Attributes[Constants.RelationAttributeComment] = relation.Attributes[Constants.RelationAttributeComment];
                        // Generate the patch operation
                        JsonPatchOperation attachmentAddOperation = MigrationHelpers.GetRelationAddOperation(workItemRelation);
                        jsonPatchOperations.Add(attachmentAddOperation);
                    }
                }
            }
            return jsonPatchOperations;
        }

        /// <summary>
        /// Migrates an attachment from the source instance to the target instance.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="workItem">The source work item.</param>
        /// <param name="relation">The attachment realtion.</param>
        /// <returns>An AttachmentLink referencing the attachment in the target instance.</returns>
        private async Task<AttachmentLink> MigrateAttachment(IMigrationContext context, WorkItem workItem, WorkItemRelation relation)
        {
            // Only process attachments
            if (relation.Rel != Constants.AttachedFile)
            {
                return null;
            }
            string filename = (string)relation.Attributes.GetValue(Constants.RelationAttributeName);
            string comment = (string)relation.Attributes.GetValue(Constants.RelationAttributeComment);
            // Check the attachment size
            long resourceSize = (long)relation.Attributes.GetValue(Constants.RelationAttributeResourceSize);
            if (resourceSize > context.Configuration.MaxAttachmentSize)
            {
                Logger.LogWarning(LogDestination.File, $"Attachment ({filename}:{relation.Url}) from source work item {workItem.Id} exceeded the maximum attachment size of {context.Configuration.MaxAttachmentSize} bytes. Skipped creating the attachment in the target account.");
                return null;
            }
            // Get the attachment GUID
            var match = GuidRegex.Match(relation.Url);
            if (!match.Success || match.Groups.Count < 2)
            {
                Logger.LogError(LogDestination.File, $"The attachment URL ({relation.Url}) is incorrect for work item {workItem.Id}. Skipped creating the attachment in the target account.");
                context.GetWorkItemMigrationState(workItem.Id.Value).AddFailureReason(FailureReason.AttachmentDownloadError);
                return null;
            }
            var attachmentGuid = Guid.Parse(match.Groups[1].Value);
            var filePath = Path.GetTempFileName();
            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                try
                {
                    // Download the attachment
                    Logger.LogInformation(LogDestination.File, $"Downloading attachment {filename} from source work item {workItem.Id}.");
                    await (await WorkItemApi.GetAttachmentAsync(context.SourceClient.WorkItemTrackingHttpClient, attachmentGuid)).CopyToAsync(stream);
                }
                catch (Exception e)
                {
                    Logger.LogError(LogDestination.File, e, $"Unable to download attachment {filename} from source work item {workItem.Id}.");
                    context.GetWorkItemMigrationState(workItem.Id.Value).AddFailureReason(FailureReason.AttachmentDownloadError);
                    return null;
                }
                // Reset the stream
                stream.Position = 0;
                try
                {
                    // Upload the attachment
                    Logger.LogTrace(LogDestination.File, $"Uploading attachment {filename} from source work item {workItem.Id}");
                    var attachmentReference = await WorkItemApi.CreateAttachmentAsync(context.TargetClient.WorkItemTrackingHttpClient, stream);
                    // To do: reimplement the chunked upload
                    //await WorkItemTrackingHelper.CreateAttachmentChunkedAsync(context.TargetClient.WorkItemTrackingHttpClient, context.TargetClient.Connection, stream, context.Configuration.AttachmentUploadChunkSize);
                    return new AttachmentLink(filename, attachmentReference, resourceSize, comment);
                }
                catch (Exception e)
                {
                    Logger.LogError(LogDestination.File, e, $"Unable to upload attachment {filename} from source work item {workItem.Id} to the target account");
                    context.GetWorkItemMigrationState(workItem.Id.Value).AddFailureReason(FailureReason.AttachmentUploadError);
                    return null;
                }
            }
        }
    }
}