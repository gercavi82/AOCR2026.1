using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using CapaDatos.Constants;
using CapaDatos.Interfaces;
using CapaDatos.Services;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    /// <summary>Persistencia autoritativa AC-12. No realiza SMTP.</summary>
    public sealed class EntregaFinalDAO : IEntregaFinalRepository
    {
        private const string TipoAocr = "RECONOCIMIENTO";
        private const string TipoCl = "CONDICIONES_LIMITACIONES";
        private readonly string _connectionString;

        public EntregaFinalDAO()
        {
            var configured = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = configured != null && !string.IsNullOrWhiteSpace(configured.ConnectionString)
                ? configured.ConnectionString : ConexionDAO.CadenaConexion;
        }

        public EntregaFinalDAO(string connectionString) { _connectionString = connectionString; }

        public EntregaFinalResult Solicitar(SolicitarEntregaFinalRequest request)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        Lock(cn, tx, request.SolicitudId);
                        var proceso = CargarProceso(cn, tx, request.SolicitudId);
                        if (proceso == null) return Rollback(tx, 404, "EXPEDIENTE_NO_EXISTE", "El expediente no existe.");
                        if (proceso.Version != request.VersionExpedienteEsperada)
                            return Rollback(tx, 409, "VERSION_DESACTUALIZADA", "La versión del expediente cambió.");
                        if (!EsEstado(proceso.Estado, AocrEstadosProceso.FirmasCompletas, AocrEstadosProceso.ListoParaEntrega, AocrEstadosProceso.Entregado))
                            return Rollback(tx, 409, "FIRMAS_INCOMPLETAS", "El expediente no está en FIRMAS_COMPLETAS.");

                        var expediente = CargarExpediente(cn, tx, request.SolicitudId);
                        if (expediente == null) return Rollback(tx, 404, "EXPEDIENTE_NO_EXISTE", "No existe la solicitud o inspección asociada.");
                        var aocr = CargarDocumento(cn, tx, request.SolicitudId, TipoAocr);
                        var cl = CargarDocumento(cn, tx, request.SolicitudId, TipoCl);
                        var validacion = ValidarDocumentos(aocr, cl);
                        if (validacion != null) return Rollback(tx, 409, validacion.Item1, validacion.Item2);

                        var existente = CargarEntrega(cn, tx, request.SolicitudId, aocr.Version, cl.Version);
                        if (existente != null)
                        {
                            tx.Commit();
                            return Resultado(true, "IDEMPOTENTE", "La entrega final ya fue solicitada para estas versiones.", existente.Estado,
                                proceso.Estado, proceso.Version, existente.Id, existente.CorrelationId);
                        }

                        var rt = CargarUsuario(cn, tx, expediente.RtUsuarioId, "RT");
                        var inspector = CargarUsuario(cn, tx, expediente.InspectorUsuarioId, "INSPECTOR");
                        if (rt == null || inspector == null)
                            return Rollback(tx, 409, "DESTINATARIOS_INVALIDOS", "RT e Inspector deben estar activos, relacionados y tener correo institucional válido.");

                        string errorArchivo;
                        if (!ValidarArchivo(aocr, out errorArchivo) || !ValidarArchivo(cl, out errorArchivo))
                            return Rollback(tx, 409, "ARCHIVO_INVALIDO", errorArchivo);

                        var correlationId = "ENTREGA-FINAL-" + request.SolicitudId + "-" + aocr.Version + "-" + cl.Version;
                        var key = Clave(request.IdempotencyKey, request.SolicitudId, aocr.Version, cl.Version);
                        var entregaId = InsertarEntrega(cn, tx, expediente, aocr, cl, correlationId, key, request.Actor);
                        InsertarDocumento(cn, tx, entregaId, aocr);
                        InsertarDocumento(cn, tx, entregaId, cl);

                        var emailIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        CrearDestinatario(cn, tx, entregaId, expediente, rt, "RT", aocr, cl, request, correlationId, emailIds);
                        CrearDestinatario(cn, tx, entregaId, expediente, inspector, "INSPECTOR", aocr, cl, request, correlationId, emailIds);

                        var versionNueva = proceso.Version;
                        var estadoExpediente = proceso.Estado;
                        if (EsEstado(proceso.Estado, AocrEstadosProceso.FirmasCompletas))
                        {
                            versionNueva = CambiarProceso(cn, tx, proceso, AocrEstadosProceso.ListoParaEntrega, request.Actor);
                            estadoExpediente = AocrEstadosProceso.ListoParaEntrega;
                        }
                        RegistrarTrazabilidad(cn, tx, key, "ENTREGA_FINAL_ENCOLADA", request.SolicitudId, proceso.Estado,
                            estadoExpediente, request.Actor, correlationId, aocr.Id, aocr.Version, aocr.HashFirmado);
                        tx.Commit();
                        return Resultado(false, "ENTREGA_ENCOLADA", "La entrega final quedó disponible y sus correos fueron encolados.",
                            EstadosEntregaFinal.Encolada, estadoExpediente, versionNueva, entregaId, correlationId);
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public IList<DocumentoFinalDisponibleViewModel> ListarDocumentos(EntregaFinalActor actor)
        {
            var result = new List<DocumentoFinalDisponibleViewModel>();
            const string sql = @"SELECT e.id entrega_id,e.solicitud_id,s.numero_solicitud,e.compania,d.documento_id,d.tipo_documento,
d.nombre_archivo,d.version_documento,d.nombre_firmante,d.rol_firma,d.fecha_firma,e.estado estado_entrega,
r.estado_correo,r.tipo_destinatario
FROM public.aocr_entrega_final e
JOIN public.aocr_entrega_destinatario r ON r.entrega_id=e.id
JOIN public.aocr_entrega_documento d ON d.entrega_id=e.id
JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=e.solicitud_id
WHERE r.usuario_id=@usuario AND r.estado_bandeja='DISPONIBLE'
  AND ((@es_rt AND r.tipo_destinatario='RT' AND (@compania='' OR UPPER(e.codigo_compania)=UPPER(@compania)))
    OR (@es_inspector AND r.tipo_destinatario='INSPECTOR'))
ORDER BY e.created_at DESC,d.tipo_documento;";
            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@usuario", actor.UsuarioId);
                cmd.Parameters.AddWithValue("@es_rt", AocrRolesInstitucionales.EsRt(actor.RolActivo));
                cmd.Parameters.AddWithValue("@es_inspector", AocrRolesInstitucionales.EsInspector(actor.RolActivo));
                cmd.Parameters.AddWithValue("@compania", (actor.CompaniaCodigo ?? string.Empty).Trim());
                cn.Open();
                using (var rd = cmd.ExecuteReader()) while (rd.Read()) result.Add(MapDocumento(rd));
            }
            return result;
        }

        public DescargaFinalAutorizada AutorizarDescarga(int documentoId, EntregaFinalActor actor)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();
                const string sql = @"SELECT d.documento_id,d.ruta_fisica,d.nombre_archivo,d.hash_sha256,d.tamanio,d.mime_type,
