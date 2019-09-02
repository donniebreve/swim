using Common.Api;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Threading.Tasks;

namespace Common.Validation
{
    [RunOrder(3)]
    public class ValidateSourceQuery : IConfigurationValidator
    {
        private ILogger Logger { get; } = MigratorLogging.CreateLogger<ValidateSourceQuery>();

        public string Name => "Source query";

        public async Task Validate(IValidationContext context)
        {
            Logger.LogInformation(LogDestination.File, "Checking if the migration query exists in the source project");
            QueryHierarchyItem query;
            try
            {
                query = await WorkItemApi.GetQueryAsync(context.SourceClient.WorkItemTrackingHttpClient, context.Configuration.SourceConnection.Project, context.Configuration.Query);
            }
            catch (Exception e)
            {
                throw new ValidationException("Unable to read the migration query", e);
            }
            if (query.QueryType != QueryType.Flat)
            {
                throw new ValidationException("Only flat queries are supported for migration");
            }
        }
    }
}
