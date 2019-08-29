using System.Collections.Concurrent;
using Common.Configuration;
using Common.Migration;

namespace Common
{
    public interface IContext
    {
        IConfiguration Configuration { get; }

        WorkItemClientConnection SourceClient { get; }

        WorkItemClientConnection TargetClient { get; }

        /// <summary>
        /// The state and information for all work items to migrate.
        /// </summary>
        ConcurrentDictionary<int, WorkItemMigrationState> WorkItemMigrationStates { get; set; }

        //remote relation types, do not need to exist on target since they're 
        //recreated as hyperlinks
        ConcurrentSet<string> RemoteLinkRelationTypes { get; set; }
    }
}
