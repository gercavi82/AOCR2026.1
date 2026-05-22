using System;
using System.Collections.Generic;
using System.Text;
using CapaDatos.Infrastructure;
using Npgsql;

namespace CapaDatos.Services
{
    /// <summary>
    /// Servicio de auditoría completa para operaciones críticas.
    /// Registra cambios de estado, modificaciones de datos, y acciones de usuario.
    /// Resiliente: nunca lanza excepciones al caller.
    /// </summary>
    public class AuditTrailService : Infrastructure.BaseDAO
    {
        private readonly ILoggingService _logger;
        private static volatile bool _schemaEnsured;
        private static readonly object _schemaLock = new object();

        public AuditTrailService()
            : base(GetConnectionStringSafe())
        {
            _logger = LoggingServiceFactory.Create();
            EnsureSchema();
        }

        #region Registro de auditoría

        /// <summary>
        /// Registra un cambio de estado en una orden.
        /// </summary>
        public void RegistrarCambioEstado(
            int ordenId,
            string estadoAnterior,
            string estadoNuevo,
            int? usuarioId,
            string usuarioNombre,
            string ipOrigen = null,
            string modulo = "OrdenRecaudacion",
            string metadata = null)
        {
            RegistrarAuditoria(
                tabla: "aocr_or_orden",
                registroId: ordenId,
                accion: "CAMBIO_ESTADO",
                campoModificado: "estado",
                valorAnterior: estadoAnterior,
                valorNuevo: estadoNuevo,
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                ipOrigen: ipOrigen,
                modulo: modulo,
                metadata: metadata);
        }

        /// <summary>
        /// Registra la generación de un FR3.
        /// </summary>
        public void RegistrarFr3Generado(
            int ordenId,
            string fr3Numero,
            int? usuarioId,
            string usuarioNombre,
            string ipOrigen = null)
        {
            RegistrarAuditoria(
                tabla: "aocr_or_orden",
                registroId: ordenId,
                accion: "FR3_GENERADO",
                campoModificado: "fr3_numero",
                valorAnterior: null,
                valorNuevo: fr3Numero,
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                ipOrigen: ipOrigen,
                modulo: "FR3");
        }

        /// <summary>
        /// Registra un error en la generación de FR3.
        /// </summary>
        public void RegistrarFr3Error(
            int ordenId,
            string error,
            int? usuarioId,
            string usuarioNombre,
            string ipOrigen = null)
        {
            RegistrarAuditoria(
                tabla: "aocr_or_orden",
                registroId: ordenId,
                accion: "FR3_ERROR",
                campoModificado: "fr3_estado",
                valorAnterior: null,
                valorNuevo: "FR3_ERROR: " + (error ?? "desconocido"),
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                ipOrigen: ipOrigen,
                modulo: "FR3");
        }

        /// <summary>
        /// Registra un registro de pago.
        /// </summary>
        public void RegistrarPago(
            int ordenId,
            int pagoId,
            decimal monto,
            string metodoPago,
            int? usuarioId,
            string usuarioNombre,
            string ipOrigen = null)
        {
            RegistrarAuditoria(
                tabla: "aocr_tb_pago",
                registroId: pagoId,
                accion: "INSERT",
                campoModificado: null,
                valorAnterior: null,
                valorNuevo: string.Format("Orden:{0}, Monto:{1:0.00}, Método:{2}", ordenId, monto, metodoPago),
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                ipOrigen: ipOrigen,
                modulo: "Pagos");
        }

        /// <summary>
        /// Registra la aprobación/rechazo de un pago por Financiero.
        /// </summary>
        public void RegistrarValidacionPago(
            int ordenId,
            int pagoId,
            string accion,
            string motivo,
            int? usuarioId,
            string usuarioNombre,
            string ipOrigen = null)
        {
            RegistrarAuditoria(
                tabla: "aocr_tb_pago",
                registroId: pagoId,
                accion: accion,
                campoModificado: "estado",
                valorAnterior: null,
                valorNuevo: string.Format("Orden:{0}, Acción:{1}, Motivo:{2}", ordenId, accion, motivo ?? "N/A"),
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                ipOrigen: ipOrigen,
                modulo: "Financiero");
        }

        /// <summary>
        /// Registra acceso a una vista/acción protegida.
        /// </summary>
        public void RegistrarAcceso(
            string controllerAction,
            int? registroId,
            int? usuarioId,
            string usuarioNombre,
            string ipOrigen = null,
            string userAgent = null)
        {
            RegistrarAuditoria(
                tabla: controllerAction ?? "ACCESO",
                registroId: registroId,
                accion: "ACCESO",
                campoModificado: null,
                valorAnterior: null,
                valorNuevo: null,
                usuarioId: usuarioId,
                usuarioNombre: usuarioNombre,
                ipOrigen: ipOrigen,
                modulo: "Seguridad",
                userAgent: userAgent);
        }

        #endregion

        #region Método base

