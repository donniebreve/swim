using Common.Configuration;
using Common.Migration;
using System;
using System.Collections.Concurrent;

namespace Common
{
    public abstract class BaseContext : IContext
    {
        public IConfiguration Configuration { get; }

        public WorkItemClientConnection SourceClient { get; }

        public WorkItemClientConnection TargetClient { get; }

        /// <summary>
        /// A collection of all the work items to be migrated.
        /// </summary>
        public ConcurrentDictionary<int, WorkItemMigrationState> WorkItemMigrationStates { get; set; } = new ConcurrentDictionary<int, WorkItemMigrationState>();



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
    }
}
