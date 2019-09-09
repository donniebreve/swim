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
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using System.Linq;
using Microsoft.VisualStudio.Services.WebApi.Patch;

namespace Common.Migration
{
    /// <summary>
    /// Adds an attachment to the target work item containing the history of the source work item.
    /// </summary>
    public class HistoryProcessor : IPhase2Processor
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<HistoryProcessor>();

        private IList<string> _ignoredFields = new List<string>()
        {
            "System.AreaId",
            "System.AreaLevel1",
            "System.AreaLevel2",
            "System.AreaLevel3",
            "System.AuthorizedAs",
            "System.AuthorizedDate",
            "System.BoardColumnDone",
            "System.ChangedDate",
            "System.IterationId",
            "System.IterationLevel1",
            "System.IterationLevel2",
            "System.IterationLevel3",
            "System.PersonId",
            "System.Rev",
            "System.RevisedDate",
            "System.Watermark",
            "Microsoft.VSTS.Common.StackRank",
            "WEF_"
        };

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

        public async Task Preprocess(IContext context, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems)
        {
            // Nothing required
        }

        public async Task<IEnumerable<JsonPatchOperation>> Process(IContext context, WorkItem sourceWorkItem, WorkItem targetWorkItem, object state = null)
        {
            // Get the work item history
            var workItemHistory = await GetWorkItemHistory(sourceWorkItem, context);
            // Convert to the desired format
            if (context.Configuration.HistoryAttachmentFormat == ".json")
            {
                string filename = "history.json";
                byte[] data = ConvertToJson(workItemHistory);
                return await AddAttachment(targetWorkItem, filename, data, context.TargetClient.WorkItemTrackingHttpClient);
            }
            if (context.Configuration.HistoryAttachmentFormat == ".txt")
            {
                string filename = "history.txt";
                byte[] data = ConvertToText(workItemHistory);
                return await AddAttachment(targetWorkItem, filename, data, context.TargetClient.WorkItemTrackingHttpClient);
            }
            if (context.Configuration.HistoryAttachmentFormat == "comment")
            {
                string data = ConvertToHtml(workItemHistory);
                return await AddComment(targetWorkItem, data, context);
            }
            return new List<JsonPatchOperation>();
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
            int updateLimit = context.Configuration.HistoryLimit.HasValue ? context.Configuration.HistoryLimit.Value : Int32.MaxValue;
            List<WorkItemUpdate> workItemHistory = new List<WorkItemUpdate>();
            while (updateCount < updateLimit)
            {
                var itemsToRetrieve = Math.Min(Constants.PageSize, updateLimit - updateCount);
                var results = await WorkItemTrackingApi.GetUpdatesAsync(context.SourceClient.WorkItemTrackingHttpClient, workItem.Id.Value, itemsToRetrieve, updateCount);
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
        private byte[] ConvertToJson(List<WorkItemUpdate> workItemHistory)
        {
            if (workItemHistory.Count <= 0)
            {
                return null;
            }
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(workItemHistory));
        }

        /// <summary>
        /// Converts a work item history to a text document.
        /// </summary>
        /// <remarks>
        /// Excludes some fields from the document, and potentially some revisions. Attempts to replicate the history view in TFS.
        /// </remarks>
        /// <param name="workItemHistory">A list of WorkItemUpdate objects.</param>
        /// <returns>A text document.</returns>
        private byte[] ConvertToText(List<WorkItemUpdate> workItemHistory)
        {
            if (workItemHistory.Count <= 0)
            {
                return null;
            }
            StringBuilder documentBuilder = new StringBuilder();
            for (int i = workItemHistory.Count - 1; i >= 0; i--)
            {
                StringBuilder revisionBuilder = new StringBuilder();
                var update = workItemHistory[i];
                if (update.Fields != null)
                {
                    foreach (var field in update.Fields)
                    {
                        if (this.ShouldIgnore(field.Key))
                        {
                            // These fields are unwanted in the output
                            continue;
                        }
                        if (!Object.Equals(field.Value.OldValue, field.Value.NewValue))
                        {
                            revisionBuilder.AppendLine($"Changed {field.Key} from '{Convert.ToString(field.Value.OldValue)}' to '{Convert.ToString(field.Value.NewValue)}'");
                        }
                    }
                }
                if (update.Links != null)
                {
                    // Not sure yet
                }
                if (update.Relations != null)
                {
                    if (update.Relations.Added != null)
                    {
                        foreach (var relation in update.Relations.Added)
                        {
                            revisionBuilder.AppendLine($"Added a {relation.Rel} relation to {relation.Title}:{relation.Url}");
                        }
                    }
                    if (update.Relations.Removed != null)
                    {
                        foreach (var relation in update.Relations.Removed)
                        {
                            revisionBuilder.AppendLine($"Removed a {relation.Rel} relation to {relation.Title}:{relation.Url}");
                        }
                    }
                    if (update.Relations.Updated != null)
                    {
                        foreach (var relation in update.Relations.Updated)
                        {
                            revisionBuilder.AppendLine($"Updated a {relation.Rel} relation to {relation.Title}:{relation.Url}");
                        }
                    }
                }
                // If any information was added besides the header, add the revision to the document
                if (revisionBuilder.Length > 0)
                {
                    documentBuilder.AppendLine($"Revised By: {update.RevisedBy.Name}");
                    documentBuilder.AppendLine($"Revision Date: {update.RevisedDate}");
                    documentBuilder.AppendLine(revisionBuilder.ToString());
                    documentBuilder.AppendLine();
                }
            }
            return Encoding.UTF8.GetBytes(documentBuilder.ToString());
        }

        /// <summary>
        /// Converts the work item history to a HTML table.
        /// </summary>
        /// <remarks>
        /// Excludes some fields from the document, and potentially some revisions. Attempts to replicate the history view in TFS.
        /// </remarks>
        /// <param name="workItemHistory">The work item history.</param>
        /// <returns>A string containing the HTML.</returns>
        private string ConvertToHtml(List<WorkItemUpdate> workItemHistory)
        {
            if (workItemHistory.Count <= 0)
            {
                return null;
            }
            StringBuilder documentBuilder = new StringBuilder();
            documentBuilder.AppendLine("<p>Previous history:</p>");
            documentBuilder.AppendLine("<table>");
            for (int i = workItemHistory.Count - 1; i >= 0; i--)
            {
                StringBuilder revisionBuilder = new StringBuilder();
                var update = workItemHistory[i];
                if (update.Fields != null)
                {
                    foreach (var field in update.Fields)
                    {
                        if (this.ShouldIgnore(field.Key))
                        {
                            // These fields are unwanted in the output
                            continue;
                        }
                        if (!Object.Equals(field.Value.OldValue, field.Value.NewValue))
                        {
                            revisionBuilder.AppendLine($"Changed {field.Key} from '{Convert.ToString(field.Value.OldValue)}' to '{Convert.ToString(field.Value.NewValue)}'<br />");
                        }
                    }
                }
                if (update.Links != null)
                {
                    // Not sure yet
                }
                if (update.Relations != null)
                {
                    if (update.Relations.Added != null)
                    {
                        foreach (var relation in update.Relations.Added)
                        {
                            revisionBuilder.AppendLine($"Added a {relation.GetRelationName()} link to {relation.Url} ({relation.Title})<br />");
                        }
                    }
                    if (update.Relations.Removed != null)
                    {
                        foreach (var relation in update.Relations.Removed)
                        {
                            revisionBuilder.AppendLine($"Removed a {relation.GetRelationName()} link to {relation.Url} ({relation.Title})<br />");
                        }
                    }
                    if (update.Relations.Updated != null)
                    {
                        foreach (var relation in update.Relations.Updated)
                        {
                            revisionBuilder.AppendLine($"Updated a {relation.GetRelationName()} link to {relation.Url} ({relation.Title})<br />");
                        }
                    }
                }
                // If any information was added, add the revision to the document
                if (revisionBuilder.Length > 0)
                {
                    documentBuilder.AppendLine("<tr>");
                    documentBuilder.Append($"<td style='width: 360px;'>{update.RevisedBy.Name}");
                    if (update.RevisedDate.Year != 9999) // Weird dates on my system... corruption maybe?
                    {
                        documentBuilder.Append(" on ");
                        documentBuilder.Append(update.RevisedDate);
                    }
                    documentBuilder.Append("</td>");
                    documentBuilder.AppendLine($"<td>{revisionBuilder.ToString()}</td>");
                    documentBuilder.AppendLine("</tr>");
                }
            }
            documentBuilder.AppendLine($"</table>");
            return documentBuilder.ToString();
        }

        /// <summary>
        /// Checks if the given field should be ignored.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>True or false.</returns>
        private bool ShouldIgnore(string fieldName)
        {
            foreach (var identifier in this._ignoredFields)
            {
                if (fieldName.StartsWith(identifier))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds a history attachment to the work item using JsonPatchOperation.
        /// </summary>
        /// <param name="workItem">The work item.</param>
        /// <param name="filename">The attachment filename.</param>
        /// <param name="data">The attachment data.</param>
        /// <param name="client">The client used to create the attachment on the instance.</param>
        /// <returns>The necessary patch operations.</returns>
        private async Task<IList<JsonPatchOperation>> AddAttachment(WorkItem workItem, string filename, byte[] data, WorkItemTrackingHttpClient client)
        {
            IList<JsonPatchOperation> operations = new List<JsonPatchOperation>();
            // Get the existing relation
            int index = -1;
            var workItemRelation = workItem.FindAttachment(filename, out index);
            // History attachment does not exist on the target
            if (workItemRelation == null)
            {
                using (MemoryStream stream = new MemoryStream(data))
                {
                    // Upload the attachment
                    var attachmentReference = await WorkItemTrackingApi.CreateAttachmentAsync(client, stream);
                    // Create the attachment link
                    var attachmentLink = new AttachmentLink(filename, attachmentReference, data.LongLength);
                    // Add the patch operation
                    operations.Add(MigrationHelpers.GetAttachmentAddOperation(attachmentLink));
                }
            }
            // The history has changed
            else if (Convert.ToInt64(workItemRelation.Attributes.GetValue(Constants.RelationAttributeResourceSize, 0)) != data.LongLength)
            {
                using (MemoryStream stream = new MemoryStream(data))
                {
                    // Upload the attachment
                    var attachmentReference = await WorkItemTrackingApi.CreateAttachmentAsync(client, stream);
                    // Create the attachment link
                    var attachmentLink = new AttachmentLink(filename, attachmentReference, data.LongLength);
                    // Add the patch operation
                    operations.Add(MigrationHelpers.GetAttachmentOperation(Operation.Replace, attachmentLink, index.ToString()));
                }
            }
            return operations;
        }

        /// <summary>
        /// Adds a history attachment to the work item using JsonPatchOperation.
        /// </summary>
        /// <param name="workItem">The work item.</param>
        /// <param name="text">The comment text.</param>
        /// <param name="client">The client used to create the attachment on the instance.</param>
        /// <returns>The necessary patch operations.</returns>
        private async Task<IList<JsonPatchOperation>> AddComment(WorkItem workItem, string text, IContext context)
        {
            var workItemComments = await WorkItemTrackingApi.GetCommentsAsync(context.TargetClient.WorkItemTrackingHttpClient, context.Configuration.TargetConnection.Project, workItem.Id.Value);
            var comment = workItemComments.Comments.FirstOrDefault(item => item.Text.StartsWith("<p>Previous history:</p>"));
            if (comment != null)
            {
                await WorkItemTrackingApi.UpdateCommentAsync(
                    context.TargetClient.WorkItemTrackingHttpClient,
                    text,
                    context.Configuration.TargetConnection.Project,
                    workItem.Id.Value,
                    comment.Id);
            }
            else
            {
                await WorkItemTrackingApi.AddCommentAsync(
                    context.TargetClient.WorkItemTrackingHttpClient,
                    text,
                    context.Configuration.TargetConnection.Project,
                    workItem.Id.Value);
            }
            return new List<JsonPatchOperation>();
        }
    }
}
