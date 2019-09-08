using Common.Configuration;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Migration.Phase2.Processors
{
    /// <summary>
    /// Clears all the relations from the work item.
    /// </summary>
    [RunOrder(99)]
    public class ClearRelationsProcessor : IPhase2Processor
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<ClearRelationsProcessor>();

        /// <summary>
        /// The name to use for logging.
        /// </summary>
        /// <remarks>
        /// This is also the name used for the description on the hyperlink to the source work item.
        /// </remarks>
        public string Name => "Clear Relations Processor";

        /// <summary>
        /// Returns true if this processor should be invoked.
        /// </summary>
        /// <param name="configuration">The current configuration.</param>
        /// <returns>True or false.</returns>
        public bool IsEnabled(IConfiguration configuration)
        {
            return false;
        }

        public async Task Preprocess(IContext context, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems)
        {
            // Nothing required
        }

        public async Task<IEnumerable<JsonPatchOperation>> Process(IContext context, WorkItem sourceWorkItem, WorkItem targetWorkItem, object state = null)
        {
            List<JsonPatchOperation> patchOperations = new List<JsonPatchOperation>();
            for (int i = 0; i < targetWorkItem.Relations.Count; i++)
            {
                bool exists = false;
                for (int ii = 0; ii < sourceWorkItem.Relations.Count; ii++)
                {
                    var targetRelation = targetWorkItem.Relations[i];
                    var sourceRelation = sourceWorkItem.Relations[i];
                    if (targetRelation.Rel == sourceRelation.Rel
                        && targetRelation.Title == sourceRelation.Title)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    patchOperations.Add(MigrationHelpers.GetRelationRemoveOperation(i));
                }
            }
            return patchOperations;
        }
    }
}
