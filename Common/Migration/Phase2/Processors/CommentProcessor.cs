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
            IList<JsonPatchOperation> jsonPatchOperations = new List<JsonPatchOperation>();
            var result = await WorkItemApi.GetCommentsAsync(context.SourceClient.WorkItemTrackingHttpClient, sourceWorkItem.Id.Value);
            foreach (var comment in result.Comments)
            {
                jsonPatchOperations.Add(MigrationHelpers.GetCommentAddOperation(comment));
            }
            return jsonPatchOperations;
        }
    }
}
