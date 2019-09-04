using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Common.Migration
{
    public interface IPhase2Processor : IProcessor
    {
        /// <summary>
        /// Performs work necessary prior to processing the work item batch.
        /// </summary>
        /// <param name="migrationContext">The migration context.</param>
        /// <param name="batchContext">The batch context.</param>
        /// <param name="sourceWorkItems">The list of source work items.</param>
        /// <param name="targetWorkItems">The list of target work items.</param>
        /// <returns>An awaitable Task.</returns>
        Task Preprocess(IMigrationContext migrationContext, IBatchMigrationContext batchContext, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems);

        /// <summary>
        /// Process the work item batch.
        /// </summary>
        /// <param name="migrationContext">The migration context.</param>
        /// <param name="batchContext">The batch context.</param>
        /// <param name="sourceWorkItem">The source work item.</param>
        /// <param name="targetWorkItem">The target work item.</param>
        /// <returns>A enumerable of JsonPatchOperations.</returns>
        Task<IEnumerable<JsonPatchOperation>> Process(IMigrationContext migrationContext, IBatchMigrationContext batchContext, WorkItem sourceWorkItem, WorkItem targetWorkItem);
    }
}
