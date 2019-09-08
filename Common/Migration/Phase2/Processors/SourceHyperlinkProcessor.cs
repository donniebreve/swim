using Common.Configuration;
using Common.Extensions;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Common.Migration
{
    /// <summary>
    /// Migrates comments from the source work item to the target work item.
    /// </summary>
    public class SourceHyperlinkProcessor : IPhase2Processor
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<SourceHyperlinkProcessor>();

        /// <summary>
        /// The name to use for logging.
        /// </summary>
        public string Name => "Source Hyperlink Processor";

        /// <summary>
        /// Returns true if this processor should be invoked.
        /// </summary>
        /// <param name="configuration">The current configuration.</param>
        /// <returns>True or false.</returns>
        public bool IsEnabled(IConfiguration configuration)
        {
            return true;
        }

        /// <summary>
        /// Performs work necessary prior to processing the work item batch.
        /// </summary>
        /// <param name="migrationContext">The migration context.</param>
        /// <param name="batchContext">The batch context.</param>
        /// <param name="sourceWorkItems">The list of source work items.</param>
        /// <param name="targetWorkItems">The list of target work items.</param>
        /// <returns>An awaitable Task.</returns>
        public async Task Preprocess(IContext context, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems)
        {
            // Nothing required
        }

        /// <summary>
        /// Process the work item batch.
        /// </summary>
        /// <param name="migrationContext">The migration context.</param>
        /// <param name="sourceWorkItem">The source work item.</param>
        /// <param name="targetWorkItem">The target work item.</param>
        /// <returns>A enumerable of JsonPatchOperations.</returns>
        public async Task<IEnumerable<JsonPatchOperation>> Process(IContext context, WorkItem sourceWorkItem, WorkItem targetWorkItem, object state = null)
        {
            IList<JsonPatchOperation> patchOperations = new List<JsonPatchOperation>();
            var sourceRev = sourceWorkItem.Rev.Value;
            var sourceUrl = sourceWorkItem.Url;
            // Attempt to find the hyperlink on the target
            var workItemRelation = targetWorkItem.FindHyperlink(sourceUrl);
            if (workItemRelation == null)
            {
                patchOperations.Add(
                    MigrationHelpers.GetHyperlinkOperation(
                        Operation.Add,
                        sourceUrl,
                        JsonConvert.SerializeObject(new SourceHyperlinkComment(sourceRev))));
            }
            else
            {
                SourceHyperlinkComment sourceHyperlinkComment = JsonConvert.DeserializeObject<SourceHyperlinkComment>((string)workItemRelation.Attributes.GetValue(Constants.RelationAttributeComment));
                if (sourceHyperlinkComment == null || sourceHyperlinkComment.SourceRev != sourceRev)
                {
                    patchOperations.Add(
                        MigrationHelpers.GetHyperlinkOperation(
                            Operation.Replace,
                            sourceUrl,
                            JsonConvert.SerializeObject(new SourceHyperlinkComment(sourceRev))));
                }
            }
            return patchOperations;
        }
    }
}
