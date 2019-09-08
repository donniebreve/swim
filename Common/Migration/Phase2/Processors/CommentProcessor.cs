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
            // To do: this is not working. WitBatchRequest with an add on /comments/- does not seem to be functional.
            IList<JsonPatchOperation> jsonPatchOperations = new List<JsonPatchOperation>();
            //var result = await WorkItemTrackingApi.GetCommentsAsync(context.SourceClient.WorkItemTrackingHttpClient, sourceWorkItem.Id.Value);
            //foreach (var comment in result.Comments)
            //{
            //    jsonPatchOperations.Add(MigrationHelpers.GetCommentAddOperation(comment));
            //}
            return jsonPatchOperations;
        }
    }
}
