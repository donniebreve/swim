using Common.Api;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Validation.Configuration
{
    [RunOrder(4)]
    public class ValidateIdentities : IWorkItemValidator
    {
        private ILogger Logger { get; } = MigratorLogging.CreateLogger<ValidateIdentities>();

        public string Name => "Work item identity validator";

        public async Task Prepare(IValidationContext context)
        {
            ConcurrentBag<string> concurrentBag = new ConcurrentBag<string>();
            await context.WorkItemMigrationStates
                .Where(item => item.MigrationAction != Migration.MigrationAction.None)
                .Batch(Constants.BatchSize)
                .ForEachAsync(context.Configuration.Parallelism,
                    async (batch, batchId) =>
                    {
                        IList<WorkItem> workItems = await WorkItemTrackingApi.GetWorkItemsAsync(context.SourceClient.WorkItemTrackingHttpClient, batch.Select(item => item.SourceId).ToList());
                        foreach (var workItem in workItems)
                        {
                            foreach (var field in workItem.Fields)
                            {
                                var workItemField = context.SourceFields[field.Key];
                                if (workItemField.IsIdentity)
                                {
                                    if (!concurrentBag.Contains(field.Value.ToString()))
                                    {
                                        concurrentBag.Add(field.Value.ToString());
                                    }
                                }
                            }
                        }
                    });
            foreach (var identity in concurrentBag)
            {
                var message = $"Discovered source identity: '{identity}'";
                if (context.Configuration.IdentityMappings != null)
                {
                    var identityMapping = context.Configuration.IdentityMappings.SingleOrDefault(m => m.Source == identity);
                    if (identityMapping != null)
                    {
                        message += $", mapped to '{identityMapping.Target}'";
                    }
                }
                Logger.LogInformation(LogDestination.File, message);
            }
        }

        public async Task Validate(IValidationContext context, WorkItem workItem) { }
    }
}
