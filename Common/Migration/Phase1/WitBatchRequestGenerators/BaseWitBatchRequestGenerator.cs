using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Common.Configuration;
using Logging;

namespace Common.Migration
{
    public abstract class BaseWitBatchRequestGenerator
    {
        static ILogger Logger { get; } = MigratorLogging.CreateLogger<BaseWitBatchRequestGenerator>();

        protected IMigrationContext migrationContext; // stop passing this around
        protected IBatchMigrationContext batchContext;

        // Chose List of tuples instead of Dictionary because this guarantees that the ordering is maintained. Also lets us use custom names for the 2 values rather than key/value.
        // WorkItem Id property is a nullable int, so we have it here also
        protected List<(int BatchId, int WorkItemId)> IdWithinBatchToWorkItemIdMapping { get; }
        protected int IdWithinBatch;
        protected IList<WitBatchRequest> WitBatchRequests;
        protected string QueryString;

        public BaseWitBatchRequestGenerator()
        {
        }

        public BaseWitBatchRequestGenerator(IMigrationContext migrationContext, IBatchMigrationContext batchContext)
        {
            this.migrationContext = migrationContext;
            this.batchContext = batchContext;
            this.IdWithinBatchToWorkItemIdMapping = new List<(int BatchId, int WorkItemId)>();
            this.IdWithinBatch = -1;
            this.WitBatchRequests = new List<WitBatchRequest>();
            bool bypassRules = true;
            bool suppressNotifications = true;
            this.QueryString = $"bypassRules={bypassRules}&suppressNotifications={suppressNotifications}&api-version=4.0";

            // we only have a batch context when it's create/update work items
            if (batchContext != null)
            {
                // remove any work items marked as failed during preprocessing
                this.batchContext.SourceWorkItems = RemoveWorkItemsThatFailedPreprocessing(batchContext.WorkItemMigrationState, batchContext.SourceWorkItems);
            }
        }

        public abstract Task Write();

        protected bool WorkItemHasFailureState(WorkItem sourceWorkItem)
        {
            WorkItemMigrationState state = batchContext.WorkItemMigrationState.FirstOrDefault(a => a.SourceId == sourceWorkItem.Id.Value);
            if (state != null && state.FailureReason != FailureReason.None)
            {
                Logger.LogWarning(LogDestination.File, $"Skipping migration of work item with id {sourceWorkItem.Id.Value} due to Error in migration state with failure reasons: {state.FailureReason.ToString()}");
                return true;
            }
            return false;
        }

        protected IList<WorkItem> RemoveWorkItemsThatFailedPreprocessing(IList<WorkItemMigrationState> workItemMigrationState, IList<WorkItem> sourceWorkItems)
        {
            if (sourceWorkItems != null)
            {
                Dictionary<int, FailureReason> notMigratedWorkItems = ClientHelpers.GetNotMigratedWorkItemsFromWorkItemsMigrationState(workItemMigrationState);
                return sourceWorkItems.Where(w => !notMigratedWorkItems.ContainsKey(w.Id.Value)).ToList();
            }
            else
            {
                return null;
            }
        }

        protected void DecrementIdWithinBatch(int? sourceWorkItemId)
        {
            this.IdWithinBatchToWorkItemIdMapping.Add((this.IdWithinBatch, sourceWorkItemId.Value));
            this.IdWithinBatch--;
        }

        protected JsonPatchDocument CreateJsonPatchDocumentFromWorkItemFields(WorkItem sourceWorkItem)
        {
            string sourceWorkItemType = GetWorkItemTypeFromWorkItem(sourceWorkItem);
            JsonPatchDocument jsonPatchDocument = new JsonPatchDocument();

            IList<string> fieldNamesAlreadyPopulated = new List<string>();

            foreach (var sourceField in sourceWorkItem.Fields)
            {
                if (fieldNamesAlreadyPopulated.Contains(sourceField.Key)) // we have already processed the content for this target field so skip
                {
                    continue;
                }

                if (FieldIsWithinType(sourceField.Key, sourceWorkItemType) && !IsFieldUnsupported(sourceField.Key))
                {
                    KeyValuePair<string, object> fieldProcessedForConfigFields = GetTargetField(sourceField, fieldNamesAlreadyPopulated);
                    KeyValuePair<string, object> preparedField = UpdateProjectNameIfNeededForField(sourceWorkItem, fieldProcessedForConfigFields);

                    // If this is an identity field
                    if (this.migrationContext.IdentityFields.Contains(sourceField.Key))
                    {
                        // Apply identity mapping
                        if (this.migrationContext.Configuration.IdentityMappings != null)
                        {
                            var identityMapping = this.migrationContext.Configuration.IdentityMappings.SingleOrDefault(m => m.Source == preparedField.Value.ToString());
                            if (identityMapping != null)
                            {
                                preparedField = new KeyValuePair<string, object>(preparedField.Key, identityMapping.Target);
                            }
                        }
                        // Remove emojis from identities, referred to as a temoprary hack in the original source
                        if (this.migrationContext.Configuration.RemoveEmojisFromIdentityDisplayNames)
                        {
                            preparedField = new KeyValuePair<string, object>(preparedField.Key, preparedField.Value.ToString().RemoveEmojis());
                        }
                    }

                    // Add inline image urls
                    JsonPatchOperation jsonPatchOperation;
                    if (this.migrationContext.HtmlFieldReferenceNames.Contains(preparedField.Key) 
                        && preparedField.Value is string)
                    {
                        string updatedHtmlFieldValue = GetUpdatedHtmlField((string)preparedField.Value);
                        KeyValuePair<string, object> updatedField = new KeyValuePair<string, object>(preparedField.Key, updatedHtmlFieldValue);
                        jsonPatchOperation = MigrationHelpers.GetJsonPatchOperationAddForField(updatedField);
                    }
                    else
                    {
                        jsonPatchOperation = MigrationHelpers.GetJsonPatchOperationAddForField(preparedField);
                    }
                    jsonPatchDocument.Add(jsonPatchOperation);
                }
            }

            return jsonPatchDocument;
        }

