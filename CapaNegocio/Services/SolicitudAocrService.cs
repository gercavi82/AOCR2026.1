using System;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Services;
using Npgsql;

namespace CapaNegocio.Services
{
    public class SolicitudAocrService
    {
        public const string MensajeNuevaOrdenRequerida = "La Solicitud AOCR fue completada correctamente. Para iniciar una nueva solicitud o habilitar un nuevo proceso, debe generar una nueva Orden de Recaudación y completar el pago correspondiente.";
        public const string MensajePagoPendiente = "El módulo de Solicitud AOCR se habilitará cuando Financiero apruebe el pago correspondiente.";
        public const string ObservacionBloqueoRt = "El RT finalizó la Solicitud AOCR. El módulo queda bloqueado hasta una nueva Orden de Recaudación y aprobación de pago.";

        private readonly string _connectionString;
        private readonly ILoggingService _logger;
        private readonly OrdenRecaudacionService _ordenService;
        private readonly OrdenRecaudacionDAO _ordenDao;

        public SolicitudAocrService()
            : this(
                new SecureConfigurationService().GetConnectionString("PostgreSQL")
                ?? new SecureConfigurationService().GetConnectionString("AOCRConnection")
                ?? string.Empty,
                new OrdenRecaudacionDAO())
        {
        }

        public SolicitudAocrService(string connectionString, OrdenRecaudacionDAO ordenDao = null)
        {
            _connectionString = connectionString ?? string.Empty;
            _ordenDao = ordenDao ?? new OrdenRecaudacionDAO();
            _ordenService = new OrdenRecaudacionService(_connectionString, _ordenDao);
            _logger = LoggingServiceFactory.Create();
        }

        public bool PuedeRtEditarSolicitud(int codigoSolicitud, int codigoUsuarioRt, out string mensaje)
        {
            mensaje = string.Empty;

            if (codigoSolicitud <= 0 || codigoUsuarioRt <= 0)
            {
                mensaje = "Solicitud inválida.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                mensaje = "No existe una conexión configurada para validar la Solicitud AOCR.";
                return false;
            }

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                    EnsureSolicitudRtColumns(cn);

                    const string sql = @"
                        SELECT
                            s.codigo_usuario,
                            s.estado,
                            COALESCE(s.pago_aprobado, FALSE) AS pago_aprobado,
                            COALESCE(s.modulo_solicitud_rt_habilitado, FALSE) AS modulo_habilitado,
                            COALESCE(s.requiere_nueva_orden, FALSE) AS requiere_nueva_orden,
                            COALESCE(s.solicitud_finalizada_rt, FALSE) AS solicitud_finalizada_rt,
                            COALESCE(o.estado, '') AS estado_orden
                        FROM aocr_tbsolicitud s
                        LEFT JOIN LATERAL (
                            SELECT estado
                            FROM aocr_or_orden o
                            WHERE UPPER(TRIM(COALESCE(o.estado, ''))) <> 'ANULADA'
                              AND (
                                  TRIM(COALESCE(o.codigo_solicitud::text, '')) = s.codigo_solicitud::text
                                  OR o.codigo_usuario::text = s.codigo_usuario::text
                              )
                            ORDER BY
                                CASE
                                    WHEN TRIM(COALESCE(o.codigo_solicitud::text, '')) = s.codigo_solicitud::text THEN 0
                                    ELSE 1
                                END,
                                o.fecha_creacion DESC NULLS LAST,
                                o.id DESC
                            LIMIT 1
                        ) o ON TRUE
                        WHERE s.codigo_solicitud = @codigo_solicitud
                          AND s.deleted_at IS NULL
                        LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read())
                            {
                                mensaje = "La solicitud no existe.";
                                return false;
                            }

