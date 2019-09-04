using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Common.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Common.Configuration
{
    /// <summary>
    /// The current application configuration.
    /// </summary>
    public interface IConfiguration
    {
        [DefaultValue(null)]
        IConnection SourceConnection { get; set; }

        [DefaultValue(null)]
        IConnection TargetConnection { get; set; }

        [DefaultValue(null)]
        string Query { get; set; }

        [DefaultValue(true)]
        bool CreateNewWorkItems { get; set; }

        [DefaultValue(true)]
        bool UpdateModifiedWorkItems { get; set; }

        [DefaultValue(false)]
        bool OverwriteExistingWorkItems { get; set; }

        [DefaultValue(null)]
        int Parallelism { get; set; }

        [DefaultValue(null)]
        int LinkParallelism { get; set; }

        [DefaultValue(null)]
        int HeartbeatFrequencyInSeconds { get; set; }

        [DefaultValue(null)]
        int QueryPageSize { get; set; }

        [DefaultValue(null)]
        long MaxAttachmentSize { get; set; }

        [DefaultValue(null)]
        int AttachmentUploadChunkSize { get; set; }

        [DefaultValue(false)]
        bool MigrateIdentities { get; set; }

        [DefaultValue(false)]
        bool RemoveEmojisFromIdentityDisplayNames { get; set; }

        [DefaultValue(false)]
        bool MigrateComments { get; set; }

        [DefaultValue(false)]
        bool MigrateHistory { get; set; }

        [DefaultValue(false)]
        string HistoryAttachmentFormat { get; set; }

        [DefaultValue(200)]
        int HistoryLimit { get; set; }

        [DefaultValue(false)]
        bool MigrateGitLinks { get; set; }

        [DefaultValue(false)]
        bool MigrateAttachments { get; set; }

        [DefaultValue(false)]
        bool MigrateLinks { get; set; }

        [DefaultValue(null)]
        string SourcePostMoveTag { get; set; }

        [DefaultValue(null)]
        string TargetPostMoveTag { get; set; }

        [DefaultValue(false)]
        bool SkipWorkItemsWithTypeMissingFields { get; set; }

        [DefaultValue(false)]
        bool SkipWorkItemsWithMissingAreaPath { get; set; }

        [DefaultValue(false)]
        bool SkipWorkItemsWithMissingIterationPath { get; set; }

        [DefaultValue(null)]
        string DefaultAreaPath { get; set; }

        [DefaultValue(null)]
        string DefaultIterationPath { get; set; }

        [DefaultValue(LogLevel.Information)]
        LogLevel LogLevelForFile { get; set; }

        [DefaultValue(null)]
        IList<IIdentityMapping> IdentityMappings { get; set; }

        [DefaultValue(null)]
        IList<IFieldMapping> FieldMappings { get; set; }

        [DefaultValue(null)]
        List<IFieldSubstitution> FieldSubstitutions { get; set; }

        [DefaultValue(null)]
        List<IFieldReplacement> FieldReplacements { get; set; }

        [DefaultValue(false)]
        bool SendEmailNotification { get; set; }

        [DefaultValue(null)]
        IEmailSettings EmailSettings { get; set; }
    }
}
