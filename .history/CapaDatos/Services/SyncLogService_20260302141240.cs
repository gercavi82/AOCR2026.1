using System;
using System.Collections.Generic;
using System.Globalization;
using Npgsql;
using NpgsqlTypes;

namespace CapaDatos.Services
{
    /// <summary>
    /// Registro de operación de sincronización para auditoría completa.
    /// </summary>
    public class SyncLogEntry
    {
        public int Id { get; set; }
        public string Operacion { get; set; }
        public string SistemaOrigen { get; set; }
        public string SistemaDestino { get; set; }
        public int? OrdenId { get; set; }
        public int? PagoId { get; set; }
        public string IdempotencyKey { get; set; }
        public string Estado { get; set; }
        public string DetalleRequest { get; set; }
        public string DetalleResponse { get; set; }
        public string ErrorMensaje { get; set; }
        public string ErrorCodigo { get; set; }
        public int Intentos { get; set; }
        public int MaxIntentos { get; set; }
        public DateTime? ProximoReintento { get; set; }
        public DateTime InicioOperacion { get; set; }
        public DateTime? FinOperacion { get; set; }
        public long? DuracionMs { get; set; }
        public string Usuario { get; set; }
        public string IpOrigen { get; set; }
        public string CorrelacionId { get; set; }
        public string Fr3Numero { get; set; }
        public decimal? Fr3Secuencial { get; set; }
        public string Fr3Aeropuerto { get; set; }
        public string Fr3Anio { get; set; }
        public string Metadata { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    /// <summary>
    /// Servicio de logging de sincronización AS400 ↔ PostgreSQL.
    /// Registra cada operación de facturación FR3 con trazabilidad completa.
    /// Thread-safe y resiliente a errores de logging.
    /// </summary>
    public class SyncLogService : Infrastructure.BaseDAO
    {
        private readonly ILoggingService _logger;
        private static volatile bool _schemaEnsured;
        private static readonly object _schemaLock = new object();

        public SyncLogService()
            : base(GetConnectionStringSafe())
        {
            _logger = LoggingServiceFactory.Create();
            EnsureSchema();
        }

        public SyncLogService(string connectionString)
            : base(connectionString)
        {
            _logger = LoggingServiceFactory.Create();
            EnsureSchema();
        }

        #region Registro de Operaciones

        /// <summary>
        /// Inicia el registro de una operación de sincronización.
        /// Retorna el ID del log para actualizar después.
        /// </summary>
        public int IniciarOperacion(
            string operacion,
            int? ordenId = null,
            int? pagoId = null,
            string idempotencyKey = null,
            string usuario = null,
            string ipOrigen = null,
            string detalleRequest = null)
        {
            try
            {
                var correlacionId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();

                return ExecuteWithConnection(conn =>
                {
                    var sql = @"
                        INSERT INTO aocr_sync_log 
                            (operacion, sistema_origen, sistema_destino, orden_id, pago_id, 
                             idempotency_key, estado, detalle_request, usuario, ip_origen, 
                             correlacion_id, inicio_operacion, fecha_creacion, fecha_actualizacion)
                        VALUES 
                            (@operacion, 'AOCR', 'AS400', @ordenId, @pagoId, 
                             @idempotencyKey, 'EN_PROCESO', @detalleRequest, @usuario, @ipOrigen,
                             @correlacionId, NOW(), NOW(), NOW())
                        RETURNING id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@operacion", (object)operacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ordenId", ordenId.HasValue ? (object)ordenId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@pagoId", pagoId.HasValue ? (object)pagoId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@idempotencyKey", (object)idempotencyKey ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@detalleRequest", (object)detalleRequest ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@usuario", (object)usuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ipOrigen", (object)ipOrigen ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@correlacionId", correlacionId);

                        var result = cmd.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SyncLog: No se pudo registrar inicio de operación: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Marca una operación como completada exitosamente.
        /// </summary>
        public void CompletarOperacion(
            int logId,
            string detalleResponse = null,
            string fr3Numero = null,
            decimal? fr3Secuencial = null,
            string fr3Aeropuerto = null,
            string fr3Anio = null)
        {
            if (logId <= 0) return;

            try
            {
                ExecuteWithConnection(conn =>
                {
                    var sql = @"
                        UPDATE aocr_sync_log SET 
                            estado = 'COMPLETADO',
                            detalle_response = @response,
                            fin_operacion = NOW(),
                            duracion_ms = EXTRACT(EPOCH FROM (NOW() - inicio_operacion)) * 1000,
                            fr3_numero = @fr3Numero,
                            fr3_secuencial = @fr3Sec,
                            fr3_aeropuerto = @fr3Aer,
                            fr3_anio = @fr3Anio,
                            intentos = intentos + 1,
                            fecha_actualizacion = NOW()
                        WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", logId);
                        cmd.Parameters.AddWithValue("@response", (object)detalleResponse ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fr3Numero", (object)fr3Numero ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fr3Sec", fr3Secuencial.HasValue ? (object)fr3Secuencial.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@fr3Aer", (object)fr3Aeropuerto ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fr3Anio", (object)fr3Anio ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SyncLog: No se pudo completar log " + logId + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Marca una operación como fallida.
        /// </summary>
        public void FallarOperacion(
            int logId,
            string errorMensaje,
            string errorCodigo = null,
            bool programarReintento = false)
        {
            if (logId <= 0) return;

            try
            {
                ExecuteWithConnection(conn =>
                {
                    var sql = @"
                        UPDATE aocr_sync_log SET 
                            estado = CASE WHEN @programarReintento AND intentos < max_intentos THEN 'REINTENTANDO' ELSE 'ERROR' END,
                            error_mensaje = @errorMsg,
                            error_codigo = @errorCode,
                            fin_operacion = NOW(),
                            duracion_ms = EXTRACT(EPOCH FROM (NOW() - inicio_operacion)) * 1000,
                            intentos = intentos + 1,
                            proximo_reintento = CASE 
                                WHEN @programarReintento AND intentos < max_intentos 
                                THEN NOW() + (POWER(2, intentos) * INTERVAL '1 minute')
                                ELSE NULL 
                            END,
                            fecha_actualizacion = NOW()
                        WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", logId);
                        cmd.Parameters.AddWithValue("@errorMsg", (object)errorMensaje ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@errorCode", (object)errorCodigo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@programarReintento", programarReintento);
                        cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SyncLog: No se pudo registrar fallo de log " + logId + ": " + ex.Message);
            }
        }

        #endregion

        #region Consultas

        /// <summary>
        /// Obtiene el log de sincronización por orden.
        /// </summary>
        public List<SyncLogEntry> ObtenerPorOrden(int ordenId, int limit = 20)
        {
            return ExecuteWithConnection(conn =>
            {
                var entries = new List<SyncLogEntry>();
                var sql = @"
                    SELECT id, operacion, estado, error_mensaje, error_codigo, intentos,
                           inicio_operacion, fin_operacion, duracion_ms, fr3_numero,
                           fr3_secuencial, fr3_aeropuerto, fr3_anio, usuario, correlacion_id
                    FROM aocr_sync_log 
                    WHERE orden_id = @ordenId
                    ORDER BY fecha_creacion DESC 
                    LIMIT @limit";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ordenId", ordenId);
                    cmd.Parameters.AddWithValue("@limit", limit);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            entries.Add(MapearEntry(reader));
                        }
                    }
                }
                return entries;
            });
        }

        /// <summary>
        /// Obtiene operaciones pendientes de reintento.
        /// </summary>
        public List<SyncLogEntry> ObtenerPendientesReintento(int limit = 50)
        {
            return ExecuteWithConnection(conn =>
            {
                var entries = new List<SyncLogEntry>();
                var sql = @"
                    SELECT id, operacion, estado, orden_id, pago_id, error_mensaje, 
                           error_codigo, intentos, max_intentos, proximo_reintento,
                           inicio_operacion, usuario, correlacion_id, idempotency_key,
                           fr3_numero, fr3_secuencial, fr3_aeropuerto, fr3_anio
                    FROM aocr_sync_log 
                    WHERE estado = 'REINTENTANDO'
                      AND proximo_reintento <= NOW()
                      AND intentos < max_intentos
                    ORDER BY proximo_reintento ASC
                    LIMIT @limit";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@limit", limit);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            entries.Add(MapearEntryCompleto(reader));
                        }
                    }
                }
                return entries;
            });
        }

        /// <summary>
        /// Estadísticas de sincronización para dashboard.
        /// </summary>
        public SyncStats ObtenerEstadisticas(int diasAtras = 30)
        {
            return ExecuteWithConnection(conn =>
            {
                var stats = new SyncStats();
                var sql = @"
                    SELECT 
                        COUNT(*) as total,
                        SUM(CASE WHEN estado = 'COMPLETADO' THEN 1 ELSE 0 END) as completados,
                        SUM(CASE WHEN estado = 'ERROR' THEN 1 ELSE 0 END) as errores,
                        SUM(CASE WHEN estado = 'REINTENTANDO' THEN 1 ELSE 0 END) as reintentando,
                        SUM(CASE WHEN estado = 'EN_PROCESO' THEN 1 ELSE 0 END) as en_proceso,
                        AVG(duracion_ms) FILTER (WHERE estado = 'COMPLETADO') as duracion_promedio,
                        MAX(fecha_creacion) as ultimo_registro
                    FROM aocr_sync_log 
                    WHERE fecha_creacion > NOW() - MAKE_INTERVAL(days => @dias)";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@dias", diasAtras);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.Total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            stats.Completados = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetInt64(1));
                            stats.Errores = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetInt64(2));
                            stats.Reintentando = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetInt64(3));
                            stats.EnProceso = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetInt64(4));
                            stats.DuracionPromedioMs = reader.IsDBNull(5) ? 0 : Convert.ToInt64(reader.GetDecimal(5));
                            stats.UltimoRegistro = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
                        }
                    }
                }

