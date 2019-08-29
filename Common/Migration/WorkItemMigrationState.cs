using System;

namespace Common.Migration
{
    public class WorkItemMigrationState
    {
        public int SourceId { get; set; }
        public int? TargetId { get; set; }

        public Uri SourceUri { get; set; }
        public Uri TargetUri { get; set; }

        public MigrationAction MigrationAction { get; set; }

        public FailureReason FailureReason { get; set; }

        public RequirementForExisting Requirement { get; set; }
        public MigrationCompletionStatus MigrationCompleted { get; set; }

        [Flags]
        public enum RequirementForExisting { None, UpdatePhase1, UpdatePhase2 }
        [Flags]
        public enum MigrationCompletionStatus { None, Phase1, Phase2, Phase3 }
        public RevAndPhaseStatus RevAndPhaseStatus { get; set; }
    }
}