                            var propietario = rd["codigo_usuario"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_usuario"]);
                            if (propietario != codigoUsuarioRt)
                            {
                                mensaje = "No tiene permisos sobre esta solicitud.";
                                return false;
                            }

                            var pagoAprobado = rd["pago_aprobado"] != DBNull.Value && Convert.ToBoolean(rd["pago_aprobado"]);
                            var moduloHabilitado = rd["modulo_habilitado"] != DBNull.Value && Convert.ToBoolean(rd["modulo_habilitado"]);
                            var requiereNuevaOrden = rd["requiere_nueva_orden"] != DBNull.Value && Convert.ToBoolean(rd["requiere_nueva_orden"]);
                            var solicitudFinalizada = rd["solicitud_finalizada_rt"] != DBNull.Value && Convert.ToBoolean(rd["solicitud_finalizada_rt"]);
                            var estadoOrden = rd["estado_orden"] == DBNull.Value ? string.Empty : rd["estado_orden"].ToString();
                            var ordenVigente = _ordenService.PuedeRtContinuarFlujoAocr(codigoSolicitud, codigoUsuarioRt);
                            var pagoAprobadoPorOrden = _ordenDao.TieneAprobacionFinancieraSolicitud(codigoSolicitud, codigoUsuarioRt);
                            var ordenPagada = EstadoOrden.EsPagado(estadoOrden) || pagoAprobadoPorOrden;
                            var ordenGeneradaUsuario = _ordenDao.ExisteORGeneradaOPagada(codigoUsuarioRt);

                            if (solicitudFinalizada || requiereNuevaOrden)
                            {
                                mensaje = MensajeNuevaOrdenRequerida;
                                return false;
                            }

                            if (!ordenVigente && !ordenPagada)
                            {
                                mensaje = ordenGeneradaUsuario
                                    ? MensajePagoPendiente
                                    : "Debe generar la Orden de Recaudación para continuar con el proceso AOCR.";
                                return false;
                            }

                            if (!pagoAprobado && pagoAprobadoPorOrden)
                            {
                                pagoAprobado = true;
                            }

                            if (pagoAprobadoPorOrden && !moduloHabilitado)
                            {
                                moduloHabilitado = true;
                            }

                            var pagoOk = pagoAprobado || pagoAprobadoPorOrden;
                            var moduloOk = moduloHabilitado || pagoAprobadoPorOrden || EstadoOrden.EsPagado(estadoOrden);
                            var ordenOk = ordenPagada || ordenVigente;

                            if (!pagoOk || !moduloOk || !ordenOk)
                            {
                                mensaje = (ordenVigente || ordenGeneradaUsuario)
                                    ? MensajePagoPendiente
                                    : "Debe generar la Orden de Recaudación para continuar con el proceso AOCR.";
                                return false;
                            }

                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                mensaje = "No fue posible validar la habilitación de la Solicitud AOCR.";
                return false;
            }
        }

        public bool FinalizarSolicitudRt(int codigoSolicitud, int codigoUsuarioRt, string usuario, out string mensaje)
        {
            mensaje = string.Empty;

            if (codigoSolicitud <= 0 || codigoUsuarioRt <= 0)
            {
                mensaje = "Solicitud inválida para cierre del ciclo RT.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                mensaje = "No existe una conexión configurada para finalizar la Solicitud AOCR.";
                return false;
            }

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                    EnsureSolicitudRtColumns(cn);

                    string estadoActual;
                    using (var cmdEstado = new NpgsqlCommand(@"
                        SELECT estado
                        FROM aocr_tbsolicitud
                        WHERE codigo_solicitud = @codigo_solicitud
                          AND codigo_usuario = @codigo_usuario
                          AND deleted_at IS NULL
                        LIMIT 1;", cn))
                    {
                        cmdEstado.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                        cmdEstado.Parameters.AddWithValue("@codigo_usuario", codigoUsuarioRt);
                        var scalar = cmdEstado.ExecuteScalar();
                        if (scalar == null || scalar == DBNull.Value)
                        {
                            mensaje = "La solicitud no existe o no pertenece al RT autenticado.";
                            return false;
                        }

                        estadoActual = scalar.ToString();
                    }

                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE aocr_tbsolicitud
                        SET solicitud_finalizada_rt = TRUE,
                            fecha_finalizacion_rt = NOW(),
                            requiere_nueva_orden = TRUE,
                            modulo_solicitud_rt_habilitado = FALSE,
                            updated_at = NOW(),
                            updated_by = @usuario
                        WHERE codigo_solicitud = @codigo_solicitud
                          AND codigo_usuario = @codigo_usuario
                          AND deleted_at IS NULL;", cn))
                    {
                        cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                        cmd.Parameters.AddWithValue("@codigo_usuario", codigoUsuarioRt);
                        cmd.Parameters.AddWithValue("@usuario", (object)(usuario ?? codigoUsuarioRt.ToString()) ?? DBNull.Value);

                        if (cmd.ExecuteNonQuery() <= 0)
                        {
                            mensaje = "No fue posible bloquear la Solicitud AOCR para el RT.";
                            return false;
                        }
                    }

                    try
                    {
                        new HistorialEstadoDAO().RegistrarCambio(
                            codigoSolicitud,
                            estadoActual,
                            estadoActual,
                            codigoUsuarioRt,
                            ObservacionBloqueoRt);
                    }
                    catch (Exception exHistorial)
                    {
                        _logger.LogWarning("SolicitudAocrService.FinalizarSolicitudRt: no se pudo registrar historial. " + exHistorial.Message);
                    }

                    mensaje = MensajeNuevaOrdenRequerida;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                mensaje = "No fue posible cerrar el ciclo RT de la Solicitud AOCR.";
                return false;
            }
        }

        public static void EnsureSolicitudRtColumns(NpgsqlConnection cn)
        {
            const string sql = @"
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS solicitud_finalizada_rt BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS fecha_finalizacion_rt TIMESTAMP NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS requiere_nueva_orden BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS modulo_solicitud_rt_habilitado BOOLEAN DEFAULT FALSE;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}