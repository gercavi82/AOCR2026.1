using System;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using CapaDatos.Infrastructure;

namespace CapaDatos.Services
{
    /// <summary>
    /// Interface para servicio de auditoría
    /// </summary>
    public interface IAuditService
    {
        Task RegistrarCambioEstadoAsync(CambioEstadoAudit audit);
        Task RegistrarAccionAsync(AccionAudit audit);
    }

    /// <summary>
    /// Registro de cambio de estado
    /// </summary>
    public class CambioEstadoAudit
    {
        public string TipoEntidad { get; set; } // "ORDEN", "PAGO"
        public int EntidadId { get; set; }
        public string NumeroReferencia { get; set; } // NumeroOrden o NumeroComprobante
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Usuario { get; set; }
        public string Motivo { get; set; }
        public string IpOrigen { get; set; }
        public string CorrelationId { get; set; }
    }

    /// <summary>
    /// Registro de acción general
    /// </summary>
    public class AccionAudit
    {
        public string TipoAccion { get; set; }
        public string TipoEntidad { get; set; }
        public int? EntidadId { get; set; }
        public string Descripcion { get; set; }
        public string Usuario { get; set; }
        public string DatosAnteriores { get; set; } // JSON
        public string DatosNuevos { get; set; } // JSON
        public string IpOrigen { get; set; }
        public string CorrelationId { get; set; }
    }

    /// <summary>
    /// Implementación del servicio de auditoría
    /// </summary>
    public class AuditService : BaseDAO, IAuditService
    {
        private readonly ILoggingService _logger;

        public AuditService(string connectionString) : base(connectionString)
        {
            _logger = LoggingServiceFactory.Create();
        }

        public async Task RegistrarCambioEstadoAsync(CambioEstadoAudit audit)
        {
            const string sql = @"
                INSERT INTO audit_cambios_estado (
                    tipo_entidad, entidad_id, numero_referencia,
                    estado_anterior, estado_nuevo, usuario,
                    motivo, ip_origen, correlation_id, fecha_cambio
                ) VALUES (
                    @tipo_entidad, @entidad_id, @numero_referencia,
                    @estado_anterior, @estado_nuevo, @usuario,
                    @motivo, @ip_origen, @correlation_id, @fecha_cambio
                )";

            try
            {
                ExecuteWithConnection(conn =>
                {
                    ExecuteNonQuery(conn, sql, cmd =>
                    {
                        AddParameter(cmd, "@tipo_entidad", audit.TipoEntidad, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@entidad_id", audit.EntidadId, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@numero_referencia", audit.NumeroReferencia ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@estado_anterior", audit.EstadoAnterior ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@estado_nuevo", audit.EstadoNuevo, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@usuario", audit.Usuario, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@motivo", audit.Motivo ?? (object)DBNull.Value, NpgsqlDbType.Text);
                        AddParameter(cmd, "@ip_origen", audit.IpOrigen ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@correlation_id", audit.CorrelationId ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@fecha_cambio", DateTime.Now, NpgsqlDbType.Timestamp);
                    });
                });

                // Log estructurado
                _logger.LogAudit(
                    string.Format("CAMBIO_ESTADO: {0} -> {1}", audit.EstadoAnterior, audit.EstadoNuevo),
                    audit.TipoEntidad,
                    audit.EntidadId,
                    new LogContext
                    {
                        CorrelationId = audit.CorrelationId,
                        UserId = audit.Usuario,
                        NumeroOrden = audit.TipoEntidad == "ORDEN" ? audit.NumeroReferencia : null
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = audit.CorrelationId,
                    ErrorCode = "AUDIT_ERROR"
                });
                // No propagar error de auditoría para no afectar operación principal
            }
        }

        public async Task RegistrarAccionAsync(AccionAudit audit)
        {
            const string sql = @"
                INSERT INTO audit_acciones (
                    tipo_accion, tipo_entidad, entidad_id,
                    descripcion, usuario, datos_anteriores, datos_nuevos,
                    ip_origen, correlation_id, fecha_accion
                ) VALUES (
                    @tipo_accion, @tipo_entidad, @entidad_id,
                    @descripcion, @usuario, @datos_anteriores::jsonb, @datos_nuevos::jsonb,
                    @ip_origen, @correlation_id, @fecha_accion
                )";

            try
            {
                ExecuteWithConnection(conn =>
                {
                    ExecuteNonQuery(conn, sql, cmd =>
                    {
                        AddParameter(cmd, "@tipo_accion", audit.TipoAccion, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@tipo_entidad", audit.TipoEntidad ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@entidad_id", audit.EntidadId ?? (object)DBNull.Value, NpgsqlDbType.Integer);
                        AddParameter(cmd, "@descripcion", audit.Descripcion ?? (object)DBNull.Value, NpgsqlDbType.Text);
                        AddParameter(cmd, "@usuario", audit.Usuario, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@datos_anteriores", audit.DatosAnteriores ?? (object)DBNull.Value, NpgsqlDbType.Text);
                        AddParameter(cmd, "@datos_nuevos", audit.DatosNuevos ?? (object)DBNull.Value, NpgsqlDbType.Text);
                        AddParameter(cmd, "@ip_origen", audit.IpOrigen ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@correlation_id", audit.CorrelationId ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                        AddParameter(cmd, "@fecha_accion", DateTime.Now, NpgsqlDbType.Timestamp);
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = audit.CorrelationId,
                    ErrorCode = "AUDIT_ERROR"
                });
            }
        }
    }
}
