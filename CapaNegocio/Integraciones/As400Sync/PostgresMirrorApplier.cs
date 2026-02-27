using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using CapaNegocio.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class PostgresMirrorApplier : IMirrorApplier
    {
        private readonly string _connectionString;
        private readonly int _commandTimeoutSeconds;
        private readonly ILoggingService _logger;

        public PostgresMirrorApplier(string connectionString, int commandTimeoutSeconds, ILoggingService logger = null)
        {
            _connectionString = connectionString;
            _commandTimeoutSeconds = commandTimeoutSeconds <= 0 ? 60 : commandTimeoutSeconds;
            _logger = logger ?? LoggingServiceFactory.Create();
        }

        public MirrorApplyResult ApplyUpserts(SyncTableDefinition table, Guid batchId, IList<MirrorSyncRow> rows, bool dryRun)
        {
            var result = new MirrorApplyResult();
            if (table == null || rows == null || rows.Count == 0)
            {
                return result;
            }

            if (dryRun)
            {
                result.RowsApplied = rows.Count;
                return result;
            }

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    foreach (var row in rows)
                    {
                        try
                        {
                            UpsertOne(conn, tx, table, batchId, row);
                            result.RowsApplied++;
                        }
                        catch (Exception ex)
                        {
                            result.Rejections.Add(new MirrorApplyRejection
                            {
                                TableName = table.Name,
                                SourceKey = row != null ? row.SourceKey : null,
                                Payload = row != null ? row.Values : null,
                                Error = ex.Message
                            });
                            _logger.LogWarning("Rechazo mirror upsert " + table.Name + " key=" + (row != null ? row.SourceKey : "N/A") + ": " + ex.Message);
                        }
                    }
                    tx.Commit();
                }
            }

            return result;
        }

        public MirrorApplyResult ApplyPendingTombstones(SyncTableDefinition table, IList<SyncTombstone> tombstones, bool dryRun)
        {
            var result = new MirrorApplyResult();
            if (table == null || tombstones == null || tombstones.Count == 0)
            {
                return result;
            }

            if (dryRun)
            {
                result.RowsDeleted = tombstones.Count;
                return result;
            }

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    foreach (var tomb in tombstones)
                    {
                        try
                        {
                            var affected = DeleteByPrimaryKey(conn, tx, table, tomb.PrimaryKeyPayload);
                            result.RowsDeleted += affected;
                        }
                        catch (Exception ex)
                        {
                            result.Rejections.Add(new MirrorApplyRejection
                            {
                                TableName = table.Name,
                                SourceKey = tomb != null ? tomb.SourceReference : null,
                                Payload = tomb != null ? tomb.PrimaryKeyPayload : null,
                                Error = ex.Message
                            });
                        }
                    }
                    tx.Commit();
                }
            }

            return result;
        }

        public IList<SyncTombstone> BuildSnapshotReconcileTombstones(SyncTableDefinition table, ISet<string> currentSourceKeys, Guid batchId)
        {
            var tombstones = new List<SyncTombstone>();
            if (table == null || table.PrimaryKeys == null || table.PrimaryKeys.Count == 0)
            {
                return tombstones;
            }

            currentSourceKeys = currentSourceKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                var pkCols = table.PrimaryKeys.ToList();
                var sql = "SELECT " + string.Join(", ", pkCols.Select(QuoteIdentifier)) +
                          " FROM " + QuoteQualified(table.TargetSchema, table.TargetTable) +
                          " WHERE COALESCE(_is_deleted, false) = false";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = _commandTimeoutSeconds;
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            for (var i = 0; i < pkCols.Count; i++)
                            {
                                payload[pkCols[i]] = rd.IsDBNull(i) ? null : rd.GetValue(i);
                            }

                            var key = BuildPkKey(payload, pkCols);
                            if (!currentSourceKeys.Contains(key))
                            {
                                tombstones.Add(new SyncTombstone
                                {
                                    TableName = table.Name,
                                    PrimaryKeyPayload = payload,
                                    SourceDeletedAt = DateTime.UtcNow,
                                    SourceReference = key
                                });
                            }
                        }
                    }
                }
            }

            return tombstones;
        }

        private void UpsertOne(NpgsqlConnection conn, NpgsqlTransaction tx, SyncTableDefinition table, Guid batchId, MirrorSyncRow row)
        {
            var baseValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in row.Values)
            {
                baseValues[kv.Key] = kv.Value;
            }

            baseValues["_source_updated_at"] = (object)row.SourceUpdatedAt ?? DBNull.Value;
            baseValues["_source_op"] = row.SourceOperation ?? "U";
            baseValues["_is_deleted"] = row.SourceIsDeleted.HasValue ? (object)row.SourceIsDeleted.Value : false;
            baseValues["_mirror_batch_id"] = batchId;
            baseValues["_mirror_synced_at"] = DateTime.UtcNow;
            baseValues["_row_hash"] = ComputeRowHash(row.Values);

            var columns = baseValues.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            var pkCols = table.PrimaryKeys.Select(x => x.ToLowerInvariant()).ToList();
            var nonPkCols = columns.Where(c => !pkCols.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

            if (pkCols.Count == 0)
            {
                throw new InvalidOperationException("Tabla sin PK configurada: " + table.Name);
            }

            var sql = new StringBuilder();
            sql.Append("INSERT INTO ");
            sql.Append(QuoteQualified(table.TargetSchema, table.TargetTable));
            sql.Append(" (");
            sql.Append(string.Join(", ", columns.Select(QuoteIdentifier)));
            sql.Append(") VALUES (");
            sql.Append(string.Join(", ", columns.Select((c, i) => "@p" + i)));
            sql.Append(") ON CONFLICT (");
            sql.Append(string.Join(", ", pkCols.Select(QuoteIdentifier)));
            sql.Append(") DO UPDATE SET ");
            sql.Append(string.Join(", ", nonPkCols.Select(c => QuoteIdentifier(c) + " = EXCLUDED." + QuoteIdentifier(c))));

            using (var cmd = new NpgsqlCommand(sql.ToString(), conn, tx))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                for (var i = 0; i < columns.Count; i++)
                {
                    var value = baseValues[columns[i]];
                    cmd.Parameters.AddWithValue("p" + i, value ?? DBNull.Value);
                }
                cmd.ExecuteNonQuery();
            }
        }

        private int DeleteByPrimaryKey(NpgsqlConnection conn, NpgsqlTransaction tx, SyncTableDefinition table, IDictionary<string, object> pkPayload)
        {
            if (pkPayload == null || pkPayload.Count == 0)
            {
                return 0;
            }

            var pkCols = table.PrimaryKeys.Select(x => x.ToLowerInvariant()).ToList();
            var where = new List<string>();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.Transaction = tx;
                cmd.CommandTimeout = _commandTimeoutSeconds;

                for (var i = 0; i < pkCols.Count; i++)
                {
                    var col = pkCols[i];
                    object value;
                    if (!pkPayload.TryGetValue(col, out value))
                    {
                        throw new InvalidOperationException("PK incompleta para delete: falta " + col + " en " + table.Name);
                    }

                    var paramName = "p" + i;
                    where.Add(QuoteIdentifier(col) + " = @" + paramName);
                    cmd.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
                }

                cmd.CommandText = "DELETE FROM " + QuoteQualified(table.TargetSchema, table.TargetTable) + " WHERE " + string.Join(" AND ", where);
                return cmd.ExecuteNonQuery();
            }
        }

        private static string ComputeRowHash(IDictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }

            var ordered = values.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Key + "=" + ToInvariantString(kv.Value));
            var payload = string.Join("|", ordered);

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(payload);
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        private static string ToInvariantString(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            var formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string BuildPkKey(IDictionary<string, object> payload, IList<string> pkCols)
        {
            return string.Join("|", pkCols.Select(pk => pk + "=" + ToInvariantString(payload.ContainsKey(pk) ? payload[pk] : null)));
        }

        private static string QuoteQualified(string schema, string table)
        {
            return QuoteIdentifier(schema) + "." + QuoteIdentifier(table);
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "\"" + (identifier ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
