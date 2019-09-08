using Common.Migration;
using Common.Validation;
using Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Common
{
    public class ValidationHeartbeatLogger : IDisposable
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<ValidationHeartbeatLogger>();

        private IValidationContext _context;
        private Timer _timer;

        public ValidationHeartbeatLogger(IValidationContext context, int frequencyInSeconds)
        {
            this._context = context;
            this._timer = new Timer(this.Beat, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(frequencyInSeconds));
        }

        public void Beat()
        {
            Beat(null);
        }

        private void Beat(object state)
        {
            string line1 = "VALIDATION STATUS UPDATE:";
            string line2 = $"New work items found:                      {GetNewWorkItemsFound()}";
            string line3 = $"Existing work items found:                 {GetExistingWorkItemsFound()}";
            string line4 = $"Existing work items validated for phase 1: {GetExistingWorkItemsValidatedForPhase1()}";
            string line5 = $"Existing work items validated for phase 2: {GetExistingWorkItemsValidatedForPhase2()}";
            string line6 = $"Waiting for query to retrieve work items to be validated...";

            int workItemCount = GetCurrentWorkItemCount();
            if (workItemCount > 0)
            {
                line6 =    $"Total work items retrieved from query:     {workItemCount}";
            }

            string output = $"{line1}{Environment.NewLine}{line2}{Environment.NewLine}{line3}{Environment.NewLine}{line4}{Environment.NewLine}{line5}{Environment.NewLine}{line6}";
            Logger.LogInformation(LogDestination.File, output);
        }

        public void Dispose()
        {
            this._timer.Dispose();
        }

        private int GetNewWorkItemsFound()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationAction == MigrationAction.Create).Count();
        }

        private int GetExistingWorkItemsFound()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationAction == MigrationAction.Update).Count();
        }

        private int GetExistingWorkItemsValidatedForPhase1()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationAction == MigrationAction.Update && w.Requirement.HasFlag(WorkItemMigrationState.RequirementForExisting.UpdatePhase1)).Count();
        }

        private int GetExistingWorkItemsValidatedForPhase2()
        {
            return this._context.WorkItemMigrationStates.Where(w => w.MigrationAction == MigrationAction.Update && w.Requirement.HasFlag(WorkItemMigrationState.RequirementForExisting.UpdatePhase2)).Count();
        }

        private int GetCurrentWorkItemCount()
        {
            if (this._context.WorkItemMigrationStates != null)
            {
                return this._context.WorkItemMigrationStates.Count();
            }
            return 0;
        }
    }
}
