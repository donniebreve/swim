using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Logging;
using Microsoft.VisualStudio.Services.Common;
using Common.Configuration;
using Common.Api;

namespace Common.Migration
{
    public class WorkItemLinksProcessor : IPhase2Processor
    {
        private static ILogger Logger { get; } = MigratorLogging.CreateLogger<WorkItemLinksProcessor>();

        public string Name => Constants.RelationPhaseWorkItemLinks;

        public bool IsEnabled(IConfiguration configuration)
        {
            return configuration.MigrateLinks;
        }

        public async Task Preprocess(IContext context, IList<WorkItem> sourceWorkItems, IList<WorkItem> targetWorkItems)
        {
            var linkedWorkItemArtifactUrls = new HashSet<string>();
            foreach (WorkItem sourceWorkItem in sourceWorkItems)
            {
                var relations = GetWorkItemLinkRelations(context, sourceWorkItem.Relations);
                var linkedIds = relations.Select(r => ClientHelpers.GetWorkItemIdFromApiEndpoint(r.Url));

                // To do
                var uris = linkedIds.Where(id => !((MigrationContext)context).SourceToTargetIds.ContainsKey(id)).Select(id => ClientHelpers.GetWorkItemApiEndpoint(context.Configuration.SourceConnection.Uri, id));
                linkedWorkItemArtifactUrls.AddRange(uris);
            }

            await linkedWorkItemArtifactUrls.Batch(Constants.BatchSize).ForEachAsync(context.Configuration.Parallelism, async (workItemArtifactUris, batchId) =>
            {
                Logger.LogTrace(LogDestination.File, $"Finding linked work items on target for batch {batchId}");

                var results = await WorkItemTrackingApi.QueryWorkItemsForArtifactUrisAsync(context.TargetClient.WorkItemTrackingHttpClient, new ArtifactUriQuery { ArtifactUris = workItemArtifactUris });
                foreach (var result in results.ArtifactUrisQueryResult)
                {
                    if (result.Value != null)
                    {
                        if (result.Value.Count() == 1)
                        {
                            var sourceId = ClientHelpers.GetWorkItemIdFromApiEndpoint(result.Key);
                            var targetId = result.Value.First().Id;

                            // To do
                            ((MigrationContext)context).SourceToTargetIds[sourceId] = targetId;
                        }
                    }
                }

                Logger.LogTrace(LogDestination.File, $"Finished finding linked work items on target for batch {batchId}");
            });
        }

        public async Task<IEnumerable<JsonPatchOperation>> Process(IContext context, WorkItem sourceWorkItem, WorkItem targetWorkItem, object state = null)
        {
            IList<JsonPatchOperation> jsonPatchOperations = new List<JsonPatchOperation>();

            if (sourceWorkItem.Relations == null)
            {
                return jsonPatchOperations;
            }

            IList<WorkItemRelation> sourceWorkItemLinkRelations = GetWorkItemLinkRelations(context, sourceWorkItem.Relations);

            if (sourceWorkItemLinkRelations.Any())
            {
                foreach (WorkItemRelation sourceWorkItemLinkRelation in sourceWorkItemLinkRelations)
                {
                    int linkedSourceId = ClientHelpers.GetWorkItemIdFromApiEndpoint(sourceWorkItemLinkRelation.Url);
                    int targetWorkItemId = targetWorkItem.Id.Value;
                    int linkedTargetId;

                    if (!((MigrationContext)context).SourceToTargetIds.TryGetValue(linkedSourceId, out linkedTargetId))
                    {
                        continue;
                    }

                    string comment = MigrationHelpers.GetCommentFromAttributes(sourceWorkItemLinkRelation);
                    WorkItemLink newWorkItemLink = new WorkItemLink(linkedTargetId, sourceWorkItemLinkRelation.Rel, false, false, comment, 0);

                    JsonPatchOperation workItemLinkAddOperation = MigrationHelpers.GetWorkItemLinkAddOperation(((MigrationContext)context), newWorkItemLink);
                    jsonPatchOperations.Add(workItemLinkAddOperation);
                }
            }

            return jsonPatchOperations;
        }

        private IList<WorkItemRelation> GetWorkItemLinkRelations(IContext context, IList<WorkItemRelation> relations)
        {
            IList<WorkItemRelation> result = new List<WorkItemRelation>();

            if (relations == null)
            {
                return result;
            }

            foreach (WorkItemRelation relation in relations)
            {
                if (IsRelationWorkItemLink(context, relation))
                {
                    result.Add(relation);
                }
            }

            return result;
        }

        private bool IsRelationWorkItemLink(IContext context, WorkItemRelation relation)
        {
            if (((MigrationContext)context).ValidatedWorkItemLinkRelationTypes.Contains(relation.Rel))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
