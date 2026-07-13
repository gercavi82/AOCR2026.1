using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaModelo.Common;
using CapaNegocio.DTOs.DocumentosPdf;
using CapaNegocio.DTOs.EnvioDocumentosDcav;

namespace CapaNegocio.Services
{
    public interface IEnvioDocumentosDcavService
    {
        ResultadoEnvioDocumentosDcav FinalizarYEnviar(EnviarDocumentosDcavRequest request);
        ResultadoValidacionEnvioDcav ValidarEnvio(int solicitudId,int inspeccionId,int usuarioId);
    }

    public sealed class EnvioDocumentosDcavService : IEnvioDocumentosDcavService
    {
        private const string EstadoDestino = "PENDIENTE_REVISION_DOCUMENTOS_DCAV";
        private readonly EnvioDocumentosDcavDAO _dao;
        private readonly IDocumentoPdfService _pdf;
        private readonly IEmailQueueService _emailQueue;

        public EnvioDocumentosDcavService(string almacenamientoProtegido)
            : this(new EnvioDocumentosDcavDAO(),new DocumentoPdfService(almacenamientoProtegido),new EmailQueueService()) { }

        internal EnvioDocumentosDcavService(EnvioDocumentosDcavDAO dao,IDocumentoPdfService pdf,IEmailQueueService emailQueue)
        { _dao=dao??throw new ArgumentNullException("dao");_pdf=pdf??throw new ArgumentNullException("pdf");_emailQueue=emailQueue??throw new ArgumentNullException("emailQueue"); }