        /// <summary>
        /// Registra una entrada de auditoría genérica.
        /// NUNCA lanza excepciones. Los fallos se registran en el logger interno.
        /// </summary>
        public void RegistrarAuditoria(
            string tabla,
            int? registroId,
            string accion,
            string campoModificado = null,
            string valorAnterior = null,
            string valorNuevo = null,
            int? usuarioId = null,
            string usuarioNombre = null,
            string ipOrigen = null,
            string modulo = null,
            string metadata = null,
            string userAgent = null)
        {
            try
            {
                var correlacionId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();

                ExecuteWithConnection(conn =>
                {
                    var sql = @"
                        INSERT INTO aocr_audit_trail 
                            (tabla, registro_id, accion, campo_modificado, valor_anterior, valor_nuevo,
                             usuario_id, usuario_nombre, ip_origen, user_agent, modulo, correlacion_id,
                             metadata, fecha_creacion)
                        VALUES 
                            (@tabla, @registroId, @accion, @campo, @valAnterior, @valNuevo,
                             @usuarioId, @usuario, @ip, @ua, @modulo, @correlacion,
                             @metadata, NOW())";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@tabla", (object)tabla ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@registroId", registroId.HasValue ? (object)registroId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@accion", (object)accion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@campo", (object)campoModificado ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@valAnterior", (object)Truncar(valorAnterior, 2000) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@valNuevo", (object)Truncar(valorNuevo, 2000) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@usuarioId", usuarioId.HasValue ? (object)usuarioId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@usuario", (object)Truncar(usuarioNombre, 100) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ip", (object)ipOrigen ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ua", (object)Truncar(userAgent, 500) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@modulo", (object)modulo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@correlacion", correlacionId);
                        cmd.Parameters.AddWithValue("@metadata", (object)metadata ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (Exception ex)
            {
                // NUNCA lanzar — la auditoría no debe interrumpir operaciones de negocio
                try
                {
                    _logger.LogWarning(
                        string.Format(
                            "AuditTrail error [Tabla={0}, RegistroId={1}, Accion={2}, Modulo={3}]: {4}",
                            tabla ?? "N/D",
                            registroId.HasValue ? registroId.Value.ToString() : "N/D",
                            accion ?? "N/D",
                            modulo ?? "N/D",
                            ConstruirDetalleTecnico(ex)));
                }
                catch { /* silenciar completamente */ }
            }
        }

        private static string ConstruirDetalleTecnico(Exception ex)
        {
            if (ex == null)
            {
                return "Sin detalle técnico.";
            }

            var sb = new StringBuilder();
            var actual = ex;
            var nivel = 0;
            while (actual != null && nivel < 5)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" | ");
                }

                sb.Append(actual.GetType().Name);
                sb.Append(": ");
                sb.Append(actual.Message);

                var dataAccess = actual as DataAccessException;
                if (dataAccess != null && !string.IsNullOrWhiteSpace(dataAccess.ErrorCode))
                {
                    sb.Append(" [ErrorCode=");
                    sb.Append(dataAccess.ErrorCode);
                    sb.Append("]");
                }

                var postgres = actual as PostgresException;
                if (postgres != null)
                {
                    if (!string.IsNullOrWhiteSpace(postgres.SqlState))
                    {
                        sb.Append(" [SqlState=");
                        sb.Append(postgres.SqlState);
                        sb.Append("]");
                    }

                    if (!string.IsNullOrWhiteSpace(postgres.ConstraintName))
                    {
                        sb.Append(" [Constraint=");
                        sb.Append(postgres.ConstraintName);
                        sb.Append("]");
                    }
                }

                actual = actual.InnerException;
                nivel++;
            }

            return sb.ToString();
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
                            CREATE TABLE IF NOT EXISTS aocr_audit_trail (
                                id                  SERIAL PRIMARY KEY,
                                tabla               VARCHAR(100) NOT NULL,
                                registro_id         INTEGER,
                                accion              VARCHAR(20) NOT NULL,
                                campo_modificado    VARCHAR(100),
                                valor_anterior      TEXT,
                                valor_nuevo         TEXT,
                                usuario_id          INTEGER,
                                usuario_nombre      VARCHAR(100),
                                ip_origen           VARCHAR(45),
                                user_agent          VARCHAR(500),
                                modulo              VARCHAR(50),
                                correlacion_id      VARCHAR(100),
                                metadata            TEXT,
                                fecha_creacion      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                            );
                            CREATE INDEX IF NOT EXISTS idx_audit_tabla ON aocr_audit_trail(tabla, registro_id);
                            CREATE INDEX IF NOT EXISTS idx_audit_accion ON aocr_audit_trail(accion);
                            CREATE INDEX IF NOT EXISTS idx_audit_usuario ON aocr_audit_trail(usuario_id);
                            CREATE INDEX IF NOT EXISTS idx_audit_fecha ON aocr_audit_trail(fecha_creacion);
                            CREATE INDEX IF NOT EXISTS idx_audit_modulo ON aocr_audit_trail(modulo);";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    });
                    _schemaEnsured = true;
                    _logger.LogInfo("AuditTrailService: Tabla aocr_audit_trail verificada/creada.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("AuditTrailService: Error al crear tabla: " + ex.Message);
                }
            }
        }

