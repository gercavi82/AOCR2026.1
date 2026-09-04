using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Mail;
using CapaDatos.Constants;
using CapaDatos.Interfaces;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Persistencia AC-11. Cada transición, historial, auditoría y outbox se confirma
    /// dentro de una única transacción PostgreSQL. No envía SMTP ni entrega al RT.
    /// </summary>
    public sealed class AocrFinalWorkflowDAO : IAocrFinalWorkflowRepository
    {
        private const string TipoAocr = "RECONOCIMIENTO";
        private const string TipoCl = "CONDICIONES_LIMITACIONES";
        private readonly string _connectionString;

        public AocrFinalWorkflowDAO()
        {
            var configured = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = configured != null && !string.IsNullOrWhiteSpace(configured.ConnectionString)
                ? configured.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public AocrFinalWorkflowDAO(string connectionString)
        {
            _connectionString = connectionString;
        }

        public AocrWorkflowResult RemitirAocrDirdac(RemitirAocrDirdacRequest request)
        {
            return EjecutarTransaccion(request.SolicitudId, (cn, tx) =>
            {
                var actual = CargarProceso(cn, tx, request.SolicitudId);
                if (actual == null) return AocrWorkflowResult.Error(404, "EXPEDIENTE_NO_EXISTE", "El expediente no existe o no tiene flujo activo.");
                var key = Clave(request.IdempotencyKey, request.SolicitudId, "REMITIR_AOCR_DIRDAC", request.VersionEsperada, request.Actor.UsuarioId);
                var repetido = ResultadoIdempotente(cn, tx, key, actual);
                if (repetido != null) return repetido;
                if (actual.Version != request.VersionEsperada) return ConflictoVersion(actual);
                if (!EsEstado(actual.Estado, AocrEstadosProceso.ClFirmadaDircav, AocrEstadosProceso.PendienteRevisionFinalDircav,
                    AocrEstadosProceso.DocumentosFinalesPorGenerar, AocrEstadosProceso.DocumentosFinalesEnFirma))
                    return AocrWorkflowResult.Error(409, "ESTADO_INVALIDO", "DIRCAV solo puede remitir un expediente revisado y con CL firmada.");

                var aocr = CargarDocumento(cn, tx, request.SolicitudId, TipoAocr);
                var cl = CargarDocumento(cn, tx, request.SolicitudId, TipoCl);
                if (aocr == null || aocr.Id != request.DocumentoId || aocr.Version != request.VersionAocrEsperada || !aocr.Vigente)
                    return AocrWorkflowResult.Error(409, "AOCR_VERSION_INVALIDA", "El AOCR indicado no es la versión vigente del expediente.");
                if (string.IsNullOrWhiteSpace(aocr.Hash) || string.IsNullOrWhiteSpace(aocr.Ruta))
                    return AocrWorkflowResult.Error(409, "AOCR_INCOMPLETO", "El AOCR vigente no tiene evidencia íntegra para remisión.");
                if (cl == null || !cl.Vigente || !EsEstado(cl.Estado, AocrEstadosProceso.ClFirmadaDircav, AocrEstadosProceso.CondicionesFirmadasDcav)
                    || !AocrRolesInstitucionales.EsDircav(cl.RolFirma) || string.IsNullOrWhiteSpace(cl.HashFirmado))
                    return AocrWorkflowResult.Error(409, "CL_NO_FIRMADA", "Condiciones y Limitaciones no cuenta con firma DIRCAV vigente.");

                ActualizarDocumento(cn, tx, aocr.Id, AocrEstadosProceso.AocrPendienteDirdac, request.Actor.UsuarioId);
                var siguiente = CambiarProceso(cn, tx, actual, AocrEstadosProceso.AocrPendienteDirdac, "REVISION_LEGALIZACION_AOCR", AocrRolesInstitucionales.Dirdac, request.Actor, request.Observacion);
                RegistrarTrazabilidad(cn, tx, key, "AOCR_REMITIDO_DIRDAC", actual, siguiente, request.Actor, request.Observacion, aocr.Id, aocr.Version, aocr.Hash);
                EncolarAUsuariosRol(cn, tx, AocrRolesInstitucionales.DirdacSqlTokens, request.SolicitudId, key,
                    "AOCR_PENDIENTE_DIRDAC", "AOCR pendiente de revisión y firma", request.BaseUrl, "/Dirdac/BandejaAocr");
                return Exito("REMITIDO_DIRDAC", "AOCR remitido a DIRDAC.", actual, siguiente, aocr.Id, key);
            });
        }

        public AocrWorkflowResult DevolverAocrDircav(DevolverAocrDircavRequest request)
        {
            return EjecutarTransaccion(request.SolicitudId, (cn, tx) =>
            {
                var actual = CargarProceso(cn, tx, request.SolicitudId);
                if (actual == null) return AocrWorkflowResult.Error(404, "EXPEDIENTE_NO_EXISTE", "El expediente no existe o no está visible.");
                var key = Clave(request.IdempotencyKey, request.SolicitudId, "DEVOLVER_AOCR_DIRCAV", request.VersionEsperada, request.Actor.UsuarioId);
                var repetido = ResultadoIdempotente(cn, tx, key, actual);
                if (repetido != null) return repetido;
                if (actual.Version != request.VersionEsperada) return ConflictoVersion(actual);
                if (!EsEstado(actual.Estado, AocrEstadosProceso.AocrPendienteDirdac))
                    return AocrWorkflowResult.Error(409, "ESTADO_INVALIDO", "El AOCR ya no está pendiente de decisión DIRDAC.");

                var aocr = CargarDocumento(cn, tx, request.SolicitudId, TipoAocr);
                if (aocr == null) return AocrWorkflowResult.Error(404, "AOCR_NO_EXISTE", "No existe AOCR vigente para devolver.");
                var siguiente = CambiarProceso(cn, tx, actual, AocrEstadosProceso.DevueltoDircav, "CORRECCION_AOCR", AocrRolesInstitucionales.Dircav, request.Actor, request.Observacion);
                RegistrarTrazabilidad(cn, tx, key, "AOCR_DEVUELTO_DIRCAV", actual, siguiente, request.Actor, request.Observacion, aocr.Id, aocr.Version, aocr.Hash);
                EncolarAUsuariosRol(cn, tx, AocrRolesInstitucionales.DircavSqlTokens, request.SolicitudId, key,
                    "AOCR_DEVUELTO_DIRCAV", "AOCR devuelto por DIRDAC", request.BaseUrl, "/Dircav/Bandeja?tab=devueltos");
                return Exito("DEVUELTO_DIRCAV", "AOCR devuelto a DIRCAV con observación.", actual, siguiente, aocr.Id, key);
            });
        }

        public AocrWorkflowResult FirmarLegalizarAocr(FirmarLegalizarAocrRequest request)
        {
            return EjecutarTransaccion(request.SolicitudId, (cn, tx) =>
            {
                var actual = CargarProceso(cn, tx, request.SolicitudId);
                if (actual == null) return AocrWorkflowResult.Error(404, "EXPEDIENTE_NO_EXISTE", "El expediente no existe o no está visible.");
                var key = Clave(request.IdempotencyKey, request.SolicitudId, "FIRMAR_AOCR_DIRDAC", request.VersionEsperada, request.Actor.UsuarioId);
                var repetido = ResultadoIdempotente(cn, tx, key, actual);
                if (repetido != null) return repetido;
                if (actual.Version != request.VersionEsperada) return ConflictoVersion(actual);
                if (!EsEstado(actual.Estado, AocrEstadosProceso.AocrPendienteDirdac))
                    return AocrWorkflowResult.Error(409, "ESTADO_INVALIDO", "El AOCR no está pendiente de firma DIRDAC.");

                var aocr = CargarDocumento(cn, tx, request.SolicitudId, TipoAocr);
                var cl = CargarDocumento(cn, tx, request.SolicitudId, TipoCl);
                if (aocr == null || aocr.Id != request.DocumentoId || aocr.Version != request.VersionAocrEsperada || !aocr.Vigente)
                    return AocrWorkflowResult.Error(409, "AOCR_VERSION_INVALIDA", "La versión AOCR cambió; recargue el expediente.");
                if (!string.IsNullOrWhiteSpace(aocr.HashFirmado) || EsEstado(aocr.Estado, AocrEstadosProceso.AocrFirmadaDirdac, AocrEstadosProceso.AocrFirmadoDirdac))
                    return AocrWorkflowResult.Error(409, "AOCR_YA_FIRMADA", "La versión AOCR ya fue firmada y es inmutable.");
                if (cl == null || !EsEstado(cl.Estado, AocrEstadosProceso.ClFirmadaDircav, AocrEstadosProceso.CondicionesFirmadasDcav)
                    || cl.Version != aocr.Version || !AocrRolesInstitucionales.EsDircav(cl.RolFirma))
                    return AocrWorkflowResult.Error(409, "VERSIONES_INCOMPATIBLES", "AOCR y CL firmada no pertenecen a versiones compatibles.");

                const string firma = @"INSERT INTO public.aocr_tbfirma_documento
(codigo_solicitud,codigo_inspeccion,tipo_documento,nombre_archivo,ruta_documento,hash_documento,
 tamanio_pdf_firmado,firmado_por_rol,sujeto_certificado,nombre_firmante,cargo_firmante,
 fecha_firma,codigo_usuario,usuario_nombre,created_at,estado_documento,version)
VALUES(@solicitud,NULLIF(@inspeccion,0),'RECONOCIMIENTO',@nombre,@ruta,@hash,@bytes,'DIRDAC',
 @sujeto,@firmante,@cargo,NOW(),@usuario,@usuario_nombre,NOW(),'AOCR_FIRMADA_DIRDAC',@version)
ON CONFLICT (codigo_solicitud,UPPER(tipo_documento),version) DO NOTHING;";
                using (var cmd = new NpgsqlCommand(firma, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@solicitud", request.SolicitudId); cmd.Parameters.AddWithValue("@inspeccion", actual.InspeccionId);
                    cmd.Parameters.AddWithValue("@nombre", System.IO.Path.GetFileName(request.RutaPdfFirmado)); cmd.Parameters.AddWithValue("@ruta", request.RutaPdfFirmado);
                    cmd.Parameters.AddWithValue("@hash", request.HashPdfFirmado); cmd.Parameters.AddWithValue("@bytes", request.TamanioPdfFirmado);
                    cmd.Parameters.AddWithValue("@sujeto", (object)request.SujetoCertificado ?? DBNull.Value); cmd.Parameters.AddWithValue("@firmante", (object)request.NombreFirmante ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cargo", (object)request.CargoFirmante ?? DBNull.Value); cmd.Parameters.AddWithValue("@usuario", request.Actor.UsuarioId);
                    cmd.Parameters.AddWithValue("@usuario_nombre", request.Actor.UsuarioNombre); cmd.Parameters.AddWithValue("@version", aocr.Version); cmd.ExecuteNonQuery();
                }

                const string update = @"UPDATE public.aocr_tbdocumento_generado
SET estado=@estado,ruta_pdf_firmado=@ruta,hash_pdf_firmado=@hash,tamanio_pdf_firmado=@bytes,
    codigo_usuario_firma=@usuario,rol_firma='DIRDAC',fecha_firma=NOW(),bloqueado=TRUE,
    version_concurrencia=version_concurrencia+1
WHERE codigo_documento=@documento AND vigente=TRUE AND hash_pdf_firmado IS NULL;";
                using (var cmd = new NpgsqlCommand(update, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@estado", AocrEstadosProceso.AocrFirmadaDirdac);
                    cmd.Parameters.AddWithValue("@ruta", request.RutaPdfFirmado);
                    cmd.Parameters.AddWithValue("@hash", request.HashPdfFirmado);
                    cmd.Parameters.AddWithValue("@bytes", request.TamanioPdfFirmado);
                    cmd.Parameters.AddWithValue("@usuario", request.Actor.UsuarioId);
                    cmd.Parameters.AddWithValue("@documento", aocr.Id);
                    if (cmd.ExecuteNonQuery() != 1) return AocrWorkflowResult.Error(409, "FIRMA_CONCURRENTE", "La firma ya fue procesada por otra solicitud.");
                }

                var firmado = CambiarProceso(cn, tx, actual, AocrEstadosProceso.AocrFirmadaDirdac, "AOCR_FIRMADA_DIRDAC", AocrRolesInstitucionales.Dirdac, request.Actor, "AOCR firmado y legalizado por DIRDAC.");
                RegistrarTrazabilidad(cn, tx, key, "AOCR_FIRMADA_DIRDAC", actual, firmado, request.Actor, request.NombreFirmante, aocr.Id, aocr.Version, request.HashPdfFirmado);
                var completo = CambiarProceso(cn, tx, firmado, AocrEstadosProceso.FirmasCompletas, "FIRMAS_COMPLETAS", AocrRolesInstitucionales.Coordinador, request.Actor, "Firmas DIRCAV y DIRDAC vigentes y compatibles.");
                RegistrarTrazabilidad(cn, tx, key + ":FIRMAS_COMPLETAS", "FIRMAS_COMPLETAS", firmado, completo, request.Actor, null, aocr.Id, aocr.Version, request.HashPdfFirmado);
                RegistrarTrazabilidad(cn, tx, key + ":ENTREGA_FINAL_SOLICITADA", "ENTREGA_FINAL_SOLICITADA", completo, completo, request.Actor,
                    "AC-12 debe resolver RT, Inspector y documentos después del commit.", aocr.Id, aocr.Version, request.HashPdfFirmado);
                EncolarAUsuariosRol(cn, tx, AocrRolesInstitucionales.DircavSqlTokens, request.SolicitudId, key,
                    "AOCR_FIRMADA_DIRDAC", "AOCR firmado y legalizado por DIRDAC", string.Empty, "/Dircav/Bandeja");
                return Exito("FIRMAS_COMPLETAS", "AOCR firmado por DIRDAC; ambas firmas quedaron verificadas. AC-11 no realiza la entrega.", actual, completo, aocr.Id, key);
            });
        }

        public AocrWorkflowResult EvaluarFirmasCompletas(int solicitudId, long versionEsperada, AocrWorkflowActor actor)
        {
            return EjecutarTransaccion(solicitudId, (cn, tx) =>
            {
                var actual = CargarProceso(cn, tx, solicitudId);
                if (actual == null) return AocrWorkflowResult.Error(404, "EXPEDIENTE_NO_EXISTE", "El expediente no existe.");
                if (actual.Version != versionEsperada) return ConflictoVersion(actual);
                var aocr = CargarDocumento(cn, tx, solicitudId, TipoAocr);
                var cl = CargarDocumento(cn, tx, solicitudId, TipoCl);
                if (aocr == null || cl == null || aocr.Version != cl.Version || !aocr.Vigente || !cl.Vigente
                    || !EsEstado(aocr.Estado, AocrEstadosProceso.AocrFirmadaDirdac, AocrEstadosProceso.AocrFirmadoDirdac)
                    || !EsEstado(cl.Estado, AocrEstadosProceso.ClFirmadaDircav, AocrEstadosProceso.CondicionesFirmadasDcav)
                    || !AocrRolesInstitucionales.EsDirdac(aocr.RolFirma) || !AocrRolesInstitucionales.EsDircav(cl.RolFirma))
                    return AocrWorkflowResult.Error(409, "FIRMAS_INCOMPLETAS", "Las dos firmas vigentes y compatibles todavía no están completas.");
                if (EsEstado(actual.Estado, AocrEstadosProceso.FirmasCompletas)) return Exito("IDEMPOTENTE", "Las firmas ya estaban completas.", actual, actual, aocr.Id, "");
                var siguiente = CambiarProceso(cn, tx, actual, AocrEstadosProceso.FirmasCompletas, "FIRMAS_COMPLETAS", AocrRolesInstitucionales.Coordinador, actor, null);
                return Exito("FIRMAS_COMPLETAS", "Firmas completas verificadas.", actual, siguiente, aocr.Id, "");
            });
        }

        public IList<BandejaAocrDirdacItemViewModel> ListarBandejaDirdac()
        {
            var items = new List<BandejaAocrDirdacItemViewModel>();
            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(BandejaSql + " ORDER BY pe.fecha_estado ASC;", cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader()) while (rd.Read()) items.Add(MapBandeja(rd));
            }
            return items;
        }

        public DetalleAocrDirdacViewModel ObtenerDetalleDirdac(int solicitudId)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(BandejaSql + " AND s.codigo_solicitud=@solicitud LIMIT 1;", cn))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId); cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    var item = MapBandeja(rd);
                    return new DetalleAocrDirdacViewModel
                    {
                        Expediente = item,
                        EstadoCondicionesLimitaciones = S(rd, "estado_cl"),
                        VersionCondicionesLimitaciones = I(rd, "version_cl"),
                        CondicionesFirmadasDircav = B(rd, "cl_firmada"),
                        AocrFirmadaDirdac = B(rd, "aocr_firmada")
                    };
                }
            }
        }

        public BandejaAocrDirdacItemViewModel ObtenerContextoRemisionDircav(int solicitudId)
        {
            const string sql=@"SELECT s.codigo_solicitud,COALESCE(NULLIF(s.numero_solicitud,''),s.codigo_solicitud::text) numero_solicitud,
COALESCE(NULLIF(s.razon_social,''),NULLIF(s.nombre_operador,''),'No registrada') compania,
a.codigo_documento,a.version_documento,a.hash_pdf,pe.version version_expediente,pe.fecha_estado,
COALESCE(a.usuario_nombre,'DIRCAV') usuario_remitente,pe.estado_actual,0 minutos_pendiente
FROM public.aocr_proceso_estado pe JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=pe.solicitud_id
JOIN public.aocr_tbdocumento_generado a ON a.codigo_solicitud=s.codigo_solicitud AND a.vigente=TRUE AND UPPER(a.tipo_documento)='RECONOCIMIENTO'
WHERE pe.activo=TRUE AND s.codigo_solicitud=@solicitud ORDER BY pe.id DESC LIMIT 1;";
            using(var cn=new NpgsqlConnection(_connectionString))using(var cmd=new NpgsqlCommand(sql,cn)){cmd.Parameters.AddWithValue("@solicitud",solicitudId);cn.Open();using(var rd=cmd.ExecuteReader())return rd.Read()?MapBandeja(rd):null;}
        }

        private const string BandejaSql = @"SELECT s.codigo_solicitud,COALESCE(NULLIF(s.numero_solicitud,''),s.codigo_solicitud::text) numero_solicitud,