        public ResultadoEnvioDocumentosDcav FinalizarYEnviar(EnviarDocumentosDcavRequest request)
        {
            ValidarRequest(request);
            var correlation=string.IsNullOrWhiteSpace(request.CorrelationId)?Guid.NewGuid().ToString("N"):request.CorrelationId.Trim();
            Trace.TraceInformation("[INSPECTOR_DCAV][SEND_IN] SolicitudId="+request.SolicitudId+";InspeccionId="+request.InspeccionId+";Usuario="+request.UsuarioInspectorId+";CorrelationId="+correlation);
            EnvioDocumentosDcavSnapshot snapshot=null;DocumentoPdfDto aocrPdf=null;DocumentoPdfDto condicionesPdf=null;string clave=null;
            try
            {
                using(var cn=_dao.CrearConexion())
                {
                    cn.Open();
                    using(var tx=cn.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            _dao.BloquearOperacion(cn,tx,request.SolicitudId,request.InspeccionId);
                            snapshot=_dao.CargarParaActualizar(cn,tx,request.SolicitudId,request.InspeccionId);
                            if(snapshot==null)throw new EnvioDocumentosDcavException(404,"El expediente o la inspeccion no existen.");
                            Trace.TraceInformation("[INSPECTOR_DCAV][CONTEXT_OK] SolicitudId="+snapshot.SolicitudId+";Estado="+snapshot.EstadoCentral+";Version="+snapshot.VersionExpediente);
                            clave=CrearClaveIdempotencia(snapshot);
                            if(!string.IsNullOrWhiteSpace(request.ClaveIdempotencia)&&!string.Equals(request.ClaveIdempotencia,clave,StringComparison.Ordinal))
                                throw Conflicto();
                            var hit=_dao.ObtenerIdempotencia(cn,tx,clave);
                            if(hit!=null)
                            {
                                aocrPdf=_pdf.ObtenerVigente(snapshot.SolicitudId,snapshot.InspeccionId,"RECONOCIMIENTO");
                                condicionesPdf=_pdf.ObtenerVigente(snapshot.SolicitudId,snapshot.InspeccionId,"CONDICIONES_LIMITACIONES");
                                tx.Commit();Trace.TraceInformation("[IDEMPOTENCY][HIT] Clave="+clave);
                                return ResultadoHit(hit,aocrPdf,condicionesPdf);
                            }

                            ValidarConcurrencia(request,snapshot);
                            var validacion=ValidarSnapshot(snapshot,true);
                            if(!validacion.Valido){Trace.TraceWarning("[INSPECTOR_DCAV][WORKFLOW_VALIDATION_ERROR] "+validacion.Mensaje);throw new EnvioDocumentosDcavException(validacion.Codigo,validacion.Mensaje);}
                            Trace.TraceInformation("[INSPECTOR_DCAV][WORKFLOW_VALIDATION_OK] SolicitudId="+snapshot.SolicitudId);

                            Trace.TraceInformation("[INSPECTOR_DCAV][AOCR_VALIDATION_IN] DocumentoId="+snapshot.AocrId);
                            try{aocrPdf=ValidarPdf(snapshot,"RECONOCIMIENTO",snapshot.AocrId);}catch(Exception ex){Trace.TraceWarning("[INSPECTOR_DCAV][AOCR_VALIDATION_ERROR] "+ex.Message);throw;}
                            Trace.TraceInformation("[INSPECTOR_DCAV][AOCR_VALIDATION_OK] DocumentoId="+snapshot.AocrId+";PdfId="+aocrPdf.Id);
                            Trace.TraceInformation("[INSPECTOR_DCAV][CONDICIONES_VALIDATION_IN] DocumentoId="+snapshot.CondicionesId);
                            try{condicionesPdf=ValidarPdf(snapshot,"CONDICIONES_LIMITACIONES",snapshot.CondicionesId);}catch(Exception ex){Trace.TraceWarning("[INSPECTOR_DCAV][CONDICIONES_VALIDATION_ERROR] "+ex.Message);throw;}
                            Trace.TraceInformation("[INSPECTOR_DCAV][CONDICIONES_VALIDATION_OK] DocumentoId="+snapshot.CondicionesId+";PdfId="+condicionesPdf.Id);
                            ValidarIdsEnviados(request,snapshot,aocrPdf,condicionesPdf);
                            Trace.TraceInformation("[INSPECTOR_DCAV][PACKAGE_VALIDATION_OK] SolicitudId="+snapshot.SolicitudId);

                            var detalle=Detalle(snapshot,aocrPdf,condicionesPdf,request,clave,correlation);
                            foreach(var evento in new[]{"ENVIO_DOCUMENTOS_DCAV_INICIADO","AOCR_VALIDADO_PARA_ENVIO","CONDICIONES_VALIDADAS_PARA_ENVIO","PAQUETE_DOCUMENTAL_VALIDADO"})
                                _dao.RegistrarAuditoria(cn,tx,snapshot,EstadoDestino,request.UsuarioInspectorId,evento,detalle);
                            _dao.MarcarDocumentosEnviados(cn,tx,snapshot,request.UsuarioInspectorId);
                            Trace.TraceInformation("[INSPECTOR_DCAV][DOCUMENTS_LOCKED] AocrId="+snapshot.AocrId+";CondicionesId="+snapshot.CondicionesId);
                            _dao.RegistrarAuditoria(cn,tx,snapshot,EstadoDestino,request.UsuarioInspectorId,"AOCR_ENVIADO_DCAV",detalle);
                            _dao.RegistrarAuditoria(cn,tx,snapshot,EstadoDestino,request.UsuarioInspectorId,"CONDICIONES_ENVIADAS_DCAV",detalle);
                            _dao.CambiarEstadoCentral(cn,tx,snapshot,EstadoDestino,request.UsuarioInspectorId);
                            Trace.TraceInformation("[INSPECTOR_DCAV][STATE_UPDATED] EstadoNuevo="+EstadoDestino);
                            _dao.RegistrarHistorial(cn,tx,snapshot,EstadoDestino,request.UsuarioInspectorId,request.Rol,clave,request.Ip,correlation,detalle);
                            _dao.RegistrarAuditoria(cn,tx,snapshot,EstadoDestino,request.UsuarioInspectorId,"DOCUMENTOS_ENVIADOS_DCAV",detalle);
                            var notificaciones=_dao.CrearNotificacionesDcav(cn,tx,snapshot.SolicitudId,clave,correlation);
                            Trace.TraceInformation("[INSPECTOR_DCAV][NOTIFICATION_CREATED] Total="+notificaciones);
                            _dao.RegistrarIdempotencia(cn,tx,clave,snapshot,EstadoDestino,correlation);
                            Trace.TraceInformation("[IDEMPOTENCY][CREATED] Clave="+clave);
                            tx.Commit();
                        }
                        catch
                        {
                            try{tx.Rollback();}catch{}
                            Trace.TraceWarning("[INSPECTOR_DCAV][ROLLBACK] SolicitudId="+request.SolicitudId+";CorrelationId="+correlation);
                            throw;
                        }
                    }
                }
                EncolarCorreosPostCommit(snapshot.SolicitudId,clave,correlation);
                Trace.TraceInformation("[INSPECTOR_DCAV][SEND_OK] SolicitudId="+snapshot.SolicitudId+";AocrId="+snapshot.AocrId+";CondicionesId="+snapshot.CondicionesId);
                return new ResultadoEnvioDocumentosDcav{Exitoso=true,Codigo=200,Mensaje="AOCR y Condiciones y Limitaciones fueron enviados conjuntamente a DCAV.",EstadoAnterior=snapshot.EstadoCentral,EstadoNuevo=EstadoDestino,AocrId=snapshot.AocrId,AocrPdfId=aocrPdf.Id,CondicionesId=snapshot.CondicionesId,CondicionesPdfId=condicionesPdf.Id,FechaEnvio=DateTime.Now};
            }
            catch(EnvioDocumentosDcavException ex){RegistrarRechazo(request,ex.Message,correlation);Trace.TraceWarning("[INSPECTOR_DCAV][SEND_ERROR] Codigo="+ex.Codigo+";Motivo="+ex.Message);throw;}
            catch(InvalidOperationException ex) when(ex.Message=="CONCURRENCY_CONFLICT"){var conflict=Conflicto();RegistrarRechazo(request,conflict.Message,correlation);Trace.TraceWarning("[CONCURRENCY][CONFLICT] SolicitudId="+request.SolicitudId);throw conflict;}
            catch(Exception ex){RegistrarRechazo(request,ex.Message,correlation);Trace.TraceError("[INSPECTOR_DCAV][SEND_ERROR] "+ex);throw new EnvioDocumentosDcavException(500,"Error interno al enviar los documentos a DCAV.",ex);}
        }

