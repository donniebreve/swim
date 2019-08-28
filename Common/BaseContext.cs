using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Common.Configuration;

namespace Common
{
    public abstract class BaseContext : IContext
    {
        public IConfiguration Configuration { get; }

        public WorkItemClientConnection SourceClient { get; }

        public WorkItemClientConnection TargetClient { get; }

        public ConcurrentDictionary<int, string> WorkItemIdsUris { get; set; }

        public ConcurrentBag<WorkItemMigrationState> WorkItemsMigrationState { get; set; } = new ConcurrentBag<WorkItemMigrationState>();

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
