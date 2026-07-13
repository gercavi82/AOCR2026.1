using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.DTOs.DocumentosPdf;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace CapaPresentacion.Services
{
    public interface IRevisionDocumentosInspectorService
    {
        RevisionDocumentosInspectorBandejaViewModel ObtenerPendientes(int usuarioId,UrlHelper url);
        RevisionDocumentosInspectorViewModel ObtenerDetalle(int solicitudId,int usuarioId,UrlHelper url);
        RevisionDocumentosOperacionResult GuardarAocr(GuardarAocrInspectorRequest request,int usuarioId,string usuario);
        RevisionDocumentosOperacionResult GuardarCondiciones(GuardarCondicionesInspectorRequest request,int usuarioId,string usuario);
        RevisionDocumentosOperacionResult PrevisualizarAocr(int solicitudId,int usuarioId,ControllerContext controllerContext);
        RevisionDocumentosOperacionResult PrevisualizarCondiciones(int solicitudId,int usuarioId,ControllerContext controllerContext);
        RevisionDocumentosOperacionResult GenerarPdfAocr(int solicitudId,int documentoId,int versionEsperada,int usuarioId,string usuario,ControllerContext controllerContext);
        RevisionDocumentosOperacionResult GenerarPdfCondiciones(int solicitudId,int documentoId,int versionEsperada,int usuarioId,string usuario,ControllerContext controllerContext);
        RevisionDocumentosOperacionResult ObtenerPdfGenerado(int solicitudId,string tipoDocumento,int version,int usuarioId);
    }

    public sealed class RevisionDocumentosInspectorService : IRevisionDocumentosInspectorService
    {
        private readonly FirmaAocrStorageService _storage;
        private readonly FirmaAocrWorkflowService _workflow;
        private readonly FirmaAocrPdfService _pdf=new FirmaAocrPdfService();
        private readonly AocrDocumentoGeneradoDAO _documentos=new AocrDocumentoGeneradoDAO();
        private readonly AocrProcesoEstadoDAO _estados=new AocrProcesoEstadoDAO();
        private readonly SolicitudAOCRDAO _solicitudes=new SolicitudAOCRDAO();
        private readonly AuditoriaDAO _auditoria=new AuditoriaDAO();
        private readonly IDocumentoPdfService _documentoPdf;

        public RevisionDocumentosInspectorService(FirmaAocrStorageService storage)
        {
            _storage=storage;
            _workflow=new FirmaAocrWorkflowService(new FirmaAocrAuthorizationService(),storage);
            _documentoPdf=new DocumentoPdfService(storage.ResolverRutaFisica("~/App_Data/AOCR"),
                (usuarioId,documento)=>new AocrAuthorizationService().PuedeInspectorAbrirInspeccion(documento.InspeccionId,usuarioId));
        }

        public RevisionDocumentosInspectorBandejaViewModel ObtenerPendientes(int usuarioId,UrlHelper url)
        {
            Trace.TraceInformation("[INSPECTOR_DOCS][BANDEJA_IN] UsuarioId="+usuarioId+";");
            var queue=new FirmaAocrInspectorQueueService().Obtener(usuarioId,false);
            var items=queue.Editables.Concat(queue.Observados).GroupBy(x=>x.Estado.SolicitudId).Select(g=>g.First()).Select(x=>new RevisionDocumentosInspectorFilaViewModel
            {
                SolicitudId=x.Estado.SolicitudId,InspeccionId=x.Inspeccion.CodigoInspeccion,
                NumeroSolicitud=x.Solicitud.NumeroSolicitud??x.Estado.SolicitudId.ToString(),
                Operador=FirmaAocrWorkflowService.PrimerValorNoVacio(x.Solicitud.RazonSocial,x.Solicitud.NombreOperador,x.Solicitud.NombreComercial),
                Estado=x.Estado.EstadoActual,FechaEstado=x.Estado.FechaEstado,
                UrlDetalle=url.Action("Detalle","InspectorDocumentosFinales",new{solicitudId=x.Estado.SolicitudId})
            }).OrderByDescending(x=>x.FechaEstado).ToList();
            Trace.TraceInformation("[INSPECTOR_DOCS][BANDEJA_OUT] UsuarioId="+usuarioId+"; Total="+items.Count+";");
            return new RevisionDocumentosInspectorBandejaViewModel{Items=items,Total=items.Count};
        }

        public RevisionDocumentosInspectorViewModel ObtenerDetalle(int solicitudId,int usuarioId,UrlHelper url)
        {
            Trace.TraceInformation("[INSPECTOR_DOCS][DETALLE_IN] SolicitudId="+solicitudId+"; UsuarioId="+usuarioId+";");
            var c=ContextoAutorizado(solicitudId,usuarioId);
            var estado=_estados.ObtenerActivoPorSolicitud(solicitudId);
            if(!EstadoPermitido(estado!=null?estado.EstadoActual:null))throw new RevisionDocumentosHttpException(409,"El expediente no se encuentra habilitado para revision final del Inspector.");
            var aocr=_documentos.ObtenerPorExpedienteTipo(solicitudId,c.Inspeccion.CodigoInspeccion,"RECONOCIMIENTO");
            var condiciones=_documentos.ObtenerPorExpedienteTipo(solicitudId,c.Inspeccion.CodigoInspeccion,"CONDICIONES_LIMITACIONES");
            if(aocr==null||condiciones==null)throw new RevisionDocumentosHttpException(404,"No existe el par AOCR y Condiciones para este expediente.");
            var observaciones=ConstruirObservaciones(solicitudId);
            var observado=string.Equals(estado.EstadoActual,AocrEstadosProceso.DocumentosObservadosDcav,StringComparison.OrdinalIgnoreCase);
            var editaAocr=PuedeEditar(aocr,c.FirmaReconocimiento)&&(!observado||DocumentoObservado(observaciones,"RECONOCIMIENTO"));
            var editaCond=PuedeEditar(condiciones,c.FirmaCondiciones)&&(!observado||DocumentoObservado(observaciones,"CONDICIONES_LIMITACIONES"));
            RegistrarAuditoria("AOCR_ABIERTO_INSPECTOR",solicitudId,usuarioId.ToString(),"InspeccionId="+c.Inspeccion.CodigoInspeccion);
            RegistrarAuditoria("CONDICIONES_ABIERTAS_INSPECTOR",solicitudId,usuarioId.ToString(),"InspeccionId="+c.Inspeccion.CodigoInspeccion);
            var pdfAocr=_documentoPdf.ObtenerVigente(solicitudId,c.Inspeccion.CodigoInspeccion,"RECONOCIMIENTO");
            var pdfCondiciones=_documentoPdf.ObtenerVigente(solicitudId,c.Inspeccion.CodigoInspeccion,"CONDICIONES_LIMITACIONES");
            var aocrListo=string.Equals(aocr.Estado,"GENERADO",StringComparison.OrdinalIgnoreCase)||(observado&&string.Equals(aocr.Estado,"APROBADO_DCAV",StringComparison.OrdinalIgnoreCase));
            var condicionesListas=string.Equals(condiciones.Estado,"GENERADO",StringComparison.OrdinalIgnoreCase)||(observado&&string.Equals(condiciones.Estado,"APROBADO_DCAV",StringComparison.OrdinalIgnoreCase));
            var puedeEnviar=(string.Equals(estado.EstadoActual,AocrEstadosProceso.DocumentosHabilitadosInspector,StringComparison.OrdinalIgnoreCase)||observado)
                && aocrListo&&condicionesListas&&(!observado||string.Equals(aocr.Estado,"GENERADO",StringComparison.OrdinalIgnoreCase)||string.Equals(condiciones.Estado,"GENERADO",StringComparison.OrdinalIgnoreCase))
                && pdfAocr!=null&&pdfCondiciones!=null&&pdfAocr.DocumentoOrigenId==aocr.CodigoDocumento&&pdfCondiciones.DocumentoOrigenId==condiciones.CodigoDocumento;
            return new RevisionDocumentosInspectorViewModel
            {
                SolicitudId=solicitudId,InspeccionId=c.Inspeccion.CodigoInspeccion,NumeroSolicitud=c.Solicitud.NumeroSolicitud,
                Operador=c.Documento.NombreExplotador,EstadoProceso=estado.EstadoActual,VersionExpediente=estado.Version,
                PuedeFinalizarYEnviar=puedeEnviar,AocrPdfId=pdfAocr!=null?pdfAocr.Id:0,CondicionesPdfId=pdfCondiciones!=null?pdfCondiciones.Id:0,
                ClaveIdempotenciaEnvio=puedeEnviar?solicitudId+":"+c.Inspeccion.CodigoInspeccion+":"+aocr.CodigoDocumento+":"+aocr.Version+":"+condiciones.CodigoDocumento+":"+condiciones.Version+":ENVIAR_DOCUMENTOS_DCAV":null,
                Observaciones=observaciones,
                Aocr=new AocrInspectorViewModel{DocumentoId=aocr.CodigoDocumento,Version=aocr.Version,Estado=aocr.Estado,Numero=c.Documento.NumeroAocr,Operador=c.Documento.NombreExplotador,Pais=c.Solicitud.Pais,TipoTramite=Convert.ToString(c.Solicitud.TipoSolicitud),EstadoExplotador=c.Documento.EstadoExplotador,FechaEmision=c.Documento.FechaEmisionDocumento,FechaVencimiento=c.Documento.FechaVencimiento,Aeronaves=Lista(c.Aeronaves.Select(x=>x.Matricula)),Aeropuertos=FirmaAocrWorkflowService.PrimerValorNoVacio(c.Solicitud.AeropuertosEcuador,c.Solicitud.AeropuertosEcuadorOtros),Editable=editaAocr,PdfExiste=_storage.Existe(aocr.RutaDocumento),Versiones=Versiones(solicitudId,c.Inspeccion.CodigoInspeccion,"RECONOCIMIENTO",url)},
                Condiciones=new CondicionesInspectorViewModel{DocumentoId=condiciones.CodigoDocumento,Version=condiciones.Version,Estado=condiciones.Estado,Operador=c.Documento.NombreExplotador,Aeronaves=Lista(c.Aeronaves.Select(x=>x.Matricula)),Modelos=Lista(c.Aeronaves.Select(x=>FirmaAocrWorkflowService.PrimerValorNoVacio(x.Marca,x.Modelo))),Aeropuertos=FirmaAocrWorkflowService.PrimerValorNoVacio(c.Solicitud.AeropuertosEcuador,c.Solicitud.AeropuertosEcuadorOtros),Rutas=c.Solicitud.ResumenOperacionesEae,Limitaciones=c.Solicitud.AprobacionesEspecialesOtros,Condiciones=c.Solicitud.AprobacionesEspeciales,Editable=editaCond,PdfExiste=_storage.Existe(condiciones.RutaDocumento),Versiones=Versiones(solicitudId,c.Inspeccion.CodigoInspeccion,"CONDICIONES_LIMITACIONES",url)}
            };
        }

        public RevisionDocumentosOperacionResult GuardarAocr(GuardarAocrInspectorRequest r,int usuarioId,string usuario)
        {
            Trace.TraceInformation("[INSPECTOR_DOCS][AOCR_GUARDAR_IN] SolicitudId="+(r!=null?r.SolicitudId:0)+"; UsuarioId="+usuarioId+";");
            if(r==null||r.SolicitudId<=0||r.DocumentoId<=0||r.VersionEsperada<=0)return Error(400,"Datos AOCR invalidos.");
            if(string.IsNullOrWhiteSpace(r.EstadoExplotador)||!r.FechaVencimiento.HasValue)return Error(422,"Estado del explotador y fecha de vencimiento son obligatorios.");
            var c=ContextoAutorizado(r.SolicitudId,usuarioId);ValidarDocumento(c,r.DocumentoId,r.VersionEsperada,"RECONOCIMIENTO");
            _workflow.GuardarDatosObligatorios(r.SolicitudId,r.EstadoExplotador,r.FechaVencimiento,usuarioId,usuario);
            var nuevoEstado=EstadoCorregido(c,"RECONOCIMIENTO");
            var nuevaVersion=_documentos.ActualizarEdicionOptimista(r.DocumentoId,r.SolicitudId,c.Inspeccion.CodigoInspeccion,usuarioId,r.VersionEsperada,nuevoEstado);
            if(nuevaVersion<=0){Trace.TraceWarning("[INSPECTOR_DOCS][CONCURRENCY_CONFLICT] DocumentoId="+r.DocumentoId+";");return Error(409,"El AOCR fue modificado por otro proceso.");}
            RegistrarAuditoria("AOCR_GUARDADO_INSPECTOR",r.SolicitudId,usuario,"DocumentoId="+r.DocumentoId+";VersionAnterior="+r.VersionEsperada+";VersionNueva="+nuevaVersion);
            Trace.TraceInformation("[INSPECTOR_DOCS][AOCR_GUARDAR_OK] SolicitudId="+r.SolicitudId+"; Version="+nuevaVersion+";");return Ok("AOCR guardado correctamente.");
        }

        public RevisionDocumentosOperacionResult GuardarCondiciones(GuardarCondicionesInspectorRequest r,int usuarioId,string usuario)
        {
            Trace.TraceInformation("[INSPECTOR_DOCS][CONDICIONES_GUARDAR_IN] SolicitudId="+(r!=null?r.SolicitudId:0)+"; UsuarioId="+usuarioId+";");
            if(r==null||r.SolicitudId<=0||r.DocumentoId<=0||r.VersionEsperada<=0)return Error(400,"Datos de Condiciones invalidos.");
            if(string.IsNullOrWhiteSpace(r.Limitaciones)||string.IsNullOrWhiteSpace(r.Condiciones))return Error(422,"Limitaciones y condiciones son obligatorias.");
            var c=ContextoAutorizado(r.SolicitudId,usuarioId);ValidarDocumento(c,r.DocumentoId,r.VersionEsperada,"CONDICIONES_LIMITACIONES");
            c.Solicitud.AprobacionesEspecialesOtros=r.Limitaciones.Trim();c.Solicitud.AprobacionesEspeciales=r.Condiciones.Trim();c.Solicitud.UpdatedBy=usuario;_solicitudes.Actualizar(c.Solicitud);
            var nuevoEstado=EstadoCorregido(c,"CONDICIONES_LIMITACIONES");
            var nuevaVersion=_documentos.ActualizarEdicionOptimista(r.DocumentoId,r.SolicitudId,c.Inspeccion.CodigoInspeccion,usuarioId,r.VersionEsperada,nuevoEstado);
            if(nuevaVersion<=0){Trace.TraceWarning("[INSPECTOR_DOCS][CONCURRENCY_CONFLICT] DocumentoId="+r.DocumentoId+";");return Error(409,"Condiciones fueron modificadas por otro proceso.");}
            RegistrarAuditoria("CONDICIONES_GUARDADAS_INSPECTOR",r.SolicitudId,usuario,"DocumentoId="+r.DocumentoId+";VersionAnterior="+r.VersionEsperada+";VersionNueva="+nuevaVersion);
            Trace.TraceInformation("[INSPECTOR_DOCS][CONDICIONES_GUARDAR_OK] SolicitudId="+r.SolicitudId+"; Version="+nuevaVersion+";");return Ok("Condiciones guardadas correctamente.");
        }

        public RevisionDocumentosOperacionResult PrevisualizarAocr(int solicitudId,int usuarioId,ControllerContext cc){return Previsualizar(solicitudId,usuarioId,"RECONOCIMIENTO",cc);}
        public RevisionDocumentosOperacionResult PrevisualizarCondiciones(int solicitudId,int usuarioId,ControllerContext cc){return Previsualizar(solicitudId,usuarioId,"CONDICIONES_LIMITACIONES",cc);}
        public RevisionDocumentosOperacionResult GenerarPdfAocr(int solicitudId,int documentoId,int versionEsperada,int usuarioId,string usuario,ControllerContext cc){return Generar(solicitudId,documentoId,versionEsperada,usuarioId,usuario,"RECONOCIMIENTO",cc);}
        public RevisionDocumentosOperacionResult GenerarPdfCondiciones(int solicitudId,int documentoId,int versionEsperada,int usuarioId,string usuario,ControllerContext cc){return Generar(solicitudId,documentoId,versionEsperada,usuarioId,usuario,"CONDICIONES_LIMITACIONES",cc);}
        public RevisionDocumentosOperacionResult ObtenerPdfGenerado(int solicitudId,string tipoDocumento,int version,int usuarioId)
        {
            var c=ContextoAutorizado(solicitudId,usuarioId);var tipo=FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
            var doc=_documentos.ListarVersionesPorExpediente(solicitudId,c.Inspeccion.CodigoInspeccion,tipo).FirstOrDefault(x=>x.Version==version);
            if(doc==null||string.IsNullOrWhiteSpace(doc.RutaDocumento))return Error(404,"PDF inexistente.");var path=_storage.ResolverRutaFisica(doc.RutaDocumento);if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))return Error(404,"El archivo PDF no se encuentra disponible.");var bytes=File.ReadAllBytes(path);return new RevisionDocumentosOperacionResult{Ok=true,Codigo=200,Contenido=bytes,Tamanio=bytes.LongLength,Ruta=doc.RutaDocumento};
        }

        private RevisionDocumentosOperacionResult Previsualizar(int solicitudId,int usuarioId,string tipo,ControllerContext cc)
        {
            var c=ContextoAutorizado(solicitudId,usuarioId);var bytes=Pdf(tipo,c,cc);if(bytes==null||bytes.Length==0)return Error(500,"No se pudo previsualizar el documento.");bytes=MarcaBorrador(bytes);
            var evento=tipo=="RECONOCIMIENTO"?"AOCR_PREVISUALIZADO":"CONDICIONES_PREVISUALIZADAS";RegistrarAuditoria(evento,solicitudId,usuarioId.ToString(),"Sin cambio de estado");
            Trace.TraceInformation(tipo=="RECONOCIMIENTO"?"[INSPECTOR_DOCS][AOCR_PREVIEW] SolicitudId="+solicitudId+";":"[INSPECTOR_DOCS][CONDICIONES_PREVIEW] SolicitudId="+solicitudId+";");return new RevisionDocumentosOperacionResult{Ok=true,Codigo=200,Contenido=bytes,Tamanio=bytes.LongLength};
        }

        private RevisionDocumentosOperacionResult Generar(int solicitudId,int documentoId,int versionEsperada,int usuarioId,string usuario,string tipo,ControllerContext cc)
        {
            var c=ContextoAutorizado(solicitudId,usuarioId);var doc=ValidarDocumento(c,documentoId,versionEsperada,tipo);
            var faltantes=tipo=="RECONOCIMIENTO"?c.CamposFaltantes:new List<string>();if(tipo!="RECONOCIMIENTO"){if(string.IsNullOrWhiteSpace(c.Solicitud.AprobacionesEspecialesOtros))faltantes.Add("Limitaciones");if(string.IsNullOrWhiteSpace(c.Solicitud.AprobacionesEspeciales))faltantes.Add("Condiciones");}if(faltantes.Count>0)return Error(422,"Campos obligatorios incompletos: "+string.Join(", ",faltantes));
            try
            {
                var generado=_documentoPdf.Generar(new GenerarPdfRequest
                {
                    SolicitudId=solicitudId,InspeccionId=c.Inspeccion.CodigoInspeccion,DocumentoOrigenId=doc.CodigoDocumento,
                    TipoDocumento=tipo,VersionOrigen=versionEsperada,VersionRegistroEsperada=versionEsperada,
                    UsuarioId=usuarioId,Rol="InspectorTecnico",EstadoEsperado=doc.Estado,CodigoCompania=doc.CodigoCompania,
                    CamposFaltantes=faltantes,Generador=()=>Pdf(tipo,c,cc),CorrelationId=Guid.NewGuid().ToString("N")
                });
                var d=generado.Documento;
                var evento=tipo=="RECONOCIMIENTO"?"AOCR_PDF_GENERADO":"CONDICIONES_PDF_GENERADO";
                RegistrarAuditoria(evento,solicitudId,usuario,"DocumentoPdfId="+d.Id+";Version="+d.Version+";RutaLogica="+d.RutaLogica+";Hash="+d.HashSha256+";Tamanio="+d.TamanoBytes+";Idempotente="+generado.YaProcesado);
                Trace.TraceInformation(tipo=="RECONOCIMIENTO"?"[INSPECTOR_DOCS][AOCR_PDF_OK] SolicitudId="+solicitudId+";":"[INSPECTOR_DOCS][CONDICIONES_PDF_OK] SolicitudId="+solicitudId+";");
                return new RevisionDocumentosOperacionResult{Ok=true,Codigo=200,Mensaje=generado.Mensaje,Ruta=d.RutaLogica,Hash=d.HashSha256,Tamanio=d.TamanoBytes};
            }
            catch(DocumentoPdfException ex){return Error(ex.Codigo,ex.Message);}
        }

        private FirmaAocrContexto ContextoAutorizado(int solicitudId,int usuarioId){if(usuarioId<=0)throw new RevisionDocumentosHttpException(401,"Usuario no autenticado.");var c=_workflow.CargarContexto(solicitudId);if(c==null||c.Solicitud==null||c.Inspeccion==null)throw new RevisionDocumentosHttpException(404,"Solicitud o inspeccion inexistente.");var asignado=c.Inspeccion.CodigoInspector??c.Solicitud.CodigoTecnico??0;if(asignado!=usuarioId)throw new RevisionDocumentosHttpException(403,"Solo el Inspector asignado puede acceder a estos documentos.");return c;}
        private AocrDocumentoGenerado ValidarDocumento(FirmaAocrContexto c,int id,int version,string tipo){var d=_documentos.ObtenerPorExpedienteTipo(c.Solicitud.CodigoSolicitud,c.Inspeccion.CodigoInspeccion,tipo);if(d==null||d.CodigoDocumento!=id)throw new RevisionDocumentosHttpException(404,"Documento inexistente.");if(d.Version!=version)throw new RevisionDocumentosHttpException(409,"Conflicto de version documental.");var firma=tipo=="RECONOCIMIENTO"?c.FirmaReconocimiento:c.FirmaCondiciones;if(!PuedeEditar(d,firma))throw new RevisionDocumentosHttpException(409,"El documento no es editable o ya fue firmado.");return d;}
        private static bool PuedeEditar(AocrDocumentoGenerado d,AocrFirmaDocumento f){return d!=null&&d.Vigente&&!d.Eliminado&&(f==null||string.IsNullOrWhiteSpace(f.RutaDocumento))&&new[]{"BORRADOR","EN_REVISION_INSPECTOR","GENERADO","CORRECCION_INSPECTOR","CORREGIDO_INSPECTOR"}.Contains((d.Estado??"").ToUpperInvariant());}
        private string EstadoCorregido(FirmaAocrContexto c,string tipo){var e=_estados.ObtenerActivoPorSolicitud(c.Solicitud.CodigoSolicitud);return e!=null&&string.Equals(e.EstadoActual,AocrEstadosProceso.DocumentosObservadosDcav,StringComparison.OrdinalIgnoreCase)?"CORREGIDO_INSPECTOR":"EN_REVISION_INSPECTOR";}
        private static bool EstadoPermitido(string e){return string.Equals(e,AocrEstadosProceso.DocumentosHabilitadosInspector,StringComparison.OrdinalIgnoreCase)||string.Equals(e,AocrEstadosProceso.DocumentosEnRevisionInspector,StringComparison.OrdinalIgnoreCase)||string.Equals(e,AocrEstadosProceso.DocumentosObservadosDcav,StringComparison.OrdinalIgnoreCase);}
        private IList<VersionDocumentoViewModel> Versiones(int s,int i,string t,UrlHelper u){return _documentoPdf.ObtenerVersiones(s,i,t).Select(x=>new VersionDocumentoViewModel{DocumentoId=x.Id,Version=x.Version,Estado=x.Estado,Fecha=x.FechaGeneracion,Vigente=x.Vigente,UrlPdf=u.Action("Descargar","DocumentoPdf",new{id=x.Id})}).ToList();}
        private IList<ObservacionDocumentoViewModel> ConstruirObservaciones(int s){return new DevolucionDocumentosDcavService().ObtenerObservaciones(s).Select(x=>new ObservacionDocumentoViewModel{ObservacionId=x.ObservacionId,TipoDocumento=x.TipoDocumento,Seccion=x.Seccion,Campo=x.Campo,Observacion=x.Texto,Estado=x.Estado,DocumentoOrigenId=x.DocumentoOrigenId,VersionOrigen=x.VersionOrigen,PdfOrigenId=x.PdfOrigenId,DocumentoCorreccionId=x.DocumentoCorreccionId,VersionCorreccion=x.VersionCorreccion,Fecha=x.Fecha,Usuario=x.UsuarioDcav,Rol=x.RolDcav}).ToList();}
        private static string InferirTipo(string o){var t=(o??"").ToUpperInvariant();if(t.Contains("CONDIC")&&!t.Contains("AOCR"))return "CONDICIONES_LIMITACIONES";if(t.Contains("AOCR")&&!t.Contains("CONDIC"))return "RECONOCIMIENTO";return "AMBOS";}
        private static bool DocumentoObservado(IEnumerable<ObservacionDocumentoViewModel> o,string t){return o.Any(x=>x.TipoDocumento=="AMBOS"||x.TipoDocumento==t);}
        private byte[] Pdf(string t,FirmaAocrContexto c,ControllerContext cc){return t=="RECONOCIMIENTO"?_pdf.GenerarPdfReconocimiento(cc,c.Documento):_pdf.GenerarPdfCondiciones(cc,c.Documento);}
        private static byte[] MarcaBorrador(byte[] input){using(var r=new PdfReader(input))using(var ms=new MemoryStream()){using(var s=new PdfStamper(r,ms)){var bf=BaseFont.CreateFont(BaseFont.HELVETICA_BOLD,BaseFont.CP1252,BaseFont.NOT_EMBEDDED);for(var p=1;p<=r.NumberOfPages;p++){var size=r.GetPageSizeWithRotation(p);var cb=s.GetOverContent(p);var gs=new PdfGState{FillOpacity=0.16f};cb.SaveState();cb.SetGState(gs);cb.SetColorFill(BaseColor.GRAY);cb.BeginText();cb.SetFontAndSize(bf,64);cb.ShowTextAligned(Element.ALIGN_CENTER,"BORRADOR",size.Width/2,size.Height/2,45);cb.EndText();cb.RestoreState();}}return ms.ToArray();}}
        private static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        private void RegistrarAuditoria(string a,int id,string u,string datos){_auditoria.Registrar(new Auditoria{Entidad="DOCUMENTOS_FINALES_INSPECTOR",Accion=a,Usuario=u,Fecha=DateTime.Now,DatosPrevios=null,DatosNuevos=datos});}
        private static string Lista(IEnumerable<string> v){return string.Join(", ",(v??Enumerable.Empty<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct());}
        private static RevisionDocumentosOperacionResult Error(int c,string m){return new RevisionDocumentosOperacionResult{Ok=false,Codigo=c,Mensaje=m};}
        private static RevisionDocumentosOperacionResult Ok(string m){return new RevisionDocumentosOperacionResult{Ok=true,Codigo=200,Mensaje=m};}
    }

    public sealed class RevisionDocumentosHttpException:Exception
    {
        public int Codigo{get;private set;}
        public RevisionDocumentosHttpException(int codigo,string mensaje):base(mensaje){Codigo=codigo;}
    }
}
