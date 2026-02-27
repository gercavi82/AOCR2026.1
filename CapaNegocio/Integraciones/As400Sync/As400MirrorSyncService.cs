using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CapaNegocio.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class As400MirrorSyncService
    {
        private readonly IAs400SourceReader _sourceReader;
        private readonly IMirrorApplier _applier;
        private readonly ISyncStateStore _stateStore;
        private readonly ILoggingService _logger;
        private readonly SyncExecutionOptions _options;
        private readonly IList<SyncTableDefinition> _tables;

        public As400MirrorSyncService(
            IAs400SourceReader sourceReader,
            IMirrorApplier applier,
            ISyncStateStore stateStore,
            SyncExecutionOptions options,
            IList<SyncTableDefinition> tables,
            ILoggingService logger = null)
        {
            _sourceReader = sourceReader ?? throw new ArgumentNullException("sourceReader");
            _applier = applier ?? throw new ArgumentNullException("applier");
            _stateStore = stateStore ?? throw new ArgumentNullException("stateStore");
            _options = options ?? new SyncExecutionOptions();
            _tables = tables ?? new List<SyncTableDefinition>();
            _logger = logger ?? LoggingServiceFactory.Create();
        }

        public static As400MirrorSyncService CreateDefault()
        {
            var env = As400MirrorSyncOptionsFactory.Create();
            var logger = LoggingServiceFactory.Create();
            var source = new As400OdbcSourceReader(env.As400OdbcConnectionString, env.Options.As400CommandTimeoutSeconds, logger);
            var applier = new PostgresMirrorApplier(env.PostgresMirrorConnectionString, env.Options.PostgresCommandTimeoutSeconds, logger);
            var state = new PostgresSyncStateStore(env.PostgresMirrorConnectionString, env.Options.PostgresCommandTimeoutSeconds, logger);
            var tables = As400MirrorSyncDefinitions.CreateDefault();
            return new As400MirrorSyncService(source, applier, state, env.Options, tables, logger);
        }

        public IList<SyncBatchResult> RunAllEnabled(bool includeDisabled = false)
        {
            var results = new List<SyncBatchResult>();
            foreach (var table in _tables)
            {
                if (table == null) continue;
                if (!includeDisabled && !table.Enabled) continue;
                if (table.IncrementalMode == SyncIncrementalMode.Disabled) continue;
                results.Add(RunTable(table.Name));
            }
            return results;
        }

        public SyncBatchResult RunTable(string tableName)
        {
            var table = _tables.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
            if (table == null)
            {
                return new SyncBatchResult
                {
                    BatchId = Guid.NewGuid(),
                    TableName = tableName,
                    Status = "ERROR",
                    Error = "Tabla no configurada: " + tableName
                };
            }

            if (!_options.Enabled)
            {
                return new SyncBatchResult
                {
                    BatchId = Guid.NewGuid(),
                    TableName = table.Name,
                    Status = "SKIPPED",
                    Error = "Sync:Enabled=false"
                };
            }

            var attempts = Math.Max(1, _options.MaxRetries + 1);
            Exception lastEx = null;
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    return RunTableInternal(table);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    _logger.LogError(ex, new LogContext
                    {
                        Controller = "As400MirrorSync",
                        Action = "RunTable",
                        AdditionalData = new Dictionary<string, object>
                        {
                            ["Table"] = table.Name,
                            ["Attempt"] = attempt,
                            ["MaxAttempts"] = attempts
                        }
                    });

                    if (attempt < attempts)
                    {
                        Thread.Sleep(Math.Max(250, _options.RetryBackoffMs * attempt));
                    }
                }
            }

            return new SyncBatchResult
            {
                BatchId = Guid.NewGuid(),
                TableName = table.Name,
                Status = "ERROR",
                Error = lastEx != null ? lastEx.Message : "Error desconocido"
            };
        }

        private SyncBatchResult RunTableInternal(SyncTableDefinition table)
        {
            var batchId = Guid.NewGuid();
            var startedAt = DateTime.UtcNow;
            var batchResult = new SyncBatchResult
            {
                BatchId = batchId,
                TableName = table.Name,
                Status = "RUNNING"
            };

            long advisoryLockKey;
            if (!_stateStore.TryAcquireTableLock(table.Name, batchId, out advisoryLockKey))
            {
                batchResult.Status = "SKIPPED";
                batchResult.Error = "Otra ejecucion ya esta sincronizando esta tabla.";
                return batchResult;
            }

            try
            {
                _stateStore.RegisterBatchStart(batchId, table.Name, startedAt);
                var watermark = _stateStore.GetWatermark(table.Name);
                var batchSize = table.BatchSize.HasValue && table.BatchSize.Value > 0 ? table.BatchSize.Value : _options.BatchSize;

                var source = _sourceReader.ReadChanges(table, watermark, batchSize);
                batchResult.RowsRead = source.Rows != null ? source.Rows.Count : 0;

                var apply = _applier.ApplyUpserts(table, batchId, source.Rows, _options.DryRun);
                batchResult.RowsApplied += apply.RowsApplied;
                batchResult.RowsDeleted += apply.RowsDeleted;
                batchResult.RowsRejected += apply.Rejections != null ? apply.Rejections.Count : 0;

                if (apply.Rejections != null && apply.Rejections.Count > 0)
                {
                    _stateStore.SaveRejections(batchId, table.Name, apply.Rejections);
                }

                batchResult.RowsDeleted += ProcessDeletes(table, batchId, source, batchResult);

                if (!_options.DryRun)
                {
                    var finalTs = source.MaxSourceTimestamp.HasValue ? source.MaxSourceTimestamp : watermark.LastSuccessTs;
                    var finalKey = !string.IsNullOrWhiteSpace(source.MaxSourceKey) ? source.MaxSourceKey : watermark.LastSuccessKey;
                    _stateStore.MarkSuccess(table.Name, batchId, finalTs, finalKey);
                }
                else
                {
                    _stateStore.MarkSuccess(table.Name, batchId, watermark.LastSuccessTs, watermark.LastSuccessKey);
                }

                batchResult.Status = batchResult.RowsRejected > 0 ? "OK_WITH_REJECTIONS" : "OK";
                return batchResult;
            }
            catch (Exception ex)
            {
                batchResult.Status = "ERROR";
                batchResult.Error = ex.Message;
                _stateStore.MarkError(table.Name, batchId, ex.ToString());
                throw;
            }
            finally
            {
                batchResult.Duration = DateTime.UtcNow - startedAt;
                _stateStore.RegisterBatchFinish(batchResult, startedAt);
                _stateStore.ReleaseTableLock(advisoryLockKey);
            }
        }

        private int ProcessDeletes(SyncTableDefinition table, Guid batchId, SourceReadResult source, SyncBatchResult batchResult)
        {
            var deleted = 0;

            if (table.DeleteStrategy == DeleteStrategy.FullSnapshotReconcile && table.AllowFullSnapshotDeleteReconcile && source != null && source.IsCompleteSnapshotPage)
            {
                var snapshotKeys = source.SnapshotKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var tombstones = _applier.BuildSnapshotReconcileTombstones(table, snapshotKeys, batchId);
                if (tombstones.Count > 0)
                {
                    _stateStore.SaveTombstones(batchId, table.Name, tombstones);
                }
            }

            if (table.DeleteStrategy == DeleteStrategy.TombstoneTable || table.DeleteStrategy == DeleteStrategy.FullSnapshotReconcile)
            {
                var pending = _stateStore.GetPendingTombstones(table.Name, Math.Max(1, _options.BatchSize));
                if (pending.Count > 0)
                {
                    var applied = _applier.ApplyPendingTombstones(table, pending, _options.DryRun);
                    deleted += applied.RowsDeleted;
                    if (applied.Rejections != null && applied.Rejections.Count > 0)
                    {
                        _stateStore.SaveRejections(batchId, table.Name, applied.Rejections);
                        batchResult.RowsRejected += applied.Rejections.Count;
                        _stateStore.MarkTombstonesApplied(pending, "Error aplicando tombstones: " + applied.Rejections[0].Error);
                    }
                    else
                    {
                        _stateStore.MarkTombstonesApplied(pending, null);
                    }
                }
            }

            return deleted;
        }
    }
}
