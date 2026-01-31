using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using CapaDatos.Infrastructure;

namespace CapaDatos.Services
{
    #region Entidades

    /// <summary>
    /// Elemento de la cola de correos
    /// </summary>
    public class EmailQueueItem
    {
        public int Id { get; set; }
        public string Para { get; set; }
        public string ParaNombre { get; set; }
        public string Asunto { get; set; }
        public string Cuerpo { get; set; }
        public bool EsHtml { get; set; }
        public string AdjuntoNombre { get; set; }
        public byte[] AdjuntoContenido { get; set; }
        public string AdjuntoMimeType { get; set; }
        public string Estado { get; set; } // PENDIENTE, ENVIANDO, ENVIADO, ERROR, CANCELADO
        public int Intentos { get; set; }
        public int MaxIntentos { get; set; }
        public string UltimoError { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaEnvio { get; set; }
        public DateTime? ProximoIntento { get; set; }
        public string CorrelationId { get; set; }
        public string NumeroOrden { get; set; }
        public int? OrdenId { get; set; }
        public string TipoNotificacion { get; set; }
    }

    #endregion

    #region Interface

    /// <summary>
    /// Interface para servicio de cola de correos
    /// </summary>
    public interface IEmailQueueService
    {
        Task<int> EncolarAsync(EmailQueueItem item);
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
        private const int DefaultMaxIntentos = 3;

        public EmailQueueService() : this(new SecureConfigurationService().GetConnectionString("PostgreSQL") ?? "")
        {
        }

        public EmailQueueService(string connectionString) : base(connectionString)
        {
            _logger = LoggingServiceFactory.Create();
        }

        public async Task<int> EncolarAsync(EmailQueueItem item)
        {
            const string sql = @"
                INSERT INTO email_queue (
                    para, para_nombre, asunto, cuerpo, es_html,
                    adjunto_nombre, adjunto_contenido, adjunto_mime_type,
                    estado, intentos, max_intentos, fecha_creacion,
                    proximo_intento, correlation_id, numero_orden, orden_id, tipo_notificacion
                ) VALUES (
                    @para, @para_nombre, @asunto, @cuerpo, @es_html,
                    @adjunto_nombre, @adjunto_contenido, @adjunto_mime_type,
                    @estado, @intentos, @max_intentos, @fecha_creacion,
                    @proximo_intento, @correlation_id, @numero_orden, @orden_id, @tipo_notificacion
                ) RETURNING id";

            return ExecuteWithConnection(conn =>
            {
                return ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@para", item.Para, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@para_nombre", item.ParaNombre ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@asunto", item.Asunto, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@cuerpo", item.Cuerpo, NpgsqlDbType.Text);
                    AddParameter(cmd, "@es_html", item.EsHtml, NpgsqlDbType.Boolean);
                    AddParameter(cmd, "@adjunto_nombre", item.AdjuntoNombre ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@adjunto_contenido", item.AdjuntoContenido ?? (object)DBNull.Value, NpgsqlDbType.Bytea);
                    AddParameter(cmd, "@adjunto_mime_type", item.AdjuntoMimeType ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@estado", "PENDIENTE", NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@intentos", 0, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@max_intentos", item.MaxIntentos > 0 ? item.MaxIntentos : DefaultMaxIntentos, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@fecha_creacion", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@proximo_intento", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@correlation_id", item.CorrelationId ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@numero_orden", item.NumeroOrden ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@orden_id", item.OrdenId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@tipo_notificacion", item.TipoNotificacion ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                });
            });
        }

        public async Task<EmailQueueItem> ObtenerSiguienteAsync()
        {
            // Usar FOR UPDATE SKIP LOCKED para evitar conflictos en procesamiento concurrente
            const string sql = @"
                UPDATE email_queue 
                SET estado = 'ENVIANDO', intentos = intentos + 1
                WHERE id = (
                    SELECT id FROM email_queue
                    WHERE estado = 'PENDIENTE' 
                      AND proximo_intento <= NOW()
                      AND intentos < max_intentos
                    ORDER BY fecha_creacion ASC
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *";

            return ExecuteWithConnection(conn =>
            {
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
            const string sql = @"
                SELECT * FROM email_queue
                WHERE estado = 'PENDIENTE' 
                  AND proximo_intento <= NOW()
                  AND intentos < max_intentos
                ORDER BY fecha_creacion ASC
                LIMIT @limite";

            return ExecuteWithConnection(conn =>
            {
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
            const string sql = @"
                UPDATE email_queue SET
                    estado = @estado,
                    ultimo_error = @error
                WHERE id = @id";

            ExecuteWithConnection(conn =>
            {
                ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@estado", estado, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@error", error ?? (object)DBNull.Value, NpgsqlDbType.Text);
                });
            });
        }

        public async Task MarcarEnviadoAsync(int id, string messageId)
        {
            const string sql = @"
                UPDATE email_queue SET
                    estado = 'ENVIADO',
                    fecha_envio = NOW(),
                    ultimo_error = NULL
                WHERE id = @id";

            ExecuteWithConnection(conn =>
            {
                ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                });
            });

            _logger.LogInfo(string.Format("Email {0} enviado exitosamente", id));
        }

        public async Task ReprogramarReintentoAsync(int id, TimeSpan delay)
        {
            const string sql = @"
                UPDATE email_queue SET
                    estado = 'PENDIENTE',
                    proximo_intento = @proximo
                WHERE id = @id AND intentos < max_intentos";

            var proximoIntento = DateTime.Now.Add(delay);

            ExecuteWithConnection(conn =>
            {
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
            return new EmailQueueItem
            {
                Id = GetInt(reader, "id"),
                Para = GetString(reader, "para"),
                ParaNombre = GetString(reader, "para_nombre"),
                Asunto = GetString(reader, "asunto"),
                Cuerpo = GetString(reader, "cuerpo"),
                EsHtml = GetBool(reader, "es_html"),
                AdjuntoNombre = GetString(reader, "adjunto_nombre"),
                Estado = GetString(reader, "estado"),
                Intentos = GetInt(reader, "intentos"),
                MaxIntentos = GetInt(reader, "max_intentos"),
                UltimoError = GetString(reader, "ultimo_error"),
                FechaCreacion = GetDateTime(reader, "fecha_creacion"),
                FechaEnvio = GetNullableDateTime(reader, "fecha_envio"),
                ProximoIntento = GetNullableDateTime(reader, "proximo_intento"),
                CorrelationId = GetString(reader, "correlation_id"),
                NumeroOrden = GetString(reader, "numero_orden"),
                OrdenId = GetInt(reader, "orden_id"),
                TipoNotificacion = GetString(reader, "tipo_notificacion")
            };
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

            if (item.Intentos >= item.MaxIntentos)
            {
                // Máximo de intentos alcanzado
                await _queueService.ActualizarEstadoAsync(item.Id, "ERROR", error);
                _logger.LogError(string.Format("Email {0} marcado como ERROR después de {1} intentos",
                    item.Id, item.Intentos), context);
            }
            else
            {
                // Programar reintento con backoff
                var delayIndex = Math.Min(item.Intentos - 1, RetryDelays.Length - 1);
                var delay = RetryDelays[delayIndex];
                await _queueService.ReprogramarReintentoAsync(item.Id, delay);
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
