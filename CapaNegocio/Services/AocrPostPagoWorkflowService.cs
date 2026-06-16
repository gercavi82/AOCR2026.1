using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Npgsql;
using NpgsqlTypes;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class AocrPostPagoWorkflowService
    {
        public const string TipoCorreoCoordinador = "PAGO_APROBADO_COORDINADOR_ASIGNACION_INSPECTOR";
        public const string TipoCorreoRt = "PAGO_APROBADO_RT_SOLICITUD_AOCR_HABILITADA";
        public const string TipoCorreoSolicitudHabilitada = "SOLICITUD_AOCR_HABILITADA";
        public const string EventKeyPagoAprobadoSolicitudHabilitada = "PAGO_APROBADO_SOLICITUD_AOCR_HABILITADA";
        public const string EstadoSolicitudAocrHabilitada = "SOLICITUD_AOCR_HABILITADA";
        public const string EstadoPendienteCargaDocumentalRt = "PENDIENTE_CARGA_DOCUMENTAL_RT";
        public const string EstadoPendienteRevisionDocumental = "PENDIENTE_REVISION_DOCUMENTAL";

        private readonly string _connectionString;
        private readonly ILoggingService _logger;

        public AocrPostPagoWorkflowService()
            : this(new SecureConfigurationService().GetConnectionString("PostgreSQL")
                ?? new SecureConfigurationService().GetConnectionString("AOCRConnection")
                ?? string.Empty)
        {
        }

        public AocrPostPagoWorkflowService(string connectionString)
        {
            _connectionString = connectionString;
            _logger = LoggingServiceFactory.Create();
        }

        public void ProcesarPagoAprobado(int ordenId, string usuarioFinanciero)
        {
            if (ordenId <= 0 || string.IsNullOrWhiteSpace(_connectionString))
            {
                return;
            }

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                    EnsureSolicitudControlColumns(cn);

                    var ctx = ObtenerContextoPagoAprobado(cn, ordenId);
                    if (ctx == null || ctx.CodigoSolicitud <= 0)
                    {
                        _logger.LogWarning("AocrPostPagoWorkflowService.ProcesarPagoAprobado: sin solicitud vinculada. OrdenId=" + ordenId);
                        return;
                    }

                    using (var tx = cn.BeginTransaction())
                    {
                        string error;
                        if (!EjecutarHabilitacionPostAprobacionEnTransaccion(
                            cn,
                            tx,
                            ordenId,
                            ctx.CodigoSolicitud,
                            usuarioFinanciero,
                            0,
                            encolarCorreoRt: true,
                            out error))
                        {
                            tx.Rollback();
                            _logger.LogWarning("AocrPostPagoWorkflowService.ProcesarPagoAprobado: " + (error ?? "fallo habilitacion"));
                            return;
                        }

                        tx.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        public bool EjecutarHabilitacionPostAprobacionEnTransaccion(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int ordenId,
            int codigoSolicitud,
            string usuarioFinanciero,
            int usuarioFinancieroId,
            bool encolarCorreoRt,
            out string error)
        {
            error = null;
            if (cn == null || tx == null || ordenId <= 0 || codigoSolicitud <= 0)
            {
                error = "Parámetros inválidos para habilitar Solicitud AOCR.";
                return false;
            }

            EnsureSolicitudControlColumns(cn);

            var ctx = ObtenerContextoPagoAprobado(cn, tx, ordenId, codigoSolicitud);
            if (ctx == null)
            {
                error = "No se encontró la solicitud AOCR asociada a la orden.";
                return false;
            }

            if (EsOrdenNoNotificable(ctx.EstadoOrden) && !EstadoOrden.EsOrdenCerradaPostAprobacionFinanciera(ctx.EstadoOrden))
            {
                error = "La orden no puede habilitar Solicitud AOCR en su estado actual.";
                return false;
            }

            var tieneDocumentos = TieneDocumentosHabilitantes(cn, tx, ctx.CodigoSolicitud);
            MarcarSolicitudPagoAprobado(cn, tx, ctx, tieneDocumentos, usuarioFinanciero);

            if (encolarCorreoRt && !ctx.NotificadoRtModuloHabilitado)
            {
                NotificarRtModuloHabilitado(cn, tx, ctx, usuarioFinanciero, usuarioFinancieroId);
            }

            if (!ctx.TieneInspectorAsignado && !ctx.NotificadoCoordinadorPagoAprobado)
            {
                NotificarCoordinadoresAsignacionPendiente(cn, tx, ctx, usuarioFinanciero);
            }

            RegistrarHistorial(ctx, usuarioFinanciero, "PAGO_APROBADO_FINANCIERO", ctx.EstadoSolicitud, EstadoSolicitudAocrHabilitada, "Pago aprobado por Financiero. Solicitud AOCR habilitada para el RT.");
            RegistrarHistorial(ctx, usuarioFinanciero, "MODULO_SOLICITUD_RT_HABILITADO", null, EstadoSolicitudAocrHabilitada, "Módulo de Solicitud AOCR habilitado para el RT.");
            if (!tieneDocumentos)
            {
                RegistrarHistorial(ctx, usuarioFinanciero, "SOLICITUD_PENDIENTE_CARGA_DOCUMENTAL_RT", ctx.EstadoSolicitud, EstadoPendienteCargaDocumentalRt, "Pendiente de carga documental por parte del RT.");
            }

            return true;
        }

        public bool PuedeRtAccederModuloSolicitud(int codigoSolicitud, int codigoUsuarioRt, out string mensaje)
        {
            return new SolicitudAocrService(_connectionString).PuedeRtEditarSolicitud(codigoSolicitud, codigoUsuarioRt, out mensaje);
        }

        public bool PuedeInspectorIniciarRevisionDocumental(int codigoInspeccion, out string mensaje)
        {
            mensaje = string.Empty;
            if (codigoInspeccion <= 0)
            {
                mensaje = "Inspección inválida.";
                return false;
            }

            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSolicitudControlColumns(cn);

                const string sql = @"
                    SELECT
                        i.codigo_inspeccion,
                        i.codigo_solicitud,
                        i.codigo_inspector,
                        COALESCE(s.estado, '') AS estado_solicitud,
                        COALESCE(s.pago_aprobado, FALSE) AS pago_aprobado,
                        COALESCE(s.modulo_solicitud_rt_habilitado, FALSE) AS modulo_habilitado,
                        COALESCE(s.pendiente_carga_documental_rt, TRUE) AS pendiente_documentos,
                        COALESCE(o.estado, '') AS estado_orden
                    FROM aocr_tbinspeccion i
                    INNER JOIN aocr_tbsolicitud s ON s.codigo_solicitud = i.codigo_solicitud
                    LEFT JOIN aocr_or_orden o ON o.codigo_solicitud::text = s.codigo_solicitud::text
                    WHERE i.codigo_inspeccion = @codigo_inspeccion
                    ORDER BY o.fecha_creacion DESC NULLS LAST, o.id DESC
                    LIMIT 1";

                int codigoSolicitud;
                bool pagoAprobado = false;
                bool moduloHabilitado = false;
                bool pendienteDocumentos = true;
                string estadoOrden = string.Empty;
                string estadoSolicitud = string.Empty;

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            mensaje = "La inspección no existe.";
                            return false;
                        }

                        codigoSolicitud = rd["codigo_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_solicitud"]);
                        var inspector = rd["codigo_inspector"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_inspector"]);
                        estadoSolicitud = rd["estado_solicitud"] == DBNull.Value ? string.Empty : rd["estado_solicitud"].ToString();
                        pagoAprobado = rd["pago_aprobado"] != DBNull.Value && Convert.ToBoolean(rd["pago_aprobado"]);
                        moduloHabilitado = rd["modulo_habilitado"] != DBNull.Value && Convert.ToBoolean(rd["modulo_habilitado"]);
                        pendienteDocumentos = rd["pendiente_documentos"] != DBNull.Value && Convert.ToBoolean(rd["pendiente_documentos"]);
                        estadoOrden = rd["estado_orden"] == DBNull.Value ? string.Empty : rd["estado_orden"].ToString();

                        if (inspector <= 0)
                        {
                            mensaje = "No existe inspector asignado.";
                            return false;
                        }
                    }
                }

                var ordenPagada = EstadoOrden.EsPagado(estadoOrden);
                var tieneDocumentos = TieneDocumentosHabilitantes(cn, codigoSolicitud);
                if (EsFlujoPostAsignacionCoordinador(estadoSolicitud, inspector: true) && tieneDocumentos)
                {
                    return true;
                }

                if (ordenPagada && (!pagoAprobado || !moduloHabilitado))
                {
                    SincronizarSolicitudPagadaDesdeOrden(cn, codigoSolicitud);
                    pagoAprobado = true;
                    moduloHabilitado = true;
                    pendienteDocumentos = !tieneDocumentos;
                }

                if (!pagoAprobado || !moduloHabilitado || !ordenPagada)
                {
                    mensaje = "No se puede iniciar la revisión documental porque el pago aún no está aprobado por Financiero.";
                    return false;
                }

                if (pendienteDocumentos && !tieneDocumentos)
                {
                    mensaje = "No se puede iniciar la revisión documental porque el RT aún no ha cargado los documentos habilitantes.";
                    return false;
                }

                if (!tieneDocumentos)
                {
                    mensaje = "No se puede iniciar la revisión documental porque el RT aún no ha cargado los documentos habilitantes.";
                    return false;
                }

                return true;
            }
        }

        public void MarcarDocumentosHabilitantesCargados(int codigoSolicitud, string usuario)
        {
            if (codigoSolicitud <= 0 || string.IsNullOrWhiteSpace(_connectionString))
            {
                return;
            }

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                    EnsureSolicitudControlColumns(cn);
                    SincronizarSolicitudPagadaDesdeOrden(cn, codigoSolicitud);

                    var ctx = ObtenerContextoPorSolicitud(cn, codigoSolicitud);
                    if (ctx == null)
                    {
                        return;
                    }

                    const string sql = @"
                        UPDATE aocr_tbsolicitud
                        SET estado = @estado,
                            pendiente_carga_documental_rt = FALSE,
                            updated_at = NOW(),
                            updated_by = @usuario
                        WHERE codigo_solicitud = @codigo_solicitud
                          AND deleted_at IS NULL
                          AND COALESCE(pago_aprobado, FALSE) = TRUE";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@estado", EstadoPendienteRevisionDocumental);
                        cmd.Parameters.AddWithValue("@usuario", (object)(usuario ?? "RT") ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                        cmd.ExecuteNonQuery();
                    }

                    RegistrarHistorial(ctx, usuario, "DOCUMENTOS_HABILITANTES_CARGADOS_RT", ctx.EstadoSolicitud, EstadoPendienteRevisionDocumental, "El RT cargó documentos habilitantes.");
                    NotificarInspectorDocumentosCargados(ctx, usuario);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        public bool SolicitudTienePendienteCargaDocumentalRt(int codigoSolicitud)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSolicitudControlColumns(cn);
                SincronizarSolicitudPagadaDesdeOrden(cn, codigoSolicitud);

                const string sql = @"
                    SELECT COALESCE(pendiente_carga_documental_rt, TRUE)
                    FROM aocr_tbsolicitud
                    WHERE codigo_solicitud = @codigo_solicitud
                    LIMIT 1";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    var value = cmd.ExecuteScalar();
                    var pendienteDocumentos = value == null || value == DBNull.Value || Convert.ToBoolean(value);
                    if (!pendienteDocumentos)
                    {
                        return false;
                    }

                    if (TieneDocumentosHabilitantes(cn, codigoSolicitud))
                    {
                        using (var syncCmd = new NpgsqlCommand(@"
                            UPDATE aocr_tbsolicitud
                            SET pendiente_carga_documental_rt = FALSE,
                                updated_at = NOW(),
                                updated_by = @usuario
                            WHERE codigo_solicitud = @codigo_solicitud", cn))
                        {
                            syncCmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                            syncCmd.Parameters.AddWithValue("@usuario", "SYSTEM_SYNC");
                            syncCmd.ExecuteNonQuery();
                        }

                        return false;
                    }

                    return true;
                }
            }
        }

        private static bool EsOrdenNoNotificable(string estadoOrden)
        {
            var estado = (estadoOrden ?? string.Empty).Trim().ToUpperInvariant();
            return estado == "ANULADA" || estado == "RECHAZADA";
        }

        private PagoAprobadoContext ObtenerContextoPagoAprobado(NpgsqlConnection cn, int ordenId)
        {
            return ObtenerContextoPagoAprobado(cn, null, ordenId, null);
        }

        private PagoAprobadoContext ObtenerContextoPagoAprobado(NpgsqlConnection cn, NpgsqlTransaction tx, int ordenId, int? codigoSolicitudForzado)
        {
            var columnasSolicitud = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
            var selectCodigoTecnico = SelectSolicitudColumn(columnasSolicitud, "codigo_tecnico", "codigo_tecnico", "integer");
            var exprCodigoTecnico = columnasSolicitud.Contains("codigo_tecnico")
                ? "COALESCE(s.codigo_tecnico, 0)"
                : "0";
            var exprCorreoRt = ConstruirExpresionCorreoRt(columnasSolicitud, "s", "o");
            var filtroSolicitud = codigoSolicitudForzado.HasValue && codigoSolicitudForzado.Value > 0
                ? " AND s.codigo_solicitud = @codigo_solicitud_forzado"
                : string.Empty;

            var sql = @"
                SELECT
                    o.id AS orden_id,
                    o.numero_orden,
                    o.estado AS estado_orden,
                    o.codigo_solicitud,
                    o.total AS total_orden,
                    s.numero_solicitud,
                    s.estado AS estado_solicitud,
                    s.codigo_usuario,
                    COALESCE(s.notificado_rt_modulo_habilitado, FALSE) AS notificado_rt_modulo_habilitado,
                    COALESCE(s.notificado_coordinador_pago_aprobado, FALSE) AS notificado_coordinador_pago_aprobado,
                    " + selectCodigoTecnico + @",
                    CASE
                        WHEN " + exprCodigoTecnico + @" > 0
                             OR EXISTS (
                                 SELECT 1
                                 FROM aocr_tbinspeccion i_asg
                                 WHERE i_asg.codigo_solicitud = s.codigo_solicitud
                                   AND i_asg.codigo_inspector IS NOT NULL
                             )
                        THEN TRUE
                        ELSE FALSE
                    END AS tiene_inspector_asignado,
                    COALESCE(NULLIF(TRIM(s.razon_social), ''), NULLIF(TRIM(s.nombre_operador), ''), NULLIF(TRIM(o.compania), ''), '') AS nombre_operadora,
                    COALESCE(NULLIF(TRIM(s.ruc), ''), NULLIF(TRIM(o.ruc_cedula), ''), '') AS ruc_operadora,
                    " + exprCorreoRt + @" AS correo_rt,
                    COALESCE(NULLIF(TRIM(s.representante_legal), ''), 'Representante Técnico') AS nombre_rt
                FROM aocr_or_orden o
                INNER JOIN aocr_tbsolicitud s ON s.codigo_solicitud::text = o.codigo_solicitud::text
                WHERE o.id = @orden_id
                  AND s.deleted_at IS NULL" + filtroSolicitud + @"
                LIMIT 1";
            return LeerContexto(cn, tx, sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@orden_id", ordenId);
                if (codigoSolicitudForzado.HasValue && codigoSolicitudForzado.Value > 0)
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud_forzado", codigoSolicitudForzado.Value);
                }
            });
        }

        private PagoAprobadoContext ObtenerContextoPorSolicitud(NpgsqlConnection cn, int codigoSolicitud)
        {
            var columnasSolicitud = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");
            var selectCodigoTecnico = SelectSolicitudColumn(columnasSolicitud, "codigo_tecnico", "codigo_tecnico", "integer");
            var exprCodigoTecnico = columnasSolicitud.Contains("codigo_tecnico")
                ? "COALESCE(s.codigo_tecnico, 0)"
                : "0";
            var exprCorreoRt = ConstruirExpresionCorreoRt(columnasSolicitud, "s", "o");

            var sql = @"
                SELECT
                    o.id AS orden_id,
                    o.numero_orden,
                    o.estado AS estado_orden,
                    s.codigo_solicitud,
                    s.numero_solicitud,
                    s.estado AS estado_solicitud,
                    s.codigo_usuario,
                    COALESCE(s.notificado_rt_modulo_habilitado, FALSE) AS notificado_rt_modulo_habilitado,
                    COALESCE(s.notificado_coordinador_pago_aprobado, FALSE) AS notificado_coordinador_pago_aprobado,
                    " + selectCodigoTecnico + @",
                    CASE
                        WHEN " + exprCodigoTecnico + @" > 0
                             OR EXISTS (
                                 SELECT 1
                                 FROM aocr_tbinspeccion i_asg
                                 WHERE i_asg.codigo_solicitud = s.codigo_solicitud
                                   AND i_asg.codigo_inspector IS NOT NULL
                             )
                        THEN TRUE
                        ELSE FALSE
                    END AS tiene_inspector_asignado,
                    COALESCE(NULLIF(TRIM(s.razon_social), ''), NULLIF(TRIM(s.nombre_operador), ''), NULLIF(TRIM(o.compania), ''), '') AS nombre_operadora,
                    COALESCE(NULLIF(TRIM(s.ruc), ''), NULLIF(TRIM(o.ruc_cedula), ''), '') AS ruc_operadora,
                    " + exprCorreoRt + @" AS correo_rt,
                    COALESCE(NULLIF(TRIM(s.representante_legal), ''), 'Representante Técnico') AS nombre_rt
                FROM aocr_tbsolicitud s
                LEFT JOIN aocr_or_orden o ON o.codigo_solicitud::text = s.codigo_solicitud::text
                WHERE s.codigo_solicitud = @codigo_solicitud
                  AND s.deleted_at IS NULL
                ORDER BY o.fecha_creacion DESC NULLS LAST, o.id DESC
                LIMIT 1";
            return LeerContexto(cn, sql, cmd => cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud));
        }

        private static PagoAprobadoContext LeerContexto(NpgsqlConnection cn, NpgsqlTransaction tx, string sql, Action<NpgsqlCommand> parametros)
        {
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                parametros(cmd);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                    {
                        return null;
                    }

                    var ctx = new PagoAprobadoContext
                    {
                        OrdenId = GetInt(rd, "orden_id"),
                        NumeroOrden = GetString(rd, "numero_orden"),
                        EstadoOrden = GetString(rd, "estado_orden"),
                        CodigoSolicitud = GetInt(rd, "codigo_solicitud"),
                        NumeroSolicitud = GetString(rd, "numero_solicitud"),
                        EstadoSolicitud = GetString(rd, "estado_solicitud"),
                        CodigoUsuarioRt = GetInt(rd, "codigo_usuario"),
                        NotificadoRtModuloHabilitado = GetBool(rd, "notificado_rt_modulo_habilitado"),
                        NotificadoCoordinadorPagoAprobado = GetBool(rd, "notificado_coordinador_pago_aprobado"),
                        CodigoTecnico = GetNullableInt(rd, "codigo_tecnico"),
                        TieneInspectorAsignado = GetBool(rd, "tiene_inspector_asignado"),
                        NombreOperadora = GetString(rd, "nombre_operadora"),
                        RucOperadora = GetString(rd, "ruc_operadora"),
                        CorreoRt = GetString(rd, "correo_rt"),
                        NombreRt = GetString(rd, "nombre_rt"),
                        TotalOrden = TryGetDecimal(rd, "total_orden")
                    };
                    return ctx;
                }
            }
        }

        private static PagoAprobadoContext LeerContexto(NpgsqlConnection cn, string sql, Action<NpgsqlCommand> parametros)
        {
            return LeerContexto(cn, null, sql, parametros);
        }

        private static HashSet<string> ObtenerColumnasTabla(NpgsqlConnection cn, string tabla)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            const string sql = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tabla;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd["column_name"] == DBNull.Value)
                        {
                            continue;
                        }

                        var columna = rd["column_name"].ToString();
                        if (!string.IsNullOrWhiteSpace(columna))
                        {
                            columnas.Add(columna);
                        }
                    }
                }
            }

            return columnas;
        }

        private static string SelectSolicitudColumn(HashSet<string> columnas, string columnName, string alias, string nullCast = "text")
        {
            return columnas.Contains(columnName)
                ? $"s.{columnName} AS {alias}"
                : $"NULL::{nullCast} AS {alias}";
        }

        private static string ConstruirExpresionCorreoRt(HashSet<string> columnasSolicitud, string aliasSolicitud, string aliasOrden)
        {
            var expresiones = new List<string>();

            foreach (var columna in new[]
            {
                "email",
                "correo_electronico",
                "correo",
                "email_representante_tecnico",
                "correo_representante_tecnico",
                "email_representante",
                "correo_representante"
            })
            {
                if (columnasSolicitud.Contains(columna))
                {
                    expresiones.Add("NULLIF(TRIM(" + aliasSolicitud + "." + columna + "), '')");
                }
            }

            expresiones.Add("NULLIF(TRIM(" + aliasOrden + ".correo), '')");
            expresiones.Add("''");
            return "COALESCE(" + string.Join(", ", expresiones) + ")";
        }

        private static void EnsureSolicitudControlColumns(NpgsqlConnection cn)
        {
            const string sql = @"
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS pago_aprobado BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS fecha_aprobacion_pago TIMESTAMP NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS solicitud_finalizada_rt BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS fecha_finalizacion_rt TIMESTAMP NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS requiere_nueva_orden BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS modulo_solicitud_rt_habilitado BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS notificado_coordinador_pago_aprobado BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS fecha_notificacion_coordinador_pago TIMESTAMP NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS notificado_rt_modulo_habilitado BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS fecha_notificacion_rt_modulo_habilitado TIMESTAMP NULL;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS pendiente_asignacion_inspector BOOLEAN DEFAULT FALSE;
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS pendiente_carga_documental_rt BOOLEAN DEFAULT TRUE;";
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static bool TieneDocumentosHabilitantes(NpgsqlConnection cn, int codigoSolicitud)
        {
            return TieneDocumentosHabilitantes(cn, null, codigoSolicitud);
        }

        private static bool TieneDocumentosHabilitantes(NpgsqlConnection cn, NpgsqlTransaction tx, int codigoSolicitud)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM aocr_tbdocumento
                WHERE codigo_solicitud = @codigo_solicitud
                  AND COALESCE(tamano_bytes, 0) > 0
                  AND NULLIF(TRIM(COALESCE(nombre_archivo, '')), '') IS NOT NULL
                  AND NULLIF(TRIM(COALESCE(ruta_guardada, '')), '') IS NOT NULL
                  AND UPPER(TRIM(COALESCE(tipo_documento, ''))) NOT IN ('BORRADOR_AOCR', 'AOCR_GENERADO', 'AOCR')";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private void SincronizarSolicitudPagadaDesdeOrden(NpgsqlConnection cn, int codigoSolicitud)
        {
            var ctx = ObtenerContextoPorSolicitud(cn, codigoSolicitud);
            if (ctx == null || !EstadoOrden.EsPagado(ctx.EstadoOrden))
            {
                return;
            }

            var tieneDocumentos = TieneDocumentosHabilitantes(cn, codigoSolicitud);
            MarcarSolicitudPagoAprobado(cn, ctx, tieneDocumentos, "SYSTEM_SYNC");
        }

        private static void MarcarSolicitudPagoAprobado(NpgsqlConnection cn, PagoAprobadoContext ctx, bool tieneDocumentos, string usuario)
        {
            MarcarSolicitudPagoAprobado(cn, null, ctx, tieneDocumentos, usuario);
        }

        private static void MarcarSolicitudPagoAprobado(NpgsqlConnection cn, NpgsqlTransaction tx, PagoAprobadoContext ctx, bool tieneDocumentos, string usuario)
        {
            const string sql = @"
                UPDATE aocr_tbsolicitud
                SET estado = CASE
                                WHEN UPPER(COALESCE(estado, '')) IN (
                                    '',
                                    'PENDIENTE',
                                    'PENDIENTE_CARGA_DOCUMENTAL_RT',
                                    'EN_REVISION_DOCUMENTAL',
                                    'PENDIENTE_REVISION_DOCUMENTAL',
                                    'PAGO_PENDIENTE',
                                    'PAGO_VALIDADO',
                                    'SOLICITUD_CREADA',
                                    'DOCUMENTACION_PENDIENTE'
                                ) THEN @estado_habilitada
                                WHEN UPPER(COALESCE(estado, '')) IN ('PENDIENTE_CARGA_DOCUMENTAL_RT')
                                     AND @tiene_documentos = TRUE THEN @estado_revision
                                ELSE estado
                             END,
                    pago_aprobado = TRUE,
                    fecha_aprobacion_pago = COALESCE(fecha_aprobacion_pago, NOW()),
                    solicitud_finalizada_rt = FALSE,
                    fecha_finalizacion_rt = NULL,
                    requiere_nueva_orden = FALSE,
                    modulo_solicitud_rt_habilitado = TRUE,
                    pendiente_asignacion_inspector = @pendiente_asignacion,
                    pendiente_carga_documental_rt = @pendiente_documentos,
                    updated_at = NOW(),
                    updated_by = @usuario
                WHERE codigo_solicitud = @codigo_solicitud
                  AND deleted_at IS NULL";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@estado_habilitada", EstadoSolicitudAocrHabilitada);
                cmd.Parameters.AddWithValue("@estado_revision", EstadoPendienteRevisionDocumental);
                cmd.Parameters.AddWithValue("@tiene_documentos", tieneDocumentos);
                cmd.Parameters.AddWithValue("@pendiente_asignacion", !ctx.TieneInspectorAsignado);
                cmd.Parameters.AddWithValue("@pendiente_documentos", !tieneDocumentos);
                cmd.Parameters.AddWithValue("@usuario", (object)(usuario ?? "FINANCIERO") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@codigo_solicitud", ctx.CodigoSolicitud);
                cmd.ExecuteNonQuery();
            }
        }

        private void NotificarRtModuloHabilitado(PagoAprobadoContext ctx, string usuarioFinanciero)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    NotificarRtModuloHabilitado(cn, tx, ctx, usuarioFinanciero, 0);
                    tx.Commit();
                }
            }
        }

        private void NotificarRtModuloHabilitado(NpgsqlConnection cn, NpgsqlTransaction tx, PagoAprobadoContext ctx, string usuarioFinanciero, int usuarioFinancieroId)
        {
            var mensajeInApp = "El pago fue aprobado por Financiero. La Solicitud AOCR se encuentra habilitada para continuar con la carga documental.";
            if (string.IsNullOrWhiteSpace(ctx.CorreoRt))
            {
                NotificacionBL.EnviarNotificacion(ctx.CodigoUsuarioRt, "Solicitud AOCR habilitada", mensajeInApp, TipoCorreoSolicitudHabilitada, "/SolicitudAOCR/Index", "SolicitudAOCR", ctx.CodigoSolicitud, "SolicitudAOCR");
                MarcarNotificacionRt(cn, tx, ctx.CodigoSolicitud);
                return;
            }

            var asunto = "Sistema AOCR - Solicitud AOCR habilitada";
            var cuerpo = ConstruirCorreoRt(ctx, usuarioFinanciero);
            var eventKey = string.Format(
                "{0}_{1}_{2}",
                EventKeyPagoAprobadoSolicitudHabilitada,
                ctx.OrdenId,
                ctx.CodigoSolicitud);
            EncolarCorreo(cn, tx, ctx.CodigoSolicitud, ctx.OrdenId, ctx.CorreoRt, ctx.NombreRt, asunto, cuerpo, TipoCorreoSolicitudHabilitada, eventKey);
            MarcarNotificacionRt(cn, tx, ctx.CodigoSolicitud);
            NotificacionBL.EnviarNotificacion(ctx.CodigoUsuarioRt, "Solicitud AOCR habilitada", mensajeInApp, TipoCorreoSolicitudHabilitada, "/SolicitudAOCR/Index", "SolicitudAOCR", ctx.CodigoSolicitud, "SolicitudAOCR");
            RegistrarHistorial(ctx, usuarioFinanciero, "NOTIFICACION_RT_MODULO_HABILITADO", null, null, "Notificación enviada/encolada al RT: " + ctx.CorreoRt);
        }

        private void NotificarCoordinadoresAsignacionPendiente(PagoAprobadoContext ctx, string usuarioFinanciero)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    NotificarCoordinadoresAsignacionPendiente(cn, tx, ctx, usuarioFinanciero);
                    tx.Commit();
                }
            }
        }

        private void NotificarCoordinadoresAsignacionPendiente(NpgsqlConnection cn, NpgsqlTransaction tx, PagoAprobadoContext ctx, string usuarioFinanciero)
        {
            if (EstadoSolicitudNoNotificable(ctx.EstadoSolicitud))
            {
                _logger.LogInfo("AocrPostPagoWorkflowService.NotificarCoordinadoresAsignacionPendiente: omite notificacion por estado final/anulado. Solicitud=" + ctx.CodigoSolicitud);
                return;
            }
            var correoInstitucional = new CorreoInstitucionalService().ObtenerDestinatariosPorArea(CorreoInstitucionalService.CoordinadorAocr);
            if (correoInstitucional != null)
            {
                var asunto = string.Format("Pago aprobado para Solicitud AOCR #{0} - pendiente asignación de inspector", NumeroSolicitud(ctx));
                var cuerpo = ConstruirCorreoCoordinador(ctx, correoInstitucional.NombreArea);
                foreach (var correo in correoInstitucional.ObtenerTodosLosCorreos().Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var eventKey = string.Format("{0}_{1}_{2}", TipoCorreoCoordinador, ctx.CodigoSolicitud, correo.Trim().ToUpperInvariant());
                    EncolarCorreo(cn, tx, ctx.CodigoSolicitud, ctx.OrdenId, correo, correoInstitucional.NombreArea, asunto, cuerpo, TipoCorreoCoordinador, eventKey);
                }
            }
            else
            {
                _logger.LogWarning("No se encontró un correo institucional activo para COORDINADOR_AOCR. Revise Administración > Configuración de correos institucionales.");
            }

            var coordinadores = ObtenerCoordinadores();
            foreach (var coordinador in coordinadores)
            {
                if (coordinador == null)
                {
                    continue;
                }

                if (correoInstitucional == null && !string.IsNullOrWhiteSpace(coordinador.Email))
                {
                    var asunto = string.Format("Pago aprobado para Solicitud AOCR #{0} - pendiente asignación de inspector", NumeroSolicitud(ctx));
                    var cuerpo = ConstruirCorreoCoordinador(ctx, coordinador);
                    var eventKey = string.Format("{0}_{1}_{2}", TipoCorreoCoordinador, ctx.CodigoSolicitud, coordinador.Email.Trim().ToUpperInvariant());
                    EncolarCorreo(cn, tx, ctx.CodigoSolicitud, ctx.OrdenId, coordinador.Email, NombreUsuario(coordinador), asunto, cuerpo, TipoCorreoCoordinador, eventKey);
                }

                if (coordinador.Id > 0)
                {
                    NotificacionBL.EnviarNotificacion(
                        coordinador.Id,
                        "Pago aprobado - asignación de Inspector pendiente",
                        "La Solicitud AOCR " + NumeroSolicitud(ctx) + " tiene el pago aprobado y requiere asignación de Inspector.",
                        "PAGO_APROBADO",
                        "/Inspeccion/Detalle/" + ctx.CodigoSolicitud,
                        "Inspeccion",
                        ctx.CodigoSolicitud,
                        "SolicitudAOCR");
                }
            }

            MarcarNotificacionCoordinador(cn, tx, ctx.CodigoSolicitud);
            RegistrarHistorial(ctx, usuarioFinanciero, "NOTIFICACION_COORDINADOR_ASIGNACION_PENDIENTE", null, null, "Notificación enviada/encolada a coordinación para asignar inspector.");
        }

        private void NotificarInspectorDocumentosCargados(PagoAprobadoContext ctx, string usuario)
        {
            if (!ctx.CodigoTecnico.HasValue || ctx.CodigoTecnico.Value <= 0)
            {
                return;
            }

            NotificacionBL.EnviarNotificacion(
                ctx.CodigoTecnico.Value,
                "Documentos habilitantes cargados",
                "El RT ha cargado documentos habilitantes para revisión de la Solicitud AOCR " + NumeroSolicitud(ctx) + ".",
                "DOCUMENTOS_CARGADOS",
                "/SolicitudAOCR/Detalle/" + ctx.CodigoSolicitud,
                "SolicitudAOCR",
                ctx.CodigoSolicitud,
                "SolicitudAOCR");
        }

        private void EncolarCorreo(int codigoSolicitud, int ordenId, string para, string nombre, string asunto, string cuerpo, string tipo, string eventKey)
        {
            try
            {
                var emailFlujo = new AocrEmailFlujoService(new EmailQueueService(_connectionString));
                var encolado = emailFlujo.EncolarSiNoDuplicadoAsync(
                    tipo,
                    codigoSolicitud,
                    ordenId > 0 ? (int?)ordenId : null,
                    para,
                    asunto,
                    cuerpo,
                    eventKey).GetAwaiter().GetResult();

                if (!encolado)
                {
                    _logger.LogInfo("AocrPostPagoWorkflowService.EncolarCorreo: correo omitido por duplicado. Tipo=" + tipo + ", Solicitud=" + codigoSolicitud);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        private void EncolarCorreo(NpgsqlConnection cn, NpgsqlTransaction tx, int codigoSolicitud, int ordenId, string para, string nombre, string asunto, string cuerpo, string tipo, string eventKey)
        {
            try
            {
                var queueService = new EmailQueueService(_connectionString);
                var queueItem = new EmailQueueItem
                {
                    Para = para.Trim(),
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    EsHtml = true,
                    SolicitudId = codigoSolicitud,
                    OrdenId = ordenId > 0 ? ordenId : (int?)null,
                    EventKey = eventKey,
                    TipoNotificacion = tipo,
                    Estado = EstadoEmail.Pendiente,
                    FechaCreacion = DateTime.UtcNow,
                    MaxIntentos = 5
                };

                bool duplicateEvent;
                queueService.EncolarConAdjuntosEnTransaccion(cn, tx, queueItem, null, out duplicateEvent);
                if (duplicateEvent)
                {
                    _logger.LogInfo("AocrPostPagoWorkflowService.EncolarCorreo: correo omitido por duplicado. EventKey=" + eventKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        private static List<Usuario> ObtenerCoordinadores()
        {
            var result = new Dictionary<int, Usuario>();
            foreach (var rol in new[] { "CoordinadorInspecciones", "Coordinador", "Coordinacion", "JefaturaTecnica", "Administrador" })
            {
                foreach (var usuario in UsuarioDAO.ListarPorRol(rol) ?? new List<Usuario>())
                {
                    if (usuario != null && usuario.Id > 0 && !result.ContainsKey(usuario.Id))
                    {
                        result[usuario.Id] = usuario;
                    }
                }
            }
            return result.Values.ToList();
        }

        private void MarcarNotificacionRt(int codigoSolicitud)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    MarcarNotificacionRt(cn, tx, codigoSolicitud);
                    tx.Commit();
                }
            }
        }

        private static void MarcarNotificacionRt(NpgsqlConnection cn, NpgsqlTransaction tx, int codigoSolicitud)
        {
            EjecutarUpdateSolicitudNotificacion(cn, tx, codigoSolicitud, "notificado_rt_modulo_habilitado = TRUE, fecha_notificacion_rt_modulo_habilitado = COALESCE(fecha_notificacion_rt_modulo_habilitado, NOW())");
        }

        private void MarcarNotificacionCoordinador(int codigoSolicitud)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    MarcarNotificacionCoordinador(cn, tx, codigoSolicitud);
                    tx.Commit();
                }
            }
        }

        private static void MarcarNotificacionCoordinador(NpgsqlConnection cn, NpgsqlTransaction tx, int codigoSolicitud)
        {
            EjecutarUpdateSolicitudNotificacion(cn, tx, codigoSolicitud, "notificado_coordinador_pago_aprobado = TRUE, fecha_notificacion_coordinador_pago = COALESCE(fecha_notificacion_coordinador_pago, NOW())");
        }

        private static void EjecutarUpdateSolicitudNotificacion(NpgsqlConnection cn, NpgsqlTransaction tx, int codigoSolicitud, string setSql)
        {
            using (var cmd = new NpgsqlCommand("UPDATE aocr_tbsolicitud SET " + setSql + " WHERE codigo_solicitud = @codigo_solicitud", cn, tx))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                cmd.ExecuteNonQuery();
            }
        }

        private void EjecutarUpdateSolicitudNotificacion(int codigoSolicitud, string setSql)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                EnsureSolicitudControlColumns(cn);
                using (var cmd = new NpgsqlCommand("UPDATE aocr_tbsolicitud SET " + setSql + " WHERE codigo_solicitud = @codigo_solicitud", cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void RegistrarHistorial(PagoAprobadoContext ctx, string usuario, string evento, string estadoAnterior, string estadoNuevo, string observacion)
        {
            try
            {
                new AuditTrailService().RegistrarAuditoria(
                    "aocr_tbsolicitud",
                    ctx.CodigoSolicitud,
                    evento,
                    "estado",
                    estadoAnterior,
                    estadoNuevo,
                    null,
                    usuario ?? "SISTEMA",
                    null,
                    "Workflow AOCR",
                    observacion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo registrar historial " + evento + ": " + ex.Message);
            }
        }

        private static string ConstruirCorreoCoordinador(PagoAprobadoContext ctx, Usuario coordinador)
        {
            return ConstruirCorreoCoordinador(ctx, NombreUsuario(coordinador));
        }

        private static string ConstruirCorreoCoordinador(PagoAprobadoContext ctx, string nombreCoordinador)
        {
            return PlantillaInstitucional(
                "Estimado/a " + Html(string.IsNullOrWhiteSpace(nombreCoordinador) ? "Coordinador AOCR" : nombreCoordinador) + ",",
                "Se informa que el área Financiera ha aprobado el pago correspondiente a la Orden de Recaudación asociada a la Solicitud AOCR " + Html(NumeroSolicitud(ctx)) + ".",
                new[]
                {
                    Pair("Número de solicitud", NumeroSolicitud(ctx)),
                    Pair("Operadora / Compañía", ctx.NombreOperadora),
                    Pair("RUC", ctx.RucOperadora),
                    Pair("Representante Técnico", ctx.NombreRt),
                    Pair("Orden de Recaudación", ctx.NumeroOrden),
                    Pair("Fecha de aprobación del pago", DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                    Pair("Estado actual", "Pendiente de asignación de Inspector")
                },
                "Ingrese al sistema AOCR y asigne el inspector responsable para continuar con el proceso.");
        }

        private static string ConstruirCorreoRt(PagoAprobadoContext ctx, string usuarioFinanciero)
        {
            return PlantillaInstitucional(
                "Estimado/a " + Html(ctx.NombreRt) + ",",
                "Se informa que el área Financiera ha aprobado el comprobante de depósito o transferencia correspondiente a la Orden de Recaudación " + Html(ctx.NumeroOrden) + ".",
                new[]
                {
                    Pair("Número de orden", ctx.NumeroOrden),
                    Pair("Número de solicitud AOCR", NumeroSolicitud(ctx)),
                    Pair("Compañía", ctx.NombreOperadora),
                    Pair("RUC", ctx.RucOperadora),
                    Pair("Fecha de aprobación", DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                    Pair("Valor aprobado", ctx.TotalOrden > 0m ? ctx.TotalOrden.ToString("C2", new System.Globalization.CultureInfo("es-EC")) : "N/D"),
                    Pair("Aprobado por", string.IsNullOrWhiteSpace(usuarioFinanciero) ? "Financiero AOCR" : usuarioFinanciero)
                },
                "A partir de este momento, se encuentra habilitada la Solicitud AOCR para la compañía " + Html(ctx.NombreOperadora) + ". Debe ingresar al Sistema AOCR para continuar con el proceso y cargar la documentación correspondiente. La Orden de Recaudación aprobada quedará cerrada para este proceso y no podrá ser modificada ni reutilizada mientras el trámite AOCR se encuentre activo.");
        }

        private static string PlantillaInstitucional(string saludo, string mensaje, IEnumerable<KeyValuePair<string, string>> datos, string accion)
        {
            var rows = string.Join("", datos.Select(d =>
                "<tr><td style='padding:8px 10px;border-bottom:1px solid #e5e7eb;font-weight:bold;width:38%;'>" + Html(d.Key) + "</td><td style='padding:8px 10px;border-bottom:1px solid #e5e7eb;'>" + Html(d.Value) + "</td></tr>"));
            return "<div style='font-family:Arial,sans-serif;color:#1f2937;line-height:1.5;'>" +
                   "<div style='background:#003b70;color:#fff;padding:16px 20px;font-size:18px;font-weight:bold;'>Sistema AOCR - DGAC</div>" +
                   "<div style='padding:20px;border:1px solid #d1d5db;border-top:0;'>" +
                   "<p>" + saludo + "</p>" +
                   "<p>" + mensaje + "</p>" +
                   "<div style='margin:18px 0;border:1px solid #e5e7eb;'><table style='border-collapse:collapse;width:100%;font-size:14px;'>" + rows + "</table></div>" +
                   "<p><strong>Acción requerida:</strong><br/>" + Html(accion) + "</p>" +
                   "<p style='margin-top:24px;'>Atentamente,<br/>Sistema AOCR<br/>Dirección General de Aviación Civil</p>" +
                   "</div></div>";
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value ?? string.Empty);
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string NumeroSolicitud(PagoAprobadoContext ctx)
        {
            return string.IsNullOrWhiteSpace(ctx.NumeroSolicitud) ? ctx.CodigoSolicitud.ToString() : ctx.NumeroSolicitud.Trim();
        }

        private static bool EstadoSolicitudNoNotificable(string estadoSolicitud)
        {
            var estado = (estadoSolicitud ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(estado))
            {
                return false;
            }

            return estado.Contains("ANUL")
                || estado.Contains("FINAL")
                || estado.Contains("EMITIDO")
                || estado.Contains("ENTREG")
                || estado.Contains("RECIBID");
        }

        private static string NombreUsuario(Usuario usuario)
        {
            if (usuario == null)
            {
                return "Usuario AOCR";
            }

            var nombre = string.Join(" ", new[] { usuario.NombreCompleto, usuario.NombreUsuario, usuario.ApellidoUsuario }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            return string.IsNullOrWhiteSpace(nombre) ? (usuario.CodigoUsuario ?? "Usuario AOCR") : nombre;
        }

        private static int GetInt(System.Data.IDataRecord rd, string name)
        {
            var value = rd[name];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int? GetNullableInt(System.Data.IDataRecord rd, string name)
        {
            var value = rd[name];
            return value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        private static string GetString(System.Data.IDataRecord rd, string name)
        {
            var value = rd[name];
            return value == DBNull.Value ? string.Empty : value.ToString();
        }

        private static bool GetBool(System.Data.IDataRecord rd, string name)
        {
            var value = rd[name];
            return value != DBNull.Value && Convert.ToBoolean(value);
        }

        private static decimal TryGetDecimal(System.Data.IDataRecord rd, string name)
        {
            try
            {
                var value = rd[name];
                return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
            }
            catch
            {
                return 0m;
            }
        }

        private static bool EsFlujoPostAsignacionCoordinador(string estadoSolicitud, bool inspector)
        {
            if (string.IsNullOrWhiteSpace(estadoSolicitud))
            {
                return false;
            }

            if (!inspector)
            {
                return false;
            }

            var canonico = EstadoSolicitud.Normalizar(estadoSolicitud);
            return string.Equals(canonico, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(canonico, EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase);
        }

        private class PagoAprobadoContext
        {
            public int OrdenId { get; set; }
            public string NumeroOrden { get; set; }
            public string EstadoOrden { get; set; }
            public int CodigoSolicitud { get; set; }
            public string NumeroSolicitud { get; set; }
            public string EstadoSolicitud { get; set; }
            public int CodigoUsuarioRt { get; set; }
            public bool NotificadoRtModuloHabilitado { get; set; }
            public bool NotificadoCoordinadorPagoAprobado { get; set; }
            public int? CodigoTecnico { get; set; }
            public string TecnicoResponsableCedula { get; set; }
            public bool TieneInspectorAsignado { get; set; }
            public string NombreOperadora { get; set; }
            public string RucOperadora { get; set; }
            public string CorreoRt { get; set; }
            public string NombreRt { get; set; }
            public decimal TotalOrden { get; set; }
        }
    }
}
