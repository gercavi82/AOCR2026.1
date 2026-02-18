using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using CapaDatos.Infrastructure;
using CapaDatos.Constants;

namespace CapaDatos.Services
{
    #region Entidades

    /// <summary>
    /// Elemento de la cola de correos
    /// </summary>
    /// <remarks>
    /// NOTA: Algunas propiedades pueden no existir en la base de datos en ambientes antiguos.
    /// Para compatibilidad, se usan valores por defecto cuando no están disponibles.
    /// 
    /// Columnas mínimas:
    /// - id, to_address, subject, body, status, solicitud_id, created_at, proximo_intento
    /// 
    /// Columnas extendidas (si existen):
    /// - orden_id, tipo_notificacion, correlation_id, intentos, max_intentos, ultimo_error, fecha_envio
    /// - adjunto_ruta, adjunto_nombre, adjunto_mime
    /// </remarks>
    public class EmailQueueItem
    {
        // Propiedades persistidas en la base de datos
        public int Id { get; set; }
        public string Para { get; set; }
        public string Asunto { get; set; }
        public string Cuerpo { get; set; }
        public string Estado { get; set; } // PENDIENTE, ENVIANDO, ENVIADO, ERROR, CANCELADO
        public DateTime FechaCreacion { get; set; }
        public DateTime? ProximoIntento { get; set; }
        public int? SolicitudId { get; set; } // Columna: solicitud_id (FK a aocr_tbsolicitud)
        public int? OrdenId { get; set; } // Columna: orden_id (FK a aocr_or_orden) si existe
        public int Intentos { get; set; }
        public int MaxIntentos { get; set; }
        public string UltimoError { get; set; }
        public DateTime? FechaEnvio { get; set; }
        public string AdjuntoRuta { get; set; }
        public string AdjuntoNombre { get; set; }
        public string AdjuntoMimeType { get; set; }
        
        // Propiedades solo en memoria (no persistidas en BD)
        public string ParaNombre { get; set; }
        public bool EsHtml { get; set; }
        public byte[] AdjuntoContenido { get; set; }
        public string CorrelationId { get; set; }
        public string NumeroOrden { get; set; }
        public string TipoNotificacion { get; set; }
        public string EventKey { get; set; }
        public string RolOrigen { get; set; }
        public string EstadoFinal { get; set; }
    }

    #endregion

    #region Interface

    /// <summary>
    /// Interface para servicio de cola de correos
    /// </summary>
    public interface IEmailQueueService
    {
        Task<int> EncolarAsync(EmailQueueItem item);
        Task<int> EncolarIdempotenteAsync(EmailQueueItem item);
        Task<int> RegistrarErrorEventoAsync(string eventKey, string asunto, string detalleError, int? solicitudId = null, int? ordenId = null, string tipoNotificacion = null);
        Task ProbarConexionAsync();
        Task<EmailQueueItem> ObtenerSiguienteAsync();
        Task ActualizarEstadoAsync(int id, string estado, string error = null);
        Task MarcarEnviadoAsync(int id, string messageId);
        Task<IEnumerable<EmailQueueItem>> ObtenerPendientesAsync(int limite = 10);
        Task ReprogramarReintentoAsync(int id, TimeSpan delay);
    }

    #endregion

    #region Implementación

    /// <summary>
    /// Implementación de cola de correos persistente
    /// </summary>
    public class EmailQueueService : BaseDAO, IEmailQueueService
    {
        private readonly ILoggingService _logger;
        private bool? _hasExtendedColumns;
        private bool? _hasEventColumns;
        private string _statusColumn;
        private bool? _hasProximoIntentoColumn;
        private bool? _hasCreatedAtColumn;
        private const int DefaultMaxIntentos = 3;

        public EmailQueueService() : this(
            new SecureConfigurationService().GetConnectionString("PostgreSQL")
            ?? new SecureConfigurationService().GetConnectionString("AOCRConnection")
            ?? "")
        {
        }

        public EmailQueueService(string connectionString) : base(connectionString)
        {
            _logger = LoggingServiceFactory.Create();
        }

        public Task ProbarConexionAsync()
        {
            ExecuteWithConnection(conn =>
            {
                ExecuteScalar<int>(conn, "SELECT 1");
                return 1;
            });
            return Task.CompletedTask;
        }

