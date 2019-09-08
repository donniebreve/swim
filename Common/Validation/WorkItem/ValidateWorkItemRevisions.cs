using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Common.Validation
{
    [RunOrder(1)]
    public class ValidateWorkItemRevisions : IWorkItemValidator
    {
        public string Name => "Work item revisions";

        public async Task Prepare(IValidationContext context)
        {
        }

        public async Task Validate(IValidationContext context, WorkItem workItem)
        {
            //context.GetWorkItemMigrationState(workItem.Id.Value).SourceRevision = workItem.Rev.Value;
            //context.SourceWorkItemRevision.TryAdd(workItem.Id.Value, workItem.Rev.Value);
        }
    }
}
