using Common.Serialization.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace Common.Configuration.Json
{
    /// <summary>
    /// The current application configuration.
    /// </summary>
    public class Configuration : IConfiguration
    {
        [JsonConverter(typeof(ConcreteConverter<Connection>))]
        [JsonProperty(PropertyName = "source-connection", Required = Required.Always)]
        public IConnection SourceConnection { get; set; }

        [JsonConverter(typeof(ConcreteConverter<Connection>))]
        [JsonProperty(PropertyName = "target-connection", Required = Required.Always)]
        public IConnection TargetConnection { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Query { get; set; }

        [JsonProperty(PropertyName = "create-new-work-items", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool CreateNewWorkItems { get; set; }

        [JsonProperty(PropertyName = "update-modified-work-items", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool UpdateModifiedWorkItems { get; set; }

        [JsonProperty(PropertyName = "overwrite-work-items", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool OverwriteWorkItems { get; set; }

        [JsonProperty(PropertyName = "parallelism", DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Parallelism { get; set; }

        [JsonProperty(PropertyName = "link-parallelism", DefaultValueHandling = DefaultValueHandling.Populate)]
        public int LinkParallelism { get; set; }

        [JsonProperty(PropertyName = "heartbeat-frequency-in-seconds", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(30)]
        public int HeartbeatFrequencyInSeconds { get; set; }

        [JsonProperty(PropertyName = "query-page-size", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(20000)]
        public int QueryPageSize { get; set; }

        [JsonProperty(PropertyName = "max-attachment-size", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(60 * 1024 * 1024)]
        public int MaxAttachmentSize { get; set; }

        [JsonProperty(PropertyName = "attachment-upload-chunk-size", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(1 * 1024 * 1024)]
        public int AttachmentUploadChunkSize { get; set; }

        [JsonProperty(PropertyName = "move-history", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool MoveHistory { get; set; }

        [JsonProperty(PropertyName = "move-history-limit", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(200)]
        public int MoveHistoryLimit { get; set; }

        [JsonProperty(PropertyName = "move-git-links", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool MoveGitLinks { get; set; }

        [JsonProperty(PropertyName = "move-attachments", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool MoveAttachments { get; set; }

        [JsonProperty(PropertyName = "move-links", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool MoveLinks { get; set; }

        [JsonProperty(PropertyName = "source-post-move-tag", DefaultValueHandling = DefaultValueHandling.Populate)]
        public string SourcePostMoveTag { get; set; }

        [JsonProperty(PropertyName = "target-post-move-tag", DefaultValueHandling = DefaultValueHandling.Populate)]
        public string TargetPostMoveTag { get; set; }

        [JsonProperty(PropertyName = "skip-work-items-with-type-missing-fields", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool SkipWorkItemsWithTypeMissingFields { get; set; }

        [JsonProperty(PropertyName = "skip-work-items-with-missing-area-path", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool SkipWorkItemsWithMissingAreaPath { get; set; }

        [JsonProperty(PropertyName = "skip-work-items-with-missing-iteration-path", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool SkipWorkItemsWithMissingIterationPath { get; set; }

        [JsonProperty(PropertyName = "default-area-path", DefaultValueHandling = DefaultValueHandling.Populate)]
        public string DefaultAreaPath { get; set; }

        [JsonProperty(PropertyName = "default-iteration-path", DefaultValueHandling = DefaultValueHandling.Populate)]
        public string DefaultIterationPath { get; set; }

        [JsonProperty(PropertyName = "clear-identity-display-names", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool ClearIdentityDisplayNames { get; set; }

        [JsonProperty(PropertyName = "ensure-identities", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool EnsureIdentities { get; set; }

        [JsonProperty(PropertyName = "log-level-for-file", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(LogLevel.Information)]
        public LogLevel LogLevelForFile { get; set; }

        [JsonConverter(typeof(ConcreteConverter<FieldMapping>))]
        [JsonProperty(PropertyName = "field-mappings", DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<IFieldMapping> FieldMappings { get; set; }

        [JsonConverter(typeof(ConcreteConverter<FieldSubstitution>))]
        [JsonProperty(PropertyName = "field-substitutions", DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<IFieldSubstitution> FieldSubstitutions { get; set; }

        [JsonConverter(typeof(ConcreteConverter<FieldReplacement>))]
        [JsonProperty(PropertyName = "field-replacements", DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<IFieldReplacement> FieldReplacements { get; set; }

        [JsonProperty(PropertyName = "send-email-notification", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool SendEmailNotification { get; set; }

        [JsonConverter(typeof(ConcreteConverter<EmailSettings>))]
        [JsonProperty(PropertyName = "email-settings", Required = Required.DisallowNull)]
        public IEmailSettings EmailSettings { get; set; }
    }
}