        public async Task<int> EncolarAsync(EmailQueueItem item)
        {
            return ExecuteWithConnection(conn =>
            {
                var hasExtended = HasExtendedColumns(conn);
                var hasEvent = HasEventColumns(conn);
                var statusColumn = GetStatusColumn(conn);
                var sql = hasExtended
                    ? (hasEvent
                        ? string.Format(@"
                        INSERT INTO email_queue (
                            to_address, subject, body, {0},
                            solicitud_id, orden_id, tipo_notificacion, correlation_id, event_key,
                            rol_origen, estado_final,
                            intentos, max_intentos, ultimo_error,
                            adjunto_ruta, adjunto_nombre, adjunto_mime,
                            created_at, proximo_intento
                        ) VALUES (
                            @to_address, @subject, @body, @status,
                            @solicitud_id, @orden_id, @tipo_notificacion, @correlation_id, @event_key,
                            @rol_origen, @estado_final,
                            @intentos, @max_intentos, @ultimo_error,
                            @adjunto_ruta, @adjunto_nombre, @adjunto_mime,
                            @created_at, @proximo_intento
                        ) RETURNING id", statusColumn)
                        : string.Format(@"
                        INSERT INTO email_queue (
                            to_address, subject, body, {0},
                            solicitud_id, orden_id, tipo_notificacion, correlation_id,
                            intentos, max_intentos, ultimo_error,
                            adjunto_ruta, adjunto_nombre, adjunto_mime,
                            created_at, proximo_intento
                        ) VALUES (
                            @to_address, @subject, @body, @status,
                            @solicitud_id, @orden_id, @tipo_notificacion, @correlation_id,
                            @intentos, @max_intentos, @ultimo_error,
                            @adjunto_ruta, @adjunto_nombre, @adjunto_mime,
                            @created_at, @proximo_intento
                        ) RETURNING id", statusColumn))
                    : string.Format(@"
                        INSERT INTO email_queue (
                            to_address, subject, body, {0},
                            solicitud_id, created_at, proximo_intento
                        ) VALUES (
                            @to_address, @subject, @body, @status,
                            @solicitud_id, @created_at, @proximo_intento
                        ) RETURNING id", statusColumn);

                return ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@to_address", item.Para, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@subject", item.Asunto, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@body", item.Cuerpo, NpgsqlDbType.Text);
                    AddParameter(cmd, "@status", EstadoEmail.Pendiente, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@solicitud_id", item.SolicitudId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@created_at", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@proximo_intento", DateTime.Now, NpgsqlDbType.Timestamp);

                    if (hasExtended)
                    {
                        AddParameter(cmd, "@orden_id", item.OrdenId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@tipo_notificacion", item.TipoNotificacion ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@correlation_id", item.CorrelationId ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        if (hasEvent)
                        {
                            AddParameter(cmd, "@event_key", item.EventKey ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                            AddParameter(cmd, "@rol_origen", item.RolOrigen ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                            AddParameter(cmd, "@estado_final", item.EstadoFinal ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        }
                        AddParameter(cmd, "@intentos", item.Intentos, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@max_intentos", item.MaxIntentos > 0 ? item.MaxIntentos : DefaultMaxIntentos, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@ultimo_error", item.UltimoError ?? (object)DBNull.Value, NpgsqlDbType.Text);
                        AddParameter(cmd, "@adjunto_ruta", item.AdjuntoRuta ?? (object)DBNull.Value, NpgsqlDbType.Text);
                        AddParameter(cmd, "@adjunto_nombre", item.AdjuntoNombre ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@adjunto_mime", item.AdjuntoMimeType ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    }
                });
            });
        }

        public async Task<int> EncolarIdempotenteAsync(EmailQueueItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(item.EventKey))
            {
                return await EncolarAsync(item);
            }

            var existente = ObtenerIdPorEventKey(item.EventKey);
            if (existente > 0) return existente;

            try
            {
                return await EncolarAsync(item);
            }
            catch (DataAccessException ex) when (string.Equals(ex.ErrorCode, "DUPLICATE_KEY", StringComparison.OrdinalIgnoreCase))
            {
                return ObtenerIdPorEventKey(item.EventKey);
            }
        }

        public async Task<int> RegistrarErrorEventoAsync(string eventKey, string asunto, string detalleError, int? solicitudId = null, int? ordenId = null, string tipoNotificacion = null)
        {
            var item = new EmailQueueItem
            {
                Para = "sin-correo@invalid.local",
                Asunto = asunto ?? "Evento de correo sin destinatario",
                Cuerpo = detalleError ?? "No se pudo resolver destinatario.",
                SolicitudId = solicitudId,
                OrdenId = ordenId,
                TipoNotificacion = tipoNotificacion,
                CorrelationId = eventKey,
                EventKey = eventKey,
                UltimoError = detalleError,
                Estado = "ERROR"
            };

            return ExecuteWithConnection(conn =>
            {
                var hasExtended = HasExtendedColumns(conn);
                var hasEvent = HasEventColumns(conn);
                var statusColumn = GetStatusColumn(conn);
                var sql = hasExtended
                    ? (hasEvent
                        ? string.Format(@"
                            INSERT INTO email_queue (
                                to_address, subject, body, {0}, solicitud_id, orden_id, tipo_notificacion, correlation_id, event_key,
                                rol_origen, estado_final, intentos, max_intentos, ultimo_error, created_at, proximo_intento
                            ) VALUES (
                                @to_address, @subject, @body, 'ERROR', @solicitud_id, @orden_id, @tipo_notificacion, @correlation_id, @event_key,
                                @rol_origen, @estado_final, 0, @max_intentos, @ultimo_error, @created_at, @proximo_intento
                            ) RETURNING id", statusColumn)
                        : string.Format(@"
                            INSERT INTO email_queue (
                                to_address, subject, body, {0}, solicitud_id, orden_id, tipo_notificacion, correlation_id,
                                intentos, max_intentos, ultimo_error, created_at, proximo_intento
                            ) VALUES (
                                @to_address, @subject, @body, 'ERROR', @solicitud_id, @orden_id, @tipo_notificacion, @correlation_id,
                                0, @max_intentos, @ultimo_error, @created_at, @proximo_intento
                            ) RETURNING id", statusColumn))
                    : string.Format(@"
                            INSERT INTO email_queue (
                                to_address, subject, body, {0}, solicitud_id, created_at, proximo_intento
                            ) VALUES (
                                @to_address, @subject, @body, 'ERROR', @solicitud_id, @created_at, @proximo_intento
                            ) RETURNING id", statusColumn);

                return ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@to_address", item.Para, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@subject", item.Asunto, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@body", item.Cuerpo, NpgsqlDbType.Text);
                    AddParameter(cmd, "@solicitud_id", item.SolicitudId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@created_at", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@proximo_intento", DateTime.Now, NpgsqlDbType.Timestamp);

                    if (hasExtended)
                    {
                        AddParameter(cmd, "@orden_id", item.OrdenId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@tipo_notificacion", item.TipoNotificacion ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@correlation_id", item.CorrelationId ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@max_intentos", DefaultMaxIntentos, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@ultimo_error", item.UltimoError ?? (object)DBNull.Value, NpgsqlDbType.Text);
                        if (hasEvent)
                        {
                            AddParameter(cmd, "@event_key", item.EventKey ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                            AddParameter(cmd, "@rol_origen", item.RolOrigen ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                            AddParameter(cmd, "@estado_final", item.EstadoFinal ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        }
                    }
                });
            });
        }

        public async Task<EmailQueueItem> ObtenerSiguienteAsync()
        {
            // Usar FOR UPDATE SKIP LOCKED para evitar conflictos en procesamiento concurrente
            return ExecuteWithConnection(conn =>
            {
                var hasExtended = HasExtendedColumns(conn);
                var statusColumn = GetStatusColumn(conn);
                var hasProximoIntento = HasProximoIntentoColumn(conn);
                var hasCreatedAt = HasCreatedAtColumn(conn);
                var filtroProximo = hasProximoIntento ? "AND proximo_intento <= NOW()" : string.Empty;
                var orderClause = hasCreatedAt ? "ORDER BY created_at ASC" : "ORDER BY id ASC";
                var sql = hasExtended
                    ? string.Format(@"
                        UPDATE email_queue 
                        SET {0} = 'ENVIANDO',
                            intentos = COALESCE(intentos, 0) + 1,
                            ultimo_error = NULL
                        WHERE id = (
                            SELECT id FROM email_queue
                            WHERE {0} = 'PENDIENTE' 
                              {1}
                            {2}
                            LIMIT 1
                            FOR UPDATE SKIP LOCKED
                        )
                        RETURNING *", statusColumn, filtroProximo, orderClause)
                    : string.Format(@"
                        UPDATE email_queue 
                        SET {0} = 'ENVIANDO'
                        WHERE id = (
                            SELECT id FROM email_queue
                            WHERE {0} = 'PENDIENTE' 
                              {1}
                            {2}
                            LIMIT 1
                            FOR UPDATE SKIP LOCKED
                        )
                        RETURNING *", statusColumn, filtroProximo, orderClause);

                using (var cmd = CreateCommand(conn, sql))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapearItem(reader);
                    }
                    return null;
                }
            });
        }

        public async Task<IEnumerable<EmailQueueItem>> ObtenerPendientesAsync(int limite = 10)
        {
            return ExecuteWithConnection(conn =>
            {
                var statusColumn = GetStatusColumn(conn);
                var hasProximoIntento = HasProximoIntentoColumn(conn);
                var hasCreatedAt = HasCreatedAtColumn(conn);
                var filtroProximo = hasProximoIntento ? "AND proximo_intento <= NOW()" : string.Empty;
                var orderClause = hasCreatedAt ? "ORDER BY created_at ASC" : "ORDER BY id ASC";
                var sql = string.Format(@"
                    SELECT * FROM email_queue
                    WHERE {0} = 'PENDIENTE' 
                      {1}
                    {2}
                    LIMIT @limite", statusColumn, filtroProximo, orderClause);
                var lista = new List<EmailQueueItem>();
                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@limite", limite, NpgsqlDbType.Integer);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapearItem(reader));
                        }
                    }
                }
                return lista;
            });
        }

        public async Task ActualizarEstadoAsync(int id, string estado, string error = null)
        {
            ExecuteWithConnection(conn =>
            {
                var hasExtended = HasExtendedColumns(conn);
                var statusColumn = GetStatusColumn(conn);
                var sql = hasExtended
                    ? string.Format(@"
                        UPDATE email_queue SET
                            {0} = @status,
                            ultimo_error = @error
                        WHERE id = @id", statusColumn)
                    : string.Format(@"
                        UPDATE email_queue SET
                            {0} = @status
                        WHERE id = @id", statusColumn);

                ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@status", estado, NpgsqlDbType.Varchar);
                    if (hasExtended)
                    {
                        AddParameter(cmd, "@error", (object)error ?? DBNull.Value, NpgsqlDbType.Text);
                    }
                });
            });
        }

        public async Task MarcarEnviadoAsync(int id, string messageId)
        {
            ExecuteWithConnection(conn =>
            {
                var hasExtended = HasExtendedColumns(conn);
                var statusColumn = GetStatusColumn(conn);
                var sql = hasExtended
                    ? string.Format(@"
                        UPDATE email_queue SET
                            {0} = 'ENVIADO',
                            fecha_envio = NOW(),
                            ultimo_error = NULL
                        WHERE id = @id", statusColumn)
                    : string.Format(@"
                        UPDATE email_queue SET
                            {0} = 'ENVIADO'
                        WHERE id = @id", statusColumn);

                ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                });
            });

            _logger.LogInfo(string.Format("Email {0} enviado exitosamente", id));
        }

        public async Task ReprogramarReintentoAsync(int id, TimeSpan delay)
        {
            var proximoIntento = DateTime.Now.Add(delay);

            ExecuteWithConnection(conn =>
            {
                var statusColumn = GetStatusColumn(conn);
                var sql = string.Format(@"
                    UPDATE email_queue SET
                        {0} = 'PENDIENTE',
                        proximo_intento = @proximo
                    WHERE id = @id", statusColumn);
                ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@proximo", proximoIntento, NpgsqlDbType.Timestamp);
                });
            });

            _logger.LogInfo(string.Format("Email {0} reprogramado para {1:HH:mm:ss}", id, proximoIntento));
        }

        private EmailQueueItem MapearItem(System.Data.IDataReader reader)
        {
            var item = new EmailQueueItem
            {
                Id = GetInt(reader, "id"),
                Para = GetString(reader, "to_address"),
                Asunto = GetString(reader, "subject"),
                Cuerpo = GetString(reader, "body"),
                Estado = GetString(reader, "status") ?? GetString(reader, "estado"),
                FechaCreacion = GetDateTime(reader, "created_at"),
                ProximoIntento = GetNullableDateTime(reader, "proximo_intento")
            };

            if (HasColumn(reader, "solicitud_id"))
                item.SolicitudId = GetValue<int?>(reader, "solicitud_id");
            if (HasColumn(reader, "orden_id"))
                item.OrdenId = GetValue<int?>(reader, "orden_id");
            if (HasColumn(reader, "tipo_notificacion"))
                item.TipoNotificacion = GetString(reader, "tipo_notificacion");
            if (HasColumn(reader, "correlation_id"))
                item.CorrelationId = GetString(reader, "correlation_id");
            if (HasColumn(reader, "event_key"))
                item.EventKey = GetString(reader, "event_key");
            if (HasColumn(reader, "rol_origen"))
                item.RolOrigen = GetString(reader, "rol_origen");
            if (HasColumn(reader, "estado_final"))
                item.EstadoFinal = GetString(reader, "estado_final");
            if (HasColumn(reader, "intentos"))
                item.Intentos = GetInt(reader, "intentos");
            if (HasColumn(reader, "max_intentos"))
                item.MaxIntentos = GetInt(reader, "max_intentos");
            if (HasColumn(reader, "ultimo_error"))
                item.UltimoError = GetString(reader, "ultimo_error");
            if (HasColumn(reader, "fecha_envio"))
                item.FechaEnvio = GetNullableDateTime(reader, "fecha_envio");
            if (HasColumn(reader, "adjunto_ruta"))
                item.AdjuntoRuta = GetString(reader, "adjunto_ruta");
            if (HasColumn(reader, "adjunto_nombre"))
                item.AdjuntoNombre = GetString(reader, "adjunto_nombre");
            if (HasColumn(reader, "adjunto_mime"))
                item.AdjuntoMimeType = GetString(reader, "adjunto_mime");

            return item;
        }

        private static bool HasColumn(System.Data.IDataReader reader, string columnName)
        {
            try
            {
                return reader.GetOrdinal(columnName) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private bool HasExtendedColumns(NpgsqlConnection conn)
        {
            if (_hasExtendedColumns.HasValue)
            {
                return _hasExtendedColumns.Value;
            }

            try
            {
                const string sql = @"
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'email_queue'
                      AND column_name = 'intentos'
                    LIMIT 1";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    var result = cmd.ExecuteScalar();
                    _hasExtendedColumns = result != null;
                }
            }
            catch
            {
                _hasExtendedColumns = false;
            }

            return _hasExtendedColumns.Value;
        }

        private bool HasEventColumns(NpgsqlConnection conn)
        {
            if (_hasEventColumns.HasValue)
            {
                return _hasEventColumns.Value;
            }

            try
            {
                const string sql = @"
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'email_queue'
                      AND column_name = 'event_key'
                    LIMIT 1";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    var result = cmd.ExecuteScalar();
                    _hasEventColumns = result != null;
                }
            }
            catch
            {
                _hasEventColumns = false;
            }

            return _hasEventColumns.Value;
        }

        private bool HasProximoIntentoColumn(NpgsqlConnection conn)
        {
            if (_hasProximoIntentoColumn.HasValue)
            {
                return _hasProximoIntentoColumn.Value;
            }

            _hasProximoIntentoColumn = HasQueueColumn(conn, "proximo_intento");
            return _hasProximoIntentoColumn.Value;
        }

        private bool HasCreatedAtColumn(NpgsqlConnection conn)
        {
            if (_hasCreatedAtColumn.HasValue)
            {
                return _hasCreatedAtColumn.Value;
            }

            _hasCreatedAtColumn = HasQueueColumn(conn, "created_at");
            return _hasCreatedAtColumn.Value;
        }

        private static bool HasQueueColumn(NpgsqlConnection conn, string columnName)
        {
            try
            {
                const string sql = @"
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'email_queue'
                      AND column_name = @column
                    LIMIT 1";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@column", columnName);
                    var result = cmd.ExecuteScalar();
                    return result != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private int ObtenerIdPorEventKey(string eventKey)
        {
            if (string.IsNullOrWhiteSpace(eventKey))
            {
                return 0;
            }

            return ExecuteWithConnection(conn =>
            {
                if (!HasEventColumns(conn))
                {
                    return 0;
                }

                const string sql = "SELECT id FROM email_queue WHERE event_key = @event_key LIMIT 1";
                return ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@event_key", eventKey, NpgsqlDbType.Varchar);
                });
            });
        }

        private string GetStatusColumn(NpgsqlConnection conn)
        {
            if (!string.IsNullOrWhiteSpace(_statusColumn))
            {
                return _statusColumn;
            }

            try
            {
                const string sql = @"
                    SELECT column_name
                    FROM information_schema.columns
                    WHERE table_name = 'email_queue'
                      AND column_name IN ('status', 'estado')";
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var hasStatus = false;
                    var hasEstado = false;
                    while (reader.Read())
                    {
                        var column = reader.GetString(0);
                        if (column == "status") hasStatus = true;
                        if (column == "estado") hasEstado = true;
                    }

                    _statusColumn = hasStatus ? "status" : (hasEstado ? "estado" : "status");
                }
            }
            catch
            {
                _statusColumn = "status";
            }

            return _statusColumn;
        }
    }

    #endregion

    #region Procesador de Cola

    /// <summary>
    /// Procesador de cola de correos (ejecutar como background job)
    /// </summary>
    public class EmailQueueProcessor : IDisposable
    {
        private readonly IEmailQueueService _queueService;
        private readonly IEmailService _emailService;
        private readonly ILoggingService _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;
        private bool _disposed;

        // Configuración de reintentos con backoff exponencial
        private const int DefaultMaxIntentos = 3;
        private static readonly TimeSpan[] RetryDelays = new[]
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15)
        };

        public EmailQueueProcessor(
            IEmailQueueService queueService,
            IEmailService emailService)
        {
            _queueService = queueService;
            _emailService = emailService;
            _logger = LoggingServiceFactory.Create();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            _logger.LogInfo("Iniciando procesador de cola de correos");
            try
            {
                _queueService.ProbarConexionAsync().GetAwaiter().GetResult();
                // Health check de esquema mínimo antes de iniciar el loop.
                _queueService.ObtenerPendientesAsync(1).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    ErrorCode = "EMAIL_QUEUE_HEALTHCHECK_ERROR",
                    AdditionalData =
                    {
                        { "Message", ex.Message },
                        { "InnerMessage", ex.InnerException != null ? ex.InnerException.Message : string.Empty }
                    }
                });
                return;
            }
            _processingTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
        }

        public void Stop()
        {
            _logger.LogInfo("Deteniendo procesador de cola de correos");
            _cancellationTokenSource.Cancel();
            
            try
            {
                // Esperar a que termine la tarea, ignorando excepciones de cancelación
                _processingTask?.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException || e is OperationCanceledException))
            {
                // Ignorar excepciones de cancelación - es el comportamiento esperado
                _logger.LogInfo("Procesador de cola detenido correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "EMAIL_QUEUE_STOP_ERROR" });
            }
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var item = await _queueService.ObtenerSiguienteAsync();

                    if (item != null)
                    {
                        await ProcessItemAsync(item);
                    }
                    else
                    {
                        // No hay correos pendientes, esperar
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (ex is CapaDatos.Infrastructure.DataAccessException dex)
                    {
                        var sqlState = dex.Data.Contains("SqlState") ? (dex.Data["SqlState"] ?? "N/A").ToString() : "N/A";
                        var dbMessage = dex.Data.Contains("DbMessage") ? (dex.Data["DbMessage"] ?? string.Empty).ToString() : string.Empty;
                        var root = dex.Data.Contains("RootCause") ? (dex.Data["RootCause"] ?? string.Empty).ToString() : (dex.InnerException != null ? dex.InnerException.Message : string.Empty);
                        _logger.LogError(
                            string.Format("EMAIL_QUEUE_ERROR | Code={0} | SqlState={1} | DbMessage={2} | RootCause={3}",
                                dex.ErrorCode, sqlState, dbMessage, root),
                            new LogContext { ErrorCode = "EMAIL_QUEUE_ERROR_DETAIL" });
                    }
                    _logger.LogError(ex, new LogContext { ErrorCode = "EMAIL_QUEUE_ERROR" });
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
        }

        private async Task ProcessItemAsync(EmailQueueItem item)
        {
            var context = new LogContext
            {
                CorrelationId = item.CorrelationId,
                NumeroOrden = item.NumeroOrden
            };

            try
            {
                _logger.LogInfo(string.Format("Procesando email {0} para {1}", item.Id, item.Para), context);
                if (item.AdjuntoContenido == null && !string.IsNullOrWhiteSpace(item.AdjuntoRuta))
                {
                    try
                    {
                        if (File.Exists(item.AdjuntoRuta))
                        {
                            item.AdjuntoContenido = File.ReadAllBytes(item.AdjuntoRuta);
                            if (string.IsNullOrWhiteSpace(item.AdjuntoNombre))
                            {
                                item.AdjuntoNombre = Path.GetFileName(item.AdjuntoRuta);
                            }
                        }
                        else
                        {
                            _logger.LogWarning(string.Format("Adjunto no encontrado: {0}", item.AdjuntoRuta), context);
                        }
                    }
                    catch (Exception exAdj)
                    {
                        _logger.LogError(exAdj, new LogContext { CorrelationId = item.CorrelationId, ErrorCode = "EMAIL_ATTACH_ERROR" });
                    }
                }

                var result = await _emailService.EnviarAsync(
                    item.Para,
                    item.ParaNombre,
                    item.Asunto,
                    item.Cuerpo,
                    item.AdjuntoContenido,
                    item.AdjuntoNombre);

                if (result.Success)
                {
                    await _queueService.MarcarEnviadoAsync(item.Id, result.MessageId);
                    _logger.LogInfo(string.Format("Email {0} enviado exitosamente", item.Id), context);
                }
                else
                {
                    await HandleFailureAsync(item, result.Error, context);
                }
            }
            catch (Exception ex)
            {
                await HandleFailureAsync(item, ex.Message, context);
                _logger.LogError(ex, context);
            }
        }

        private async Task HandleFailureAsync(EmailQueueItem item, string error, LogContext context)
        {
            _logger.LogWarning(string.Format("Error enviando email {0}: {1}", item.Id, error), context);

            // Calcular cuántos intentos se han hecho basándose en ProximoIntento
            int intentosEstimados = item.Intentos > 0 ? item.Intentos : CalcularIntentosDesdeProximoIntento(item);

            if (intentosEstimados >= (item.MaxIntentos > 0 ? item.MaxIntentos : DefaultMaxIntentos))
            {
                // Máximo de intentos alcanzado
                await _queueService.ActualizarEstadoAsync(item.Id, "ERROR", error);
                _logger.LogError(string.Format("Email {0} marcado como ERROR después de ~{1} intentos",
                    item.Id, intentosEstimados), context);
            }
            else
            {
                // Programar reintento con backoff
                var delayIndex = Math.Min(intentosEstimados, RetryDelays.Length - 1);
                var delay = RetryDelays[delayIndex];
                await _queueService.ReprogramarReintentoAsync(item.Id, delay);
            }
        }

        private int CalcularIntentosDesdeProximoIntento(EmailQueueItem item)
        {
            // Si no hay ProximoIntento o es la primera vez, retornar 1
            if (!item.ProximoIntento.HasValue || item.ProximoIntento.Value <= item.FechaCreacion)
                return 1;

            // Estimar intentos basándose en cuánto tiempo ha pasado desde la creación
            var tiempoTranscurrido = item.ProximoIntento.Value - item.FechaCreacion;
            
            // Mapeo aproximado: 1min=1, 5min=2, 15min=3
            if (tiempoTranscurrido.TotalMinutes < 2)
                return 1;
            else if (tiempoTranscurrido.TotalMinutes < 10)
                return 2;
            else
                return 3;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }
    }

    #endregion
}



