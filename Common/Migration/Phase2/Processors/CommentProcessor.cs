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
using System.Linq;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;

namespace Common.Migration
{
    /// <summary>
    /// Migrates comments from the source work item to the target work item.
    /// </summary>
    public class CommentProcessor : IPhase2Processor
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<CommentProcessor>();

        /// <summary>
        /// The name to use for logging.
        /// </summary>
        public string Name => "Comment Processor";

        /// <summary>
        /// Returns true if this processor should be invoked.
        /// </summary>
        /// <param name="configuration">The current configuration.</param>
        /// <returns>True or false.</returns>
        public bool IsEnabled(IConfiguration configuration)
        {
            return configuration.MigrateComments;
        }

        public async Task Preprocess(IContext context, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems)
        {
            // Nothing required
        }

        public async Task<IEnumerable<JsonPatchOperation>> Process(IContext context, WorkItem sourceWorkItem, WorkItem targetWorkItem, object state = null)
        {
            // To do: this should detect the capabilities of the source and use the appropriate API
            // Get the work item comments
            var commentList = await WorkItemTrackingApi.GetCommentsAsync(context.SourceClient.WorkItemTrackingHttpClient, sourceWorkItem.Id.Value);
            // Convert to the desired format
            if (context.Configuration.HistoryAttachmentFormat == ".json")
            {
                string filename = "comments.json";
                byte[] data = ConvertToJson(commentList.Comments.ToList());
                return await AddAttachment(targetWorkItem, filename, data, context.TargetClient.WorkItemTrackingHttpClient);
            }
            if (context.Configuration.HistoryAttachmentFormat == ".txt")
            {
                string filename = "comments.txt";
                byte[] data = ConvertToText(commentList.Comments.ToList());
                return await AddAttachment(targetWorkItem, filename, data, context.TargetClient.WorkItemTrackingHttpClient);
            }
            if (context.Configuration.HistoryAttachmentFormat == "comment")
            {
                string data = ConvertToHtml(commentList.Comments.ToList());
                if (data != null)
                {
                    return await AddComment(targetWorkItem, data, context);
                }
            }
            return new List<JsonPatchOperation>();
        }

        /// <summary>
        /// Converts a list of WorkItemComment objects to a JSON document.
        /// </summary>
        /// <param name="comments">A list of WorkItemComment objects.</param>
        /// <returns>A JSON document.</returns>
        private byte[] ConvertToJson(List<WorkItemComment> comments)
        {
            if (comments.Count <= 0)
            {
                return null;
            }
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(comments));
        }

        /// <summary>
        /// Converts the list of comments to a text document.
        /// </summary>
        /// <param name="comments">The list of comments.</param>
        /// <returns>A text document.</returns>
        private byte[] ConvertToText(List<WorkItemComment> comments)
        {
            if (comments.Count <= 0)
            {
                return null;
            }
            StringBuilder documentBuilder = new StringBuilder();
            for (int i = comments.Count - 1; i >= 0; i--)
            {
                StringBuilder revisionBuilder = new StringBuilder();
                var comment = comments[i];
                documentBuilder.Append($"{comment.RevisedBy.Name} commented");
                if (comment.RevisedDate.Year != 9999) // Weird dates on my system... corruption maybe?
                {
                    documentBuilder.Append(" on ");
                    documentBuilder.Append(comment.RevisedDate);
                }
                documentBuilder.AppendLine();
                documentBuilder.AppendLine(comment.Text);
                documentBuilder.AppendLine();
            }
            return Encoding.UTF8.GetBytes(documentBuilder.ToString());
        }

        /// <summary>
        /// Converts the list of comments to a HTML table.
        /// </summary>
        /// <param name="comments">The list of comments.</param>
        /// <returns>A string containing the HTML.</returns>
        private string ConvertToHtml(List<WorkItemComment> comments)
        {
            if (comments.Count <= 0)
            {
                return null;
            }
            StringBuilder documentBuilder = new StringBuilder();
            documentBuilder.AppendLine("<p>Previous comments:</p>");
            documentBuilder.AppendLine("<table>");
            for (int i = comments.Count - 1; i >= 0; i--)
            {
                StringBuilder revisionBuilder = new StringBuilder();
                var comment = comments[i];
                documentBuilder.AppendLine("<tr>");
                documentBuilder.Append($"<td style='width: 360px;'>{comment.RevisedBy.Name}");
                if (comment.RevisedDate.Year != 9999) // Weird dates on my system... corruption maybe?
                {
                    documentBuilder.Append(" on ");
                    documentBuilder.Append(comment.RevisedDate);
                }
                documentBuilder.Append("</td>");
                documentBuilder.Append($"<td>{comment.Text}</td>");
                documentBuilder.AppendLine("</tr>");
            }
            documentBuilder.AppendLine($"</table>");
            return documentBuilder.ToString();
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
            var comment = workItemComments.Comments.FirstOrDefault(item => item.Text.StartsWith("<p>Previous comments:</p>"));
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
