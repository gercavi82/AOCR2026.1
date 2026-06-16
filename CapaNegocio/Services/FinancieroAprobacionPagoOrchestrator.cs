using System;
using System.Globalization;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;
using CapaNegocio.Helpers;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class FinancieroAprobacionPagoResult
    {
        public bool Exito { get; set; }
        public bool Idempotente { get; set; }
        public string Error { get; set; }
        public int OrdenId { get; set; }
        public int SolicitudId { get; set; }
        public string NumeroOrden { get; set; }
        public string NumeroSolicitud { get; set; }
    }

    /// <summary>
    /// Orquesta la aprobación financiera en una sola transacción: pago, cierre de orden, habilitación solicitud y correo RT.
    /// </summary>
    public sealed class FinancieroAprobacionPagoOrchestrator
    {
        private readonly string _connectionString;
        private readonly ILoggingService _logger;
        private readonly ComprobanteService _comprobanteService = new ComprobanteService();
        private readonly AocrPostPagoWorkflowService _postPagoWorkflow;
        private readonly AocrCompaniaContextService _companiaContextService = new AocrCompaniaContextService();

        public FinancieroAprobacionPagoOrchestrator()
            : this(new SecureConfigurationService().GetConnectionString("PostgreSQL")
                ?? new SecureConfigurationService().GetConnectionString("AOCRConnection")
                ?? string.Empty)
        {
        }

        public FinancieroAprobacionPagoOrchestrator(string connectionString)
        {
            _connectionString = connectionString ?? string.Empty;
            _logger = LoggingServiceFactory.Create();
            _postPagoWorkflow = new AocrPostPagoWorkflowService(_connectionString);
        }

        public FinancieroAprobacionPagoResult AprobarPagoCompleto(
            int ordenId,
            int? pagoId,
            string usuarioFinanciero,
            int usuarioFinancieroId)
        {
            var result = new FinancieroAprobacionPagoResult { OrdenId = ordenId };

            if (ordenId <= 0)
            {
                result.Error = "Orden inválida.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                result.Error = "Conexión a base de datos no configurada.";
                return result;
            }

            if (!_comprobanteService.ExisteComprobanteValido(ordenId, out var mensajeComprobante))
            {
                result.Error = mensajeComprobante;
                return result;
            }

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        var orden = LeerOrdenParaAprobacion(cn, tx, ordenId);
                        if (orden == null)
                        {
                            result.Error = "Orden no encontrada.";
                            return result;
                        }

                        result.NumeroOrden = orden.NumeroOrden;

                        if (OrdenRecaudacionOperativaHelper.EsOrdenCerradaPostAprobacionFinanciera(orden.Estado))
                        {
                            if (orden.CodigoSolicitud > 0)
                            {
                                if (!ActualizarEstadoOrdenCerrada(cn, tx, ordenId, usuarioFinanciero))
                                {
                                    tx.Rollback();
                                    result.Error = "No se pudo sincronizar el cierre operativo de la orden.";
                                    return result;
                                }

                                string errorSync;
                                if (!_postPagoWorkflow.EjecutarHabilitacionPostAprobacionEnTransaccion(
                                    cn,
                                    tx,
                                    ordenId,
                                    orden.CodigoSolicitud,
                                    usuarioFinanciero,
                                    usuarioFinancieroId,
                                    encolarCorreoRt: true,
                                    out errorSync))
                                {
                                    tx.Rollback();
                                    result.Error = errorSync ?? "No se pudo habilitar la Solicitud AOCR.";
                                    return result;
                                }
                            }

                            tx.Commit();
                            result.Exito = true;
                            result.Idempotente = true;
                            result.SolicitudId = orden.CodigoSolicitud;
                            result.NumeroSolicitud = ObtenerNumeroSolicitud(cn, orden.CodigoSolicitud);
                            return result;
                        }

                        var estadoNormalizado = EstadoOrden.NormalizarEstado(orden.Estado);
                        if (estadoNormalizado == EstadoOrden.Anulada)
                        {
                            result.Error = "No se puede aprobar una orden anulada.";
                            return result;
                        }

                        if (estadoNormalizado != EstadoOrden.EnRevisionFinanciera)
                        {
                            result.Error = "Solo se pueden aprobar órdenes en revisión financiera con comprobante cargado.";
                            return result;
                        }

                        var codigoSolicitud = orden.CodigoSolicitud;
                        if (codigoSolicitud <= 0 || !ExisteSolicitud(cn, tx, codigoSolicitud))
                        {
                            codigoSolicitud = CrearSolicitudDesdeOrden(cn, tx, orden);
                            if (codigoSolicitud <= 0)
                            {
                                tx.Rollback();
                                result.Error = "No se pudo crear ni vincular la Solicitud AOCR asociada a la orden.";
                                return result;
                            }

                            if (!ActualizarCodigoSolicitudOrden(cn, tx, ordenId, codigoSolicitud))
                            {
                                tx.Rollback();
                                result.Error = "No se pudo vincular la Solicitud AOCR con la orden.";
                                return result;
                            }

                            orden.CodigoSolicitud = codigoSolicitud;
                        }

                        if (ExisteOtraSolicitudActivaParaOrden(cn, tx, ordenId, codigoSolicitud))
                        {
                            tx.Rollback();
                            result.Error = "La orden ya está asociada a otra Solicitud AOCR activa.";
                            return result;
                        }

                        var targetPagoId = ResolverPagoId(cn, tx, ordenId, codigoSolicitud, pagoId);
                        if (targetPagoId <= 0)
                        {
                            tx.Rollback();
                            result.Error = "No se encontró comprobante de pago asociado a la orden.";
                            return result;
                        }

                        if (!ActualizarEstadoPago(cn, tx, targetPagoId, usuarioFinanciero, "Aprobado por Finanzas"))
                        {
                            tx.Rollback();
                            result.Error = "No se pudo actualizar el estado del pago.";
                            return result;
                        }

                        if (!ActualizarEstadoOrdenCerrada(cn, tx, ordenId, usuarioFinanciero))
                        {
                            tx.Rollback();
                            result.Error = "No se pudo cerrar la orden de recaudación.";
                            return result;
                        }

                        string errorWorkflow;
                        if (!_postPagoWorkflow.EjecutarHabilitacionPostAprobacionEnTransaccion(
                            cn,
                            tx,
                            ordenId,
                            codigoSolicitud,
                            usuarioFinanciero,
                            usuarioFinancieroId,
                            encolarCorreoRt: true,
                            out errorWorkflow))
                        {
                            tx.Rollback();
                            result.Error = string.IsNullOrWhiteSpace(errorWorkflow)
                                ? "No se pudo habilitar la Solicitud AOCR para el RT."
                                : errorWorkflow;
                            return result;
                        }

                        tx.Commit();
                        result.Exito = true;
                        result.SolicitudId = codigoSolicitud;
                        result.NumeroSolicitud = ObtenerNumeroSolicitud(cn, codigoSolicitud);
                        return result;
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex);
                result.Error = ex.Message != null && ex.Message.IndexOf("no está permitido", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "No se pudo aprobar el pago porque el estado financiero configurado no es válido. Contacte al administrador del sistema."
                    : ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                result.Error = "Error interno al aprobar el pago: " + ex.Message;
                return result;
            }
        }

        private static OrdenAprobacionContext LeerOrdenParaAprobacion(NpgsqlConnection cn, NpgsqlTransaction tx, int ordenId)
        {
            const string sql = @"
                SELECT id, numero_orden, estado, codigo_solicitud, codigo_usuario, compania,
                       ruc_cedula, correo, telefono, total
                FROM aocr_or_orden
                WHERE id = @id
                FOR UPDATE";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@id", ordenId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                    {
                        return null;
                    }

                    return new OrdenAprobacionContext
                    {
                        Id = Convert.ToInt32(rd["id"]),
                        NumeroOrden = rd["numero_orden"] == DBNull.Value ? string.Empty : rd["numero_orden"].ToString(),
                        Estado = rd["estado"] == DBNull.Value ? string.Empty : rd["estado"].ToString(),
                        CodigoSolicitud = ParseInt(rd["codigo_solicitud"]),
                        CodigoUsuario = ParseIntNullable(rd["codigo_usuario"]),
                        Compania = rd["compania"] == DBNull.Value ? string.Empty : rd["compania"].ToString(),
                        CompaniaCodigo = string.Empty,
                        RucCedula = rd["ruc_cedula"] == DBNull.Value ? string.Empty : rd["ruc_cedula"].ToString(),
                        Correo = rd["correo"] == DBNull.Value ? string.Empty : rd["correo"].ToString(),
                        Telefono = rd["telefono"] == DBNull.Value ? string.Empty : rd["telefono"].ToString(),
                        Total = rd["total"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["total"], CultureInfo.InvariantCulture)
                    };
                }
            }
        }

        private static bool ExisteSolicitud(NpgsqlConnection cn, NpgsqlTransaction tx, int codigoSolicitud)
        {
            const string sql = @"
                SELECT 1
                FROM aocr_tbsolicitud
                WHERE codigo_solicitud = @codigo
                  AND deleted_at IS NULL
                LIMIT 1";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigo", codigoSolicitud);
                return cmd.ExecuteScalar() != null;
            }
        }

        private static bool ExisteOtraSolicitudActivaParaOrden(NpgsqlConnection cn, NpgsqlTransaction tx, int ordenId, int codigoSolicitudActual)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM aocr_or_orden o
                INNER JOIN aocr_tbsolicitud s ON s.codigo_solicitud::text = o.codigo_solicitud::text
                WHERE o.id = @ordenId
                  AND s.codigo_solicitud <> @codigoSolicitud
                  AND s.deleted_at IS NULL
                  AND COALESCE(s.solicitud_finalizada_rt, FALSE) = FALSE
                  AND UPPER(COALESCE(s.estado, '')) NOT IN ('FINALIZADO', 'ANULADA', 'CERRADO')";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@ordenId", ordenId);
                cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitudActual);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static int ResolverPagoId(NpgsqlConnection cn, NpgsqlTransaction tx, int ordenId, int codigoSolicitud, int? pagoId)
        {
            if (pagoId.HasValue && pagoId.Value > 0)
            {
                return pagoId.Value;
            }

            const string sql = @"
                SELECT codigo_pago
                FROM aocr_tbpago
                WHERE codigo_solicitud = @codigoSolicitud
                   OR codigo_solicitud = @ordenId
                ORDER BY fecha_pago DESC NULLS LAST, codigo_pago DESC
                LIMIT 1";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                cmd.Parameters.AddWithValue("@ordenId", ordenId);
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
        }

        private bool ActualizarEstadoPago(NpgsqlConnection cn, NpgsqlTransaction tx, int pagoId, string usuario, string observaciones)
        {
            var estadoAnterior = ObtenerEstadoPago(cn, tx, pagoId);
            if (EstadoPago.EsPagoAprobadoFinancieramente(estadoAnterior))
            {
                return true;
            }

            string estadoNuevo;
            try
            {
                estadoNuevo = EstadoPago.ValidarOPrepararEstadoPersistencia(
                    OrdenRecaudacionOperativaHelper.ResolverEstadoPagoPostAprobacion());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex);
                throw;
            }

            System.Diagnostics.Trace.TraceInformation(
                "[AOCR][PAGO_ESTADO_UPDATE] PagoId={0}; EstadoAnterior={1}; EstadoNuevo={2}; Usuario={3}",
                pagoId,
                estadoAnterior ?? string.Empty,
                estadoNuevo,
                usuario ?? string.Empty);

            const string sql = @"
                UPDATE aocr_tbpago
                SET estado = @estado,
                    fecha_validacion = COALESCE(fecha_validacion, NOW()),
                    validado_por = @usuario,
                    observaciones = COALESCE(@observaciones, observaciones)
                WHERE codigo_pago = @pagoId
                  AND UPPER(COALESCE(estado, '')) NOT IN ('VALIDADO', 'APROBADO', 'CONFIRMADO', 'PAGADO', 'COMPLETADO')";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@estado", estadoNuevo);
                cmd.Parameters.AddWithValue("@usuario", (object)usuario ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@observaciones", (object)observaciones ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pagoId", pagoId);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private static string ObtenerEstadoPago(NpgsqlConnection cn, NpgsqlTransaction tx, int pagoId)
        {
            const string sql = @"
                SELECT COALESCE(estado, '')
                FROM aocr_tbpago
                WHERE codigo_pago = @pagoId
                LIMIT 1";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@pagoId", pagoId);
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? string.Empty : value.ToString();
            }
        }

        private bool ActualizarEstadoOrdenCerrada(NpgsqlConnection cn, NpgsqlTransaction tx, int ordenId, string usuario)
        {
            string estadoAnterior = null;
            const string sqlEstadoAnterior = "SELECT COALESCE(estado, '') FROM aocr_or_orden WHERE id = @id";
            using (var cmdEstado = new NpgsqlCommand(sqlEstadoAnterior, cn, tx))
            {
                cmdEstado.Parameters.AddWithValue("@id", ordenId);
                estadoAnterior = cmdEstado.ExecuteScalar()?.ToString();
            }

            string estadoNuevo;
            try
            {
                estadoNuevo = EstadoOrden.ValidarOPrepararEstadoPersistencia(
                    OrdenRecaudacionOperativaHelper.ResolverEstadoOrdenPostAprobacion());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex);
                throw;
            }

            System.Diagnostics.Trace.TraceInformation(
                "[AOCR][ORDEN_ESTADO_UPDATE] OrdenId={0}; EstadoAnterior={1}; EstadoNuevo={2}; Usuario={3}",
                ordenId,
                estadoAnterior ?? string.Empty,
                estadoNuevo,
                usuario ?? string.Empty);

            const string sql = @"
                UPDATE aocr_or_orden
                SET estado = @estado,
                    observacion = CASE
                        WHEN NULLIF(TRIM(COALESCE(observacion, '')), '') IS NULL
                        THEN @mensajeCierre
                        ELSE observacion
                    END
                WHERE id = @id";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@estado", estadoNuevo);
                cmd.Parameters.AddWithValue("@mensajeCierre", OrdenRecaudacionOperativaHelper.MensajeOrdenCerradaPostAprobacion);
                cmd.Parameters.AddWithValue("@id", ordenId);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private static bool ActualizarCodigoSolicitudOrden(NpgsqlConnection cn, NpgsqlTransaction tx, int ordenId, int codigoSolicitud)
        {
            const string sql = "UPDATE aocr_or_orden SET codigo_solicitud = @codigoSolicitud WHERE id = @id";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud.ToString(CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@id", ordenId);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private int CrearSolicitudDesdeOrden(NpgsqlConnection cn, NpgsqlTransaction tx, OrdenAprobacionContext orden)
        {
            if (orden == null || orden.CodigoUsuario.GetValueOrDefault() <= 0)
            {
                return 0;
            }

            var usuario = UsuarioDAO.ObtenerPorId(orden.CodigoUsuario.Value);
            var numero = new SolicitudBL().GenerarNumeroSolicitud(DateTime.Now.Year);
            var codigoCompaniaOrden = _companiaContextService.ResolverCodigoCompaniaDesdeOrden(
                new CapaDatos.Entidades.OrdenRecaudacion
                {
                    Compania = orden.Compania,
                    CodigoSolicitud = orden.CodigoSolicitud > 0 ? (int?)orden.CodigoSolicitud : null,
                    RucCedula = orden.RucCedula
                });
            var solicitud = new SolicitudAOCR
            {
                NumeroSolicitud = numero,
                FechaSolicitud = DateTime.Now,
                TipoSolicitud = 1,
                Estado = AocrPostPagoWorkflowService.EstadoSolicitudAocrHabilitada,
                CodigoUsuario = orden.CodigoUsuario.Value,
                NombreOperador = !string.IsNullOrWhiteSpace(orden.Compania) ? orden.Compania.Trim() : (usuario?.NombreCompleto ?? string.Empty),
                Ruc = orden.RucCedula,
                RazonSocial = orden.Compania,
                Email = !string.IsNullOrWhiteSpace(orden.Correo) ? orden.Correo.Trim() : (usuario?.Email ?? string.Empty),
                Telefono = orden.Telefono,
                CompaniasSeleccionadas = codigoCompaniaOrden,
                Direccion = string.Empty,
                Ciudad = "Quito"
            };

            return new SolicitudAOCRDAO().InsertarConReturn(cn, tx, solicitud);
        }

        private static string ObtenerNumeroSolicitud(NpgsqlConnection cn, int codigoSolicitud)
        {
            const string sql = "SELECT numero_solicitud FROM aocr_tbsolicitud WHERE codigo_solicitud = @codigo LIMIT 1";
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo", codigoSolicitud);
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? codigoSolicitud.ToString(CultureInfo.InvariantCulture) : value.ToString();
            }
        }

        private static int ParseInt(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            int parsed;
            return int.TryParse(value.ToString(), out parsed) ? parsed : 0;
        }

        private static int? ParseIntNullable(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            int parsed;
            return int.TryParse(value.ToString(), out parsed) ? (int?)parsed : null;
        }

        private sealed class OrdenAprobacionContext
        {
            public int Id { get; set; }
            public string NumeroOrden { get; set; }
            public string Estado { get; set; }
            public int CodigoSolicitud { get; set; }
            public int? CodigoUsuario { get; set; }
            public string Compania { get; set; }
            public string CompaniaCodigo { get; set; }
            public string RucCedula { get; set; }
            public string Correo { get; set; }
            public string Telefono { get; set; }
            public decimal Total { get; set; }
        }
    }
}