                if (stats.Total > 0)
                {
                    stats.TasaExito = Math.Round((decimal)stats.Completados / stats.Total * 100, 1);
                }

                return stats;
            });
        }

        #endregion

        #region Helpers

        private static SyncLogEntry MapearEntry(NpgsqlDataReader reader)
        {
            return new SyncLogEntry
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Operacion = GetStringSafe(reader, "operacion"),
                Estado = GetStringSafe(reader, "estado"),
                ErrorMensaje = GetStringSafe(reader, "error_mensaje"),
                ErrorCodigo = GetStringSafe(reader, "error_codigo"),
                Intentos = GetIntSafe(reader, "intentos"),
                InicioOperacion = GetDateTimeSafe(reader, "inicio_operacion"),
                FinOperacion = GetNullableDateTimeSafe(reader, "fin_operacion"),
                DuracionMs = GetNullableLongSafe(reader, "duracion_ms"),
                Fr3Numero = GetStringSafe(reader, "fr3_numero"),
                Usuario = GetStringSafe(reader, "usuario"),
                CorrelacionId = GetStringSafe(reader, "correlacion_id")
            };
        }

        private static SyncLogEntry MapearEntryCompleto(NpgsqlDataReader reader)
        {
            var entry = new SyncLogEntry
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Operacion = GetStringSafe(reader, "operacion"),
                Estado = GetStringSafe(reader, "estado"),
                ErrorMensaje = GetStringSafe(reader, "error_mensaje"),
                ErrorCodigo = GetStringSafe(reader, "error_codigo"),
                Intentos = GetIntSafe(reader, "intentos"),
                MaxIntentos = GetIntSafe(reader, "max_intentos"),
                InicioOperacion = GetDateTimeSafe(reader, "inicio_operacion"),
                Usuario = GetStringSafe(reader, "usuario"),
                CorrelacionId = GetStringSafe(reader, "correlacion_id"),
                IdempotencyKey = GetStringSafe(reader, "idempotency_key")
            };

            var ordOrdId = TryGetOrdinal(reader, "orden_id");
            if (ordOrdId >= 0 && !reader.IsDBNull(ordOrdId))
                entry.OrdenId = reader.GetInt32(ordOrdId);

            var ordPagoId = TryGetOrdinal(reader, "pago_id");
            if (ordPagoId >= 0 && !reader.IsDBNull(ordPagoId))
                entry.PagoId = reader.GetInt32(ordPagoId);

            return entry;
        }

        private static string GetStringSafe(NpgsqlDataReader reader, string column)
        {
            var ord = TryGetOrdinal(reader, column);
            return ord >= 0 && !reader.IsDBNull(ord) ? reader.GetString(ord) : null;
        }

        private static int GetIntSafe(NpgsqlDataReader reader, string column)
        {
            var ord = TryGetOrdinal(reader, column);
            return ord >= 0 && !reader.IsDBNull(ord) ? reader.GetInt32(ord) : 0;
        }

        private static DateTime GetDateTimeSafe(NpgsqlDataReader reader, string column)
        {
            var ord = TryGetOrdinal(reader, column);
            return ord >= 0 && !reader.IsDBNull(ord) ? reader.GetDateTime(ord) : DateTime.MinValue;
        }

        private static DateTime? GetNullableDateTimeSafe(NpgsqlDataReader reader, string column)
        {
            var ord = TryGetOrdinal(reader, column);
            return ord >= 0 && !reader.IsDBNull(ord) ? reader.GetDateTime(ord) : (DateTime?)null;
        }

        private static long? GetNullableLongSafe(NpgsqlDataReader reader, string column)
        {
            var ord = TryGetOrdinal(reader, column);
            return ord >= 0 && !reader.IsDBNull(ord) ? reader.GetInt64(ord) : (long?)null;
        }

        private static int TryGetOrdinal(NpgsqlDataReader reader, string column)
        {
            try { return reader.GetOrdinal(column); }
            catch { return -1; }
        }

        private void EnsureSchema()
        {
            if (_schemaEnsured) return;
            lock (_schemaLock)
            {
                if (_schemaEnsured) return;
                try
                {
                    ExecuteWithConnection(conn =>
                    {
                        var sql = @"
                            CREATE TABLE IF NOT EXISTS aocr_sync_log (
                                id                  SERIAL PRIMARY KEY,
                                operacion           VARCHAR(50) NOT NULL,
                                sistema_origen      VARCHAR(20) NOT NULL DEFAULT 'AOCR',
                                sistema_destino     VARCHAR(20) NOT NULL DEFAULT 'AS400',
                                orden_id            INTEGER,
                                pago_id             INTEGER,
                                idempotency_key     VARCHAR(128),
                                estado              VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
                                detalle_request     TEXT,
                                detalle_response    TEXT,
                                error_mensaje       TEXT,
                                error_codigo        VARCHAR(50),
                                intentos            INTEGER NOT NULL DEFAULT 0,
                                max_intentos        INTEGER NOT NULL DEFAULT 3,
                                proximo_reintento   TIMESTAMP WITH TIME ZONE,
                                inicio_operacion    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                                fin_operacion       TIMESTAMP WITH TIME ZONE,
                                duracion_ms         BIGINT,
                                usuario             VARCHAR(100),
                                ip_origen           VARCHAR(45),
                                correlacion_id      VARCHAR(100),
                                fr3_numero          VARCHAR(50),
                                fr3_secuencial      NUMERIC(15,0),
                                fr3_aeropuerto      VARCHAR(10),
                                fr3_anio            VARCHAR(4),
                                metadata            TEXT,
                                fecha_creacion      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                                fecha_actualizacion TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                            );
                            CREATE INDEX IF NOT EXISTS idx_sync_log_orden ON aocr_sync_log(orden_id);
                            CREATE INDEX IF NOT EXISTS idx_sync_log_estado ON aocr_sync_log(estado);
                            CREATE INDEX IF NOT EXISTS idx_sync_log_operacion ON aocr_sync_log(operacion);
                            CREATE INDEX IF NOT EXISTS idx_sync_log_idempotency ON aocr_sync_log(idempotency_key) WHERE idempotency_key IS NOT NULL;";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    });
                    _schemaEnsured = true;
                    _logger.LogInfo("SyncLogService: Tabla aocr_sync_log verificada/creada.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SyncLogService: Error al crear tabla: " + ex.Message);
                }
            }
        }

        private static string GetConnectionStringSafe()
        {
            var config = System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"];
            return config != null ? config.ConnectionString : string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// Estadísticas de sincronización.
    /// </summary>
    public class SyncStats
    {
        public int Total { get; set; }
        public int Completados { get; set; }
        public int Errores { get; set; }
        public int Reintentando { get; set; }
        public int EnProceso { get; set; }
        public long DuracionPromedioMs { get; set; }
        public decimal TasaExito { get; set; }
        public DateTime? UltimoRegistro { get; set; }
    }
}