e.solicitud_id,e.codigo_compania,
EXISTS(SELECT 1 FROM public.aocr_entrega_destinatario r WHERE r.entrega_id=e.id AND r.usuario_id=@usuario
 AND r.estado_bandeja='DISPONIBLE' AND ((r.tipo_destinatario='RT' AND @es_rt AND (@compania='' OR UPPER(e.codigo_compania)=UPPER(@compania)))
 OR (r.tipo_destinatario='INSPECTOR' AND @es_inspector))) autorizado
FROM public.aocr_entrega_documento d JOIN public.aocr_entrega_final e ON e.id=d.entrega_id
WHERE d.documento_id=@documento AND d.vigente=TRUE ORDER BY d.id DESC LIMIT 1;";
                DocumentoEntrega doc = null;
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@documento", documentoId); cmd.Parameters.AddWithValue("@usuario", actor.UsuarioId);
                    cmd.Parameters.AddWithValue("@es_rt", AocrRolesInstitucionales.EsRt(actor.RolActivo));
                    cmd.Parameters.AddWithValue("@es_inspector", AocrRolesInstitucionales.EsInspector(actor.RolActivo));
                    cmd.Parameters.AddWithValue("@compania", (actor.CompaniaCodigo ?? string.Empty).Trim());
                    using (var rd = cmd.ExecuteReader()) if (rd.Read()) doc = new DocumentoEntrega
                    {
                        Id=I(rd,"documento_id"),SolicitudId=I(rd,"solicitud_id"),Ruta=S(rd,"ruta_fisica"),Nombre=S(rd,"nombre_archivo"),
                        HashFirmado=S(rd,"hash_sha256"),Tamanio=L(rd,"tamanio"),Mime=S(rd,"mime_type"),Autorizado=B(rd,"autorizado")
                    };
                }
                if (doc == null) return ErrorDescarga(404, "DOCUMENTO_NO_EXISTE", "El documento no existe.");
                var institucional = actor.TienePermiso && (AocrRolesInstitucionales.EsCoordinador(actor.RolActivo)
                    || AocrRolesInstitucionales.EsDircav(actor.RolActivo) || AocrRolesInstitucionales.EsDirdac(actor.RolActivo));
                if (!doc.Autorizado && !institucional)
                {
                    AuditarDescarga(cn, doc.SolicitudId, documentoId, actor, "DENEGADA", "Propiedad o asignación no válida.");
                    return ErrorDescarga(404, "DOCUMENTO_NO_VISIBLE", "El documento no existe o no está disponible.");
                }
                string error;
                if (!ValidarArchivo(doc, out error))
                {
                    AuditarDescarga(cn, doc.SolicitudId, documentoId, actor, "FALLIDA", error);
                    return ErrorDescarga(409, "INTEGRIDAD_INVALIDA", "El documento ya no supera la validación de integridad.");
                }
                AuditarDescarga(cn, doc.SolicitudId, documentoId, actor, "AUTORIZADA", null);
                return new DescargaFinalAutorizada { Autorizada=true,HttpStatusCode=200,Codigo="AUTORIZADA",RutaFisica=ResolverRuta(doc.Ruta),
                    NombreArchivo=Path.GetFileName(doc.Nombre),MimeType="application/pdf",HashSha256=doc.HashFirmado,Tamanio=doc.Tamanio };
            }
        }

        public IList<EstadoEntregaFinalViewModel> ConsultarEstados(int? solicitudId)
        {
            var list = new List<EstadoEntregaFinalViewModel>();
            const string sql=@"SELECT e.id,e.solicitud_id,s.numero_solicitud,e.compania,e.version_aocr,e.version_cl,e.estado,e.correlation_id,
e.created_at,e.fecha_completada,COUNT(r.id) destinatarios,
COUNT(r.id) FILTER(WHERE r.estado_correo='ENVIADO') enviados,
COUNT(r.id) FILTER(WHERE r.estado_correo LIKE 'ERROR%') fallidos
FROM public.aocr_entrega_final e JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=e.solicitud_id
LEFT JOIN public.aocr_entrega_destinatario r ON r.entrega_id=e.id
WHERE (@solicitud IS NULL OR e.solicitud_id=@solicitud)
GROUP BY e.id,s.numero_solicitud ORDER BY e.created_at DESC LIMIT 200;";
            using(var cn=new NpgsqlConnection(_connectionString))using(var cmd=new NpgsqlCommand(sql,cn))
            {cmd.Parameters.AddWithValue("@solicitud",(object)solicitudId??DBNull.Value);cn.Open();using(var rd=cmd.ExecuteReader())while(rd.Read())list.Add(new EstadoEntregaFinalViewModel{
                EntregaId=L(rd,"id"),SolicitudId=I(rd,"solicitud_id"),NumeroSolicitud=S(rd,"numero_solicitud"),Compania=S(rd,"compania"),
                VersionAocr=I(rd,"version_aocr"),VersionCl=I(rd,"version_cl"),Estado=S(rd,"estado"),CorrelationId=S(rd,"correlation_id"),
                FechaCreacion=D(rd,"created_at"),FechaCompletada=ND(rd,"fecha_completada"),Destinatarios=I(rd,"destinatarios"),CorreosEnviados=I(rd,"enviados"),CorreosFallidos=I(rd,"fallidos")});}
            return list;
        }

        public void ActualizarDesdeCola(int emailQueueId, string estadoCola, string messageId, string error)
        {
            using(var cn=new NpgsqlConnection(_connectionString)){cn.Open();using(var tx=cn.BeginTransaction())
            {try
                {
                    const string linked="SELECT entrega_id FROM public.aocr_entrega_destinatario WHERE email_queue_id=@id LIMIT 1 FOR UPDATE;";
                    long entregaId;using(var cmd=new NpgsqlCommand(linked,cn,tx)){cmd.Parameters.AddWithValue("@id",emailQueueId);var v=cmd.ExecuteScalar();if(v==null){tx.Commit();return;}entregaId=Convert.ToInt64(v);}
                    var normal=(estadoCola??string.Empty).Trim().ToUpperInvariant();
                    var estadoCorreo=normal=="ENVIADO"?"ENVIADO":normal=="PENDIENTE"?"REINTENTO_PENDIENTE":normal.StartsWith("ERROR")?normal:"EN_PROCESO";
                    using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_entrega_destinatario SET estado_correo=@estado,message_id=COALESCE(NULLIF(@message,''),message_id),
ultimo_error=@error,fecha_envio=CASE WHEN @estado='ENVIADO' THEN NOW() ELSE fecha_envio END,updated_at=NOW() WHERE email_queue_id=@id;",cn,tx))
                    {cmd.Parameters.AddWithValue("@estado",estadoCorreo);cmd.Parameters.AddWithValue("@message",messageId??string.Empty);cmd.Parameters.AddWithValue("@error",(object)error??DBNull.Value);cmd.Parameters.AddWithValue("@id",emailQueueId);cmd.ExecuteNonQuery();}
                    using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_entrega_intento(entrega_id,email_queue_id,estado,detalle_error,message_id,fecha)
VALUES(@entrega,@queue,@estado,@error,@message,NOW());",cn,tx)){cmd.Parameters.AddWithValue("@entrega",entregaId);cmd.Parameters.AddWithValue("@queue",emailQueueId);cmd.Parameters.AddWithValue("@estado",estadoCorreo);cmd.Parameters.AddWithValue("@error",(object)error??DBNull.Value);cmd.Parameters.AddWithValue("@message",(object)messageId??DBNull.Value);cmd.ExecuteNonQuery();}
                    RecalcularEstado(cn,tx,entregaId);tx.Commit();
                }catch{try{tx.Rollback();}catch{}throw;}}}
        }

        private static Tuple<string,string> ValidarDocumentos(DocumentoEntrega aocr, DocumentoEntrega cl)
        {
            if(aocr==null)return Tuple.Create("AOCR_FIRMADA_FALTANTE","No existe AOCR vigente firmado por DIRDAC.");
            if(cl==null)return Tuple.Create("CL_FIRMADA_FALTANTE","No existe CL vigente firmada por DIRCAV.");
            if(aocr.Version!=cl.Version)return Tuple.Create("VERSIONES_INCOMPATIBLES","Las versiones de AOCR y CL no son compatibles.");
            if(!aocr.Vigente||!cl.Vigente||string.IsNullOrWhiteSpace(aocr.HashFirmado)||string.IsNullOrWhiteSpace(cl.HashFirmado))return Tuple.Create("FIRMAS_INCOMPLETAS","Los documentos vigentes no contienen ambas firmas.");
            if(!EsEstado(aocr.Estado,AocrEstadosProceso.AocrFirmadaDirdac,AocrEstadosProceso.AocrFirmadoDirdac)
                || !EsEstado(cl.Estado,AocrEstadosProceso.ClFirmadaDircav,AocrEstadosProceso.CondicionesFirmadasDcav))
                return Tuple.Create("ESTADO_DOCUMENTAL_INVALIDO","AOCR y CL no están en sus estados de firma institucional.");
            if(!AocrRolesInstitucionales.EsDirdac(aocr.RolFirma)||!AocrRolesInstitucionales.EsDircav(cl.RolFirma))return Tuple.Create("FIRMANTE_INVALIDO","Las firmas no corresponden a DIRDAC y DIRCAV.");
            return null;
        }

        private static bool ValidarArchivo(DocumentoEntrega doc,out string error)
        {
            error=null;try{var path=ResolverRuta(doc.Ruta);if(string.IsNullOrWhiteSpace(path)||!RutaControlada(path)||!File.Exists(path)){error="No existe uno de los PDF firmados en el almacenamiento autorizado.";return false;}
                if(!string.Equals(Path.GetExtension(path),".pdf",StringComparison.OrdinalIgnoreCase)){error="El documento final no es PDF.";return false;}
                var info=new FileInfo(path);if(info.Length<=4||doc.Tamanio>0&&info.Length!=doc.Tamanio){error="El tamaño del PDF no coincide.";return false;}
                using(var fs=File.OpenRead(path)){var sig=new byte[5];if(fs.Read(sig,0,5)!=5||System.Text.Encoding.ASCII.GetString(sig)!="%PDF-"){error="El archivo no contiene una firma PDF válida.";return false;}fs.Position=0;using(var sha=SHA256.Create()){var hash=BitConverter.ToString(sha.ComputeHash(fs)).Replace("-","");if(!string.Equals(hash,doc.HashFirmado,StringComparison.OrdinalIgnoreCase)){error="El hash SHA-256 del PDF no coincide.";return false;}}}return true;
            }catch{error="No fue posible validar el archivo final.";return false;}}

        private static string ResolverRuta(string value){if(string.IsNullOrWhiteSpace(value))return null;var p=value.Trim();if(Path.IsPathRooted(p))return Path.GetFullPath(p);p=p.TrimStart('~','/','\\').Replace('/',Path.DirectorySeparatorChar);return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,p));}
        private static bool RutaControlada(string path){var n=(Path.GetFullPath(path)??string.Empty).Replace('\\','/');return n.IndexOf("/App_Data/Uploads/AOCR/Firmados/",StringComparison.OrdinalIgnoreCase)>=0;}

        private static void CrearDestinatario(NpgsqlConnection cn,NpgsqlTransaction tx,long entregaId,Expediente exp,UsuarioEntrega user,string tipo,DocumentoEntrega aocr,DocumentoEntrega cl,SolicitarEntregaFinalRequest req,string corr,IDictionary<string,int> emails)
        {
            int queueId;var email=user.Email.Trim().ToLowerInvariant();if(!emails.TryGetValue(email,out queueId))
            {var maxBytes=ObtenerLimiteAdjuntos();var adjuntos=new List<EmailAttachmentItem>();if(aocr.Tamanio+cl.Tamanio<=maxBytes){adjuntos.Add(Adjunto(aocr));adjuntos.Add(Adjunto(cl));}
                var enlace=(req.BaseUrl??string.Empty).TrimEnd('/')+"/"+(tipo=="RT"?"Rt":"Inspeccion")+"/DocumentosFinales";
                var body="<p>Los documentos finales de la solicitud <strong>"+Html(exp.NumeroSolicitud)+"</strong>, compañía <strong>"+Html(exp.Compania)+"</strong>, están disponibles.</p><p><a href=\""+Html(enlace)+"\">Consultar documentos finales</a></p>";
                var item=new EmailQueueItem{Para=user.Email,ParaNombre=user.Nombre,Asunto="Sistema AOCR - documentos finales disponibles",Cuerpo=body,Estado="PENDIENTE",SolicitudId=exp.SolicitudId,
                    EventKey="ENTREGA_FINAL:"+exp.SolicitudId+":"+aocr.Version+":"+cl.Version+":EMAIL:"+Sha(email).Substring(0,16),TipoNotificacion="ENTREGA_FINAL",CorrelationId=corr,EsHtml=true,MaxIntentos=3};
                bool duplicate;queueId=new EmailQueueService().EncolarConAdjuntosEnTransaccion(cn,tx,item,adjuntos,out duplicate);emails[email]=queueId;}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_entrega_destinatario(entrega_id,tipo_destinatario,usuario_id,correo,email_queue_id,estado_bandeja,estado_correo,created_at,updated_at)
VALUES(@entrega,@tipo,@usuario,@correo,@queue,'DISPONIBLE','ENCOLADO',NOW(),NOW()) ON CONFLICT(entrega_id,tipo_destinatario,usuario_id) DO NOTHING;",cn,tx))
            {cmd.Parameters.AddWithValue("@entrega",entregaId);cmd.Parameters.AddWithValue("@tipo",tipo);cmd.Parameters.AddWithValue("@usuario",user.Id);cmd.Parameters.AddWithValue("@correo",user.Email);cmd.Parameters.AddWithValue("@queue",queueId);cmd.ExecuteNonQuery();}
        }

        private static EmailAttachmentItem Adjunto(DocumentoEntrega d){return new EmailAttachmentItem{FileName=Path.GetFileName(d.Nombre),ContentType="application/pdf",FilePath=d.Ruta,FileSize=d.Tamanio,Sha256=d.HashFirmado};}
        private static long ObtenerLimiteAdjuntos(){long n;return long.TryParse(ConfigurationManager.AppSettings["EntregaFinalMaxAdjuntosBytes"],out n)&&n>0?n:20L*1024*1024;}
        private static long InsertarEntrega(NpgsqlConnection cn,NpgsqlTransaction tx,Expediente e,DocumentoEntrega a,DocumentoEntrega c,string corr,string key,EntregaFinalActor actor)
        {using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_entrega_final(solicitud_id,inspeccion_id,codigo_compania,compania,version_aocr,version_cl,estado,correlation_id,event_key,created_at,created_by,updated_at)
VALUES(@s,@i,@cc,@comp,@va,@vc,@estado,@corr,@key,NOW(),@user,NOW()) RETURNING id;",cn,tx)){cmd.Parameters.AddWithValue("@s",e.SolicitudId);cmd.Parameters.AddWithValue("@i",e.InspeccionId);cmd.Parameters.AddWithValue("@cc",e.CodigoCompania);cmd.Parameters.AddWithValue("@comp",e.Compania);cmd.Parameters.AddWithValue("@va",a.Version);cmd.Parameters.AddWithValue("@vc",c.Version);cmd.Parameters.AddWithValue("@estado",EstadosEntregaFinal.Encolada);cmd.Parameters.AddWithValue("@corr",corr);cmd.Parameters.AddWithValue("@key",key);cmd.Parameters.AddWithValue("@user",actor.UsuarioId);return Convert.ToInt64(cmd.ExecuteScalar());}}
        private static void InsertarDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,long entrega,DocumentoEntrega d){using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_entrega_documento(entrega_id,documento_id,tipo_documento,version_documento,nombre_archivo,ruta_fisica,hash_sha256,tamanio,mime_type,nombre_firmante,rol_firma,fecha_firma,vigente,created_at)
VALUES(@e,@d,@t,@v,@n,@r,@h,@b,'application/pdf',@f,@rol,@fecha,TRUE,NOW());",cn,tx)){cmd.Parameters.AddWithValue("@e",entrega);cmd.Parameters.AddWithValue("@d",d.Id);cmd.Parameters.AddWithValue("@t",d.Tipo);cmd.Parameters.AddWithValue("@v",d.Version);cmd.Parameters.AddWithValue("@n",Path.GetFileName(d.Nombre));cmd.Parameters.AddWithValue("@r",d.Ruta);cmd.Parameters.AddWithValue("@h",d.HashFirmado);cmd.Parameters.AddWithValue("@b",d.Tamanio);cmd.Parameters.AddWithValue("@f",(object)d.Firmante??DBNull.Value);cmd.Parameters.AddWithValue("@rol",d.RolFirma);cmd.Parameters.AddWithValue("@fecha",(object)d.FechaFirma??DBNull.Value);cmd.ExecuteNonQuery();}}

        private static Proceso CargarProceso(NpgsqlConnection cn,NpgsqlTransaction tx,int id){using(var cmd=new NpgsqlCommand("SELECT id,estado_actual,version,COALESCE(inspeccion_id,0) inspeccion_id FROM public.aocr_proceso_estado WHERE solicitud_id=@id AND activo=TRUE ORDER BY id DESC LIMIT 1 FOR UPDATE;",cn,tx)){cmd.Parameters.AddWithValue("@id",id);using(var r=cmd.ExecuteReader())return r.Read()?new Proceso{Id=I(r,"id"),SolicitudId=id,InspeccionId=I(r,"inspeccion_id"),Estado=S(r,"estado_actual"),Version=L(r,"version")}:null;}}
        private static Expediente CargarExpediente(NpgsqlConnection cn,NpgsqlTransaction tx,int id){const string sql=@"SELECT s.codigo_solicitud,COALESCE(NULLIF(s.numero_solicitud,''),s.codigo_solicitud::text) numero,
COALESCE(NULLIF(s.razon_social,''),NULLIF(s.nombre_operador,''),'No registrada') compania,
COALESCE(NULLIF(s.codigo_oaci,''),NULLIF(s.ruc,''),s.codigo_solicitud::text) codigo_compania,s.codigo_usuario,
i.codigo_inspeccion,COALESCE(i.codigo_inspector,0) codigo_inspector FROM public.aocr_tbsolicitud s
JOIN LATERAL(SELECT x.codigo_inspeccion,x.codigo_inspector FROM public.aocr_tbinspeccion x WHERE x.codigo_solicitud=s.codigo_solicitud ORDER BY x.codigo_inspeccion DESC LIMIT 1)i ON TRUE
WHERE s.codigo_solicitud=@id AND s.deleted_at IS NULL FOR UPDATE OF s;";using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@id",id);using(var r=cmd.ExecuteReader())return r.Read()?new Expediente{SolicitudId=id,NumeroSolicitud=S(r,"numero"),Compania=S(r,"compania"),CodigoCompania=S(r,"codigo_compania"),RtUsuarioId=I(r,"codigo_usuario"),InspeccionId=I(r,"codigo_inspeccion"),InspectorUsuarioId=I(r,"codigo_inspector")}:null;}}
        private static DocumentoEntrega CargarDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,int id,string tipo){const string sql=@"SELECT d.codigo_documento,d.codigo_solicitud,d.tipo_documento,d.version_documento,d.vigente,d.estado,d.nombre_archivo,d.ruta_pdf_firmado,d.hash_pdf_firmado,d.tamanio_pdf_firmado,d.rol_firma,d.fecha_firma,
COALESCE(f.nombre_firmante,d.usuario_nombre) firmante FROM public.aocr_tbdocumento_generado d LEFT JOIN LATERAL(SELECT x.nombre_firmante FROM public.aocr_tbfirma_documento x WHERE x.codigo_solicitud=d.codigo_solicitud AND UPPER(x.tipo_documento)=UPPER(d.tipo_documento) AND x.version=d.version_documento ORDER BY x.codigo_firma DESC LIMIT 1)f ON TRUE
WHERE d.codigo_solicitud=@id AND UPPER(d.tipo_documento)=@tipo AND d.vigente=TRUE ORDER BY d.version_documento DESC,d.codigo_documento DESC LIMIT 1 FOR UPDATE OF d;";using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@tipo",tipo);using(var r=cmd.ExecuteReader())return r.Read()?new DocumentoEntrega{Id=I(r,"codigo_documento"),SolicitudId=id,Tipo=S(r,"tipo_documento"),Version=I(r,"version_documento"),Vigente=B(r,"vigente"),Estado=S(r,"estado"),Nombre=S(r,"nombre_archivo"),Ruta=S(r,"ruta_pdf_firmado"),HashFirmado=S(r,"hash_pdf_firmado"),Tamanio=L(r,"tamanio_pdf_firmado"),RolFirma=S(r,"rol_firma"),FechaFirma=ND(r,"fecha_firma"),Firmante=S(r,"firmante"),Mime="application/pdf"}:null;}}
        private static UsuarioEntrega CargarUsuario(NpgsqlConnection cn,NpgsqlTransaction tx,int id,string tipo){using(var cmd=new NpgsqlCommand("SELECT idusuario,TRIM(correo) correo,TRIM(COALESCE(nombreusuario,'')||' '||COALESCE(apellidousuario,'')) nombre FROM public.usuario WHERE idusuario=@id AND COALESCE(estadoactividad::text,'1')='1' LIMIT 1;",cn,tx)){cmd.Parameters.AddWithValue("@id",id);using(var r=cmd.ExecuteReader()){if(!r.Read())return null;var email=S(r,"correo");return EmailValido(email)?new UsuarioEntrega{Id=id,Email=email,Nombre=S(r,"nombre"),Tipo=tipo}:null;}}}
        private static Entrega CargarEntrega(NpgsqlConnection cn,NpgsqlTransaction tx,int id,int va,int vc){using(var cmd=new NpgsqlCommand("SELECT id,estado,correlation_id FROM public.aocr_entrega_final WHERE solicitud_id=@s AND version_aocr=@va AND version_cl=@vc LIMIT 1 FOR UPDATE;",cn,tx)){cmd.Parameters.AddWithValue("@s",id);cmd.Parameters.AddWithValue("@va",va);cmd.Parameters.AddWithValue("@vc",vc);using(var r=cmd.ExecuteReader())return r.Read()?new Entrega{Id=L(r,"id"),Estado=S(r,"estado"),CorrelationId=S(r,"correlation_id")}:null;}}
        private static long CambiarProceso(NpgsqlConnection cn,NpgsqlTransaction tx,Proceso p,string estado,EntregaFinalActor a){using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_proceso_estado SET activo=FALSE,updated_at=NOW(),updated_by=@u WHERE id=@id AND activo=TRUE AND version=@v;
INSERT INTO public.aocr_proceso_estado(solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,observacion,activo,version,fecha_estado,created_at,created_by,updated_at,updated_by)
VALUES(@s,NULLIF(@i,0),@e,'ENTREGA_FINAL','RT_INSPECTOR','Documentos disponibles; correo procesado por outbox.',TRUE,@n,NOW(),NOW(),@u,NOW(),@u);",cn,tx)){cmd.Parameters.AddWithValue("@u",a.UsuarioId);cmd.Parameters.AddWithValue("@id",p.Id);cmd.Parameters.AddWithValue("@v",p.Version);cmd.Parameters.AddWithValue("@s",p.SolicitudId);cmd.Parameters.AddWithValue("@i",p.InspeccionId);cmd.Parameters.AddWithValue("@e",estado);cmd.Parameters.AddWithValue("@n",p.Version+1);cmd.ExecuteNonQuery();return p.Version+1;}}
        private static void RegistrarTrazabilidad(NpgsqlConnection cn,NpgsqlTransaction tx,string key,string evento,int solicitud,string anterior,string nuevo,EntregaFinalActor a,string corr,int documento,int version,string hash){using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_evento_workflow(evento,event_key,correlation_id,modulo,accion,entidad,entidad_id,solicitud_id,documento_id,estado_anterior,estado_nuevo,usuario_id,usuario,rol,ip,version,hash,resultado,intentos,fecha,created_at,updated_at)
VALUES(@ev,@key,@corr,'ENTREGA_FINAL',@ev,'aocr_entrega_final',@s,@s,@doc,@ant,@nuevo,@u,@un,@rol,@ip,@v,@hash,'REGISTRADO',1,NOW(),NOW(),NOW()) ON CONFLICT(event_key) DO NOTHING;",cn,tx)){cmd.Parameters.AddWithValue("@ev",evento);cmd.Parameters.AddWithValue("@key",key);cmd.Parameters.AddWithValue("@corr",corr);cmd.Parameters.AddWithValue("@s",solicitud);cmd.Parameters.AddWithValue("@doc",documento);cmd.Parameters.AddWithValue("@ant",anterior);cmd.Parameters.AddWithValue("@nuevo",nuevo);cmd.Parameters.AddWithValue("@u",a.UsuarioId);cmd.Parameters.AddWithValue("@un",a.UsuarioNombre??string.Empty);cmd.Parameters.AddWithValue("@rol",a.RolActivo??string.Empty);cmd.Parameters.AddWithValue("@ip",(object)a.Ip??DBNull.Value);cmd.Parameters.AddWithValue("@v",version);cmd.Parameters.AddWithValue("@hash",hash);cmd.ExecuteNonQuery();}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbhistorial_estado(codigo_solicitud,estado_anterior,estado_nuevo,observacion,codigo_usuario,fecha_cambio,created_at)
VALUES(@s,@a,@n,'Entrega final disponible para RT e Inspector.',@u,NOW(),NOW());",cn,tx)){cmd.Parameters.AddWithValue("@s",solicitud);cmd.Parameters.AddWithValue("@a",anterior);cmd.Parameters.AddWithValue("@n",nuevo);cmd.Parameters.AddWithValue("@u",a.UsuarioId);cmd.ExecuteNonQuery();}}
        private static void RecalcularEstado(NpgsqlConnection cn,NpgsqlTransaction tx,long entrega){int total,enviados,fallidos,reintentos;using(var cmd=new NpgsqlCommand(@"SELECT COUNT(*),COUNT(*)FILTER(WHERE estado_correo='ENVIADO'),COUNT(*)FILTER(WHERE estado_correo LIKE 'ERROR%'),COUNT(*)FILTER(WHERE estado_correo='REINTENTO_PENDIENTE') FROM public.aocr_entrega_destinatario WHERE entrega_id=@e;",cn,tx)){cmd.Parameters.AddWithValue("@e",entrega);using(var r=cmd.ExecuteReader()){r.Read();total=Convert.ToInt32(r.GetValue(0));enviados=Convert.ToInt32(r.GetValue(1));fallidos=Convert.ToInt32(r.GetValue(2));reintentos=Convert.ToInt32(r.GetValue(3));}}
            var estado=enviados==total&&total>0?EstadosEntregaFinal.Completa:enviados>0?EstadosEntregaFinal.Parcial:fallidos==total&&total>0?EstadosEntregaFinal.FallidaDefinitiva:reintentos>0?EstadosEntregaFinal.FallidaReintentable:EstadosEntregaFinal.EnProceso;
            using(var cmd=new NpgsqlCommand("UPDATE public.aocr_entrega_final SET estado=@s,fecha_completada=CASE WHEN @s='ENTREGA_COMPLETA' THEN NOW() ELSE fecha_completada END,updated_at=NOW() WHERE id=@e;",cn,tx)){cmd.Parameters.AddWithValue("@s",estado);cmd.Parameters.AddWithValue("@e",entrega);cmd.ExecuteNonQuery();}
            if(estado==EstadosEntregaFinal.Completa)using(var cmd=new NpgsqlCommand(@"WITH p AS(SELECT id,solicitud_id,inspeccion_id,version FROM public.aocr_proceso_estado WHERE solicitud_id=(SELECT solicitud_id FROM public.aocr_entrega_final WHERE id=@e) AND activo=TRUE AND estado_actual='LISTO_PARA_ENTREGA' FOR UPDATE),u AS(UPDATE public.aocr_proceso_estado x SET activo=FALSE,updated_at=NOW() FROM p WHERE x.id=p.id)
INSERT INTO public.aocr_proceso_estado(solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,observacion,activo,version,fecha_estado,created_at,updated_at)
SELECT solicitud_id,inspeccion_id,'ENTREGADO','ENTREGA_FINAL','RT_INSPECTOR','Correo confirmado para ambos destinatarios.',TRUE,version+1,NOW(),NOW(),NOW() FROM p;",cn,tx)){cmd.Parameters.AddWithValue("@e",entrega);cmd.ExecuteNonQuery();}}
        private static void AuditarDescarga(NpgsqlConnection cn,int solicitud,int doc,EntregaFinalActor a,string resultado,string obs){using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_evento_workflow(evento,event_key,correlation_id,modulo,accion,entidad,entidad_id,solicitud_id,documento_id,usuario_id,usuario,rol,ip,observacion,resultado,intentos,fecha,created_at,updated_at)
VALUES('DESCARGA_FINAL',@key,@corr,'ENTREGA_FINAL','DESCARGA_FINAL','aocr_entrega_documento',@doc,@s,@doc,@u,@un,@rol,@ip,@obs,@res,1,NOW(),NOW(),NOW());",cn)){cmd.Parameters.AddWithValue("@key","DESCARGA_FINAL:"+solicitud+":"+doc+":"+a.UsuarioId+":"+Guid.NewGuid().ToString("N"));cmd.Parameters.AddWithValue("@corr","DESCARGA-"+solicitud);cmd.Parameters.AddWithValue("@doc",doc);cmd.Parameters.AddWithValue("@s",solicitud);cmd.Parameters.AddWithValue("@u",a.UsuarioId);cmd.Parameters.AddWithValue("@un",a.UsuarioNombre??string.Empty);cmd.Parameters.AddWithValue("@rol",a.RolActivo??string.Empty);cmd.Parameters.AddWithValue("@ip",(object)a.Ip??DBNull.Value);cmd.Parameters.AddWithValue("@obs",(object)obs??DBNull.Value);cmd.Parameters.AddWithValue("@res",resultado);cmd.ExecuteNonQuery();}}

        private static EntregaFinalResult Rollback(NpgsqlTransaction tx,int status,string code,string msg){tx.Rollback();return EntregaFinalResult.Error(status,code,msg);}
        private static EntregaFinalResult Resultado(bool idem,string code,string msg,string entrega,string expediente,long version,long id,string corr){return new EntregaFinalResult{Exito=true,Idempotente=idem,HttpStatusCode=200,Codigo=code,Mensaje=msg,EstadoEntrega=entrega,EstadoExpediente=expediente,VersionExpediente=version,EntregaId=id,CorrelationId=corr};}
        private static DescargaFinalAutorizada ErrorDescarga(int status,string code,string msg){return new DescargaFinalAutorizada{HttpStatusCode=status,Codigo=code,Mensaje=msg};}
        private static DocumentoFinalDisponibleViewModel MapDocumento(NpgsqlDataReader r){return new DocumentoFinalDisponibleViewModel{EntregaId=L(r,"entrega_id"),SolicitudId=I(r,"solicitud_id"),NumeroSolicitud=S(r,"numero_solicitud"),Compania=S(r,"compania"),DocumentoId=I(r,"documento_id"),TipoDocumento=S(r,"tipo_documento"),NombreArchivo=S(r,"nombre_archivo"),Version=I(r,"version_documento"),Firmante=S(r,"nombre_firmante"),RolFirmante=S(r,"rol_firma"),FechaFirma=ND(r,"fecha_firma"),EstadoEntrega=S(r,"estado_entrega"),EstadoCorreo=S(r,"estado_correo"),TipoDestinatario=S(r,"tipo_destinatario")};}
        private static string Clave(string supplied,int s,int a,int c){return "ENTREGA_FINAL:"+s+":"+a+":"+c;}
        private static bool EmailValido(string e){if(string.IsNullOrWhiteSpace(e)||e.EndsWith("@invalid.local",StringComparison.OrdinalIgnoreCase))return false;try{return string.Equals(new MailAddress(e.Trim()).Address,e.Trim(),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        private static string Html(string v){return System.Net.WebUtility.HtmlEncode(v??string.Empty);}
        private static string Sha(string v){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(v))).Replace("-","");}
        private static bool EsEstado(string value,params string[] states){return states.Any(x=>string.Equals(value,x,StringComparison.OrdinalIgnoreCase));}
        private static void Lock(NpgsqlConnection c,NpgsqlTransaction t,int id){using(var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(@id::bigint);",c,t)){cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();}}
        private static string S(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?null:Convert.ToString(r[n]);} private static int I(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt32(r[n]);} private static long L(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt64(r[n]);} private static bool B(NpgsqlDataReader r,string n){return r[n]!=DBNull.Value&&Convert.ToBoolean(r[n]);} private static DateTime D(NpgsqlDataReader r,string n){return Convert.ToDateTime(r[n]);} private static DateTime? ND(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(r[n]);}
        private sealed class Proceso{public int Id;public int SolicitudId;public int InspeccionId;public string Estado;public long Version;} private sealed class Expediente{public int SolicitudId;public int InspeccionId;public int RtUsuarioId;public int InspectorUsuarioId;public string NumeroSolicitud;public string Compania;public string CodigoCompania;} private sealed class UsuarioEntrega{public int Id;public string Email;public string Nombre;public string Tipo;} private sealed class Entrega{public long Id;public string Estado;public string CorrelationId;}
        private sealed class DocumentoEntrega{public int Id;public int SolicitudId;public string Tipo;public int Version;public bool Vigente;public string Estado;public string Nombre;public string Ruta;public string HashFirmado;public long Tamanio;public string RolFirma;public DateTime? FechaFirma;public string Firmante;public string Mime;public bool Autorizado;}
    }
}
