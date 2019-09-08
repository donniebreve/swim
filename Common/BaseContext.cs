using Common.Configuration;
using Common.Migration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Common
{
    public abstract class BaseContext : IContext
    {
        public IConfiguration Configuration { get; }

        public WorkItemClientConnection SourceClient { get; }

        public WorkItemClientConnection TargetClient { get; }

        public ConcurrentDictionary<int, int> SourceToTargetIds { get; set; } = new ConcurrentDictionary<int, int>();

        public ConcurrentSet<string> RemoteLinkRelationTypes { get; set; } = new ConcurrentSet<string>(StringComparer.CurrentCultureIgnoreCase);

        public BaseContext(IConfiguration configuration)
        {
            this.Configuration = configuration;
            this.SourceClient = ClientHelpers.CreateClient(configuration.SourceConnection);
            this.TargetClient = ClientHelpers.CreateClient(configuration.TargetConnection);
        }

        /// <summary>
        /// Constructor for test purposes
        /// </summary>
        public BaseContext() { }

        /// <summary>
        /// A dictionary of all the work items to be migrated.
        /// </summary>
        public ConcurrentDictionary<int, WorkItemMigrationState> WorkItemMigrationStateDictionary { get; set; } = new ConcurrentDictionary<int, WorkItemMigrationState>();

        /// <summary>
        /// A collection of the work items to be migrated.
        /// </summary>
        public ICollection<WorkItemMigrationState> WorkItemMigrationStates
        {
            get
            {
                return this.WorkItemMigrationStateDictionary.Values;
            }
        }

        /// <summary>
        /// A collection of the source work item IDs.
        /// </summary>
        public ICollection<int> SourceWorkItemIDs
        {
            get
            {
                return this.WorkItemMigrationStateDictionary.Keys;
            }
        }

        /// <summary>
        /// Returns the WorkItemMigrationState for the given source work item ID.
        /// </summary>
        /// <param name="sourceID">The source work item ID.</param>
        /// <returns>A WorkItemMigrationState, or null.</returns>
        public WorkItemMigrationState GetWorkItemMigrationState(int sourceID)
        {
            return this.WorkItemMigrationStateDictionary[sourceID];
        }

        /// <summary>
        /// Returns the WorkItemMigrationState for the given target work item ID.
        /// </summary>
        /// <param name="targetID">The target work item ID.</param>
        /// <returns>A WorkItemMigrationState, or null.</returns>
        public WorkItemMigrationState GetWorkItemMigrationStateByTargetID(int targetID)
        {
            foreach (var workItemMigrationState in this.WorkItemMigrationStates)
            {
                if (workItemMigrationState.TargetId.Value == targetID)
                {
                    return workItemMigrationState;
                }
            }
            return null;
        }
    }
}