        private KeyValuePair<string, object> GetTargetField(KeyValuePair<string, object> sourceField, IList<string> fieldNamesAlreadyPopulated)
        {
            string sourceFieldName = sourceField.Key;
            object sourceFieldValue = sourceField.Value;
            string targetFieldName = sourceFieldName;
            object targetFieldValue = sourceFieldValue;

            if (this.migrationContext.Configuration.FieldMappings != null)
            {
                var fieldMapping = this.migrationContext.Configuration.FieldMappings.SingleOrDefault(m => m.Source == sourceFieldName);
                if (fieldMapping != null)
                {
                    targetFieldName = fieldMapping.Target;
                    fieldNamesAlreadyPopulated.Add(targetFieldName);
                }
            }

            if (this.migrationContext.Configuration.FieldSubstitutions != null)
            {
                var fieldSubstitution = this.migrationContext.Configuration.FieldSubstitutions.SingleOrDefault(s => s.Field == sourceFieldName);
                if (fieldSubstitution != null)
                {
                    targetFieldValue = fieldSubstitution.Value;
                }
            }

            if (this.migrationContext.Configuration.FieldReplacements != null)
            {
                var fieldReplacement = this.migrationContext.Configuration.FieldReplacements.SingleOrDefault(r => r.Field == sourceFieldName);
                if (fieldReplacement != null)
                {
                    targetFieldValue = Regex.Replace(targetFieldValue.ToString(), fieldReplacement.Pattern, fieldReplacement.Replacement);
                }
            }

            return new KeyValuePair<string, object>(targetFieldName, targetFieldValue);
        }

        /// <summary>
        /// Returns a JsonPatchOperation with any inline image urls in the html content replaced to point to the appropriate stored attachment on the target.
        /// </summary>
        /// <param name="htmlField"></param>
        /// <returns></returns>
        private string GetUpdatedHtmlField(string htmlFieldValue)
        {
            HashSet<string> inlineImageUrls = MigrationHelpers.GetInlineImageUrlsFromField(htmlFieldValue, this.migrationContext.SourceClient.Connection.Uri.AbsoluteUri);

            foreach (string inlineImageUrl in inlineImageUrls)
            {
                if (this.batchContext.SourceInlineImageUrlToTargetInlineImageGuid.ContainsKey(inlineImageUrl))
                {
                    string newValue = BuildTargetInlineImageUrl(inlineImageUrl, this.batchContext.SourceInlineImageUrlToTargetInlineImageGuid[inlineImageUrl]);
                    htmlFieldValue = htmlFieldValue.Replace(inlineImageUrl, newValue);
                }
            }

            return htmlFieldValue;
        }

        private string BuildTargetInlineImageUrl(string sourceInlineImageUrl, string targetInlineImageGuid)
        {
            string sourceAccount = this.migrationContext.Configuration.SourceConnection.Uri;
            string targetAccount = this.migrationContext.Configuration.TargetConnection.Uri;
            string result = sourceInlineImageUrl.Replace(sourceAccount, targetAccount);
            return MigrationHelpers.ReplaceAttachmentUrlGuid(result, targetInlineImageGuid);
        }

        protected KeyValuePair<string, object> UpdateProjectNameIfNeededForField(WorkItem sourceWorkItem, KeyValuePair<string, object> sourceField)
        {
            if (FieldRequiresProjectNameUpdate(sourceField.Key))
            {
                // do the check so we know which of the 3 it is here
                return CreateTargetField(sourceWorkItem, sourceField);
            }
            else
            {
                return sourceField;
            }
        }