COALESCE(NULLIF(s.razon_social,''),NULLIF(s.nombre_operador,''),'No registrada') compania,
a.codigo_documento,a.version_documento,a.hash_pdf,pe.version version_expediente,pe.fecha_estado,
COALESCE(ev.usuario,'DIRCAV') usuario_remitente,pe.estado_actual,
GREATEST(0,FLOOR(EXTRACT(EPOCH FROM (NOW()-pe.fecha_estado))/60))::int minutos_pendiente,
c.estado estado_cl,c.version_documento version_cl,
(c.hash_pdf_firmado IS NOT NULL AND UPPER(COALESCE(c.rol_firma,'')) IN ('DIRCAV','DCAV')) cl_firmada,
(a.hash_pdf_firmado IS NOT NULL AND UPPER(COALESCE(a.rol_firma,''))='DIRDAC') aocr_firmada
FROM public.aocr_proceso_estado pe
JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=pe.solicitud_id
JOIN public.aocr_tbdocumento_generado a ON a.codigo_solicitud=s.codigo_solicitud AND a.vigente=TRUE AND UPPER(a.tipo_documento)='RECONOCIMIENTO'
JOIN public.aocr_tbdocumento_generado c ON c.codigo_solicitud=s.codigo_solicitud AND c.vigente=TRUE AND UPPER(c.tipo_documento)='CONDICIONES_LIMITACIONES'
LEFT JOIN LATERAL (SELECT e.usuario FROM public.aocr_evento_workflow e WHERE e.solicitud_id=s.codigo_solicitud AND e.evento='AOCR_REMITIDO_DIRDAC' ORDER BY e.id DESC LIMIT 1) ev ON TRUE
WHERE pe.activo=TRUE AND pe.estado_actual IN ('AOCR_PENDIENTE_DIRDAC','AOCR_FIRMADA_DIRDAC','FIRMAS_COMPLETAS')";

        private AocrWorkflowResult EjecutarTransaccion(int solicitudId, Func<NpgsqlConnection, NpgsqlTransaction, AocrWorkflowResult> action)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        using (var l = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@id::bigint);", cn, tx)) { l.Parameters.AddWithValue("@id", solicitudId); l.ExecuteNonQuery(); }
                        var result = action(cn, tx);
                        if (result.Exito) tx.Commit(); else tx.Rollback();
                        return result;
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        private static Proceso CargarProceso(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId)
        {
            const string sql = @"SELECT pe.id,pe.solicitud_id,COALESCE(pe.inspeccion_id,0) inspeccion_id,pe.estado_actual,pe.version
FROM public.aocr_proceso_estado pe JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=pe.solicitud_id
WHERE pe.solicitud_id=@solicitud AND pe.activo=TRUE AND s.deleted_at IS NULL ORDER BY pe.id DESC LIMIT 1 FOR UPDATE OF pe,s;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx)) { cmd.Parameters.AddWithValue("@solicitud", solicitudId); using (var rd = cmd.ExecuteReader()) return rd.Read() ? new Proceso { Id=I(rd,"id"), SolicitudId=I(rd,"solicitud_id"), InspeccionId=I(rd,"inspeccion_id"), Estado=S(rd,"estado_actual"), Version=L(rd,"version") } : null; }
        }

        private static Documento CargarDocumento(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, string tipo)
        {
            const string sql = @"SELECT codigo_documento,version_documento,vigente,estado,ruta_documento,hash_pdf,ruta_pdf_firmado,hash_pdf_firmado,rol_firma
FROM public.aocr_tbdocumento_generado WHERE codigo_solicitud=@solicitud AND UPPER(tipo_documento)=@tipo AND vigente=TRUE
ORDER BY version_documento DESC,codigo_documento DESC LIMIT 1 FOR UPDATE;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx)) { cmd.Parameters.AddWithValue("@solicitud", solicitudId); cmd.Parameters.AddWithValue("@tipo", tipo); using (var rd=cmd.ExecuteReader()) return rd.Read()?new Documento { Id=I(rd,"codigo_documento"),Version=I(rd,"version_documento"),Vigente=B(rd,"vigente"),Estado=S(rd,"estado"),Ruta=S(rd,"ruta_documento"),Hash=S(rd,"hash_pdf"),RutaFirmada=S(rd,"ruta_pdf_firmado"),HashFirmado=S(rd,"hash_pdf_firmado"),RolFirma=S(rd,"rol_firma")}:null; }
        }

        private static Proceso CambiarProceso(NpgsqlConnection cn, NpgsqlTransaction tx, Proceso actual, string estado, string etapa, string rol, AocrWorkflowActor actor, string observacion)
        {
            using (var off = new NpgsqlCommand("UPDATE public.aocr_proceso_estado SET activo=FALSE,updated_at=NOW(),updated_by=@u WHERE id=@id AND activo=TRUE;",cn,tx)) { off.Parameters.AddWithValue("@u",actor.UsuarioId); off.Parameters.AddWithValue("@id",actual.Id); if(off.ExecuteNonQuery()!=1) throw new InvalidOperationException("Conflicto al actualizar el flujo final."); }
            const string insert=@"INSERT INTO public.aocr_proceso_estado(solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,usuario_responsable_id,observacion,fecha_estado,activo,version,created_at,created_by,updated_at,updated_by)
VALUES(@s,NULLIF(@i,0),@e,@et,@r,NULL,@o,NOW(),TRUE,@v,NOW(),@u,NOW(),@u) RETURNING id;";
            int id; using(var cmd=new NpgsqlCommand(insert,cn,tx)){cmd.Parameters.AddWithValue("@s",actual.SolicitudId);cmd.Parameters.AddWithValue("@i",actual.InspeccionId);cmd.Parameters.AddWithValue("@e",estado);cmd.Parameters.AddWithValue("@et",etapa);cmd.Parameters.AddWithValue("@r",rol);cmd.Parameters.AddWithValue("@o",(object)observacion??DBNull.Value);cmd.Parameters.AddWithValue("@v",actual.Version+1);cmd.Parameters.AddWithValue("@u",actor.UsuarioId);id=Convert.ToInt32(cmd.ExecuteScalar());}
            using(var cmd=new NpgsqlCommand("UPDATE public.aocr_tbsolicitud SET estado=@e,updated_at=NOW(),updated_by=@u WHERE codigo_solicitud=@s;",cn,tx)){cmd.Parameters.AddWithValue("@e",estado);cmd.Parameters.AddWithValue("@u",actor.UsuarioNombre??actor.UsuarioId.ToString());cmd.Parameters.AddWithValue("@s",actual.SolicitudId);cmd.ExecuteNonQuery();}
            return new Proceso { Id=id,SolicitudId=actual.SolicitudId,InspeccionId=actual.InspeccionId,Estado=estado,Version=actual.Version+1 };
        }

        private static void ActualizarDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,int id,string estado,int usuario)
        { using(var cmd=new NpgsqlCommand("UPDATE public.aocr_tbdocumento_generado SET estado=@e,bloqueado=TRUE,version_concurrencia=version_concurrencia+1,codigo_usuario_liberacion=@u WHERE codigo_documento=@id AND vigente=TRUE;",cn,tx)){cmd.Parameters.AddWithValue("@e",estado);cmd.Parameters.AddWithValue("@u",usuario);cmd.Parameters.AddWithValue("@id",id);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Conflicto documental al remitir AOCR.");} }

        private static void RegistrarTrazabilidad(NpgsqlConnection cn,NpgsqlTransaction tx,string key,string evento,Proceso anterior,Proceso nuevo,AocrWorkflowActor actor,string observacion,int documentoId,int version,string hash)
        {
            const string ev=@"INSERT INTO public.aocr_evento_workflow(evento,event_key,correlation_id,modulo,accion,entidad,entidad_id,solicitud_id,inspeccion_id,documento_id,estado_anterior,estado_nuevo,usuario_id,usuario,rol,ip,observacion,version,hash,resultado,intentos,fecha,created_at,updated_at)
VALUES(@evento,@key,@corr,'AC11',@evento,'aocr_tbsolicitud',@s,@s,NULLIF(@i,0),@d,@ea,@en,@u,@usuario,@rol,@ip,@o,@v,@hash,'EXITOSO',1,NOW(),NOW(),NOW()) ON CONFLICT(event_key) DO NOTHING;";
            using(var cmd=new NpgsqlCommand(ev,cn,tx)){cmd.Parameters.AddWithValue("@evento",evento);cmd.Parameters.AddWithValue("@key",key);cmd.Parameters.AddWithValue("@corr",Correlation(key));cmd.Parameters.AddWithValue("@s",anterior.SolicitudId);cmd.Parameters.AddWithValue("@i",anterior.InspeccionId);cmd.Parameters.AddWithValue("@d",documentoId);cmd.Parameters.AddWithValue("@ea",anterior.Estado);cmd.Parameters.AddWithValue("@en",nuevo.Estado);cmd.Parameters.AddWithValue("@u",actor.UsuarioId);cmd.Parameters.AddWithValue("@usuario",actor.UsuarioNombre??string.Empty);cmd.Parameters.AddWithValue("@rol",actor.RolActivo??string.Empty);cmd.Parameters.AddWithValue("@ip",(object)actor.Ip??DBNull.Value);cmd.Parameters.AddWithValue("@o",(object)observacion??DBNull.Value);cmd.Parameters.AddWithValue("@v",version);cmd.Parameters.AddWithValue("@hash",(object)hash??DBNull.Value);cmd.ExecuteNonQuery();}
            const string hist=@"INSERT INTO public.aocr_tbhistorial_estado(codigo_solicitud,estado_anterior,estado_nuevo,codigo_usuario,observaciones,fecha_cambio)
SELECT @s,@ea,@en,@u,@o,NOW() WHERE NOT EXISTS(SELECT 1 FROM public.aocr_evento_workflow WHERE event_key=@key AND evento<>@evento);";
            using(var cmd=new NpgsqlCommand(hist,cn,tx)){cmd.Parameters.AddWithValue("@s",anterior.SolicitudId);cmd.Parameters.AddWithValue("@ea",anterior.Estado);cmd.Parameters.AddWithValue("@en",nuevo.Estado);cmd.Parameters.AddWithValue("@u",actor.UsuarioId);cmd.Parameters.AddWithValue("@o",(object)observacion??DBNull.Value);cmd.Parameters.AddWithValue("@key",key);cmd.Parameters.AddWithValue("@evento",evento);cmd.ExecuteNonQuery();}
        }

        private static void EncolarAUsuariosRol(NpgsqlConnection cn,NpgsqlTransaction tx,string[] roles,int solicitudId,string key,string tipo,string titulo,string baseUrl,string ruta)
        {
            const string users=@"SELECT DISTINCT u.idusuario,TRIM(u.correo) correo FROM public.usuario u JOIN public.usuario_rol ur ON u.codigousuario::text=ur.codigousuario::text JOIN public.rol r ON r.codigorol=ur.codigorol WHERE regexp_replace(UPPER(TRIM(COALESCE(r.descripcion,''))),'[^A-Z0-9]+','_','g')=ANY(@roles) AND COALESCE(ur.activo,TRUE) AND COALESCE(r.activo,TRUE) AND COALESCE(u.estadoactividad::text,'1')='1';";
            var recipients=new List<Recipient>();using(var cmd=new NpgsqlCommand(users,cn,tx)){cmd.Parameters.AddWithValue("@roles",roles);using(var rd=cmd.ExecuteReader())while(rd.Read()){var mail=S(rd,"correo");if(EmailValido(mail))recipients.Add(new Recipient{Id=I(rd,"idusuario"),Email=mail});}}
            if(recipients.Count==0)throw new InvalidOperationException("No existen destinatarios institucionales activos para la transición.");
            foreach(var r in recipients.GroupBy(x=>x.Id).Select(x=>x.First()))
            {
                var eventKey=key+":"+r.Id;var url=CombinarUrl(baseUrl,ruta);
                const string notif=@"INSERT INTO public.aocr_tbnotificacion(codigousuario,titulo,mensaje,tipo,url,leida,fechacreacion,modulo,entidad_id,tipo_entidad,event_key,correlation_id,updated_at) VALUES(@u,@t,@m,@tipo,@url,FALSE,NOW(),'AC11',@s,'SolicitudAOCR',@key,@corr,NOW()) ON CONFLICT(event_key) WHERE event_key IS NOT NULL DO NOTHING;";
                using(var cmd=new NpgsqlCommand(notif,cn,tx)){cmd.Parameters.AddWithValue("@u",r.Id);cmd.Parameters.AddWithValue("@t",titulo);cmd.Parameters.AddWithValue("@m",titulo+". Solicitud "+solicitudId+".");cmd.Parameters.AddWithValue("@tipo",tipo);cmd.Parameters.AddWithValue("@url",url);cmd.Parameters.AddWithValue("@s",solicitudId);cmd.Parameters.AddWithValue("@key",eventKey+":NOTIF");cmd.Parameters.AddWithValue("@corr",Correlation(key));cmd.ExecuteNonQuery();}
                const string email=@"INSERT INTO public.email_queue(to_address,subject,body,status,solicitud_id,created_at,proximo_intento,event_key,intentos,updated_at,tipo_notificacion,correlation_id) VALUES(@to,@sub,@body,'PENDIENTE',@s,NOW(),NOW(),@key,0,NOW(),@tipo,@corr) ON CONFLICT(event_key) WHERE event_key IS NOT NULL DO NOTHING;";
                using(var cmd=new NpgsqlCommand(email,cn,tx)){cmd.Parameters.AddWithValue("@to",r.Email);cmd.Parameters.AddWithValue("@sub",titulo);cmd.Parameters.AddWithValue("@body","<p>"+System.Net.WebUtility.HtmlEncode(titulo)+"</p><p><a href=\""+System.Net.WebUtility.HtmlEncode(url)+"\">Abrir bandeja autenticada</a></p>");cmd.Parameters.AddWithValue("@s",solicitudId);cmd.Parameters.AddWithValue("@key",eventKey+":EMAIL");cmd.Parameters.AddWithValue("@tipo",tipo);cmd.Parameters.AddWithValue("@corr",Correlation(key));cmd.ExecuteNonQuery();}
            }
        }

        private static AocrWorkflowResult ResultadoIdempotente(NpgsqlConnection cn,NpgsqlTransaction tx,string key,Proceso actual)
        { using(var cmd=new NpgsqlCommand("SELECT estado_anterior,estado_nuevo,correlation_id,documento_id FROM public.aocr_evento_workflow WHERE event_key=@key LIMIT 1;",cn,tx)){cmd.Parameters.AddWithValue("@key",key);using(var rd=cmd.ExecuteReader()){if(!rd.Read())return null;return new AocrWorkflowResult{Exito=true,Idempotente=true,HttpStatusCode=200,Codigo="IDEMPOTENTE",Mensaje="La operación ya fue procesada; no se generaron duplicados.",EstadoAnterior=S(rd,"estado_anterior"),EstadoNuevo=S(rd,"estado_nuevo"),VersionAnterior=actual.Version,VersionNueva=actual.Version,DocumentoId=NI(rd,"documento_id"),CorrelationId=S(rd,"correlation_id")};}} }
        private static AocrWorkflowResult ConflictoVersion(Proceso p){var r=AocrWorkflowResult.Error(409,"VERSION_DESACTUALIZADA","El expediente cambió; recargue la bandeja.");r.VersionNueva=p.Version;r.EstadoNuevo=p.Estado;return r;}
        private static AocrWorkflowResult Exito(string codigo,string mensaje,Proceso a,Proceso n,int? doc,string key){return new AocrWorkflowResult{Exito=true,HttpStatusCode=200,Codigo=codigo,Mensaje=mensaje,EstadoAnterior=a.Estado,EstadoNuevo=n.Estado,VersionAnterior=a.Version,VersionNueva=n.Version,DocumentoId=doc,CorrelationId=Correlation(key)};}
        private static string Clave(string supplied,int solicitud,string operacion,long version,int actor){return string.IsNullOrWhiteSpace(supplied)?solicitud+":"+operacion+":"+version+":"+actor:supplied.Trim();}
        private static string Correlation(string key){return "AC11-"+Math.Abs((key??string.Empty).GetHashCode()).ToString("X8");}
        private static bool EsEstado(string actual,params string[] estados){return estados.Any(e=>string.Equals(actual,e,StringComparison.OrdinalIgnoreCase));}
        private static string CombinarUrl(string b,string r){return string.IsNullOrWhiteSpace(b)?r:b.TrimEnd('/')+"/"+r.TrimStart('/');}
        private static bool EmailValido(string value){try{return !string.IsNullOrWhiteSpace(value)&&string.Equals(new MailAddress(value.Trim()).Address,value.Trim(),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        private static BandejaAocrDirdacItemViewModel MapBandeja(NpgsqlDataReader rd){var estado=S(rd,"estado_actual");return new BandejaAocrDirdacItemViewModel{SolicitudId=I(rd,"codigo_solicitud"),NumeroSolicitud=S(rd,"numero_solicitud"),Compania=S(rd,"compania"),DocumentoId=I(rd,"codigo_documento"),Documento="AOCR",VersionDocumento=I(rd,"version_documento"),VersionExpediente=L(rd,"version_expediente"),FechaRemision=D(rd,"fecha_estado"),UsuarioRemitente=S(rd,"usuario_remitente"),Estado=estado,MinutosPendiente=I(rd,"minutos_pendiente"),PuedeDevolver=EsEstado(estado,AocrEstadosProceso.AocrPendienteDirdac),PuedeFirmar=EsEstado(estado,AocrEstadosProceso.AocrPendienteDirdac),HashDocumento=S(rd,"hash_pdf")};}
        private static string S(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?null:Convert.ToString(r[n]);} private static int I(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt32(r[n]);} private static int? NI(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?(int?)null:Convert.ToInt32(r[n]);} private static long L(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt64(r[n]);} private static bool B(NpgsqlDataReader r,string n){return r[n]!=DBNull.Value&&Convert.ToBoolean(r[n]);} private static DateTime D(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?DateTime.MinValue:Convert.ToDateTime(r[n]);}
        private sealed class Proceso{public int Id;public int SolicitudId;public int InspeccionId;public string Estado;public long Version;} private sealed class Documento{public int Id;public int Version;public bool Vigente;public string Estado;public string Ruta;public string Hash;public string RutaFirmada;public string HashFirmado;public string RolFirma;} private sealed class Recipient{public int Id;public string Email;}
    }
}
