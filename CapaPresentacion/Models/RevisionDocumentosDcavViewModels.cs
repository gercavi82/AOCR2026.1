using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Models;

namespace CapaPresentacion.Models
{
    public sealed class DocumentosPendientesDcavViewModel
    {
        public IList<DocumentosPendientesDcavFilaViewModel> Items { get; set; }=new List<DocumentosPendientesDcavFilaViewModel>();
        public int Total { get; set; }
    }
    public sealed class DocumentosPendientesDcavFilaViewModel
    {
        public int SolicitudId{get;set;}public int InspeccionId{get;set;}public string NumeroSolicitud{get;set;}public string Explotador{get;set;}public string TipoTramite{get;set;}public string Inspector{get;set;}public DateTime FechaEnvio{get;set;}public string Estado{get;set;}public int AocrId{get;set;}public int VersionAocr{get;set;}public int CondicionesId{get;set;}public int VersionCondiciones{get;set;}
    }
    public sealed class RevisionDocumentosDcavViewModel
    {
        public int SolicitudId{get;set;}public int InspeccionId{get;set;}public string NumeroSolicitud{get;set;}public string Explotador{get;set;}public string Pais{get;set;}public string TipoTramite{get;set;}public string Inspector{get;set;}public DateTime FechaEnvio{get;set;}public string Estado{get;set;}public long VersionExpediente{get;set;}public DocumentoRevisionDcavViewModel Aocr{get;set;}public DocumentoRevisionDcavViewModel Condiciones{get;set;}public DocumentoSoporteDcavViewModel Informe{get;set;}public DocumentoSoporteDcavViewModel LvEae{get;set;}public IList<HistorialDcavViewModel> Historial{get;set;}=new List<HistorialDcavViewModel>();public IList<ObservacionVerificacionDcavViewModel> Observaciones{get;set;}=new List<ObservacionVerificacionDcavViewModel>();public ObservacionDocumentoDcavViewModel Devolucion{get;set;}=new ObservacionDocumentoDcavViewModel();
    }
    public sealed class DocumentoRevisionDcavViewModel{public int DocumentoId{get;set;}public int PdfId{get;set;}public int Version{get;set;}public string Estado{get;set;}public DateTime Fecha{get;set;}public string Hash{get;set;}public long Tamano{get;set;}}
    public sealed class DocumentoSoporteDcavViewModel{public int DocumentoId{get;set;}public int PdfId{get;set;}public string Nombre{get;set;}public string Hash{get;set;}public bool Firmado{get;set;}}
    public sealed class HistorialDcavViewModel{public string Accion{get;set;}public string Usuario{get;set;}public string Rol{get;set;}public string EstadoAnterior{get;set;}public string EstadoNuevo{get;set;}public string Observacion{get;set;}public DateTime Fecha{get;set;}}
    public sealed class ObservacionDocumentoDcavViewModel{public bool ObservarAocr{get;set;}public bool ObservarCondiciones{get;set;}public string SeccionCampo{get;set;}public string Observacion{get;set;}}
    public sealed class ObservacionVerificacionDcavViewModel{public int ObservacionId{get;set;}public string TipoDocumento{get;set;}public string Seccion{get;set;}public string Campo{get;set;}public string Texto{get;set;}public string Estado{get;set;}public int DocumentoCorreccionId{get;set;}public int VersionCorreccion{get;set;}}

    public static class RevisionDocumentosDcavViewModelFactory
    {
        public static DocumentosPendientesDcavViewModel Bandeja(IEnumerable<DocumentosPendientesDcavDto> source){var items=(source??Enumerable.Empty<DocumentosPendientesDcavDto>()).Select(x=>new DocumentosPendientesDcavFilaViewModel{SolicitudId=x.SolicitudId,InspeccionId=x.InspeccionId,NumeroSolicitud=x.NumeroSolicitud,Explotador=x.Explotador,TipoTramite=x.TipoTramite,Inspector=x.InspectorNombre,FechaEnvio=x.FechaEnvio,Estado=x.EstadoFuncional,AocrId=x.AocrId,VersionAocr=x.VersionAocrEnviada,CondicionesId=x.CondicionesId,VersionCondiciones=x.VersionCondicionesEnviada}).ToList();return new DocumentosPendientesDcavViewModel{Items=items,Total=items.Count};}
        public static RevisionDocumentosDcavViewModel Detalle(RevisionDocumentosDcavDto x,CapaNegocio.DTOs.DocumentosPdf.DocumentoPdfDto a,CapaNegocio.DTOs.DocumentosPdf.DocumentoPdfDto c){return new RevisionDocumentosDcavViewModel{SolicitudId=x.SolicitudId,InspeccionId=x.InspeccionId,NumeroSolicitud=x.NumeroSolicitud,Explotador=x.Explotador,Pais=x.Pais,TipoTramite=x.TipoTramite,Inspector=x.InspectorNombre,FechaEnvio=x.FechaEnvio,Estado=x.EstadoFuncional,VersionExpediente=x.VersionExpediente,Aocr=new DocumentoRevisionDcavViewModel{DocumentoId=x.AocrId,PdfId=x.AocrPdfId,Version=x.VersionAocrEnviada,Estado=x.EstadoAocr,Fecha=a.FechaGeneracion,Hash=a.HashSha256,Tamano=a.TamanoBytes},Condiciones=new DocumentoRevisionDcavViewModel{DocumentoId=x.CondicionesId,PdfId=x.CondicionesPdfId,Version=x.VersionCondicionesEnviada,Estado=x.EstadoCondiciones,Fecha=c.FechaGeneracion,Hash=c.HashSha256,Tamano=c.TamanoBytes},Informe=new DocumentoSoporteDcavViewModel{DocumentoId=x.InformeTecnicoId,PdfId=x.InformeTecnicoPdfId,Nombre="Informe Tecnico aprobado",Hash=x.InformeHash,Firmado=true},LvEae=new DocumentoSoporteDcavViewModel{DocumentoId=x.LvEaeId,PdfId=x.LvEaePdfId,Nombre="LV/EAE firmada",Hash=x.LvEaeHash,Firmado=true},Historial=x.Historial.Select(h=>new HistorialDcavViewModel{Accion=h.Accion,Usuario=h.UsuarioNombre,Rol=h.Rol,EstadoAnterior=h.EstadoAnterior,EstadoNuevo=h.EstadoNuevo,Observacion=h.Observacion,Fecha=h.Fecha}).ToList()};}
    }
}
