using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Linq;
using CapaNegocio.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class As400OdbcSourceReader : IAs400SourceReader
    {
        private readonly string _connectionString;
        private readonly int _commandTimeoutSeconds;
        private readonly ILoggingService _logger;

        public As400OdbcSourceReader(string connectionString, int commandTimeoutSeconds, ILoggingService logger = null)
        {
            _connectionString = connectionString;
            _commandTimeoutSeconds = commandTimeoutSeconds <= 0 ? 60 : commandTimeoutSeconds;
            _logger = logger ?? LoggingServiceFactory.Create();
        }

        public SourceReadResult ReadChanges(SyncTableDefinition table, SyncWatermarkState watermark, int batchSize)
        {
            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            if (table.IncrementalMode == SyncIncrementalMode.Disabled)
            {
                return new SourceReadResult();
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("No existe connection string ODBC AS400 configurado para sincronizacion.");
            }

            using (var conn = new OdbcConnection(_connectionString))
            {
                conn.Open();
                switch (table.IncrementalMode)
                {
                    case SyncIncrementalMode.WatermarkDateTimeChars:
                        return ReadByWatermark(conn, table, watermark, batchSize);
                    case SyncIncrementalMode.FullSnapshot:
                        return ReadFullSnapshot(conn, table);
                    default:
                        throw new NotSupportedException("Modo incremental no soportado: " + table.IncrementalMode);
                }
            }
        }

        private SourceReadResult ReadByWatermark(OdbcConnection conn, SyncTableDefinition table, SyncWatermarkState watermark, int batchSize)
        {
            var result = new SourceReadResult();
            var selectColumns = BuildSelectColumns(table);
            var orderBy = string.Join(", ", new[] { table.WatermarkDateColumn, table.WatermarkTimeColumn }
                .Concat(table.PrimaryKeys.Select(ToSourceColumnName))
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            var sql = "SELECT " + selectColumns + " FROM " + table.SourceSchema + "." + table.SourceTable;
            var whereParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(table.SourceFilterSql))
            {
                whereParts.Add("(" + table.SourceFilterSql + ")");
            }

            var wm = watermark != null ? watermark.LastSuccessTs : (DateTime?)null;
            if (wm.HasValue)
            {
                whereParts.Add("((TRIM(COALESCE(" + table.WatermarkDateColumn + ", '')) > ?) OR (TRIM(COALESCE(" + table.WatermarkDateColumn + ", '')) = ? AND TRIM(COALESCE(" + table.WatermarkTimeColumn + ", '')) >= ?))");
            }

            if (whereParts.Count > 0)
            {
                sql += " WHERE " + string.Join(" AND ", whereParts);
            }

            sql += " ORDER BY " + orderBy;
            sql += " FETCH FIRST " + Math.Max(1, batchSize) + " ROWS ONLY";

            using (var cmd = new OdbcCommand(sql, conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;

                if (wm.HasValue)
                {
                    var dateValue = wm.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    var timeValue = wm.Value.ToString("HHmmss", CultureInfo.InvariantCulture);
                    AddParameter(cmd, dateValue);
                    AddParameter(cmd, dateValue);
                    AddParameter(cmd, timeValue);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = MapRow(reader, table);
                        if (ShouldSkipByWatermark(row, watermark))
                        {
                            continue;
                        }
                        result.Rows.Add(row);

                        if (row.SourceUpdatedAt.HasValue && (!result.MaxSourceTimestamp.HasValue || row.SourceUpdatedAt > result.MaxSourceTimestamp))
                        {
                            result.MaxSourceTimestamp = row.SourceUpdatedAt;
                        }

                        if (!string.IsNullOrWhiteSpace(row.SourceKey))
                        {
                            result.MaxSourceKey = row.SourceKey;
                        }
                    }
                }
            }

            return result;
        }

        private static bool ShouldSkipByWatermark(MirrorSyncRow row, SyncWatermarkState watermark)
        {
            if (row == null || watermark == null || !watermark.LastSuccessTs.HasValue)
            {
                return false;
            }

            if (!row.SourceUpdatedAt.HasValue)
            {
                return false;
            }

            if (row.SourceUpdatedAt.Value < watermark.LastSuccessTs.Value)
            {
                return true;
            }

            if (row.SourceUpdatedAt.Value > watermark.LastSuccessTs.Value)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(watermark.LastSuccessKey))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.SourceKey))
            {
                return true;
            }

            return string.Compare(row.SourceKey, watermark.LastSuccessKey, StringComparison.OrdinalIgnoreCase) <= 0;
        }

        private SourceReadResult ReadFullSnapshot(OdbcConnection conn, SyncTableDefinition table)
        {
            var result = new SourceReadResult
            {
                IsCompleteSnapshotPage = true,
                SnapshotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            var selectColumns = BuildSelectColumns(table);
            var sql = "SELECT " + selectColumns + " FROM " + table.SourceSchema + "." + table.SourceTable;
            if (!string.IsNullOrWhiteSpace(table.SourceFilterSql))
            {
                sql += " WHERE (" + table.SourceFilterSql + ")";
            }

            if (table.PrimaryKeys != null && table.PrimaryKeys.Count > 0)
            {
                sql += " ORDER BY " + string.Join(", ", table.PrimaryKeys.Select(ToSourceColumnName));
            }

            using (var cmd = new OdbcCommand(sql, conn))
            {
                cmd.CommandTimeout = _commandTimeoutSeconds;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = MapRow(reader, table);
                        result.Rows.Add(row);
                        if (!string.IsNullOrWhiteSpace(row.SourceKey))
                        {
                            result.SnapshotKeys.Add(row.SourceKey);
                            result.MaxSourceKey = row.SourceKey;
                        }
                    }
                }
            }

            return result;
        }

        private MirrorSyncRow MapRow(IDataRecord reader, SyncTableDefinition table)
        {
            var row = new MirrorSyncRow();

            foreach (var col in table.Columns)
            {
                var ordinal = SafeGetOrdinal(reader, col.SourceColumn);
                if (ordinal < 0)
                {
                    continue;
                }

                object value = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
                row.Values[col.TargetColumn] = NormalizeValue(value, col.TrimString);
            }

            if (table.IncrementalMode == SyncIncrementalMode.WatermarkDateTimeChars)
            {
                var dateStr = GetString(reader, table.WatermarkDateColumn);
                var timeStr = GetString(reader, table.WatermarkTimeColumn);
                row.SourceUpdatedAt = ParseAs400DateTime(dateStr, timeStr);
            }

            if (!string.IsNullOrWhiteSpace(table.SoftDeleteSourceColumn))
            {
                var deleteValue = GetString(reader, table.SoftDeleteSourceColumn);
                if (deleteValue != null)
                {
                    row.SourceIsDeleted = !string.Equals((deleteValue ?? string.Empty).Trim(), (table.SoftDeleteActiveValue ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
                }
            }

            row.SourceKey = BuildSourceKey(row, table);
            row.SourceOperation = row.SourceIsDeleted.HasValue && row.SourceIsDeleted.Value ? "D" : "U";
            return row;
        }

        private string BuildSelectColumns(SyncTableDefinition table)
        {
            var cols = new List<string>();
            foreach (var c in table.Columns)
            {
                if (!cols.Contains(c.SourceColumn, StringComparer.OrdinalIgnoreCase))
                {
                    cols.Add(c.SourceColumn);
                }
            }

            if (table.IncrementalMode == SyncIncrementalMode.WatermarkDateTimeChars)
            {
                AddIfMissing(cols, table.WatermarkDateColumn);
                AddIfMissing(cols, table.WatermarkTimeColumn);
            }

            if (!string.IsNullOrWhiteSpace(table.SoftDeleteSourceColumn))
            {
                AddIfMissing(cols, table.SoftDeleteSourceColumn);
            }

            return string.Join(", ", cols);
        }

        private static void AddIfMissing(IList<string> cols, string sourceColumn)
        {
            if (string.IsNullOrWhiteSpace(sourceColumn)) return;
            if (!cols.Contains(sourceColumn, StringComparer.OrdinalIgnoreCase))
            {
                cols.Add(sourceColumn);
            }
        }

        private string BuildSourceKey(MirrorSyncRow row, SyncTableDefinition table)
        {
            if (row == null || table == null || table.PrimaryKeys == null || table.PrimaryKeys.Count == 0)
            {
                return null;
            }

            var parts = new List<string>();
            foreach (var pk in table.PrimaryKeys)
            {
                object value;
                if (!row.Values.TryGetValue(pk, out value))
                {
                    parts.Add(pk + "=");
                    continue;
                }
                parts.Add(pk + "=" + ConvertToInvariantString(value));
            }
            return string.Join("|", parts);
        }

        private static object NormalizeValue(object value, bool trimString)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            var str = value as string;
            if (str != null)
            {
                return trimString ? str.TrimEnd() : str;
            }

            return value;
        }

        private static DateTime? ParseAs400DateTime(string datePart, string timePart)
        {
            datePart = (datePart ?? string.Empty).Trim();
            timePart = (timePart ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(datePart))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(timePart))
            {
                timePart = "000000";
            }
            if (timePart.Length < 6)
            {
                timePart = timePart.PadLeft(6, '0');
            }
            else if (timePart.Length > 6)
            {
                timePart = timePart.Substring(0, 6);
            }

            DateTime parsed;
            if (DateTime.TryParseExact(
                datePart + timePart,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string GetString(IDataRecord reader, string column)
        {
            var ordinal = SafeGetOrdinal(reader, column);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            return value == null || value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int SafeGetOrdinal(IDataRecord reader, string column)
        {
            if (reader == null || string.IsNullOrWhiteSpace(column)) return -1;
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static string ToSourceColumnName(string targetColumn)
        {
            if (string.IsNullOrWhiteSpace(targetColumn)) return targetColumn;
            return targetColumn.ToUpperInvariant();
        }

        private static void AddParameter(OdbcCommand cmd, object value)
        {
            var p = cmd.CreateParameter();
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static string ConvertToInvariantString(object value)
        {
            if (value == null) return string.Empty;
            var formattable = value as IFormattable;
            return formattable != null
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
        }
    }
}
