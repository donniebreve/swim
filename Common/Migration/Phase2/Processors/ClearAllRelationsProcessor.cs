﻿using Common.Configuration;
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
    [RunOrder(1)]
    public class ClearAllRelationsProcessor : IPhase2Processor
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<ClearAllRelationsProcessor>();

        /// <summary>
        /// The name to use for logging.
        /// </summary>
        /// <remarks>
        /// This is also the name used for the description on the hyperlink to the source work item.
        /// </remarks>
        public string Name => Constants.RelationPhaseClearAllRelations;

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
        public async Task<IEnumerable<JsonPatchOperation>> Process(IMigrationContext migrationContext, IBatchMigrationContext batchContext, WorkItem sourceWorkItem, WorkItem targetWorkItem)
        {
            List<JsonPatchOperation> patchOperations = new List<JsonPatchOperation>();
            if (targetWorkItem.Relations != null)
            {
                for (int i = 0; i < targetWorkItem.Relations.Count; i++)
                {
                    patchOperations.Add(MigrationHelpers.GetRelationRemoveOperation(i));
                }
            }
            return patchOperations;
        }
    }
}