        public ResultadoValidacionEnvioDcav ValidarEnvio(int solicitudId,int inspeccionId,int usuarioId)
        {
            if(solicitudId<=0||inspeccionId<=0||usuarioId<=0)return Invalido(400,"Solicitud, inspeccion y usuario son obligatorios.");
            try{using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction(IsolationLevel.RepeatableRead)){_dao.BloquearOperacion(cn,tx,solicitudId,inspeccionId);var s=_dao.CargarParaActualizar(cn,tx,solicitudId,inspeccionId);if(s==null)return Invalido(404,"El expediente o la inspeccion no existen.");if(s.InspectorId!=usuarioId)return Invalido(403,"Solo el Inspector asignado puede finalizar y enviar los documentos.");var result=ValidarSnapshot(s,true);if(result.Valido){var a=ValidarPdf(s,"RECONOCIMIENTO",s.AocrId);var c=ValidarPdf(s,"CONDICIONES_LIMITACIONES",s.CondicionesId);result.AocrId=s.AocrId;result.AocrPdfId=a.Id;result.CondicionesId=s.CondicionesId;result.CondicionesPdfId=c.Id;result.VersionAocr=s.VersionAocr;result.VersionCondiciones=s.VersionCondiciones;result.VersionExpediente=s.VersionExpediente;}tx.Rollback();return result;}}}
            catch(EnvioDocumentosDcavException ex){return Invalido(ex.Codigo,ex.Message);}
        }

        public static string CrearClaveIdempotencia(EnvioDocumentosDcavSnapshot s){return s.SolicitudId+":"+s.InspeccionId+":"+s.AocrId+":"+s.VersionAocr+":"+s.CondicionesId+":"+s.VersionCondiciones+":ENVIAR_DOCUMENTOS_DCAV";}
        private static void ValidarRequest(EnviarDocumentosDcavRequest r){if(r==null)throw new EnvioDocumentosDcavException(400,"Solicitud de envio invalida.");if(r.SolicitudId<=0||r.InspeccionId<=0||r.UsuarioInspectorId<=0)throw new EnvioDocumentosDcavException(400,"Solicitud, inspeccion y usuario son obligatorios.");if(Normalizar(r.Rol)!="INSPECTORTECNICO")throw new EnvioDocumentosDcavException(403,"Solo el rol InspectorTecnico puede finalizar y enviar los documentos.");}
        private static void ValidarConcurrencia(EnviarDocumentosDcavRequest r,EnvioDocumentosDcavSnapshot s){if(r.VersionExpediente<=0||r.VersionExpediente!=s.VersionExpediente||!string.Equals(r.EstadoEsperado,s.EstadoCentral,StringComparison.OrdinalIgnoreCase))throw Conflicto();if(s.InspectorId!=r.UsuarioInspectorId)throw new EnvioDocumentosDcavException(403,"Solo el Inspector asignado puede finalizar y enviar los documentos.");}
        private static void ValidarIdsEnviados(EnviarDocumentosDcavRequest r,EnvioDocumentosDcavSnapshot s,DocumentoPdfDto a,DocumentoPdfDto c){if(r.AocrId!=s.AocrId||r.CondicionesId!=s.CondicionesId||r.VersionAocr!=s.VersionAocr||r.VersionCondiciones!=s.VersionCondiciones||r.AocrPdfId!=a.Id||r.CondicionesPdfId!=c.Id)throw Conflicto();}

        private ResultadoValidacionEnvioDcav ValidarSnapshot(EnvioDocumentosDcavSnapshot s,bool exigirEstado)
        {
            var errores=new List<string>();if(!s.SolicitudActiva)errores.Add("La solicitud no esta activa.");var estado=(s.EstadoCentral??string.Empty).ToUpperInvariant();
            if(exigirEstado&&estado!="DOCUMENTOS_HABILITADOS_INSPECTOR"&&estado!="DOCUMENTOS_OBSERVADOS_DCAV")errores.Add("El estado central no permite el envio.");
            var estadoSolicitud=(s.EstadoSolicitud??string.Empty).ToUpperInvariant();if(estado.Contains("ANUL")||estado.Contains("FINAL")||estadoSolicitud.Contains("ANUL")||estadoSolicitud.Contains("FINAL"))errores.Add("El proceso esta anulado o finalizado.");
            if(s.InformeId<=0||!s.InformeFinalizado||!s.InformeFirmado||!string.Equals(s.EstadoInforme,"INFORME_TECNICO_APROBADO_DCAV",StringComparison.OrdinalIgnoreCase))errores.Add("El Informe Tecnico no esta aprobado por DCAV.");
            var resultadoInforme=Normalizar(s.ResultadoInforme);if((!resultadoInforme.Contains("SATISFACTORIO")||resultadoInforme.Contains("INSATISFACTORIO"))&&resultadoInforme!="FAVORABLE"&&resultadoInforme!="APROBADO"&&resultadoInforme!="CUMPLE")errores.Add("El Informe Tecnico no tiene resultado satisfactorio.");
            if(!s.ListaFirmada)errores.Add("La LV/EAE no esta firmada.");
            ValidarDocumento(s.AocrId,s.AocrVigente,s.AocrEliminado,s.AocrFirmado,s.EstadoAocr,s.CompaniaAocr,s.InspectorAocr,s,"AOCR",errores);
            ValidarDocumento(s.CondicionesId,s.CondicionesVigente,s.CondicionesEliminado,s.CondicionesFirmadas,s.EstadoCondiciones,s.CompaniaCondiciones,s.InspectorCondiciones,s,"Condiciones",errores);
            if(estado=="DOCUMENTOS_OBSERVADOS_DCAV"&&!string.Equals(s.EstadoAocr,"GENERADO",StringComparison.OrdinalIgnoreCase)&&!string.Equals(s.EstadoCondiciones,"GENERADO",StringComparison.OrdinalIgnoreCase))errores.Add("El reenvío debe incluir al menos una versión corregida nueva.");
            if(estado=="DOCUMENTOS_OBSERVADOS_DCAV"&&new DevolucionDocumentosDcavDAO().ObtenerObservaciones(s.SolicitudId).Any(x=>string.Equals(x.Estado,"ABIERTA",StringComparison.OrdinalIgnoreCase)))errores.Add("Todas las observaciones deben estar atendidas antes del reenvío.");
            if(string.IsNullOrWhiteSpace(s.CodigoCompania)||!Igual(s.CodigoCompania,s.CompaniaAocr)||!Igual(s.CodigoCompania,s.CompaniaCondiciones))errores.Add("Los documentos no pertenecen a la misma compania.");
            if(string.IsNullOrWhiteSpace(s.NumeroAoc)||string.IsNullOrWhiteSpace(s.Pais)||string.IsNullOrWhiteSpace(s.Operador)||string.IsNullOrWhiteSpace(s.PuntoContacto)||string.IsNullOrWhiteSpace(s.RepresentanteTecnico)||string.IsNullOrWhiteSpace(s.Aeropuertos)||s.AeronavesCompletas<=0||!s.FechaVencimiento.HasValue)errores.Add("Los campos obligatorios del AOCR estan incompletos.");
            if(string.IsNullOrWhiteSpace(s.Condiciones)||string.IsNullOrWhiteSpace(s.Limitaciones)||s.AeronavesCompletas<=0||string.IsNullOrWhiteSpace(s.Aeropuertos))errores.Add("Las Condiciones y Limitaciones estan incompletas o son inconsistentes.");
            var codigo=s.AocrId<=0||s.CondicionesId<=0?404:(exigirEstado&&(estado!="DOCUMENTOS_HABILITADOS_INSPECTOR"&&estado!="DOCUMENTOS_OBSERVADOS_DCAV")?409:422);
            return errores.Count==0?new ResultadoValidacionEnvioDcav{Valido=true,Codigo=200,Mensaje="Paquete documental valido para envio."}:new ResultadoValidacionEnvioDcav{Valido=false,Codigo=codigo,Mensaje=string.Join(" ",errores),Errores=errores};
        }

        private DocumentoPdfDto ValidarPdf(EnvioDocumentosDcavSnapshot s,string tipo,int origenId){var pdf=_pdf.ObtenerVigente(s.SolicitudId,s.InspeccionId,tipo);if(pdf==null||pdf.DocumentoOrigenId!=origenId||!pdf.Vigente||pdf.Firmado||!Igual(pdf.CodigoCompania,s.CodigoCompania))throw new EnvioDocumentosDcavException(422,"El PDF vigente de "+tipo+" no corresponde al documento actual.");var integridad=_pdf.ValidarArchivo(pdf.Id);if(!integridad.Valido)throw new EnvioDocumentosDcavException(integridad.Codigo==404?404:422,"El PDF de "+tipo+" no supera la validacion de integridad: "+integridad.Mensaje);return pdf;}
        private static void ValidarDocumento(int id,bool vigente,bool eliminado,bool firmado,string estado,string compania,int inspector,EnvioDocumentosDcavSnapshot s,string nombre,IList<string> errores){if(id<=0)errores.Add(nombre+" inexistente.");if(!vigente||eliminado)errores.Add(nombre+" no esta vigente.");if(firmado)errores.Add(nombre+" esta firmado.");var e=(estado??"").ToUpperInvariant();var reenvio=string.Equals(s.EstadoCentral,"DOCUMENTOS_OBSERVADOS_DCAV",StringComparison.OrdinalIgnoreCase);if(e!="GENERADO"&&!(reenvio&&e=="APROBADO_DCAV"))errores.Add(nombre+" no esta generado o ya fue enviado; tampoco esta aprobado para reutilizacion.");if(inspector!=s.InspectorId)errores.Add(nombre+" no pertenece al Inspector asignado.");if(!Igual(compania,s.CodigoCompania))errores.Add(nombre+" pertenece a otra compania.");}
        private void EncolarCorreosPostCommit(int solicitudId,string clave,string correlation){foreach(var email in _dao.ObtenerCorreosDcav()){try{_emailQueue.EncolarAsync(new EmailQueueItem{Para=email,Asunto="AOCR y Condiciones pendientes de revision DCAV",Cuerpo="El Inspector ha finalizado la revision del AOCR y de las Condiciones y Limitaciones. Los documentos se encuentran disponibles para revision DCAV.",Estado=EstadoEmail.Pendiente,SolicitudId=solicitudId,TipoNotificacion="DOCUMENTOS_ENVIADOS_DCAV",EventKey=clave+":EMAIL:"+email.ToUpperInvariant(),CorrelationId=correlation,EsHtml=false}).GetAwaiter().GetResult();}catch(Exception ex){Trace.TraceWarning("[INSPECTOR_DCAV][EMAIL_QUEUE_ERROR] Email="+email+";Error="+ex.Message);}}}
        private void RegistrarRechazo(EnviarDocumentosDcavRequest r,string motivo,string correlation){try{_dao.RegistrarRechazo(r!=null?r.SolicitudId:0,r!=null?r.UsuarioInspectorId:0,"CorrelationId="+correlation+";Motivo="+motivo);}catch{}}
        private static string Detalle(EnvioDocumentosDcavSnapshot s,DocumentoPdfDto a,DocumentoPdfDto c,EnviarDocumentosDcavRequest r,string clave,string correlation){return "SolicitudId="+s.SolicitudId+";InspeccionId="+s.InspeccionId+";InspectorId="+r.UsuarioInspectorId+";AocrId="+s.AocrId+";VersionAocr="+s.VersionAocr+";AocrPdfId="+a.Id+";VersionPdfAocr="+a.Version+";HashAocr="+a.HashSha256+";CondicionesId="+s.CondicionesId+";VersionCondiciones="+s.VersionCondiciones+";CondicionesPdfId="+c.Id+";VersionPdfCondiciones="+c.Version+";HashCondiciones="+c.HashSha256+";EstadoAnterior="+s.EstadoCentral+";EstadoNuevo="+EstadoDestino+";IP="+(r.Ip??string.Empty)+";CorrelationId="+correlation+";Clave="+clave+";Resultado=OK";}
        private static ResultadoEnvioDocumentosDcav ResultadoHit(EnvioDocumentosDcavIdempotencia h,DocumentoPdfDto a,DocumentoPdfDto c){return new ResultadoEnvioDocumentosDcav{Exitoso=true,YaProcesado=true,Codigo=200,Mensaje="El paquete documental ya fue enviado a DCAV.",EstadoAnterior=h.EstadoAnterior,EstadoNuevo=h.EstadoNuevo,AocrId=h.AocrId,AocrPdfId=a!=null?a.Id:0,CondicionesId=h.CondicionesId,CondicionesPdfId=c!=null?c.Id:0,FechaEnvio=h.Fecha};}
        private static ResultadoValidacionEnvioDcav Invalido(int codigo,string mensaje){return new ResultadoValidacionEnvioDcav{Valido=false,Codigo=codigo,Mensaje=mensaje,Errores=new List<string>{mensaje}};}
        private static EnvioDocumentosDcavException Conflicto(){return new EnvioDocumentosDcavException(409,"El expediente o sus documentos fueron actualizados por otro proceso. Recargue la informacion antes de continuar.");}
        private static bool Igual(string a,string b){return string.Equals((a??string.Empty).Trim(),(b??string.Empty).Trim(),StringComparison.OrdinalIgnoreCase);}
        private static string Normalizar(string value){return new string((value??string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());}
    }

    public sealed class EnvioDocumentosDcavException:Exception
    {
        public int Codigo{get;private set;}
        public EnvioDocumentosDcavException(int codigo,string mensaje):base(mensaje){Codigo=codigo;}
        public EnvioDocumentosDcavException(int codigo,string mensaje,Exception inner):base(mensaje,inner){Codigo=codigo;}
    }
}
