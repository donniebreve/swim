using System.Collections.Concurrent;
using System.Collections.Generic;
using Common.Configuration;
using Common.Migration;

namespace Common
{
    public interface IContext
    {
        IConfiguration Configuration { get; }

        /// <summary>
        /// The connection to the source TFS API.
        /// </summary>
        WorkItemClientConnection SourceClient { get; }

        /// <summary>
        /// The connection to the target TFS API.
        /// </summary>
        WorkItemClientConnection TargetClient { get; }

        /// <summary>
        /// The state and information for all work items to migrate.
        /// </summary>
        ConcurrentDictionary<int, WorkItemMigrationState> WorkItemMigrationStateDictionary { get; set; }

        /// <summary>
        /// A collection of the work items to be migrated.
        /// </summary>
        ICollection<WorkItemMigrationState> WorkItemMigrationStates { get; }

        /// <summary>
        /// A collection of the source work item IDs.
        /// </summary>
        ICollection<int> SourceWorkItemIDs { get; }

        /// <summary>
        /// Returns the WorkItemMigrationState for the given source work item ID.
        /// </summary>
        /// <param name="sourceID">The source work item ID.</param>
        /// <returns>A WorkItemMigrationState, or null.</returns>
        WorkItemMigrationState GetWorkItemMigrationState(int sourceID);

        //remote relation types, do not need to exist on target since they're 
        //recreated as hyperlinks
        ConcurrentSet<string> RemoteLinkRelationTypes { get; set; }
    }
}
