using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using System.Web.Hosting;
using CapaDatos.Infrastructure;
using CapaDatos.Constants;
using CapaModelo.Common;

namespace CapaDatos.Services
{
    #region Entidades

    /// <summary>
    /// Elemento de la cola de correos
    /// </summary>
    /// <remarks>
    /// NOTA: Algunas propiedades no existen en la base de datos.
    /// Estas propiedades se usan solo en memoria para pasar datos al procesador de emails:
    /// - ParaNombre, EsHtml: Para formateo de emails
    /// - AdjuntoNombre, AdjuntoContenido, AdjuntoMimeType: Para adjuntos (no se persisten en BD)
    /// - NumeroOrden: Para logging y trazabilidad
    /// - MaxIntentos: Para lógica de reintentos
    /// 
    /// Columnas en la base de datos real:
    /// - id, to_address, subject, body, status, solicitud_id, orden_id, created_at, proximo_intento,
    ///   event_key, error_message, intentos, updated_at, tipo_notificacion, correlation_id
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
        public int? OrdenId { get; set; } // Columna: orden_id (FK a aocr_or_orden)
        public string EventKey { get; set; } // Idempotencia (si existe en BD)
        public string ErrorDetalle { get; set; }
        public int Intentos { get; set; }
        
        // Propiedades solo en memoria (no persistidas en BD)
        public string ParaNombre { get; set; }
        public bool EsHtml { get; set; }
        public string AdjuntoNombre { get; set; }
        public byte[] AdjuntoContenido { get; set; }
        public string AdjuntoMimeType { get; set; }
        public string CorrelationId { get; set; }
        public string NumeroOrden { get; set; }
        public string TipoNotificacion { get; set; }
        public int MaxIntentos { get; set; }
        public List<EmailAttachmentItem> Adjuntos { get; set; }
        public string Remitente { get; set; }
        public string AliasRemitente { get; set; }
    }

    public class EmailAttachmentItem
    {
        public int Id { get; set; }
        public int EmailQueueId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string FilePath { get; set; }
        public long? FileSize { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    #endregion

    #region Interface

    /// <summary>
    /// Interface para servicio de cola de correos
    /// </summary>
    public interface IEmailQueueService
    {
        Task<int> EncolarAsync(EmailQueueItem item);
        Task<int> EncolarConAdjuntosAsync(EmailQueueItem item, IEnumerable<EmailAttachmentItem> attachments);
        Task<bool> ExisteNotificacionAsync(string tipoNotificacion, string eventKeyPrefix, int? solicitudId = null);
        Task<EmailQueueItem> ObtenerSiguienteAsync();
        Task ActualizarEstadoAsync(int id, string estado, string error = null);
        Task MarcarEnviadoAsync(int id, string messageId);
        Task<IEnumerable<EmailQueueItem>> ObtenerPendientesAsync(int limite = 10);
        Task ReprogramarReintentoAsync(int id, TimeSpan delay);
        Task<int> ReactivarEnviandoAbandonadosAsync(TimeSpan antiguedadMinima);
    }

    #endregion

    #region Implementación

    /// <summary>
    /// Implementación de cola de correos persistente
    /// </summary>
    public class EmailQueueService : BaseDAO, IEmailQueueService
    {
        private readonly ILoggingService _logger;
        private const int DefaultMaxIntentos = 3;
        private readonly object _schemaLock = new object();
        private bool _schemaVerified;

        public EmailQueueService() : this(new SecureConfigurationService().GetConnectionString("PostgreSQL") ?? "")
        {
        }

        public EmailQueueService(string connectionString) : base(connectionString)
        {
            _logger = LoggingServiceFactory.Create();
        }

        public async Task<int> EncolarAsync(EmailQueueItem item)
        {
            return await EncolarConAdjuntosAsync(item, item != null ? item.Adjuntos : null);
        }

        public Task<int> EncolarConAdjuntosAsync(EmailQueueItem item, IEnumerable<EmailAttachmentItem> attachments)
        {
            return Task.FromResult(ExecuteInTransaction((conn, tx) =>
            {
                bool duplicateEvent;
                return EncolarConAdjuntosEnTransaccion(conn, tx, item, attachments, out duplicateEvent);
            }));
        }

        public Task<bool> ExisteNotificacionAsync(string tipoNotificacion, string eventKeyPrefix, int? solicitudId = null)
        {
            return Task.FromResult(ExecuteWithConnection(conn =>
            {
                EnsureEmailQueueSchema(conn);

                const string sql = @"
                    SELECT 1
                    FROM email_queue
                    WHERE (@tipo_notificacion IS NULL OR UPPER(COALESCE(tipo_notificacion, '')) = @tipo_notificacion)
                      AND (@solicitud_id IS NULL OR solicitud_id = @solicitud_id)
                      AND (@event_key_like IS NULL OR UPPER(COALESCE(event_key, '')) LIKE @event_key_like)
                    LIMIT 1";

                var tipoNormalizado = string.IsNullOrWhiteSpace(tipoNotificacion)
                    ? null
                    : tipoNotificacion.Trim().ToUpperInvariant();
                var eventKeyLike = string.IsNullOrWhiteSpace(eventKeyPrefix)
                    ? null
                    : eventKeyPrefix.Trim().ToUpperInvariant() + "%";

                var existe = ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@tipo_notificacion", (object)tipoNormalizado ?? DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@solicitud_id", solicitudId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@event_key_like", (object)eventKeyLike ?? DBNull.Value, NpgsqlDbType.Varchar);
                });

                return existe > 0;
            }));
        }

        public int EncolarConAdjuntosEnTransaccion(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            EmailQueueItem item,
            IEnumerable<EmailAttachmentItem> attachments,
            out bool duplicateEvent)
        {
            duplicateEvent = false;

            EnsureEmailQueueSchema(conn);

            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            var now = DateTime.Now;
            var estadoInicial = string.IsNullOrWhiteSpace(item.Estado)
                ? "PENDIENTE"
                : item.Estado.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(item.Para))
            {
                item.Para = "no-reply@invalid.local";
            }

            item.Remitente = AocrEmailService.NormalizarRemitenteInstitucional(item.Remitente);
            item.AliasRemitente = AocrEmailService.NormalizarAlias(
                string.IsNullOrWhiteSpace(item.AliasRemitente)
                    ? AocrEmailService.ResolverAliasPorTipoNotificacion(item.TipoNotificacion)
                    : item.AliasRemitente);

            item.Cuerpo = EmailTemplateRenderer.EnsureStandardLayout(
                item.Asunto,
                item.Cuerpo,
                item.ParaNombre,
                "Este es un mensaje automatico del workflow AOCR.");

            bool eventKeyColumnDisponible = true;
            var eventKeyNormalizado = string.IsNullOrWhiteSpace(item.EventKey) ? null : item.EventKey.Trim();
            var tipoNotificacionNormalizado = string.IsNullOrWhiteSpace(item.TipoNotificacion)
                ? null
                : item.TipoNotificacion.Trim().ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(eventKeyNormalizado))
            {
                try
                {
                    const string sqlExisting = @"SELECT id FROM email_queue WHERE event_key = @event_key LIMIT 1";
                    var existingId = ExecuteScalar<int>(conn, sqlExisting, cmd =>
                    {
                        AddParameter(cmd, "@event_key", eventKeyNormalizado, NpgsqlDbType.Varchar);
                    }, tx);

                    if (existingId > 0)
                    {
                        duplicateEvent = true;
                        return existingId;
                    }
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "42703")
                {
                    eventKeyColumnDisponible = false;
                }
            }

            var sqlInsertConEventKey = @"
                INSERT INTO email_queue (
                    to_address, subject, body, status,
                    solicitud_id, orden_id, created_at, proximo_intento, event_key, tipo_notificacion, correlation_id
                ) VALUES (
                    @to_address, @subject, @body, @status,
                    @solicitud_id, @orden_id, @created_at, @proximo_intento, @event_key, @tipo_notificacion, @correlation_id
                ) RETURNING id";

            var sqlInsertSimple = @"
                INSERT INTO email_queue (
                    to_address, subject, body, status,
                    solicitud_id, orden_id, created_at, proximo_intento, tipo_notificacion, correlation_id
                ) VALUES (
                    @to_address, @subject, @body, @status,
                    @solicitud_id, @orden_id, @created_at, @proximo_intento, @tipo_notificacion, @correlation_id
                ) RETURNING id";

            int emailQueueId;
            try
            {
                if (eventKeyColumnDisponible && !string.IsNullOrWhiteSpace(eventKeyNormalizado))
                {
                    emailQueueId = ExecuteScalar<int>(conn, sqlInsertConEventKey, cmd =>
                    {
                        AddParameter(cmd, "@to_address", item.Para, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@subject", item.Asunto ?? string.Empty, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@body", item.Cuerpo ?? string.Empty, NpgsqlDbType.Text);
                        AddParameter(cmd, "@status", estadoInicial, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@solicitud_id", item.SolicitudId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@orden_id", item.OrdenId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@created_at", now, NpgsqlDbType.Timestamp);
                        AddParameter(cmd, "@proximo_intento", now, NpgsqlDbType.Timestamp);
                        AddParameter(cmd, "@event_key", eventKeyNormalizado, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@tipo_notificacion", (object)tipoNotificacionNormalizado ?? DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@correlation_id", (object)(string.IsNullOrWhiteSpace(item.CorrelationId) ? null : item.CorrelationId.Trim()) ?? DBNull.Value, NpgsqlDbType.Varchar);
                    }, tx);
                }
                else
                {
                    emailQueueId = ExecuteScalar<int>(conn, sqlInsertSimple, cmd =>
                    {
                        AddParameter(cmd, "@to_address", item.Para, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@subject", item.Asunto ?? string.Empty, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@body", item.Cuerpo ?? string.Empty, NpgsqlDbType.Text);
                        AddParameter(cmd, "@status", estadoInicial, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@solicitud_id", item.SolicitudId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@orden_id", item.OrdenId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@created_at", now, NpgsqlDbType.Timestamp);
                        AddParameter(cmd, "@proximo_intento", now, NpgsqlDbType.Timestamp);
                        AddParameter(cmd, "@tipo_notificacion", (object)tipoNotificacionNormalizado ?? DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@correlation_id", (object)(string.IsNullOrWhiteSpace(item.CorrelationId) ? null : item.CorrelationId.Trim()) ?? DBNull.Value, NpgsqlDbType.Varchar);
                    }, tx);
                }
            }
            catch (PostgresException pgEx)
                when (pgEx.SqlState == "23505"
                      && !string.IsNullOrWhiteSpace(eventKeyNormalizado)
                      && ((pgEx.ConstraintName ?? string.Empty).IndexOf("event_key", StringComparison.OrdinalIgnoreCase) >= 0
                          || (pgEx.MessageText ?? string.Empty).IndexOf("event_key", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                const string sqlExisting = @"SELECT id FROM email_queue WHERE event_key = @event_key LIMIT 1";
                emailQueueId = ExecuteScalar<int>(conn, sqlExisting, cmd =>
                {
                    AddParameter(cmd, "@event_key", eventKeyNormalizado, NpgsqlDbType.Varchar);
                }, tx);

                if (emailQueueId > 0)
                {
                    duplicateEvent = true;
                    return emailQueueId;
                }

                throw;
            }

            var adjuntos = (attachments ?? item.Adjuntos ?? Enumerable.Empty<EmailAttachmentItem>()).ToList();
            if (adjuntos.Count > 0)
            {
                EnsureEmailAttachmentTable(conn, tx);

                const string sqlInsertAttachment = @"
                    INSERT INTO email_attachment
                        (email_queue_id, file_name, content_type, file_path, file_size, created_at)
                    VALUES
                        (@email_queue_id, @file_name, @content_type, @file_path, @file_size, @created_at)";

                foreach (var attachment in adjuntos)
                {
                    if (attachment == null) continue;

                    ExecuteNonQuery(conn, sqlInsertAttachment, cmd =>
                    {
                        AddParameter(cmd, "@email_queue_id", emailQueueId, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@file_name", attachment.FileName ?? string.Empty, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@content_type", attachment.ContentType ?? "application/octet-stream", NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@file_path", attachment.FilePath ?? string.Empty, NpgsqlDbType.Text);
                        AddParameter(cmd, "@file_size", attachment.FileSize ?? 0L, NpgsqlDbType.Bigint);
                        AddParameter(cmd, "@created_at", now, NpgsqlDbType.Timestamp);
                    }, tx);
                }
            }

            return emailQueueId;
        }

        private void EnsureEmailAttachmentTable(NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS public.email_attachment (
                    id SERIAL PRIMARY KEY,
                    email_queue_id INTEGER NOT NULL,
                    file_name VARCHAR(255) NOT NULL,
                    content_type VARCHAR(120) NOT NULL,
                    file_path TEXT NOT NULL,
                    file_size BIGINT,
                    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                    CONSTRAINT fk_email_attachment_queue
                        FOREIGN KEY (email_queue_id) REFERENCES public.email_queue(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_email_attachment_queue_id
                    ON public.email_attachment(email_queue_id);";

            ExecuteNonQuery(conn, sql, null, tx);
        }

        private void EnsureEmailQueueSchema(NpgsqlConnection conn)
        {
            if (_schemaVerified)
            {
                return;
            }

            lock (_schemaLock)
            {
                if (_schemaVerified)
                {
                    return;
                }

                const string sql = @"
                    CREATE TABLE IF NOT EXISTS public.email_queue (
                        id SERIAL PRIMARY KEY,
                        to_address VARCHAR(255) NOT NULL,
                        subject VARCHAR(255) NOT NULL,
                        body TEXT NOT NULL,
                        status VARCHAR(20) NOT NULL,
                        solicitud_id INTEGER NULL,
                        orden_id INTEGER NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        proximo_intento TIMESTAMP NOT NULL DEFAULT NOW()
                    );

                    ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS event_key VARCHAR(200);
                    ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS error_message TEXT;
                    ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS intentos INTEGER NOT NULL DEFAULT 0;
                    ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;
                    ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS tipo_notificacion VARCHAR(120);
                    ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(64);

                    CREATE INDEX IF NOT EXISTS idx_email_queue_status_next
                        ON public.email_queue(status, proximo_intento);

                    CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud
                        ON public.email_queue(solicitud_id);

                    CREATE INDEX IF NOT EXISTS idx_email_queue_orden
                        ON public.email_queue(orden_id);

                    CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
                        ON public.email_queue(event_key)
                        WHERE event_key IS NOT NULL;";

                ExecuteNonQuery(conn, sql);
                EnsureEmailAttachmentTable(conn, null);

                _schemaVerified = true;
            }
        }

        public Task<EmailQueueItem> ObtenerSiguienteAsync()
        {
            // Usar FOR UPDATE SKIP LOCKED para evitar conflictos en procesamiento concurrente
            const string sql = @"
                UPDATE email_queue 
                SET status = 'ENVIANDO'
                WHERE id = (
                    SELECT id FROM email_queue
                    WHERE status = 'PENDIENTE' 
                      AND proximo_intento <= NOW()
                    ORDER BY created_at ASC
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *";

            return Task.FromResult(ExecuteWithConnection(conn =>
            {
                EnsureEmailQueueSchema(conn);
                EmailQueueItem item = null;
                using (var cmd = CreateCommand(conn, sql))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        item = MapearItem(reader);
                    }
                }

                // Npgsql no permite ejecutar otro comando con el reader abierto en la misma conexión.
                if (item != null)
                {
                    item.Adjuntos = ObtenerAdjuntos(conn, item.Id);
                }

                return item;
            }));
        }

        /// <summary>
        /// Reactiva correos que quedaron en estado ENVIANDO tras un reinicio/crash
        /// del procesador. Solo afecta filas cuya última actualización (updated_at
        /// o created_at) sea anterior al umbral indicado.
        /// </summary>
        public Task<int> ReactivarEnviandoAbandonadosAsync(TimeSpan antiguedadMinima)
        {
            var segundos = (int)Math.Max(60, antiguedadMinima.TotalSeconds);
            const string sql = @"
                UPDATE email_queue
                SET status = 'PENDIENTE',
                    proximo_intento = NOW(),
                    updated_at = NOW()
                WHERE status = 'ENVIANDO'
                  AND COALESCE(updated_at, created_at) < NOW() - (@segundos * INTERVAL '1 second');";

            var afectadas = ExecuteWithConnection(conn =>
            {
                EnsureEmailQueueSchema(conn);
                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@segundos", segundos, NpgsqlDbType.Integer);
                    return cmd.ExecuteNonQuery();
                }
            });

            return Task.FromResult(afectadas);
        }

        public Task<IEnumerable<EmailQueueItem>> ObtenerPendientesAsync(int limite = 10)
        {
            const string sql = @"
                SELECT * FROM email_queue
                WHERE status = 'PENDIENTE' 
                  AND proximo_intento <= NOW()
                ORDER BY created_at ASC
                LIMIT @limite";

            return Task.FromResult<IEnumerable<EmailQueueItem>>(ExecuteWithConnection(conn =>
            {
                EnsureEmailQueueSchema(conn);
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

                foreach (var item in lista)
                {
                    item.Adjuntos = ObtenerAdjuntos(conn, item.Id);
                }

                return lista;
            }));
        }

        public Task ActualizarEstadoAsync(int id, string estado, string error = null)
        {
            const string sqlConError = @"
                UPDATE email_queue SET
                    status = @status,
                    error_message = @error_message,
                    updated_at = NOW()
                WHERE id = @id";

            const string sqlSimple = @"
                UPDATE email_queue SET
                    status = @status
                WHERE id = @id";

            ExecuteWithConnection(conn =>
            {
                try
                {
                    ExecuteNonQuery(conn, sqlConError, cmd =>
                    {
                        AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@status", estado, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@error_message", error, NpgsqlDbType.Text);
                    });
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "42703")
                {
                    ExecuteNonQuery(conn, sqlSimple, cmd =>
                    {
                        AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@status", estado, NpgsqlDbType.Varchar);
                    });
                }
            });
            return Task.CompletedTask;
        }

        public Task MarcarEnviadoAsync(int id, string messageId)
        {
            const string sqlConCampos = @"
                UPDATE email_queue SET
                    status = 'ENVIADO',
                    error_message = NULL,
                    updated_at = NOW()
                WHERE id = @id";

            const string sqlSimple = @"
                UPDATE email_queue SET
                    status = 'ENVIADO'
                WHERE id = @id";

            ExecuteWithConnection(conn =>
            {
                try
                {
                    ExecuteNonQuery(conn, sqlConCampos, cmd =>
                    {
                        AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    });
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "42703")
                {
                    ExecuteNonQuery(conn, sqlSimple, cmd =>
                    {
                        AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    });
                }
            });
            return Task.CompletedTask;
        }

        public Task ReprogramarReintentoAsync(int id, TimeSpan delay)
        {
            const string sqlConIntentos = @"
                UPDATE email_queue SET
                    status = 'PENDIENTE',
                    proximo_intento = @proximo,
                    intentos = COALESCE(intentos, 0) + 1,
                    updated_at = NOW()
                WHERE id = @id";

            const string sqlSimple = @"
                UPDATE email_queue SET
                    status = 'PENDIENTE',
                    proximo_intento = @proximo
                WHERE id = @id";

            var proximoIntento = DateTime.Now.Add(delay);

            ExecuteWithConnection(conn =>
            {
                try
                {
                    ExecuteNonQuery(conn, sqlConIntentos, cmd =>
                    {
                        AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@proximo", proximoIntento, NpgsqlDbType.Timestamp);
                    });
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "42703")
                {
                    ExecuteNonQuery(conn, sqlSimple, cmd =>
                    {
                        AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@proximo", proximoIntento, NpgsqlDbType.Timestamp);
                    });
                }
            });

            _logger.LogInfo(string.Format("Email {0} reprogramado para {1:HH:mm:ss}", id, proximoIntento));
            return Task.CompletedTask;
        }

        private EmailQueueItem MapearItem(System.Data.IDataReader reader)
        {
            return new EmailQueueItem
            {
                Id = GetInt(reader, "id"),
                Para = GetString(reader, "to_address"),
                Asunto = GetString(reader, "subject"),
                Cuerpo = GetString(reader, "body"),
                Estado = GetString(reader, "status"),
                FechaCreacion = GetDateTime(reader, "created_at"),
                ProximoIntento = GetNullableDateTime(reader, "proximo_intento"),
                SolicitudId = GetValue<int?>(reader, "solicitud_id"),
                OrdenId = GetValue<int?>(reader, "orden_id"),
                EventKey = GetString(reader, "event_key"),
                TipoNotificacion = GetString(reader, "tipo_notificacion"),
                ErrorDetalle = GetString(reader, "error_message"),
                Intentos = GetInt(reader, "intentos"),
                CorrelationId = GetString(reader, "correlation_id"),
                Adjuntos = new List<EmailAttachmentItem>()
            };
        }

        private List<EmailAttachmentItem> ObtenerAdjuntos(NpgsqlConnection conn, int emailQueueId)
        {
            const string sql = @"
                SELECT id, email_queue_id, file_name, content_type, file_path, file_size, created_at
                FROM email_attachment
                WHERE email_queue_id = @email_queue_id
                ORDER BY id";

            try
            {
                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@email_queue_id", emailQueueId, NpgsqlDbType.Integer);
                    using (var reader = cmd.ExecuteReader())
                    {
                        var result = new List<EmailAttachmentItem>();
                        while (reader.Read())
                        {
                            result.Add(new EmailAttachmentItem
                            {
                                Id = GetInt(reader, "id"),
                                EmailQueueId = GetInt(reader, "email_queue_id"),
                                FileName = GetString(reader, "file_name"),
                                ContentType = GetString(reader, "content_type"),
                                FilePath = GetString(reader, "file_path"),
                                FileSize = GetValue<long?>(reader, "file_size"),
                                CreatedAt = GetNullableDateTime(reader, "created_at")
                            });
                        }

                        return result;
                    }
                }
            }
            catch (PostgresException pgEx) when (pgEx.SqlState == "42P01")
            {
                // Tabla de adjuntos no existe (compatibilidad con instalaciones antiguas).
                return new List<EmailAttachmentItem>();
            }
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
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30)
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

            _processingTask = Task.Run(() => InitializeAndProcessQueueAsync(_cancellationTokenSource.Token));
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
                    _logger.LogInfo("[EMAIL_QUEUE][READ_IN] Consultando siguiente correo en cola.");
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
                catch (DataAccessException dbEx) when (dbEx.ErrorCode == "CONNECTION_ERROR")
                {
                    // Error transitorio de BD: esperar menos para recuperarse más rápido
                    _logger.LogError(dbEx, new LogContext { ErrorCode = "EMAIL_QUEUE_DB_TRANSIENT" });
                    _logger.LogWarning(string.Format("[EMAIL_QUEUE][READ_ERROR_RETRY] Error transitorio de base de datos al leer cola. Reintentando. Detalle: {0}", dbEx.Message));
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, new LogContext { ErrorCode = "EMAIL_QUEUE_ERROR" });
                    _logger.LogWarning(string.Format("[EMAIL_QUEUE][READ_ERROR_RETRY] Error al leer o procesar cola de correos. Reintentando. Detalle: {0}", ex.Message));
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
        }

        private async Task InitializeAndProcessQueueAsync(CancellationToken cancellationToken)
        {
            var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var reactivados = await _queueService.ReactivarEnviandoAbandonadosAsync(TimeSpan.FromMinutes(10));
                if (reactivados > 0)
                {
                    _logger.LogInfo(string.Format(
                        "Reactivados {0} correos abandonados en estado ENVIANDO (> 10 min).",
                        reactivados));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "EMAIL_QUEUE_RECLAIM_ERROR" });
            }

            _logger.LogInfo(string.Format(
                "[PERF][EMAIL_QUEUE] Inicializacion de cola completada en {0} ms",
                startupStopwatch.ElapsedMilliseconds));

            _logger.LogInfo("[EMAIL_QUEUE][START_OK] Cola de correos inicializada correctamente.");

            await ProcessQueueAsync(cancellationToken);
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
                _logger.LogInfo(string.Format("[EMAIL_QUEUE][SEND_IN] Iniciando envío de email {0} para {1}", item.Id, item.Para), context);

                byte[] adjuntoContenido = item.AdjuntoContenido;
                string adjuntoNombre = item.AdjuntoNombre;
                var adjuntosCorreo = new List<EmailSendAttachment>();

                if (item.Adjuntos != null && item.Adjuntos.Count > 0)
                {
                    foreach (var adjunto in item.Adjuntos)
                    {
                        byte[] contenido;
                        string nombre;
                        string attachmentError;
                        if (!TryLoadAttachment(adjunto, out contenido, out nombre, out attachmentError))
                        {
                            await _queueService.ActualizarEstadoAsync(item.Id, "ERROR", attachmentError);
                            _logger.LogError(
                                string.Format("[EMAIL_QUEUE][SEND_ERROR_NO_RETRY] Email {0} falló por adjunto inválido. Error={1}", item.Id, attachmentError),
                                context);
                            return;
                        }

                        adjuntosCorreo.Add(new EmailSendAttachment
                        {
                            Content = contenido,
                            FileName = nombre,
                            ContentType = string.IsNullOrWhiteSpace(adjunto.ContentType) ? "application/pdf" : adjunto.ContentType
                        });
                    }
                }

                var aliasRemitente = AocrEmailService.NormalizarAlias(
                    string.IsNullOrWhiteSpace(item.AliasRemitente)
                        ? AocrEmailService.ResolverAliasPorTipoNotificacion(item.TipoNotificacion)
                        : item.AliasRemitente);

                var result = adjuntosCorreo.Count > 0
                    ? await _emailService.EnviarAsync(
                        item.Para,
                        item.ParaNombre,
                        item.Asunto,
                        item.Cuerpo,
                        adjuntosCorreo,
                        aliasRemitente)
                    : await _emailService.EnviarAsync(
                        item.Para,
                        item.ParaNombre,
                        item.Asunto,
                        item.Cuerpo,
                        adjuntoContenido,
                        adjuntoNombre,
                        aliasRemitente);

                if (result.Success)
                {
                    await _queueService.MarcarEnviadoAsync(item.Id, result.MessageId);
                    _logger.LogInfo(string.Format("[EMAIL_QUEUE][SEND_OK] Email {0} enviado exitosamente. MessageId={1}", item.Id, result.MessageId), context);
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

            if (EsErrorConfiguracionSmtp(error))
            {
                var detalle = string.IsNullOrWhiteSpace(error)
                    ? "Configuracion SMTP invalida o remitente no autorizado."
                    : error;
                await _queueService.ActualizarEstadoAsync(item.Id, "ERROR_CONFIG_SMTP", detalle);
                _logger.LogError(string.Format("[EMAIL_QUEUE][SEND_ERROR_NO_RETRY] Email {0} marcado como ERROR_CONFIG_SMTP. Error={1}", item.Id, detalle), context);
                return;
            }

            if (EsErrorNoReintentable(error))
            {
                await _queueService.ActualizarEstadoAsync(item.Id, "ERROR_NO_REINTENTABLE", error);
                _logger.LogError(string.Format("[EMAIL_QUEUE][SEND_ERROR_NO_RETRY] Email {0} marcado como ERROR_NO_REINTENTABLE. Error={1}", item.Id, error), context);
                return;
            }

            // Calcular cuántos intentos se han hecho basándose en ProximoIntento
            var intentosActuales = Math.Max(0, item.Intentos);
            var maxIntentos = item.MaxIntentos > 0 ? item.MaxIntentos : DefaultMaxIntentos;

            if (intentosActuales >= maxIntentos)
            {
                // Máximo de intentos alcanzado
                await _queueService.ActualizarEstadoAsync(item.Id, "ERROR", error);
                _logger.LogError(string.Format("[EMAIL_QUEUE][SEND_ERROR_NO_RETRY] Email {0} marcado como ERROR definitivo después de {1} intentos. Error={2}",
                    item.Id, intentosActuales, error), context);
            }
            else
            {
                // Programar reintento con backoff
                var delayIndex = Math.Min(intentosActuales, RetryDelays.Length - 1);
                var delay = RetryDelays[delayIndex];
                await _queueService.ReprogramarReintentoAsync(item.Id, delay);
                _logger.LogWarning(string.Format("[EMAIL_QUEUE][SEND_ERROR_TEMPORAL] Email {0} reprogramado para reintento. IntentoActual={1}; MaxIntentos={2}; DelayMin={3}; Error={4}",
                    item.Id, intentosActuales, maxIntentos, delay.TotalMinutes, error), context);
            }
        }

        private static bool EsErrorConfiguracionSmtp(string error)
        {
            var detalle = (error ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(detalle))
            {
                return false;
            }

            return detalle.IndexOf("ERROR_CONFIG_SMTP", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("5.7.1", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("MailboxNameNotAllowed", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("sender address rejected", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("not logged in", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("client was not authenticated", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EsErrorNoReintentable(string error)
        {
            var detalle = (error ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(detalle))
            {
                return false;
            }

            return detalle.IndexOf("5.1.1", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("user unknown", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("recipient address rejected", StringComparison.OrdinalIgnoreCase) >= 0;
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

        private bool TryLoadAttachment(
            EmailAttachmentItem attachment,
            out byte[] content,
            out string fileName,
            out string error)
        {
            content = null;
            fileName = null;
            error = null;

            if (attachment == null)
            {
                error = "Adjunto no especificado.";
                return false;
            }

            var resolvedPath = ResolveAttachmentPath(attachment.FilePath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                error = "Adjunto sin ruta configurada.";
                return false;
            }

            if (!File.Exists(resolvedPath))
            {
                error = string.Format("No se encontró el archivo adjunto: {0}", resolvedPath);
                return false;
            }

            try
            {
                content = File.ReadAllBytes(resolvedPath);
                if (content == null || content.Length == 0)
                {
                    error = string.Format("El archivo adjunto está vacío: {0}", resolvedPath);
                    return false;
                }

                fileName = !string.IsNullOrWhiteSpace(attachment.FileName)
                    ? attachment.FileName
                    : Path.GetFileName(resolvedPath);

                return true;
            }
            catch (Exception ex)
            {
                error = "Error leyendo adjunto: " + ex.Message;
                return false;
            }
        }

        private string ResolveAttachmentPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(filePath))
                {
                    return Path.GetFullPath(filePath);
                }

                if (filePath.StartsWith("~"))
                {
                    var mapped = HostingEnvironment.MapPath(filePath);
                    if (!string.IsNullOrWhiteSpace(mapped))
                    {
                        return Path.GetFullPath(mapped);
                    }
                }

                var cleaned = filePath.TrimStart('~', '/', '\\').Replace('/', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cleaned));
            }
            catch
            {
                return null;
            }
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
