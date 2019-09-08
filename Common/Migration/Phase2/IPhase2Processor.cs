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
        /// <param name="context">The current context.</param>
        /// <param name="sourceWorkItems">The list of source work items.</param>
        /// <param name="targetWorkItems">The list of target work items.</param>
        /// <returns>An awaitable Task.</returns>
        Task Preprocess(IContext context, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems);

        /// <summary>
        /// Process the work item batch.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="sourceWorkItem">The source work item.</param>
        /// <param name="targetWorkItem">The target work item.</param>
        /// <param name="state">An object that may be used for processing.</param>
        /// <returns>A enumerable of JsonPatchOperations.</returns>
        Task<IEnumerable<JsonPatchOperation>> Process(IContext context, WorkItem sourceWorkItem, WorkItem targetWorkItem, object state = null);
    }
}
