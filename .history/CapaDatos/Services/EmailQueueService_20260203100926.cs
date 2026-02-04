using System;
using System.Collections.Generic;
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
    /// NOTA: Algunas propiedades no existen en la base de datos.
    /// Estas propiedades se usan solo en memoria para pasar datos al procesador de emails:
    /// - ParaNombre, EsHtml: Para formateo de emails
    /// - AdjuntoNombre, AdjuntoContenido, AdjuntoMimeType: Para adjuntos (no se persisten en BD)
    /// - CorrelationId, NumeroOrden, TipoNotificacion: Para logging y trazabilidad
    /// - MaxIntentos: Para lógica de reintentos
    /// 
    /// Columnas en la base de datos real:
    /// - id, to_address, subject, body, status, solicitud_id, created_at, proximo_intento
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
        public int? OrdenId { get; set; } // Columna: solicitud_id (FK a aocr_tbsolicitud)
        
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
                    to_address, subject, body, status,
                    solicitud_id, created_at, proximo_intento
                ) VALUES (
                    @to_address, @subject, @body, @status,
                    @solicitud_id, @created_at, @proximo_intento
                ) RETURNING id";

            return ExecuteWithConnection(conn =>
            {
                return ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@to_address", item.Para, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@subject", item.Asunto, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@body", item.Cuerpo, NpgsqlDbType.Text);
                    AddParameter(cmd, "@status", EstadoEmail.Pendiente, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@solicitud_id", item.OrdenId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@created_at", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@proximo_intento", DateTime.Now, NpgsqlDbType.Timestamp);
                });
            });
        }

        public async Task<EmailQueueItem> ObtenerSiguienteAsync()
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
                WHERE status = 'PENDIENTE' 
                  AND proximo_intento <= NOW()
                ORDER BY created_at ASC
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
                    status = @status
                WHERE id = @id";

            ExecuteWithConnection(conn =>
            {
                ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@status", estado, NpgsqlDbType.Varchar);
                });
            });
        }

        public async Task MarcarEnviadoAsync(int id, string messageId)
        {
            const string sql = @"
                UPDATE email_queue SET
                    status = 'ENVIADO'
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
                    status = 'PENDIENTE',
                    proximo_intento = @proximo
                WHERE id = @id";

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
                Para = GetString(reader, "to_address"),
                Asunto = GetString(reader, "subject"),
                Cuerpo = GetString(reader, "body"),
                Estado = GetString(reader, "status"),
                FechaCreacion = GetDateTime(reader, "created_at"),
                ProximoIntento = GetNullableDateTime(reader, "proximo_intento"),
                OrdenId = GetValue<int?>(reader, "solicitud_id")
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
