using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Logging;
using Common.Migration;
using System.Collections.Concurrent;
using Common.Api;

namespace Common.Validation
{
    public class Validator
    {
        private ILogger Logger { get; } = MigratorLogging.CreateLogger<Validator>();

        private ValidationContext _context;

        public Validator(ValidationContext context)
        {
            this._context = context;
        }

        public async Task Validate()
        {
            Logger.LogInformation("Starting validation");

            await ValidateConfiguration();

            // Gather work items
            this._context.WorkItemMigrationStateDictionary = await WorkItemTrackingHelper.GetInitialWorkItemList(
                this._context.SourceClient.WorkItemTrackingHttpClient,
                this._context.Configuration.SourceConnection.Project,
                this._context.Configuration.Query,
                this._context.Configuration.SourcePostMoveTag,
                this._context.Configuration.QueryPageSize);

            // Gather work item revisions
            //await WorkItemTrackingHelper.GetWorkItemRevisions(this._context);

            await ValidateWorkItemMetadata();

            // Find already existing work items
            await WorkItemTrackingHelper.IdentifyMigratedWorkItems(this._context);

            await ValidateTargetWorkItems();

            // Output results
            Logger.LogInformation($"{this._context.ValidatedFields.Count} fields validated, {this._context.SkippedFields.Count} skipped for migration");
            Logger.LogInformation($"{this._context.ValidatedTypes.Count} work item types validated, {this._context.SkippedTypes.Count} skipped for migration");
            Logger.LogInformation($"{this._context.ValidatedAreaPaths.Count} area paths validated, {this._context.SkippedAreaPaths.Count} skipped for migration");
            Logger.LogInformation($"{this._context.ValidatedIterationPaths.Count} iteration paths validated, {this._context.SkippedIterationPaths.Count} skipped for migration");
            Logger.LogInformation($"{this._context.WorkItemMigrationStates.Count} work item(s) returned from the query for migration");

            // Log work items with errors
            var workItemsWithErrors = this._context.WorkItemMigrationStates.Where(item => item.FailureReason != FailureReason.None);
            if (workItemsWithErrors.Count() > 0)
            {
                Logger.LogInformation($"{workItemsWithErrors.Count()} work item(s) have error entries and will not be migrated");
                Logger.LogInformation(LogDestination.File, "Source Id");
                foreach (var item in workItemsWithErrors)
                {
                    Logger.LogInformation(LogDestination.File, $"{item.SourceId}");
                }
            }

            // Log skipped? work items
            var skippedWorkItems = this._context.SkippedWorkItems;
            if (skippedWorkItems.Count() > 0)
            {
                Logger.LogInformation($"{skippedWorkItems.Count()} work item(s) have been skipped due to an invalid area/iteration path or type and will not be migrated");
                Logger.LogInformation(LogDestination.File, "Source Id");
                foreach (var item in skippedWorkItems)
                {
                    Logger.LogInformation(LogDestination.File, $"{item}");
                }
            }

            // Log new work items
            var workItemsToCreate = this._context.WorkItemMigrationStates.Where(item => item.MigrationAction == MigrationAction.Create);
            Logger.LogInformation($"{workItemsToCreate.Count()} work item(s) are new and will be created in the target");

            // Log work items to be updated
            var workItemsToUpdate = this._context.WorkItemMigrationStates.Where(item => item.MigrationAction == MigrationAction.Update && item.Requirement.HasFlag(WorkItemMigrationState.RequirementForExisting.UpdatePhase1));
            if (this._context.Configuration.UpdateModifiedWorkItems && workItemsToUpdate.Any())
            {
                Logger.LogInformation($"{workItemsToUpdate.Count()} work item(s) will be updated in the target.");
                if (this._context.Configuration.MigrateLinks)
                {
                    Logger.LogInformation("Move-Links is set to true, additional work items may be included for link processing if they had any link changes");
                }
                Logger.LogInformation(LogDestination.File, "Source Id :: Target Id");
                foreach (var item in workItemsToUpdate)
                {
                    Logger.LogInformation(LogDestination.File, $"{item.SourceId} :: {item.TargetId}");
                }
            }

            Logger.LogSuccess(LogDestination.All, "Validation complete");
        }

        /// <summary>
        /// Runs all the IConfigurationValidator instances.
        /// </summary>
        /// <returns>An awaitable Task.</returns>
        private async Task ValidateConfiguration()
        {
            var stopwatch = new Stopwatch();
            Logger.LogInformation("Starting configuration validation");
            foreach (IConfigurationValidator validator in ClientHelpers.GetInstances<IConfigurationValidator>())
            {
                Logger.LogInformation(LogDestination.File, $"Starting configuration validation for: {validator.Name}");
                stopwatch.Start();
                await validator.Validate(this._context);
                stopwatch.Reset();
                Logger.LogInformation(LogDestination.File, $"Completed configuration validation for: {validator.Name} in {stopwatch.Elapsed.TotalSeconds}s");
            }
            Logger.LogInformation("Completed configuration validation");
        }

        /// <summary>
        /// Runs all the IWorkItemValidator instances.
        /// </summary>
        /// <returns>An awaitable Task.</returns>
        private async Task ValidateWorkItemMetadata()
        {
            Logger.LogInformation("Starting work item metadata validation");
            var validators = ClientHelpers.GetInstances<IWorkItemValidator>();
            foreach (var validator in validators)
            {
                await validator.Prepare(this._context);
            }
            var totalNumberOfBatches = ClientHelpers.GetBatchCount(this._context.WorkItemMigrationStates.Count, Constants.BatchSize);
            await this._context.SourceWorkItemIDs.Batch(Constants.BatchSize).ForEachAsync(
                this._context.Configuration.Parallelism,
                async (workItemIds, batchId) =>
                {
                    Logger.LogInformation(LogDestination.File, $"Work item metadata validation batch {batchId} of {totalNumberOfBatches}: Starting");
                    var stopwatch = Stopwatch.StartNew();
                    var workItems = await WorkItemApi.GetWorkItemsAsync(
                        this._context.SourceClient.WorkItemTrackingHttpClient,
                        workItemIds,
                        this._context.RequestedFields);
                    foreach (var validator in validators)
                    {
                        Logger.LogInformation(LogDestination.File, $"Work item metadata validation batch {batchId} of {totalNumberOfBatches}: {validator.Name}");
                        foreach (var workItem in workItems)
                        {
                            await validator.Validate(this._context, workItem);
                        }
                    }
                    stopwatch.Stop();
                    Logger.LogInformation(LogDestination.File, $"Work item metadata validation batch {batchId} of {totalNumberOfBatches}: Completed in {stopwatch.Elapsed.TotalSeconds}s");
                });
            Logger.LogInformation("Completed work item metadata validation");
        }

        /// <summary>
        /// Runs all the ITargetValidator instances.
        /// </summary>
        /// <returns>An awaitable Task.</returns>
        private async Task ValidateTargetWorkItems()
        {
            var stopwatch = new Stopwatch();
            Logger.LogInformation("Starting target work item migration status");
            foreach (ITargetValidator validator in ClientHelpers.GetInstances<ITargetValidator>())
            {
                Logger.LogInformation(LogDestination.File, $"Starting target work item migration status for: {validator.Name}");
                stopwatch.Start();
                await validator.Validate(this._context);
                stopwatch.Reset();
                Logger.LogInformation(LogDestination.File, $"Completed target work item migration status for: {validator.Name} in {stopwatch.Elapsed.TotalSeconds}s");
            }
            Logger.LogInformation("Completed target work item migration status");
        }
    }
}
