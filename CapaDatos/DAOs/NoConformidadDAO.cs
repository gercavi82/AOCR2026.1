using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class NoConformidadDAO
    {
        private string CS => ConexionDAO.CadenaConexion;
        private const string TABLA = "public.aocr_tbnoconformidad";

        private static bool TryGetOrdinal(NpgsqlDataReader dr, string column, out int ordinal)
        {
            ordinal = -1;
            if (dr == null || string.IsNullOrWhiteSpace(column)) return false;
            try
            {
                ordinal = dr.GetOrdinal(column);
                return ordinal >= 0;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
        }

        private static string LeerTextoOpcional(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return null;
            return dr.GetValue(ordinal).ToString();
        }

        private static DateTime? LeerFechaOpcional(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return null;
            return Convert.ToDateTime(dr.GetValue(ordinal));
        }

        private static int LeerEnteroObligatorio(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return 0;
            return Convert.ToInt32(dr.GetValue(ordinal));
        }
        
        private static int? LeerEnteroOpcional(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return null;
            return Convert.ToInt32(dr.GetValue(ordinal));
        }
        
        private static bool LeerBooleanObligatorio(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal) || dr.IsDBNull(ordinal)) return false;
            return Convert.ToBoolean(dr.GetValue(ordinal));
        }

        private NoConformidad MapearDesdeDataReader(NpgsqlDataReader dr)
        {
            try
            {
                return new NoConformidad
                {
                    CodigoNoConformidad = LeerEnteroObligatorio(dr, "codigo_no_conformidad"),
                    CodigoInspeccion = LeerEnteroObligatorio(dr, "codigo_inspeccion"),
                    CodigoInforme = LeerEnteroObligatorio(dr, "codigo_informe"),
                    CodigoSolicitud = LeerEnteroObligatorio(dr, "codigo_solicitud"),
                    CodigoNoConformidadRaiz = LeerEnteroOpcional(dr, "codigo_nc_raiz"),
                    CodigoSolicitudOrigen = LeerEnteroOpcional(dr, "codigo_solicitud_origen"),
                    CodigoInspeccionOrigen = LeerEnteroOpcional(dr, "codigo_inspeccion_origen"),
                    CodigoInformeOrigen = LeerEnteroOpcional(dr, "codigo_informe_origen"),
                    CodigoSolicitudNueva = LeerEnteroOpcional(dr, "codigo_solicitud_nueva"),
                    CodigoInspeccionNueva = LeerEnteroOpcional(dr, "codigo_inspeccion_nueva"),
                    CodigoInformeCierre = LeerEnteroOpcional(dr, "codigo_informe_cierre"),
                    CicloEvaluacion = Math.Max(1, LeerEnteroObligatorio(dr, "ciclo_evaluacion")),
                    TipoRuta = LeerTextoOpcional(dr, "tipo_ruta"),
                    Estado = LeerTextoOpcional(dr, "estado"),
                    NumeroNoConformidad = LeerTextoOpcional(dr, "numero_no_conformidad"),
                    Resumen = LeerTextoOpcional(dr, "resumen"),
                    Detalle = LeerTextoOpcional(dr, "detalle"),FundamentoTecnico = LeerTextoOpcional(dr, "fundamento_tecnico"),
                    AccionesRequeridas = LeerTextoOpcional(dr, "acciones_requeridas"),PlazoSubsanacion = LeerEnteroOpcional(dr, "plazo_subsanacion"),
                    RequiereNuevaInspeccion = LeerBooleanObligatorio(dr, "requiere_nueva_inspeccion"),Version = LeerEnteroObligatorio(dr, "version"),
                    RutaPdf = LeerTextoOpcional(dr, "ruta_pdf"),RutaPdfFirmadoInspector = LeerTextoOpcional(dr, "ruta_pdf_firmado_inspector"),
                    RutaPdfFirmadoCoordinador = LeerTextoOpcional(dr, "ruta_pdf_firmado_coordinador"),RutaPdfSubsanacionRt = LeerTextoOpcional(dr, "ruta_pdf_subsanacion_rt"),
                    HashDocumento = LeerTextoOpcional(dr, "hash_documento"),FechaGeneracion = LeerFechaOpcional(dr, "fecha_generacion"),
                    FechaFirmaInspector = LeerFechaOpcional(dr, "fecha_firma_inspector"),FechaEnvioCoordinador = LeerFechaOpcional(dr, "fecha_envio_coordinador"),
                    FechaDevolucion = LeerFechaOpcional(dr, "fecha_devolucion"),FechaFirmaCoordinador = LeerFechaOpcional(dr, "fecha_firma_coordinador"),
                    FechaNotificacionRt = LeerFechaOpcional(dr, "fecha_notificacion_rt"),FechaSubsanacionRt = LeerFechaOpcional(dr, "fecha_subsanacion_rt"),
                    UsuarioCreacion = LeerEnteroOpcional(dr, "usuario_creacion"),UsuarioFirmaInspector = LeerEnteroOpcional(dr, "usuario_firma_inspector"),
                    UsuarioFirmaCoordinador = LeerEnteroOpcional(dr, "usuario_firma_coordinador"),ObservacionDevolucion = LeerTextoOpcional(dr, "observacion_devolucion"),
                    FechaCierre = LeerFechaOpcional(dr, "fecha_cierre"), UsuarioCierre = LeerEnteroOpcional(dr, "usuario_cierre"),
                    ObservacionCierre = LeerTextoOpcional(dr, "observacion_cierre"), CorrelationId = LeerTextoOpcional(dr, "correlation_id"),
                    CreatedAt = LeerFechaOpcional(dr, "created_at"),UpdatedAt = LeerFechaOpcional(dr, "updated_at")
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al mapear NoConformidad: {ex.Message}");
                return null;
            }
        }

        public NoConformidad Insertar(NoConformidad entidad, NpgsqlTransaction trx = null)
        {
            string query = $@"
                INSERT INTO {TABLA} (
                    codigo_inspeccion, codigo_informe, codigo_solicitud, tipo_ruta, estado, numero_no_conformidad,
                    resumen, detalle, fundamento_tecnico, acciones_requeridas, plazo_subsanacion, requiere_nueva_inspeccion,
                    version, ruta_pdf, ruta_pdf_firmado_inspector, ruta_pdf_firmado_coordinador, ruta_pdf_subsanacion_rt, hash_documento,
                    fecha_generacion, fecha_firma_inspector, fecha_envio_coordinador, fecha_devolucion,
                    fecha_firma_coordinador, fecha_notificacion_rt, fecha_subsanacion_rt, usuario_creacion, usuario_firma_inspector,
                    usuario_firma_coordinador, observacion_devolucion,
                    codigo_nc_raiz, codigo_solicitud_origen, codigo_inspeccion_origen, codigo_informe_origen,
                    codigo_solicitud_nueva, codigo_inspeccion_nueva, codigo_informe_cierre, ciclo_evaluacion,
                    fecha_cierre, usuario_cierre, observacion_cierre, correlation_id, created_at
                ) VALUES (
                    @codigo_inspeccion, @codigo_informe, @codigo_solicitud, @tipo_ruta, @estado, @numero_no_conformidad,
                    @resumen, @detalle, @fundamento_tecnico, @acciones_requeridas, @plazo_subsanacion, @requiere_nueva_inspeccion,
                    @version, @ruta_pdf, @ruta_pdf_firmado_inspector, @ruta_pdf_firmado_coordinador, @ruta_pdf_subsanacion_rt, @hash_documento,
                    @fecha_generacion, @fecha_firma_inspector, @fecha_envio_coordinador, @fecha_devolucion,
                    @fecha_firma_coordinador, @fecha_notificacion_rt, @fecha_subsanacion_rt, @usuario_creacion, @usuario_firma_inspector,
                    @usuario_firma_coordinador, @observacion_devolucion,
                    @codigo_nc_raiz, @codigo_solicitud_origen, @codigo_inspeccion_origen, @codigo_informe_origen,
                    @codigo_solicitud_nueva, @codigo_inspeccion_nueva, @codigo_informe_cierre, @ciclo_evaluacion,
                    @fecha_cierre, @usuario_cierre, @observacion_cierre, @correlation_id, NOW()
                ) RETURNING codigo_no_conformidad;";

            bool closeConnection = false;
            NpgsqlConnection conn = trx?.Connection;

            if (conn == null)
            {
                conn = new NpgsqlConnection(CS);
                conn.Open();
                closeConnection = true;
            }

            try
            {
                using (var cmd = new NpgsqlCommand(query, conn, trx))
                {
                    cmd.Parameters.AddWithValue("@codigo_inspeccion", entidad.CodigoInspeccion);
                    cmd.Parameters.AddWithValue("@codigo_informe", entidad.CodigoInforme);
                    cmd.Parameters.AddWithValue("@codigo_solicitud", entidad.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@tipo_ruta", entidad.TipoRuta);
                    cmd.Parameters.AddWithValue("@estado", entidad.Estado);
                    cmd.Parameters.AddWithValue("@numero_no_conformidad", (object)entidad.NumeroNoConformidad ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resumen", (object)entidad.Resumen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@detalle", (object)entidad.Detalle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fundamento_tecnico", (object)entidad.FundamentoTecnico ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@acciones_requeridas", (object)entidad.AccionesRequeridas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@plazo_subsanacion", (object)entidad.PlazoSubsanacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@requiere_nueva_inspeccion", entidad.RequiereNuevaInspeccion);
                    cmd.Parameters.AddWithValue("@version", entidad.Version);
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)entidad.RutaPdf ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_inspector", (object)entidad.RutaPdfFirmadoInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_coordinador", (object)entidad.RutaPdfFirmadoCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_subsanacion_rt", (object)entidad.RutaPdfSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_documento", (object)entidad.HashDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_generacion", (object)entidad.FechaGeneracion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_inspector", (object)entidad.FechaFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_envio_coordinador", (object)entidad.FechaEnvioCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_devolucion", (object)entidad.FechaDevolucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_coordinador", (object)entidad.FechaFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_notificacion_rt", (object)entidad.FechaNotificacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_subsanacion_rt", (object)entidad.FechaSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_creacion", (object)entidad.UsuarioCreacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_inspector", (object)entidad.UsuarioFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_coordinador", (object)entidad.UsuarioFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion_devolucion", (object)entidad.ObservacionDevolucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_nc_raiz", (object)entidad.CodigoNoConformidadRaiz ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_solicitud_origen", (object)entidad.CodigoSolicitudOrigen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_inspeccion_origen", (object)entidad.CodigoInspeccionOrigen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_informe_origen", (object)entidad.CodigoInformeOrigen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_solicitud_nueva", (object)entidad.CodigoSolicitudNueva ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_inspeccion_nueva", (object)entidad.CodigoInspeccionNueva ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_informe_cierre", (object)entidad.CodigoInformeCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ciclo_evaluacion", Math.Max(1, entidad.CicloEvaluacion));
                    cmd.Parameters.AddWithValue("@fecha_cierre", (object)entidad.FechaCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_cierre", (object)entidad.UsuarioCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion_cierre", (object)entidad.ObservacionCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@correlation_id", (object)entidad.CorrelationId ?? DBNull.Value);

                    var id = cmd.ExecuteScalar();
                    if (id != null)
                    {
                        entidad.CodigoNoConformidad = Convert.ToInt32(id);
                        using (var rootCmd = new NpgsqlCommand(
                            $@"UPDATE {TABLA} nc
SET codigo_nc_raiz=COALESCE(codigo_nc_raiz,codigo_no_conformidad),
    codigo_solicitud_origen=COALESCE(codigo_solicitud_origen,
        CASE WHEN EXISTS(SELECT 1 FROM public.aocr_tbsolicitud s WHERE s.codigo_solicitud=nc.codigo_solicitud) THEN codigo_solicitud END),
    codigo_inspeccion_origen=COALESCE(codigo_inspeccion_origen,
        CASE WHEN EXISTS(SELECT 1 FROM public.aocr_tbinspeccion i WHERE i.codigo_inspeccion=nc.codigo_inspeccion) THEN codigo_inspeccion END),
    codigo_informe_origen=COALESCE(codigo_informe_origen,
        CASE WHEN EXISTS(SELECT 1 FROM public.aocr_tbinforme_inspeccion f WHERE f.codigo_informe=nc.codigo_informe) THEN codigo_informe END)
WHERE codigo_no_conformidad=@id;",
                            conn,
                            trx))
                        {
                            rootCmd.Parameters.AddWithValue("@id", entidad.CodigoNoConformidad);
                            rootCmd.ExecuteNonQuery();
                        }
                        entidad.CodigoNoConformidadRaiz = entidad.CodigoNoConformidadRaiz ?? entidad.CodigoNoConformidad;
                        return entidad;
                    }
                    return null;
                }
            }
            finally
            {
                if (closeConnection)
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public bool RegistrarSubsanacionRt(int codigoNoConformidad,string ruta,DateTime fecha)
        {
            throw new NotSupportedException("GATE 7A: la carga general de subsanación está deshabilitada; use versionado individual por documento.");
#pragma warning disable 162
            if(codigoNoConformidad<=0||string.IsNullOrWhiteSpace(ruta))return false;
            var sql=$@"UPDATE {TABLA} SET ruta_pdf_subsanacion_rt=@ruta,fecha_subsanacion_rt=@fecha,estado='SUBSANADA_RT',observacion_devolucion=NULL,updated_at=NOW()
WHERE codigo_no_conformidad=@id AND UPPER(tipo_ruta)='SIN_INSPECCION' AND estado IN ('FIRMADA_COORDINADOR','EN_SUBSANACION');";
            using(var cn=new NpgsqlConnection(CS))using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@ruta",ruta);cmd.Parameters.AddWithValue("@fecha",fecha);cmd.Parameters.AddWithValue("@id",codigoNoConformidad);cn.Open();return cmd.ExecuteNonQuery()==1;}
#pragma warning restore 162
        }

        public bool ReabrirSubsanacionRt(int codigoNoConformidad)
        {
            throw new NotSupportedException("GATE 7A: no se reabre el PDF general; la devolución genera versiones individuales N+1.");
#pragma warning disable 162
            var sql = $@"UPDATE {TABLA}
SET ruta_pdf_subsanacion_rt=NULL, fecha_subsanacion_rt=NULL, estado='EN_SUBSANACION', updated_at=NOW()
WHERE codigo_no_conformidad=@id AND estado='SUBSANADA_RT' AND UPPER(tipo_ruta)='SIN_INSPECCION';";
            using (var cn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoNoConformidad);
                cn.Open();
                return cmd.ExecuteNonQuery() == 1;
            }
#pragma warning restore 162
        }

        public bool CerrarSubsanacion(int codigoNoConformidad)
        {
            throw new NotSupportedException("GATE 7A: el cierre se realiza al aceptar todas las versiones individuales.");
#pragma warning disable 162
            var sql=$@"UPDATE {TABLA} SET estado='CERRADA',updated_at=NOW() WHERE codigo_no_conformidad=@id AND estado='SUBSANADA_RT' AND UPPER(tipo_ruta)='SIN_INSPECCION';";
            using(var cn=new NpgsqlConnection(CS))using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@id",codigoNoConformidad);cn.Open();return cmd.ExecuteNonQuery()==1;}
#pragma warning restore 162
        }

        public bool RegistrarDecisionDocumentoSubsanado(int codigoNoConformidad, int codigoDocumentoNuevaVersion,
            bool aceptar, string comentario, int usuarioInspector)
        {
            if (codigoNoConformidad <= 0 || codigoDocumentoNuevaVersion <= 0 || usuarioInspector <= 0) return false;
            comentario = (comentario ?? string.Empty).Trim();
            if (!aceptar && string.IsNullOrWhiteSpace(comentario))
                throw new ArgumentException("El comentario de rechazo es obligatorio.", nameof(comentario));
            var decision = aceptar ? "ACEPTADO_SUBSANACION" : "RECHAZADO_SUBSANACION";

            using (var cn = new NpgsqlConnection(CS))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new NpgsqlCommand(@"
UPDATE public.aocr_tbdocumento_subsanacion ds
SET decision_inspector=@decision, comentario_inspector=@comentario,
    codigo_usuario_revision=@usuario, fecha_revision=NOW()
FROM public.aocr_tbnoconformidad nc
WHERE ds.codigo_no_conformidad=@nc AND ds.codigo_documento_nueva_version=@documento
  AND nc.codigo_no_conformidad=ds.codigo_no_conformidad
  AND UPPER(nc.tipo_ruta)='SIN_INSPECCION'
  AND nc.estado IN ('SUBSANADA_RT','EN_REVISION_INSPECTOR','SUBSANACION_DEVUELTA');", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@decision", decision);
                            cmd.Parameters.AddWithValue("@comentario", (object)comentario ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@usuario", usuarioInspector);
                            cmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            cmd.Parameters.AddWithValue("@documento", codigoDocumentoNuevaVersion);
                            if (cmd.ExecuteNonQuery() != 1) return false;
                        }

                        using (var cmd = new NpgsqlCommand(@"
UPDATE public.aocr_tbdocumento
SET estado=@estado, validado=@validado, observaciones=@comentario,
    fecha_validacion=NOW(), validado_por=@usuario_texto, updated_at=NOW(), updated_by=@usuario_texto
WHERE codigo_documento=@documento;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@estado", decision);
                            cmd.Parameters.AddWithValue("@validado", aceptar);
                            cmd.Parameters.AddWithValue("@comentario", aceptar ? (object)DBNull.Value : comentario);
                            cmd.Parameters.AddWithValue("@usuario_texto", usuarioInspector.ToString());
                            cmd.Parameters.AddWithValue("@documento", codigoDocumentoNuevaVersion);
                            if (cmd.ExecuteNonQuery() != 1) return false;
                        }

                        using (var cmd = new NpgsqlCommand(@"
UPDATE public.aocr_tbnoconformidad
SET estado=CASE WHEN @aceptar THEN 'EN_REVISION_INSPECTOR' ELSE 'SUBSANACION_DEVUELTA' END,
    observacion_devolucion=CASE WHEN @aceptar THEN observacion_devolucion ELSE @comentario END,
    updated_at=NOW()
WHERE codigo_no_conformidad=@nc;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@aceptar", aceptar);
                            cmd.Parameters.AddWithValue("@comentario", (object)comentario ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                        return true;
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }

        public bool AceptarSubsanacionDocumentalCompleta(int codigoNoConformidad, int codigoInspeccion, int usuarioInspector)
        {
            using (var cn = new NpgsqlConnection(CS))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        using (var lockNc = new NpgsqlCommand(@"SELECT 1 FROM public.aocr_tbnoconformidad
WHERE codigo_no_conformidad=@nc AND codigo_inspeccion=@inspeccion AND UPPER(tipo_ruta)='SIN_INSPECCION'
AND estado IN ('SUBSANADA_RT','EN_REVISION_INSPECTOR') FOR UPDATE;", cn, tx))
                        {
                            lockNc.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            lockNc.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                            if (lockNc.ExecuteScalar() == null) return false;
                        }

                        using (var pending = new NpgsqlCommand(@"
WITH hojas AS (
 SELECT ds.* FROM public.aocr_tbdocumento_subsanacion ds
 WHERE ds.codigo_no_conformidad=@nc
   AND ds.codigo_documento_nueva_version IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM public.aocr_tbdocumento_subsanacion sig
                   WHERE sig.codigo_no_conformidad=ds.codigo_no_conformidad
                     AND sig.codigo_documento_origen=ds.codigo_documento_nueva_version)
)
SELECT COUNT(*) FILTER (WHERE decision_inspector='ACEPTADO_SUBSANACION'), COUNT(*)
FROM hojas;", cn, tx))
                        {
                            pending.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            using (var rd = pending.ExecuteReader())
                            {
                                rd.Read();
                                var aceptados = rd.GetInt64(0);
                                var total = rd.GetInt64(1);
                                if (total == 0 || aceptados != total) return false;
                            }
                        }

                        using (var cmd = new NpgsqlCommand(@"UPDATE public.aocr_tbnoconformidad
SET estado='SUBSANACION_ACEPTADA',observacion_devolucion=NULL,updated_at=NOW()
WHERE codigo_no_conformidad=@nc;", cn, tx))
                        { cmd.Parameters.AddWithValue("@nc", codigoNoConformidad); cmd.ExecuteNonQuery(); }

                        // No se modifica resultado ni resultado_evaluacion: la reevaluacion requiere un informe nuevo.
                        using (var cmd = new NpgsqlCommand(@"UPDATE public.aocr_tbinspeccion
SET estado='EN_INSPECCION',estado_documental='SUBSANACION_ACEPTADA',updated_at=NOW(),updated_by=@usuario
WHERE codigo_inspeccion=@inspeccion;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@usuario", usuarioInspector.ToString());
                            cmd.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                            if (cmd.ExecuteNonQuery() != 1) return false;
                        }

                        using (var cmd = new NpgsqlCommand(@"INSERT INTO public.aocr_tbhistorial_documental
(codigo_solicitud,codigo_documento,evento,detalle,codigo_usuario,fecha_evento,created_at,created_by)
SELECT codigo_solicitud,NULL,'SUBSANACION_ACEPTADA_REEVALUACION_PENDIENTE',
'Todos los documentos fueron aceptados. Se habilita un nuevo informe tecnico; el resultado no cambia automaticamente.',
@usuario,NOW(),NOW(),@usuario_texto FROM public.aocr_tbnoconformidad WHERE codigo_no_conformidad=@nc;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@usuario", usuarioInspector);
                            cmd.Parameters.AddWithValue("@usuario_texto", usuarioInspector.ToString());
                            cmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                        return true;
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }

        public bool VincularNuevaEvaluacion(
            int codigoNoConformidad,
            int codigoSolicitudNueva,
            int? codigoInspeccionNueva,
            string correlationId,
            NpgsqlTransaction trx)
        {
            if (codigoNoConformidad <= 0 || codigoSolicitudNueva <= 0 || trx == null || trx.Connection == null)
                throw new ArgumentException("La NC, solicitud nueva y transaccion son obligatorias.");

            const string sql = @"UPDATE public.aocr_tbnoconformidad
SET codigo_solicitud_nueva=@solicitud_nueva,
    codigo_inspeccion_nueva=COALESCE(@inspeccion_nueva,codigo_inspeccion_nueva),
    correlation_id=COALESCE(NULLIF(@correlation_id,''),correlation_id),
    updated_at=NOW()
WHERE codigo_no_conformidad=@nc
  AND UPPER(tipo_ruta)='CON_INSPECCION'
  AND (codigo_solicitud_nueva IS NULL OR codigo_solicitud_nueva=@solicitud_nueva);";
            using (var cmd = new NpgsqlCommand(sql, trx.Connection, trx))
            {
                cmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                cmd.Parameters.AddWithValue("@solicitud_nueva", codigoSolicitudNueva);
                cmd.Parameters.AddWithValue("@inspeccion_nueva", (object)codigoInspeccionNueva ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@correlation_id", (object)(correlationId ?? string.Empty));
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public bool VincularInformeCierre(
            int codigoNoConformidad,
            int codigoInformeCierre,
            int usuarioCierre,
            string observacion,
            NpgsqlTransaction trx)
        {
            if (codigoNoConformidad <= 0 || codigoInformeCierre <= 0 || usuarioCierre <= 0 || trx == null || trx.Connection == null)
                throw new ArgumentException("El contexto de cierre de NC es obligatorio.");

            const string sql = @"UPDATE public.aocr_tbnoconformidad
SET codigo_informe_cierre=@informe, fecha_cierre=NOW(), usuario_cierre=@usuario,
    observacion_cierre=@observacion, estado='CERRADA', updated_at=NOW()
WHERE codigo_no_conformidad=@nc AND codigo_informe_cierre IS NULL;";
            using (var cmd = new NpgsqlCommand(sql, trx.Connection, trx))
            {
                cmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                cmd.Parameters.AddWithValue("@informe", codigoInformeCierre);
                cmd.Parameters.AddWithValue("@usuario", usuarioCierre);
                cmd.Parameters.AddWithValue("@observacion", (object)(observacion ?? string.Empty));
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        public NoConformidad DevolverSubsanacionComoNuevaVersion(int codigoNoConformidad,string observacion)
        {
            if(codigoNoConformidad<=0||string.IsNullOrWhiteSpace(observacion))return null;
            using(var cn=new NpgsqlConnection(CS)){cn.Open();using(var tx=cn.BeginTransaction()){
                using(var close=new NpgsqlCommand($"UPDATE {TABLA} SET estado='SUBSANACION_DEVUELTA',observacion_devolucion=@obs,updated_at=NOW() WHERE codigo_no_conformidad=@id AND estado='SUBSANADA_RT' AND UPPER(tipo_ruta)='SIN_INSPECCION';",cn,tx)){close.Parameters.AddWithValue("@obs",observacion.Trim());close.Parameters.AddWithValue("@id",codigoNoConformidad);if(close.ExecuteNonQuery()!=1)return null;}
                int nuevoId;using(var insert=new NpgsqlCommand($@"INSERT INTO {TABLA}(codigo_inspeccion,codigo_informe,codigo_solicitud,tipo_ruta,estado,numero_no_conformidad,resumen,detalle,fundamento_tecnico,acciones_requeridas,plazo_subsanacion,requiere_nueva_inspeccion,version,ruta_pdf,ruta_pdf_firmado_inspector,ruta_pdf_firmado_coordinador,hash_documento,fecha_generacion,fecha_firma_inspector,fecha_envio_coordinador,fecha_firma_coordinador,fecha_notificacion_rt,usuario_creacion,usuario_firma_inspector,usuario_firma_coordinador,observacion_devolucion,codigo_nc_raiz,codigo_solicitud_origen,codigo_inspeccion_origen,codigo_informe_origen,ciclo_evaluacion,correlation_id,created_at)
SELECT codigo_inspeccion,codigo_informe,codigo_solicitud,tipo_ruta,'EN_SUBSANACION',numero_no_conformidad,resumen,detalle,fundamento_tecnico,acciones_requeridas,plazo_subsanacion,requiere_nueva_inspeccion,version+1,ruta_pdf,ruta_pdf_firmado_inspector,ruta_pdf_firmado_coordinador,hash_documento,fecha_generacion,fecha_firma_inspector,fecha_envio_coordinador,fecha_firma_coordinador,fecha_notificacion_rt,usuario_creacion,usuario_firma_inspector,usuario_firma_coordinador,@obs,COALESCE(codigo_nc_raiz,codigo_no_conformidad),COALESCE(codigo_solicitud_origen,codigo_solicitud),COALESCE(codigo_inspeccion_origen,codigo_inspeccion),COALESCE(codigo_informe_origen,codigo_informe),ciclo_evaluacion,correlation_id,NOW() FROM {TABLA} WHERE codigo_no_conformidad=@id RETURNING codigo_no_conformidad;",cn,tx)){insert.Parameters.AddWithValue("@obs",observacion.Trim());insert.Parameters.AddWithValue("@id",codigoNoConformidad);nuevoId=Convert.ToInt32(insert.ExecuteScalar());}
                tx.Commit();return ObtenerPorId(nuevoId);
            }}
        }

        public bool Actualizar(NoConformidad entidad, NpgsqlTransaction trx = null)
        {
            string query = $@"
                UPDATE {TABLA} SET 
                    estado = @estado,
                    numero_no_conformidad = @numero_no_conformidad,
                    resumen = @resumen,
                    detalle = @detalle,
                    fundamento_tecnico = @fundamento_tecnico,
                    acciones_requeridas = @acciones_requeridas,
                    plazo_subsanacion = @plazo_subsanacion,
                    ruta_pdf = @ruta_pdf,
                    ruta_pdf_firmado_inspector = @ruta_pdf_firmado_inspector,
                    ruta_pdf_firmado_coordinador = @ruta_pdf_firmado_coordinador,
                    ruta_pdf_subsanacion_rt = @ruta_pdf_subsanacion_rt,
                    hash_documento = @hash_documento,
                    fecha_firma_inspector = @fecha_firma_inspector,
                    fecha_envio_coordinador = @fecha_envio_coordinador,
                    fecha_devolucion = @fecha_devolucion,
                    fecha_firma_coordinador = @fecha_firma_coordinador,
                    fecha_notificacion_rt = @fecha_notificacion_rt,
                    fecha_subsanacion_rt = @fecha_subsanacion_rt,
                    usuario_firma_inspector = @usuario_firma_inspector,
                    usuario_firma_coordinador = @usuario_firma_coordinador,
                    observacion_devolucion = @observacion_devolucion,
                    codigo_solicitud_nueva = @codigo_solicitud_nueva,
                    codigo_inspeccion_nueva = @codigo_inspeccion_nueva,
                    codigo_informe_cierre = @codigo_informe_cierre,
                    ciclo_evaluacion = @ciclo_evaluacion,
                    fecha_cierre = @fecha_cierre,
                    usuario_cierre = @usuario_cierre,
                    observacion_cierre = @observacion_cierre,
                    correlation_id = @correlation_id,
                    updated_at = NOW()
                WHERE codigo_no_conformidad = @codigo_no_conformidad;";

            bool closeConnection = false;
            NpgsqlConnection conn = trx?.Connection;

            if (conn == null)
            {
                conn = new NpgsqlConnection(CS);
                conn.Open();
                closeConnection = true;
            }

            try
            {
                using (var cmd = new NpgsqlCommand(query, conn, trx))
                {
                    cmd.Parameters.AddWithValue("@codigo_no_conformidad", entidad.CodigoNoConformidad);
                    cmd.Parameters.AddWithValue("@estado", entidad.Estado);
                    cmd.Parameters.AddWithValue("@numero_no_conformidad", (object)entidad.NumeroNoConformidad ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resumen", (object)entidad.Resumen ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@detalle", (object)entidad.Detalle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fundamento_tecnico", (object)entidad.FundamentoTecnico ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@acciones_requeridas", (object)entidad.AccionesRequeridas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@plazo_subsanacion", (object)entidad.PlazoSubsanacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)entidad.RutaPdf ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_inspector", (object)entidad.RutaPdfFirmadoInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_firmado_coordinador", (object)entidad.RutaPdfFirmadoCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta_pdf_subsanacion_rt", (object)entidad.RutaPdfSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hash_documento", (object)entidad.HashDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_inspector", (object)entidad.FechaFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_envio_coordinador", (object)entidad.FechaEnvioCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_devolucion", (object)entidad.FechaDevolucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_firma_coordinador", (object)entidad.FechaFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_notificacion_rt", (object)entidad.FechaNotificacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_subsanacion_rt", (object)entidad.FechaSubsanacionRt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_inspector", (object)entidad.UsuarioFirmaInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_firma_coordinador", (object)entidad.UsuarioFirmaCoordinador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion_devolucion", (object)entidad.ObservacionDevolucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_solicitud_nueva", (object)entidad.CodigoSolicitudNueva ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_inspeccion_nueva", (object)entidad.CodigoInspeccionNueva ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@codigo_informe_cierre", (object)entidad.CodigoInformeCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ciclo_evaluacion", Math.Max(1, entidad.CicloEvaluacion));
                    cmd.Parameters.AddWithValue("@fecha_cierre", (object)entidad.FechaCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_cierre", (object)entidad.UsuarioCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observacion_cierre", (object)entidad.ObservacionCierre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@correlation_id", (object)entidad.CorrelationId ?? DBNull.Value);

                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
            }
            finally
            {
                if (closeConnection)
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public NoConformidad ObtenerPorId(int codigoNoConformidad)
        {
            string query = $"SELECT * FROM {TABLA} WHERE codigo_no_conformidad = @id LIMIT 1;";
            
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoNoConformidad);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        return MapearDesdeDataReader(dr);
                    }
                }
            }
            return null;
        }

        public List<NoConformidad> ObtenerPorInspeccion(int codigoInspeccion)
        {
            var lista = new List<NoConformidad>();
            string query = $"SELECT * FROM {TABLA} WHERE codigo_inspeccion = @id ORDER BY version DESC;";
            
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoInspeccion);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        var item = MapearDesdeDataReader(dr);
                        if (item != null) lista.Add(item);
                    }
                }
            }
            return lista;
        }

        public int ContarAbiertasRelacionadasConInspeccion(int codigoInspeccion)
        {
            if (codigoInspeccion <= 0) return 0;
            const string sql = @"SELECT COUNT(*) FROM public.aocr_tbnoconformidad
WHERE (codigo_inspeccion=@inspeccion OR codigo_inspeccion_nueva=@inspeccion)
  AND fecha_cierre IS NULL
  AND UPPER(COALESCE(estado,'')) NOT IN ('CERRADA','CERRADO','ANULADA');";
            using (var cn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                cmd.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public List<NoConformidad> ListarPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<NoConformidad>();
            string query = $"SELECT * FROM {TABLA} WHERE codigo_solicitud = @id ORDER BY version DESC, codigo_no_conformidad DESC;";
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoSolicitud);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        var item = MapearDesdeDataReader(dr);
                        if (item != null) lista.Add(item);
                    }
                }
            }
            return lista;
        }
        
        public NoConformidad ObtenerUltimaPorInforme(int codigoInforme)
        {
            string query = $"SELECT * FROM {TABLA} WHERE codigo_informe = @id ORDER BY version DESC LIMIT 1;";
            
            using (var conn = new NpgsqlConnection(CS))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", codigoInforme);
                conn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        return MapearDesdeDataReader(dr);
                    }
                }
            }
            return null;
        }
    }
}
