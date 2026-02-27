using System;
using System.Collections.Generic;

namespace CapaNegocio.Integraciones.As400Sync
{
    public enum SyncIncrementalMode
    {
        WatermarkDateTimeChars = 0,
        FullSnapshot = 1,
        Disabled = 99
    }

    public enum DeleteStrategy
    {
        None = 0,
        TombstoneTable = 1,
        FullSnapshotReconcile = 2,
        SoftDeleteColumn = 3
    }

    public class MirrorColumnDefinition
    {
        public string SourceColumn { get; set; }
        public string TargetColumn { get; set; }
        public bool TrimString { get; set; } = true;
    }

    public class SyncTableDefinition
    {
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;
        public string SourceSchema { get; set; }
        public string SourceTable { get; set; }
        public string TargetSchema { get; set; }
        public string TargetTable { get; set; }
        public IList<string> PrimaryKeys { get; set; } = new List<string>();
        public IList<MirrorColumnDefinition> Columns { get; set; } = new List<MirrorColumnDefinition>();
        public SyncIncrementalMode IncrementalMode { get; set; }
        public DeleteStrategy DeleteStrategy { get; set; }
        public string WatermarkDateColumn { get; set; }
        public string WatermarkTimeColumn { get; set; }
        public string SoftDeleteSourceColumn { get; set; }
        public string SoftDeleteActiveValue { get; set; }
        public string SourceFilterSql { get; set; }
        public int? BatchSize { get; set; }
        public bool AllowFullSnapshotDeleteReconcile { get; set; }
        public string Notes { get; set; }
    }

    public class SyncWatermarkState
    {
        public string TableName { get; set; }
        public DateTime? LastSuccessTs { get; set; }
        public string LastSuccessKey { get; set; }
        public Guid? LastBatchId { get; set; }
        public string Status { get; set; }
        public string LastError { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class MirrorSyncRow
    {
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string SourceKey { get; set; }
        public DateTime? SourceUpdatedAt { get; set; }
        public bool? SourceIsDeleted { get; set; }
        public string SourceOperation { get; set; }
    }

    public class SourceReadResult
    {
        public IList<MirrorSyncRow> Rows { get; set; } = new List<MirrorSyncRow>();
        public DateTime? MaxSourceTimestamp { get; set; }
        public string MaxSourceKey { get; set; }
        public bool IsCompleteSnapshotPage { get; set; }
        public HashSet<string> SnapshotKeys { get; set; }
    }

    public class MirrorApplyRejection
    {
        public string TableName { get; set; }
        public string SourceKey { get; set; }
        public Dictionary<string, object> Payload { get; set; }
        public string Error { get; set; }
    }

    public class MirrorApplyResult
    {
        public int RowsApplied { get; set; }
        public int RowsDeleted { get; set; }
        public IList<MirrorApplyRejection> Rejections { get; set; } = new List<MirrorApplyRejection>();
    }

    public class SyncBatchResult
    {
        public Guid BatchId { get; set; }
        public string TableName { get; set; }
        public int RowsRead { get; set; }
        public int RowsApplied { get; set; }
        public int RowsRejected { get; set; }
        public int RowsDeleted { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class SyncExecutionOptions
    {
        public bool Enabled { get; set; }
        public int BatchSize { get; set; } = 1000;
        public int As400CommandTimeoutSeconds { get; set; } = 60;
        public int PostgresCommandTimeoutSeconds { get; set; } = 60;
        public int MaxRetries { get; set; } = 2;
        public int RetryBackoffMs { get; set; } = 1500;
        public bool DryRun { get; set; }
        public string LockOwner { get; set; }
    }

    public class SyncTombstone
    {
        public long Id { get; set; }
        public string TableName { get; set; }
        public Dictionary<string, object> PrimaryKeyPayload { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public DateTime? SourceDeletedAt { get; set; }
        public string SourceReference { get; set; }
        public string Error { get; set; }
    }

    public interface IAs400SourceReader
    {
        SourceReadResult ReadChanges(SyncTableDefinition table, SyncWatermarkState watermark, int batchSize);
    }

    public interface IMirrorApplier
    {
        MirrorApplyResult ApplyUpserts(SyncTableDefinition table, Guid batchId, IList<MirrorSyncRow> rows, bool dryRun);
        MirrorApplyResult ApplyPendingTombstones(SyncTableDefinition table, IList<SyncTombstone> tombstones, bool dryRun);
        IList<SyncTombstone> BuildSnapshotReconcileTombstones(SyncTableDefinition table, ISet<string> currentSourceKeys, Guid batchId);
    }

    public interface ISyncStateStore
    {
        SyncWatermarkState GetWatermark(string tableName);
        bool TryAcquireTableLock(string tableName, Guid batchId, out long advisoryLockKey);
        void ReleaseTableLock(long advisoryLockKey);
        void MarkRunning(string tableName, Guid batchId);
        void MarkSuccess(string tableName, Guid batchId, DateTime? lastSuccessTs, string lastSuccessKey);
        void MarkError(string tableName, Guid batchId, string error);
        void RegisterBatchStart(Guid batchId, string tableName, DateTime startedAt);
        void RegisterBatchFinish(SyncBatchResult result, DateTime startedAt);
        void SaveRejections(Guid batchId, string tableName, IList<MirrorApplyRejection> rejections);
        IList<SyncTombstone> GetPendingTombstones(string tableName, int take);
        void SaveTombstones(Guid batchId, string tableName, IList<SyncTombstone> tombstones);
        void MarkTombstonesApplied(IList<SyncTombstone> tombstones, string error);
    }
}