        protected KeyValuePair<string, object> CreateTargetField(WorkItem sourceWorkItem, KeyValuePair<string, object> sourceField)
        {
            KeyValuePair<string, object> targetField;
            string targetProject = this.migrationContext.Configuration.TargetConnection.Project;
            string sourceProject = this.migrationContext.Configuration.SourceConnection.Project;

            string defaultAreaPath = string.IsNullOrEmpty(this.migrationContext.Configuration.DefaultAreaPath) ? targetProject : this.migrationContext.Configuration.DefaultAreaPath;
            string defaultIterationPath = string.IsNullOrEmpty(this.migrationContext.Configuration.DefaultIterationPath) ? targetProject : this.migrationContext.Configuration.DefaultIterationPath;

            // Make sure the new area path and iteration path exist on target before assigning them.
            // Otherwise assign targetProject
            if (sourceField.Key.Equals(FieldNames.AreaPath, StringComparison.OrdinalIgnoreCase))
            {
                string targetPathName = GetTargetPathName(sourceField.Value as string, sourceProject, targetProject);

                if (ExistsInTargetAreaPathList(targetPathName))
                {
                    targetField = new KeyValuePair<string, object>(sourceField.Key, targetPathName);
                }
                else
                {
                    targetField = new KeyValuePair<string, object>(sourceField.Key, defaultAreaPath);
                    Logger.LogWarning(LogDestination.File, $"Could not find corresponding AreaPath: {targetPathName} on target. Assigning the AreaPath: {defaultAreaPath} on source work item with Id: {sourceWorkItem.Id}.");
                }
            }
            else if (sourceField.Key.Equals(FieldNames.IterationPath, StringComparison.OrdinalIgnoreCase))
            {
                string targetPathName = GetTargetPathName(sourceField.Value as string, sourceProject, targetProject);

                if (ExistsInTargetIterationPathList(targetPathName))
                {
                    targetField = new KeyValuePair<string, object>(sourceField.Key, targetPathName);
                }
                else
                {
                    targetField = new KeyValuePair<string, object>(sourceField.Key, defaultIterationPath);
                    Logger.LogWarning(LogDestination.File, $"Could not find corresponding IterationPath: {targetPathName} on target. Assigning the IterationPath: {defaultIterationPath} on source work item with Id: {sourceWorkItem.Id}.");
                }
            }
            else if (sourceField.Key.Equals(FieldNames.TeamProject, StringComparison.OrdinalIgnoreCase))
            {
                targetField = new KeyValuePair<string, object>(sourceField.Key, targetProject);
            }

            return targetField;
        }

        public string GetTargetPathName(string fieldValue, string sourceProject, string targetProject)
        {
            return AreaAndIterationPathTree.ReplaceLeadingProjectName(fieldValue, sourceProject, targetProject);
        }

        public bool ExistsInTargetAreaPathList(string areaPath)
        {
            return this.migrationContext.TargetAreaPaths.Any(a => a.Equals(areaPath, StringComparison.OrdinalIgnoreCase));
        }

        public bool ExistsInTargetIterationPathList(string iterationPath)
        {
            return this.migrationContext.TargetIterationPaths.Any(a => a.Equals(iterationPath, StringComparison.OrdinalIgnoreCase));
        }

        public bool FieldRequiresProjectNameUpdate(string fieldName)
        {
            return this.migrationContext.FieldsThatRequireSourceProjectToBeReplacedWithTargetProject.Any(a => a.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsFieldUnsupported(string fieldRefName)
        {
            return this.migrationContext.UnsupportedFields.Any(a => fieldRefName.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// returns true if fieldName exists within workItemType on target ignoring case of strings.
        /// </summary>
        /// <param name="sourceFieldName"></param>
        /// <param name="sourceWorkItemType"></param>
        /// <returns></returns>
        public bool FieldIsWithinType(string sourceFieldName, string sourceWorkItemType)
        {
            ISet<string> fieldsOfKey = this.migrationContext.WorkItemTypes.First(a => a.Key.Equals(sourceWorkItemType, StringComparison.OrdinalIgnoreCase)).Value;
            return fieldsOfKey.Any(a => a.Equals(sourceFieldName, StringComparison.OrdinalIgnoreCase));
        }

        public string GetWorkItemTypeFromWorkItem(WorkItem sourceWorkItem)
        {
            return sourceWorkItem.Fields[FieldNames.WorkItemType] as string;
        }

        public JsonPatchOperation GetInsertBatchIdAddOperation()
        {
            JsonPatchOperation jsonPatchOperation = new JsonPatchOperation();
            jsonPatchOperation.Operation = Operation.Add;
            jsonPatchOperation.Path = "/id";
            jsonPatchOperation.Value = this.IdWithinBatch;

            return jsonPatchOperation;
        }
    }
}
