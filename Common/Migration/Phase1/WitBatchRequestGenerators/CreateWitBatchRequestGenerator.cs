using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Newtonsoft.Json;
using Common.ApiWrappers;
using Logging;
using Microsoft.VisualStudio.Services.WebApi.Patch;

namespace Common.Migration
{
    public class CreateWitBatchRequestGenerator : BaseWitBatchRequestGenerator
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<CreateWitBatchRequestGenerator>();

        public CreateWitBatchRequestGenerator(IMigrationContext migrationContext, IBatchMigrationContext batchContext) : base(migrationContext, batchContext)
        {
        }

        public async override Task Write()
        {
            var sourceIdToWitBatchRequests = new List<(int SourceId, WitBatchRequest WitBatchRequest)>();
            foreach (var sourceWorkItem in this.batchContext.SourceWorkItems)
            {
                if (WorkItemHasFailureState(sourceWorkItem))
                {
                    continue;
                }

                WitBatchRequest witBatchRequest = GenerateWitBatchRequestFromWorkItem(sourceWorkItem);
                if (witBatchRequest != null)
                {
                    sourceIdToWitBatchRequests.Add((sourceWorkItem.Id.Value, witBatchRequest));
                }

                DecrementIdWithinBatch(sourceWorkItem.Id);
            }

            var phase1ApiWrapper = new Phase1ApiWrapper();

            // Go to BaseBatchAPiWrapper
            await phase1ApiWrapper.ExecuteWitBatchRequests(sourceIdToWitBatchRequests, this.migrationContext, batchContext, verifyOnFailure: true);
        }

        private WitBatchRequest GenerateWitBatchRequestFromWorkItem(WorkItem sourceWorkItem)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Content-Type", "application/json-patch+json");

            JsonPatchDocument patchDocument = CreateJsonPatchDocumentFromWorkItemFields(sourceWorkItem);

            JsonPatchOperation insertIdAddOperation = GetInsertBatchIdAddOperation();
            patchDocument.Add(insertIdAddOperation);

            // add hyperlink to source WorkItem
            //string sourceWorkItemApiEndpoint = ClientHelpers.GetWorkItemApiEndpoint(this.migrationContext.Configuration.SourceConnection.Uri, sourceWorkItem.Id.Value);
            //JsonPatchOperation addHyperlinkAddOperation = MigrationHelpers.GetHyperlinkOperation(Operation.Add, sourceWorkItemApiEndpoint, sourceWorkItem.Rev.ToString());
            //jsonPatchDocument.Add(addHyperlinkAddOperation);
            var sourceHyperlinkComment = new SourceHyperlinkComment(sourceWorkItem.Rev.Value);
            patchDocument.Add(
                MigrationHelpers.GetHyperlinkOperation(
                    Operation.Add,
                    sourceWorkItem.Url,
                    JsonConvert.SerializeObject(sourceHyperlinkComment)));

            string json = JsonConvert.SerializeObject(patchDocument);

            string workItemType = patchDocument.Find(a => a.Path.Contains(FieldNames.WorkItemType)).Value as string;

            var witBatchRequest = new WitBatchRequest();
            witBatchRequest.Method = "PATCH";
            witBatchRequest.Headers = headers;
            witBatchRequest.Uri = $"/{this.migrationContext.Configuration.TargetConnection.Project}/_apis/wit/workItems/${workItemType}?{this.QueryString}";
            witBatchRequest.Body = json;

            return witBatchRequest;
        }
    }
}
