using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Npgsql;
using CapaNegocio.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class PostgresSyncStateStore : ISyncStateStore
    {
        private readonly string _connectionString;
        private readonly int _commandTimeoutSeconds;
        private readonly ILoggingService _logger;

        public PostgresSyncStateStore(string connectionString, int commandTimeoutSeconds, ILoggingService logger = null)
        {
            _connectionString = connectionString;
            _commandTimeoutSeconds = commandTimeoutSeconds <= 0 ? 60 : commandTimeoutSeconds;
            _logger = logger ?? LoggingServiceFactory.Create();
        }

        public SyncWatermarkState GetWatermark(string tableName)
        {
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(@"
                SELECT table_name, last_success_ts, last_success_key, last_batch_id, status, last_error, updated_at
                FROM sync.watermark
                WHERE table_name = @tableName", conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                cmd.Parameters.AddWithValue("tableName", tableName);
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        return new SyncWatermarkState
                        {
                            TableName = rd.GetString(0),
                            LastSuccessTs = rd.IsDBNull(1) ? (DateTime?)null : rd.GetDateTime(1),
                            LastSuccessKey = rd.IsDBNull(2) ? null : rd.GetString(2),
                            LastBatchId = rd.IsDBNull(3) ? (Guid?)null : rd.GetGuid(3),
                            Status = rd.IsDBNull(4) ? null : rd.GetString(4),
                            LastError = rd.IsDBNull(5) ? null : rd.GetString(5),
                            UpdatedAt = rd.IsDBNull(6) ? DateTime.UtcNow : rd.GetDateTime(6)
                        };
                    }
                }
            }

            return new SyncWatermarkState
            {
                TableName = tableName,
                Status = "OK",
                UpdatedAt = DateTime.UtcNow
            };
        }

        public bool TryAcquireTableLock(string tableName, Guid batchId, out long advisoryLockKey)
        {
            advisoryLockKey = ComputeAdvisoryLockKey(tableName);
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(@"
                INSERT INTO sync.watermark (table_name, last_batch_id, status, updated_at)
                VALUES (@table_name, @batch_id, 'RUNNING', now())
                ON CONFLICT (table_name) DO UPDATE
                   SET last_batch_id = EXCLUDED.last_batch_id,
                       status = 'RUNNING',
                       last_error = NULL,
                       updated_at = now()
                 WHERE COALESCE(sync.watermark.status, 'OK') <> 'RUNNING'
                    OR sync.watermark.updated_at < (now() - interval '30 minutes')
                RETURNING 1;", conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                cmd.Parameters.AddWithValue("table_name", tableName);
                cmd.Parameters.AddWithValue("batch_id", batchId);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }

        public void ReleaseTableLock(long advisoryLockKey)
        {
            // Lock lógico basado en sync.watermark.status; no requiere unlock explícito de sesión.
        }

        public void MarkRunning(string tableName, Guid batchId)
        {
            UpsertWatermarkStatus(tableName, batchId, "RUNNING", null, null, null);
        }

        public void MarkSuccess(string tableName, Guid batchId, DateTime? lastSuccessTs, string lastSuccessKey)
        {
            UpsertWatermarkStatus(tableName, batchId, "OK", lastSuccessTs, lastSuccessKey, null);
        }

        public void MarkError(string tableName, Guid batchId, string error)
        {
            UpsertWatermarkStatus(tableName, batchId, "ERROR", null, null, Truncate(error, 4000));
        }

        public void RegisterBatchStart(Guid batchId, string tableName, DateTime startedAt)
        {
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(@"
                INSERT INTO sync.batch_log (batch_id, started_at, table_name, status)
                VALUES (@batch_id, @started_at, @table_name, 'RUNNING')
                ON CONFLICT (batch_id) DO NOTHING", conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                cmd.Parameters.AddWithValue("batch_id", batchId);
                cmd.Parameters.AddWithValue("started_at", startedAt);
                cmd.Parameters.AddWithValue("table_name", tableName);
                cmd.ExecuteNonQuery();
            }
        }

        public void RegisterBatchFinish(SyncBatchResult result, DateTime startedAt)
        {
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(@"
                UPDATE sync.batch_log
                   SET ended_at = @ended_at,
                       rows_read = @rows_read,
                       rows_applied = @rows_applied,
                       rows_rejected = @rows_rejected,
                       rows_deleted = @rows_deleted,
                       latency_ms = @latency_ms,
                       status = @status,
                       error = @error
                 WHERE batch_id = @batch_id", conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                var endedAt = startedAt + (result != null ? result.Duration : TimeSpan.Zero);
                cmd.Parameters.AddWithValue("ended_at", endedAt);
                cmd.Parameters.AddWithValue("rows_read", result != null ? result.RowsRead : 0);
                cmd.Parameters.AddWithValue("rows_applied", result != null ? result.RowsApplied : 0);
                cmd.Parameters.AddWithValue("rows_rejected", result != null ? result.RowsRejected : 0);
                cmd.Parameters.AddWithValue("rows_deleted", result != null ? result.RowsDeleted : 0);
                cmd.Parameters.AddWithValue("latency_ms", result != null ? (object)(long)result.Duration.TotalMilliseconds : DBNull.Value);
                cmd.Parameters.AddWithValue("status", (object)(result != null ? result.Status : "ERROR") ?? "ERROR");
                cmd.Parameters.AddWithValue("error", (object)Truncate(result != null ? result.Error : null, 4000) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("batch_id", result != null ? result.BatchId : Guid.Empty);
                cmd.ExecuteNonQuery();
            }
        }

        public void SaveRejections(Guid batchId, string tableName, IList<MirrorApplyRejection> rejections)
        {
            if (rejections == null || rejections.Count == 0)
            {
                return;
            }

            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                foreach (var r in rejections)
                {
                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO sync.rejections (batch_id, table_name, payload, error)
                        VALUES (@batch_id, @table_name, CAST(@payload AS jsonb), @error)", conn, tx))
                    {
                        cmd.CommandTimeout = _commandTimeoutSeconds;
                        cmd.Parameters.AddWithValue("batch_id", batchId);
                        cmd.Parameters.AddWithValue("table_name", tableName ?? (r != null ? r.TableName : null) ?? string.Empty);
                        cmd.Parameters.AddWithValue("payload", JsonConvert.SerializeObject(r != null ? r.Payload : null));
                        cmd.Parameters.AddWithValue("error", Truncate(r != null ? r.Error : "Unknown rejection", 4000));
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        public IList<SyncTombstone> GetPendingTombstones(string tableName, int take)
        {
            var items = new List<SyncTombstone>();
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(@"
                SELECT id, table_name, pk_payload::text, source_deleted_at, source_reference, error
                  FROM sync.tombstones
                 WHERE table_name = @table_name
                   AND applied = false
                 ORDER BY created_at, id
                 LIMIT @take", conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                cmd.Parameters.AddWithValue("table_name", tableName);
                cmd.Parameters.AddWithValue("take", Math.Max(1, take));
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var payloadText = rd.IsDBNull(2) ? "{}" : rd.GetString(2);
                        var payload = JsonConvert.DeserializeObject<Dictionary<string, object>>(payloadText)
                                     ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        NormalizeDictionary(payload);

                        items.Add(new SyncTombstone
                        {
                            Id = rd.GetInt64(0),
                            TableName = rd.GetString(1),
                            PrimaryKeyPayload = payload,
                            SourceDeletedAt = rd.IsDBNull(3) ? (DateTime?)null : rd.GetDateTime(3),
                            SourceReference = rd.IsDBNull(4) ? null : rd.GetString(4),
                            Error = rd.IsDBNull(5) ? null : rd.GetString(5)
                        });
                    }
                }
            }
            return items;
        }

        public void SaveTombstones(Guid batchId, string tableName, IList<SyncTombstone> tombstones)
        {
            if (tombstones == null || tombstones.Count == 0)
            {
                return;
            }

            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                foreach (var t in tombstones)
                {
                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO sync.tombstones (table_name, pk_payload, source_deleted_at, source_reference, batch_id, applied)
                        VALUES (@table_name, CAST(@pk_payload AS jsonb), @source_deleted_at, @source_reference, @batch_id, false)", conn, tx))
                    {
                        cmd.CommandTimeout = _commandTimeoutSeconds;
                        cmd.Parameters.AddWithValue("table_name", tableName ?? (t != null ? t.TableName : null) ?? string.Empty);
                        cmd.Parameters.AddWithValue("pk_payload", JsonConvert.SerializeObject(t != null ? t.PrimaryKeyPayload : null));
                        cmd.Parameters.AddWithValue("source_deleted_at", (object)(t != null ? t.SourceDeletedAt : null) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("source_reference", (object)(t != null ? t.SourceReference : null) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("batch_id", batchId);
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        public void MarkTombstonesApplied(IList<SyncTombstone> tombstones, string error)
        {
            if (tombstones == null || tombstones.Count == 0)
            {
                return;
            }

            var ids = tombstones.Select(t => t.Id).Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                foreach (var id in ids)
                {
                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE sync.tombstones
                           SET applied = @applied,
                               applied_at = CASE WHEN @applied THEN now() ELSE applied_at END,
                               error = @error
                         WHERE id = @id", conn, tx))
                    {
                        cmd.CommandTimeout = _commandTimeoutSeconds;
                        var ok = string.IsNullOrWhiteSpace(error);
                        cmd.Parameters.AddWithValue("applied", ok);
                        cmd.Parameters.AddWithValue("error", (object)Truncate(error, 4000) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        private void UpsertWatermarkStatus(string tableName, Guid batchId, string status, DateTime? lastSuccessTs, string lastSuccessKey, string lastError)
        {
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(@"
                INSERT INTO sync.watermark (table_name, last_success_ts, last_success_key, last_batch_id, status, last_error, updated_at)
                VALUES (@table_name, @last_success_ts, @last_success_key, @last_batch_id, @status, @last_error, now())
                ON CONFLICT (table_name) DO UPDATE
                   SET last_success_ts = COALESCE(EXCLUDED.last_success_ts, sync.watermark.last_success_ts),
                       last_success_key = COALESCE(EXCLUDED.last_success_key, sync.watermark.last_success_key),
                       last_batch_id = EXCLUDED.last_batch_id,
                       status = EXCLUDED.status,
                       last_error = EXCLUDED.last_error,
                       updated_at = now()", conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                cmd.Parameters.AddWithValue("table_name", tableName);
                cmd.Parameters.AddWithValue("last_success_ts", (object)lastSuccessTs ?? DBNull.Value);
                cmd.Parameters.AddWithValue("last_success_key", (object)lastSuccessKey ?? DBNull.Value);
                cmd.Parameters.AddWithValue("last_batch_id", batchId);
                cmd.Parameters.AddWithValue("status", status ?? "OK");
                cmd.Parameters.AddWithValue("last_error", (object)lastError ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private NpgsqlConnection Open()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("No existe connection string PostgreSQL para mirror sync.");
            }

            var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private static long ComputeAdvisoryLockKey(string tableName)
        {
            unchecked
            {
                const ulong fnvOffset = 14695981039346656037UL;
                const ulong fnvPrime = 1099511628211UL;
                ulong hash = fnvOffset;
                var bytes = System.Text.Encoding.UTF8.GetBytes((tableName ?? string.Empty).ToUpperInvariant());
                for (var i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= fnvPrime;
                }
                return (long)hash;
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private static void NormalizeDictionary(IDictionary<string, object> dict)
        {
            if (dict == null) return;
            var keys = dict.Keys.ToList();
            foreach (var key in keys)
            {
                var lowered = (key ?? string.Empty).ToLowerInvariant();
                if (lowered != key)
                {
                    var value = dict[key];
                    dict.Remove(key);
                    dict[lowered] = NormalizeJsonValue(value);
                }
                else
                {
                    dict[key] = NormalizeJsonValue(dict[key]);
                }
            }
        }

        private static object NormalizeJsonValue(object value)
        {
            var jArrayType = value != null ? value.GetType().FullName : null;
            if (jArrayType == "Newtonsoft.Json.Linq.JValue")
            {
                dynamic dv = value;
                return dv.Value;
            }
            return value;
        }
    }
}
