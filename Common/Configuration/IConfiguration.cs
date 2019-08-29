﻿using System.Collections.Generic;
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
        bool OverwriteWorkItems { get; set; }

        [DefaultValue(null)]
        int Parallelism { get; set; }

        [DefaultValue(null)]
        int LinkParallelism { get; set; }

        [DefaultValue(null)]
        int HeartbeatFrequencyInSeconds { get; set; }

        [DefaultValue(null)]
        int QueryPageSize { get; set; }

        [DefaultValue(null)]
        int MaxAttachmentSize { get; set; }

        [DefaultValue(null)]
        int AttachmentUploadChunkSize { get; set; }

        [DefaultValue(false)]
        bool MoveHistory { get; set; }

        [DefaultValue(200)]
        int MoveHistoryLimit { get; set; }

        [DefaultValue(false)]
        bool MoveGitLinks { get; set; }

        [DefaultValue(false)]
        bool MoveAttachments { get; set; }

        [DefaultValue(false)]
        bool MoveLinks { get; set; }

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

        [DefaultValue(false)]
        bool ClearIdentityDisplayNames { get; set; }

        [DefaultValue(false)]
        bool EnsureIdentities { get; set; }

        [DefaultValue(LogLevel.Information)]
        LogLevel LogLevelForFile { get; set; }

        [DefaultValue(null)]
        List<IFieldMapping> FieldMappings { get; set; }

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
