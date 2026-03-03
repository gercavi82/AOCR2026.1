using System;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace CapaDatos.Services
{
    /// <summary>
    /// Servicio de idempotency para prevenir operaciones duplicadas.
    /// Genera y valida claves únicas por operación+orden+parámetros.
    /// </summary>
    public class IdempotencyService : Infrastructure.BaseDAO
    {
        private readonly ILoggingService _logger;

        public IdempotencyService()
            : base(GetConnectionStringSafe())
        {
            _logger = LoggingServiceFactory.Create();
        }

        public IdempotencyService(string connectionString)
            : base(connectionString)
        {
            _logger = LoggingServiceFactory.Create();
        }

        #region Generación de claves

        /// <summary>
        /// Genera una clave de idempotencia basada en los parámetros de la operación.
        /// La misma combinación siempre produce la misma clave.
        /// </summary>
        public static string GenerarClave(string operacion, int ordenId, params string[] parametrosAdicionales)
        {
            var sb = new StringBuilder();
            sb.Append(operacion ?? "UNKNOWN");
            sb.Append("|");
            sb.Append(ordenId.ToString());

            if (parametrosAdicionales != null)
            {
                foreach (var param in parametrosAdicionales)
                {
                    sb.Append("|");
                    sb.Append(param ?? string.Empty);
                }
            }

            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 32);
            }
        }

        /// <summary>
        /// Genera clave específica para operación FR3.
        /// </summary>
        public static string GenerarClaveFr3(int ordenId, string numeroFactura)
        {
            return GenerarClave("FR3", ordenId, (numeroFactura ?? string.Empty).Trim());
        }

        /// <summary>
        /// Genera clave específica para registro de pago.
        /// </summary>
        public static string GenerarClavePago(int ordenId, string numeroComprobante)
        {
            return GenerarClave("PAGO", ordenId, (numeroComprobante ?? string.Empty).Trim());
        }

        /// <summary>
        /// Genera clave específica para cambio de estado.
        /// </summary>
        public static string GenerarClaveEstado(int ordenId, string estadoNuevo)
        {
            return GenerarClave("ESTADO", ordenId, (estadoNuevo ?? string.Empty).Trim(),
                DateTime.UtcNow.ToString("yyyyMMddHH")); // Ventana de 1 hora
        }

        #endregion

        #region Verificación y registro

        /// <summary>
        /// Verifica si una operación ya fue procesada.
        /// Retorna true si ya existe (operación duplicada).
        /// </summary>
        public bool ExisteOperacion(string clave, out string resultadoPrevio)
        {
            resultadoPrevio = null;

            if (string.IsNullOrWhiteSpace(clave))
                return false;

            try
            {
                string localResultado = null;
                var existe = ExecuteWithConnection(conn =>
                {
                    var sql = @"
                        SELECT resultado, estado 
                        FROM aocr_idempotency_key 
                        WHERE clave = @clave 
                          AND fecha_expiracion > NOW()
                          AND estado = 'COMPLETADO'
                        LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@clave", clave);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                localResultado = reader.IsDBNull(0) ? null : reader.GetString(0);
                                return true;
                            }
                        }
                    }
                    return false;
                });

                resultadoPrevio = localResultado;
                return existe;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Idempotency: Error verificando clave: " + ex.Message);
                return false; // En caso de error, permitir la operación
            }
        }

        /// <summary>
        /// Intenta adquirir un lock para la operación.
        /// Retorna true si se adquirió el lock (operación nueva).
        /// Retorna false si ya existe (duplicada) o si está en proceso.
        /// </summary>
        public bool TryAcquire(string clave, string operacion, int? ordenId, string usuario, out string resultadoPrevio)
        {
            resultadoPrevio = null;

            if (string.IsNullOrWhiteSpace(clave))
                return true; // Sin clave, permitir siempre

            try
            {
                string localResultado = null;
                var acquired = ExecuteWithConnection(conn =>
                {
                    // Intentar insertar. Si ya existe, obtener resultado previo.
                    var sql = @"
                        INSERT INTO aocr_idempotency_key 
                            (clave, operacion, orden_id, estado, usuario, fecha_creacion, fecha_expiracion)
                        VALUES 
                            (@clave, @operacion, @ordenId, 'PROCESANDO', @usuario, NOW(), NOW() + INTERVAL '24 hours')
                        ON CONFLICT (clave) DO NOTHING
                        RETURNING id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@clave", clave);
                        cmd.Parameters.AddWithValue("@operacion", (object)operacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ordenId", ordenId.HasValue ? (object)ordenId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@usuario", (object)usuario ?? DBNull.Value);

                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            // Insert exitoso: operación nueva
                            return true;
                        }
                    }

                    // Ya existía la clave. Verificar si fue completada.
                    var sqlCheck = @"
                        SELECT resultado, estado 
                        FROM aocr_idempotency_key 
                        WHERE clave = @clave
                        LIMIT 1";

                    using (var cmd2 = new NpgsqlCommand(sqlCheck, conn))
                    {
                        cmd2.Parameters.AddWithValue("@clave", clave);

                        using (var reader = cmd2.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var readResult = reader.IsDBNull(0) ? null : reader.GetString(0);
                                var estado = reader.IsDBNull(1) ? null : reader.GetString(1);

                                if (string.Equals(estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase))
                                {
                                    localResultado = readResult;
                                    return false; // Operación duplicada
                                }

                                if (string.Equals(estado, "PROCESANDO", StringComparison.OrdinalIgnoreCase))
                                {
                                    localResultado = "Operación en proceso por otro request.";
                                    return false; // En proceso
                                }

                                // Estado ERROR o expirado
                                localResultado = readResult;
                            }
                        }
                    }

                    // Actualizar para reintento
                    var sqlUpdate = @"
                        UPDATE aocr_idempotency_key 
                        SET estado = 'PROCESANDO', 
                            fecha_creacion = NOW(),
                            fecha_expiracion = NOW() + INTERVAL '24 hours',
                            usuario = @usuario
                        WHERE clave = @clave AND estado != 'COMPLETADO'";

                    using (var cmd3 = new NpgsqlCommand(sqlUpdate, conn))
                    {
                        cmd3.Parameters.AddWithValue("@clave", clave);
                        cmd3.Parameters.AddWithValue("@usuario", (object)usuario ?? DBNull.Value);
                        var rows = cmd3.ExecuteNonQuery();
                        return rows > 0;
                    }
                });

                resultadoPrevio = localResultado;
                return acquired;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Idempotency: Error al adquirir lock: " + ex.Message);
                return true; // En caso de error, permitir la operación (fail-open)
            }
        }

        /// <summary>
        /// Marca una operación como completada y guarda el resultado.
        /// </summary>
        public void MarcarCompletada(string clave, string resultado)
        {
            if (string.IsNullOrWhiteSpace(clave)) return;

            try
            {
                ExecuteWithConnection(conn =>
                {
                    var sql = @"
                        UPDATE aocr_idempotency_key 
                        SET estado = 'COMPLETADO', 
                            resultado = @resultado
                        WHERE clave = @clave";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@clave", clave);
                        cmd.Parameters.AddWithValue("@resultado", (object)resultado ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Idempotency: Error al completar clave: " + ex.Message);
            }
        }

        /// <summary>
        /// Marca una operación como fallida (permite reintento).
        /// </summary>
        public void MarcarError(string clave, string error)
        {
            if (string.IsNullOrWhiteSpace(clave)) return;

            try
            {
                ExecuteWithConnection(conn =>
                {
                    var sql = @"
                        UPDATE aocr_idempotency_key 
                        SET estado = 'ERROR', 
                            resultado = @error
                        WHERE clave = @clave";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@clave", clave);
                        cmd.Parameters.AddWithValue("@error", (object)error ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Idempotency: Error al marcar clave como error: " + ex.Message);
            }
        }

        /// <summary>
        /// Libera un lock sin completar (rollback manual).
        /// </summary>
        public void Liberar(string clave)
        {
            if (string.IsNullOrWhiteSpace(clave)) return;

            try
            {
                ExecuteWithConnection(conn =>
                {
                    var sql = @"DELETE FROM aocr_idempotency_key WHERE clave = @clave AND estado = 'PROCESANDO'";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@clave", clave);
                        cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Idempotency: Error al liberar clave: " + ex.Message);
            }
        }

        private static string GetConnectionStringSafe()
        {
            var config = System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"];
            return config != null ? config.ConnectionString : string.Empty;
        }
    }
}
