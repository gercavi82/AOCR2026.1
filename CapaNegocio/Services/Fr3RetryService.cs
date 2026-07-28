using System;
using System.Collections.Generic;
using System.Globalization;
using Npgsql;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio de reintentos para facturación FR3 en AS400.
    /// Gestiona la cola de reintentos con backoff exponencial.
    /// Puede ejecutarse manualmente o desde un job programado.
    /// </summary>
    public class Fr3RetryService
    {
        private readonly ILoggingService _logger;
        private readonly string _connectionString;

        public Fr3RetryService()
        {
            _logger = LoggingServiceFactory.Create();
            var config = System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = config != null ? config.ConnectionString : string.Empty;
            EnsureTable();
        }

        /// <summary>
        /// Crea la tabla aocr_fr3_retry_queue si no existe (idempotente).
        /// Equivalente al bloque DO$$ del script 20260601_sync_audit_idempotency.sql.
        /// </summary>
        private void EnsureTable()
        {
            if (string.IsNullOrWhiteSpace(_connectionString)) return;
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"
                        CREATE TABLE IF NOT EXISTS aocr_fr3_retry_queue (
                            id                  SERIAL PRIMARY KEY,
                            orden_id            INTEGER NOT NULL,
                            pago_id             INTEGER,
                            numero_factura      VARCHAR(50),
                            autorizacion        VARCHAR(100),
                            estado              VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
                            intentos            INTEGER NOT NULL DEFAULT 0,
                            max_intentos        INTEGER NOT NULL DEFAULT 5,
                            ultimo_error        TEXT,
                            proximo_intento     TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                            factor_backoff      INTEGER NOT NULL DEFAULT 1,
                            prioridad           INTEGER NOT NULL DEFAULT 0,
                            usuario_creacion    VARCHAR(100),
                            usuario_ultimo      VARCHAR(100),
                            correlacion_id      VARCHAR(100),
                            fecha_creacion      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                            fecha_actualizacion TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                            fecha_completado    TIMESTAMP WITH TIME ZONE,
                            fr3_numero          VARCHAR(50),
                            fr3_secuencial      NUMERIC(15,0)
                        );
                        CREATE INDEX IF NOT EXISTS idx_fr3_retry_estado   ON aocr_fr3_retry_queue(estado);
                        CREATE INDEX IF NOT EXISTS idx_fr3_retry_proximo  ON aocr_fr3_retry_queue(proximo_intento)
                            WHERE estado IN ('PENDIENTE', 'EN_PROCESO');
                        CREATE INDEX IF NOT EXISTS idx_fr3_retry_orden    ON aocr_fr3_retry_queue(orden_id);";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    _logger.LogInfo("Fr3RetryService: Tabla aocr_fr3_retry_queue verificada/creada.",
                        new LogContext { Controller = "Fr3RetryService", Action = "EnsureTable" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Fr3RetryService EnsureTable error: " + ex.Message);
            }
        }

        #region Encolar reintentos

        /// <summary>
        /// Agrega una orden a la cola de reintentos FR3.
        /// </summary>
        public int Encolar(
            int ordenId,
            int? pagoId = null,
            string numeroFactura = null,
            string autorizacion = null,
            string usuario = null,
            int maxIntentos = 5,
            int prioridad = 0)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Verificar si ya existe en cola activa
                    var sqlCheck = @"
                        SELECT id FROM aocr_fr3_retry_queue 
                        WHERE orden_id = @ordenId 
                          AND estado IN ('PENDIENTE', 'EN_PROCESO')
                        LIMIT 1";

                    using (var cmdCheck = new NpgsqlCommand(sqlCheck, conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@ordenId", ordenId);
                        var existing = cmdCheck.ExecuteScalar();
                        if (existing != null && existing != DBNull.Value)
                        {
                            _logger.LogInfo(string.Format(
                                "FR3 retry: Orden {0} ya está en cola (ID: {1}).", ordenId, existing));
                            return Convert.ToInt32(existing);
                        }
                    }

                    var sql = @"
                        INSERT INTO aocr_fr3_retry_queue 
                            (orden_id, pago_id, numero_factura, autorizacion, estado, 
                             max_intentos, prioridad, usuario_creacion, proximo_intento,
                             correlacion_id, fecha_creacion, fecha_actualizacion)
                        VALUES 
                            (@ordenId, @pagoId, @numFact, @auth, 'PENDIENTE',
                             @maxIntentos, @prioridad, @usuario, NOW(),
                             @correlacion, NOW(), NOW())
                        RETURNING id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ordenId", ordenId);
                        cmd.Parameters.AddWithValue("@pagoId", pagoId.HasValue ? (object)pagoId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@numFact", (object)numeroFactura ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@auth", (object)autorizacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@maxIntentos", maxIntentos);
                        cmd.Parameters.AddWithValue("@prioridad", prioridad);
                        cmd.Parameters.AddWithValue("@usuario", (object)usuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@correlacion",
                            Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant());

                        var result = cmd.ExecuteScalar();
                        var id = result != null ? Convert.ToInt32(result) : 0;

                        _logger.LogInfo(string.Format(
                            "FR3 retry encolado: Orden={0}, QueueId={1}, MaxIntentos={2}",
                            ordenId, id, maxIntentos));

                        return id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "FR3_RETRY_ENCOLAR" });
                return 0;
            }
        }

        #endregion

        #region Procesamiento de cola

        /// <summary>
        /// Procesa todos los reintentos pendientes.
        /// Retorna el número de operaciones procesadas exitosamente.
        /// </summary>
        public Fr3RetryBatchResult ProcesarPendientes(int batchSize = 10)
        {
            var result = new Fr3RetryBatchResult();

            if (!FacturacionAS400Service.IsEnabled())
            {
                result.Mensaje = "Facturación AS400 deshabilitada.";
                return result;
            }

            try
            {
                var pendientes = ObtenerPendientes(batchSize);
                result.TotalEnCola = pendientes.Count;

                foreach (var item in pendientes)
                {
                    var itemResult = ProcesarItem(item);
                    if (itemResult.Exitoso)
                    {
                        result.Exitosos++;
                    }
                    else
                    {
                        result.Fallidos++;
                    }
                    result.Detalle.Add(itemResult);
                }

                result.Mensaje = string.Format(
                    "Procesados {0} de {1}: {2} exitosos, {3} fallidos.",
                    result.Exitosos + result.Fallidos,
                    result.TotalEnCola,
                    result.Exitosos,
                    result.Fallidos);
            }
            catch (Exception ex)
            {
                result.Mensaje = "Error procesando cola: " + ex.Message;
                _logger.LogError(ex, new LogContext { ErrorCode = "FR3_RETRY_BATCH" });
            }

            return result;
        }

        /// <summary>
        /// Procesa un único item de la cola.
        /// </summary>
        private Fr3RetryItemResult ProcesarItem(Fr3RetryQueueItem item)
        {
            var itemResult = new Fr3RetryItemResult
            {
                QueueId = item.Id,
                OrdenId = item.OrdenId,
                NumeroFactura = item.NumeroFactura
            };

            try
            {
                // Marcar como EN_PROCESO
                ActualizarEstado(item.Id, "EN_PROCESO", null);

                var service = new FacturacionAS400Service();
                string mensaje;
                var ok = service.TryReintentarFr3(item.OrdenId, item.UsuarioCreacion ?? "RETRY_JOB", out mensaje);

                if (ok)
                {
                    ActualizarCompletado(item.Id, mensaje);
                    itemResult.Exitoso = true;
                    itemResult.Mensaje = mensaje;

                    _logger.LogInfo(string.Format(
                        "FR3 retry exitoso: Orden={0}, QueueId={1}, Fr3={2}",
                        item.OrdenId, item.Id, mensaje));
                }
                else
                {
                    var nuevoIntento = item.Intentos + 1;
                    var factorBackoff = (int)Math.Pow(2, nuevoIntento);
                    ActualizarFallido(item.Id, mensaje, nuevoIntento, factorBackoff);
                    itemResult.Mensaje = mensaje;

                    _logger.LogWarning(string.Format(
                        "FR3 retry fallido: Orden={0}, QueueId={1}, Intento={2}/{3}, Error={4}",
                        item.OrdenId, item.Id, nuevoIntento, item.MaxIntentos, mensaje));
                }
            }
            catch (Exception ex)
            {
                ActualizarFallido(item.Id, ex.Message, item.Intentos + 1, 1);
                itemResult.Mensaje = ex.Message;

                _logger.LogError(ex, new LogContext
                {
                    ErrorCode = "FR3_RETRY_ITEM",
                    AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "QueueId", item.Id },
                        { "OrdenId", item.OrdenId }
                    }
                });
            }

            return itemResult;
        }

        #endregion

        #region Queries

        private List<Fr3RetryQueueItem> ObtenerPendientes(int limit)
        {
            var items = new List<Fr3RetryQueueItem>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                var sql = @"
                    SELECT id, orden_id, pago_id, numero_factura, autorizacion,
                           intentos, max_intentos, usuario_creacion
                    FROM aocr_fr3_retry_queue
                    WHERE estado IN ('PENDIENTE', 'EN_PROCESO')
                      AND proximo_intento <= NOW()
                      AND intentos < max_intentos
                    ORDER BY prioridad DESC, proximo_intento ASC
                    LIMIT @limit
                    FOR UPDATE SKIP LOCKED";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@limit", limit);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new Fr3RetryQueueItem
                            {
                                Id = reader.GetInt32(0),
                                OrdenId = reader.GetInt32(1),
                                PagoId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                NumeroFactura = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Autorizacion = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Intentos = reader.GetInt32(5),
                                MaxIntentos = reader.GetInt32(6),
                                UsuarioCreacion = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Obtiene estadísticas de la cola de reintentos.
        /// </summary>
        public Fr3RetryStats ObtenerEstadisticas()
        {
            var stats = new Fr3RetryStats();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"
                        SELECT 
                            COUNT(*) FILTER (WHERE estado = 'PENDIENTE') as pendientes,
                            COUNT(*) FILTER (WHERE estado = 'EN_PROCESO') as en_proceso,
                            COUNT(*) FILTER (WHERE estado = 'COMPLETADO') as completados,
                            COUNT(*) FILTER (WHERE estado = 'FALLIDO') as fallidos,
                            COUNT(*) FILTER (WHERE estado = 'CANCELADO') as cancelados,
                            MIN(proximo_intento) FILTER (WHERE estado = 'PENDIENTE') as proximo_intento
                        FROM aocr_fr3_retry_queue";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.Pendientes = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetInt64(0));
                            stats.EnProceso = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetInt64(1));
                            stats.Completados = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetInt64(2));
                            stats.Fallidos = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetInt64(3));
                            stats.Cancelados = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetInt64(4));
                            stats.ProximoIntento = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Fr3RetryService stats error: " + ex.Message);
            }

            return stats;
        }

        /// <summary>
        /// Cancela un item de la cola.
        /// </summary>
        public bool Cancelar(int queueId, string usuario)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"
                        UPDATE aocr_fr3_retry_queue 
                        SET estado = 'CANCELADO', 
                            usuario_ultimo = @usuario,
                            fecha_actualizacion = NOW()
                        WHERE id = @id AND estado IN ('PENDIENTE', 'FALLIDO')";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", queueId);
                        cmd.Parameters.AddWithValue("@usuario", (object)usuario ?? DBNull.Value);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Fr3RetryService cancelar error: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region Helpers privados

        private void ActualizarEstado(int id, string estado, string error)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                var sql = @"
                    UPDATE aocr_fr3_retry_queue 
                    SET estado = @estado, 
                        ultimo_error = COALESCE(@error, ultimo_error),
                        fecha_actualizacion = NOW()
                    WHERE id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@estado", estado);
                    cmd.Parameters.AddWithValue("@error", (object)error ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ActualizarCompletado(int id, string fr3Numero)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                var sql = @"
                    UPDATE aocr_fr3_retry_queue 
                    SET estado = 'COMPLETADO',
                        fr3_numero = @fr3Numero,
                        intentos = intentos + 1,
                        fecha_completado = NOW(),
                        fecha_actualizacion = NOW()
                    WHERE id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@fr3Numero", (object)fr3Numero ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ActualizarFallido(int id, string error, int intentos, int factorBackoff)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                // Backoff: 1min, 5min, 15min, 60min, 240min
                var minutosEspera = Math.Min(factorBackoff * 5, 240);

                var sql = @"
                    UPDATE aocr_fr3_retry_queue 
                    SET estado = CASE WHEN @intentos >= max_intentos THEN 'FALLIDO' ELSE 'PENDIENTE' END,
                        ultimo_error = @error,
                        intentos = @intentos,
                        factor_backoff = @factorBackoff,
                        proximo_intento = NOW() + MAKE_INTERVAL(mins => @minutos),
                        fecha_actualizacion = NOW()
                    WHERE id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@error", (object)error ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@intentos", intentos);
                    cmd.Parameters.AddWithValue("@factorBackoff", factorBackoff);
                    cmd.Parameters.AddWithValue("@minutos", minutosEspera);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion
    }

    #region DTOs

    public class Fr3RetryQueueItem
    {
        public int Id { get; set; }
        public int OrdenId { get; set; }
        public int? PagoId { get; set; }
        public string NumeroFactura { get; set; }
        public string Autorizacion { get; set; }
        public int Intentos { get; set; }
        public int MaxIntentos { get; set; }
        public string UsuarioCreacion { get; set; }
    }

    public class Fr3RetryBatchResult
    {
        public int TotalEnCola { get; set; }
        public int Exitosos { get; set; }
        public int Fallidos { get; set; }
        public string Mensaje { get; set; }
        public List<Fr3RetryItemResult> Detalle { get; set; }

        public Fr3RetryBatchResult()
        {
            Detalle = new List<Fr3RetryItemResult>();
        }
    }

    public class Fr3RetryItemResult
    {
        public int QueueId { get; set; }
        public int OrdenId { get; set; }
        public string NumeroFactura { get; set; }
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
    }

    public class Fr3RetryStats
    {
        public int Pendientes { get; set; }
        public int EnProceso { get; set; }
        public int Completados { get; set; }
        public int Fallidos { get; set; }
        public int Cancelados { get; set; }
        public DateTime? ProximoIntento { get; set; }

        public int Total { get { return Pendientes + EnProceso + Completados + Fallidos + Cancelados; } }
    }

    #endregion
}