        #endregion

        #region Consultas de auditoría

        /// <summary>
        /// Obtiene el historial de auditoría para un registro.
        /// </summary>
        public List<AuditEntry> ObtenerHistorial(string tabla, int registroId, int limit = 50)
        {
            return ExecuteWithConnection(conn =>
            {
                var entries = new List<AuditEntry>();
                var sql = @"
                    SELECT id, accion, campo_modificado, valor_anterior, valor_nuevo,
                           usuario_nombre, ip_origen, modulo, fecha_creacion
                    FROM aocr_audit_trail 
                    WHERE tabla = @tabla AND registro_id = @registroId
                    ORDER BY fecha_creacion DESC 
                    LIMIT @limit";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@tabla", tabla);
                    cmd.Parameters.AddWithValue("@registroId", registroId);
                    cmd.Parameters.AddWithValue("@limit", limit);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            entries.Add(new AuditEntry
                            {
                                Id = reader.GetInt32(0),
                                Accion = reader.IsDBNull(1) ? null : reader.GetString(1),
                                CampoModificado = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ValorAnterior = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ValorNuevo = reader.IsDBNull(4) ? null : reader.GetString(4),
                                UsuarioNombre = reader.IsDBNull(5) ? null : reader.GetString(5),
                                IpOrigen = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Modulo = reader.IsDBNull(7) ? null : reader.GetString(7),
                                FechaCreacion = reader.GetDateTime(8)
                            });
                        }
                    }
                }
                return entries;
            });
        }

        /// <summary>
        /// Obtiene auditoría por orden (incluyendo pagos y FR3).
        /// </summary>
        public List<AuditEntry> ObtenerHistorialOrden(int ordenId, int limit = 100)
        {
            return ExecuteWithConnection(conn =>
            {
                var entries = new List<AuditEntry>();
                var sql = @"
                    SELECT id, tabla, accion, campo_modificado, valor_anterior, valor_nuevo,
                           usuario_nombre, ip_origen, modulo, fecha_creacion
                    FROM aocr_audit_trail 
                    WHERE (tabla = 'aocr_or_orden' AND registro_id = @ordenId)
                       OR (modulo IN ('FR3', 'Pagos', 'Financiero') 
                           AND (valor_nuevo LIKE '%Orden:' || @ordenId::text || '%'
                                OR valor_nuevo LIKE '%' || @ordenId::text || '%'))
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
                            entries.Add(new AuditEntry
                            {
                                Id = reader.GetInt32(0),
                                Tabla = reader.IsDBNull(1) ? null : reader.GetString(1),
                                Accion = reader.IsDBNull(2) ? null : reader.GetString(2),
                                CampoModificado = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ValorAnterior = reader.IsDBNull(4) ? null : reader.GetString(4),
                                ValorNuevo = reader.IsDBNull(5) ? null : reader.GetString(5),
                                UsuarioNombre = reader.IsDBNull(6) ? null : reader.GetString(6),
                                IpOrigen = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Modulo = reader.IsDBNull(8) ? null : reader.GetString(8),
                                FechaCreacion = reader.GetDateTime(9)
                            });
                        }
                    }
                }
                return entries;
            });
        }

        #endregion

        #region Helpers

        private static string Truncar(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private static string GetConnectionStringSafe()
        {
            var config = System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"];
            return config != null ? config.ConnectionString : string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// Entrada de auditoría para display.
    /// </summary>
    public class AuditEntry
    {
        public int Id { get; set; }
        public string Tabla { get; set; }
        public string Accion { get; set; }
        public string CampoModificado { get; set; }
        public string ValorAnterior { get; set; }
        public string ValorNuevo { get; set; }
        public string UsuarioNombre { get; set; }
        public string IpOrigen { get; set; }
        public string Modulo { get; set; }
        public DateTime FechaCreacion { get; set; }

        public string AccionBadge
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Accion)) return "secondary";
                switch (Accion.ToUpperInvariant())
                {
                    case "INSERT": return "success";
                    case "UPDATE": return "info";
                    case "DELETE": return "danger";
                    case "CAMBIO_ESTADO": return "primary";
                    case "FR3_GENERADO": return "success";
                    case "FR3_ERROR": return "danger";
                    case "ACCESO": return "secondary";
                    default: return "info";
                }
            }
        }

        public string AccionIcono
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Accion)) return "fa-question";
                switch (Accion.ToUpperInvariant())
                {
                    case "INSERT": return "fa-plus-circle";
                    case "UPDATE": return "fa-pen";
                    case "DELETE": return "fa-trash";
                    case "CAMBIO_ESTADO": return "fa-exchange-alt";
                    case "FR3_GENERADO": return "fa-file-invoice";
                    case "FR3_ERROR": return "fa-exclamation-triangle";
                    case "ACCESO": return "fa-eye";
                    default: return "fa-info-circle";
                }
            }
        }
    }
}
