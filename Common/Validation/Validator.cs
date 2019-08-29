using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Logging;
using Common.Migration;
using System.Collections.Concurrent;

namespace Common.Validation
{
    public class Validator
    {
        private ILogger Logger { get; } = MigratorLogging.CreateLogger<Validator>();

        private ValidationContext context;

        public Validator(ValidationContext context)
        {
            this.context = context;
        }

        public async Task Validate()
        {
            Logger.LogInformation("Starting validation");

            await ValidateConfiguration();

            // Gather work items
            context.WorkItemMigrationStates = await WorkItemTrackingHelper.GetWorkItemIdsUrisAsync(
                context.SourceClient.WorkItemTrackingHttpClient,
                context.Configuration.SourceConnection.Project,
                context.Configuration.Query,
                context.Configuration.SourcePostMoveTag,
                context.Configuration.QueryPageSize - 1); // Have to subtract -1 from the page size due to a bug in how query interprets page size

            await ValidateWorkItemMetadata();
            await ValidateTargetWorkItems();

            // Output results
            Logger.LogInformation($"{this.context.ValidatedFields.Count} fields validated, {this.context.SkippedFields.Count} skipped for migration");
            Logger.LogInformation($"{this.context.ValidatedTypes.Count} work item types validated, {this.context.SkippedTypes.Count} skipped for migration");
            Logger.LogInformation($"{this.context.ValidatedAreaPaths.Count} area paths validated, {this.context.SkippedAreaPaths.Count} skipped for migration");
            Logger.LogInformation($"{this.context.ValidatedIterationPaths.Count} iteration paths validated, {this.context.SkippedIterationPaths.Count} skipped for migration");
            Logger.LogInformation($"{this.context.WorkItemMigrationStates.Count} work item(s) returned from the query for migration");

            // Log work items with errors
            var workItemsWithErrors = this.context.WorkItemMigrationStates.Values.Where(item => item.FailureReason != FailureReason.None);
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
            var skippedWorkItems = this.context.SkippedWorkItems;
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
            var workItemsToCreate = this.context.WorkItemMigrationStates.Values.Where(item => item.MigrationAction == MigrationAction.Create);
            Logger.LogInformation($"{workItemsToCreate.Count()} work item(s) are new and will be created in the target");

            // Log work items to be updated
            var workItemsToUpdate = this.context.WorkItemMigrationStates.Values.Where(item => item.MigrationAction == MigrationAction.Update && item.Requirement.HasFlag(WorkItemMigrationState.RequirementForExisting.UpdatePhase1));
            if (context.Configuration.UpdateModifiedWorkItems && workItemsToUpdate.Any())
            {
                Logger.LogInformation($"{workItemsToUpdate.Count()} work item(s) will be updated in the target.");
                if (context.Configuration.MoveLinks)
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
                await validator.Validate(context);
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
                await validator.Prepare(context);
            }
            var totalNumberOfBatches = ClientHelpers.GetBatchCount(context.WorkItemMigrationStates.Count, Constants.BatchSize);
            await context.WorkItemMigrationStates.Keys.Batch(Constants.BatchSize).ForEachAsync(context.Configuration.Parallelism, async (workItemIds, batchId) =>
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation(LogDestination.File, $"Work item metadata validation batch {batchId} of {totalNumberOfBatches}: Starting");
                var workItems = await WorkItemTrackingHelper.GetWorkItemsAsync(
                    context.SourceClient.WorkItemTrackingHttpClient,
                    workItemIds,
                    context.RequestedFields);
                foreach (var validator in validators)
                {
                    Logger.LogInformation(LogDestination.File, $"Work item metadata validation batch {batchId} of {totalNumberOfBatches}: {validator.Name}");
                    foreach (var workItem in workItems)
                    {
                        await validator.Validate(context, workItem);
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
                await validator.Validate(context);
                stopwatch.Reset();
                Logger.LogInformation(LogDestination.File, $"Completed target work item migration status for: {validator.Name} in {stopwatch.Elapsed.TotalSeconds}s");
            }
            Logger.LogInformation("Completed target work item migration status");
        }
    }
}
