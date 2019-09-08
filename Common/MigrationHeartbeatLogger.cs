using Common.Migration;
using Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Common
{
    public class MigrationHeartbeatLogger : IDisposable
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<MigrationHeartbeatLogger>();

        private Timer timer;
        private MigrationContext _context;

        public MigrationHeartbeatLogger(MigrationContext context, int frequencyInSeconds)
        {
            this._context = context;
            this.timer = new Timer(Beat, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(frequencyInSeconds));
        }

        public void Beat()
        {
            Beat(null);
        }

        private void Beat(object state)
        {
            string line1 = "MIGRATION STATUS:";
            string line2 = $"work items that succeeded phase 1 migration: {GetSucceededPhase1WorkItemsCount()}";
            string line3 = $"work items that failed phase 1 migration:    {GetFailedPhase1WorkItemsCount()}";
            string line4 = $"work items to be processed in phase 1:       {GetPhase1Total()}";
            string line5 = $"work items that succeeded phase 2 migration: {GetSucceededPhase2WorkItemsCount()}";
            string line6 = $"work items that failed phase 2 migration:    {GetFailedPhase2WorkItemsCount()}";
            string line7 = $"work items to be processed in phase 2:       {GetPhase2Total()}";

            Logger.LogInformation(LogDestination.Console, $"{line1}{Environment.NewLine}{line2}{Environment.NewLine}{line3}{Environment.NewLine}{line4}{Environment.NewLine}{line5}{Environment.NewLine}{line6}{Environment.NewLine}{line7}");
        }

        public void Dispose()
        {
            this.timer.Dispose();
        }

        private int GetSucceededPhase1WorkItemsCount()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationCompleted.HasFlag(WorkItemMigrationState.MigrationCompletionStatus.Phase1) && w.FailureReason == Migration.FailureReason.None).Count();
        }

        private int GetFailedPhase1WorkItemsCount()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationCompleted.HasFlag(WorkItemMigrationState.MigrationCompletionStatus.Phase1) && w.FailureReason != Migration.FailureReason.None).Count();
        }

        private int GetSucceededPhase2WorkItemsCount()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationCompleted.HasFlag(WorkItemMigrationState.MigrationCompletionStatus.Phase2) && w.FailureReason == Migration.FailureReason.None).Count();
        }

        private int GetFailedPhase2WorkItemsCount()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationCompleted.HasFlag(WorkItemMigrationState.MigrationCompletionStatus.Phase2) && w.FailureReason != Migration.FailureReason.None).Count();
        }

        private int GetPhase1Total()
        {
            int workItemsToCreateCount = this._context.WorkItemMigrationStates.Where(a => a.MigrationAction == MigrationAction.Create).Count();
            int workItemsToUpdate = this._context.WorkItemMigrationStates.Where(w => w.MigrationAction == MigrationAction.Update && w.Requirement.HasFlag(WorkItemMigrationState.RequirementForExisting.UpdatePhase1)).Count();
            return workItemsToCreateCount + workItemsToUpdate;
        }

        private int GetPhase2Total()
        {
            return this._context.WorkItemMigrationStates.Where(a => a.MigrationAction == MigrationAction.Create || (a.MigrationAction == MigrationAction.Update && a.Requirement.HasFlag(WorkItemMigrationState.RequirementForExisting.UpdatePhase2))).Count();
        }
    }
}
