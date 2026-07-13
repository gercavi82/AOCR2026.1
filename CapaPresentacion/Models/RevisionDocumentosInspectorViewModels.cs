using System;
using System.Collections.Generic;

namespace CapaPresentacion.Models
{
    public sealed class RevisionDocumentosInspectorBandejaViewModel
    {
        public IList<RevisionDocumentosInspectorFilaViewModel> Items { get; set; } = new List<RevisionDocumentosInspectorFilaViewModel>();
        public int Total { get; set; }
    }

    public sealed class RevisionDocumentosInspectorFilaViewModel
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Operador { get; set; }
        public string Estado { get; set; }
        public DateTime FechaEstado { get; set; }
        public string UrlDetalle { get; set; }
    }

    public sealed class RevisionDocumentosInspectorViewModel
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Operador { get; set; }
        public string EstadoProceso { get; set; }
        public long VersionExpediente { get; set; }
        public bool PuedeFinalizarYEnviar { get; set; }
        public int AocrPdfId { get; set; }
        public int CondicionesPdfId { get; set; }
        public string ClaveIdempotenciaEnvio { get; set; }
        public AocrInspectorViewModel Aocr { get; set; }
        public CondicionesInspectorViewModel Condiciones { get; set; }
        public IList<ObservacionDocumentoViewModel> Observaciones { get; set; } = new List<ObservacionDocumentoViewModel>();
    }

    public sealed class AocrInspectorViewModel
    {
        public int DocumentoId { get; set; }
        public int Version { get; set; }
        public string Estado { get; set; }
        public string Numero { get; set; }
        public string Operador { get; set; }
        public string Pais { get; set; }
        public string TipoTramite { get; set; }
        public string EstadoExplotador { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public string Aeronaves { get; set; }
        public string Aeropuertos { get; set; }
        public bool Editable { get; set; }
        public bool PdfExiste { get; set; }
        public IList<VersionDocumentoViewModel> Versiones { get; set; } = new List<VersionDocumentoViewModel>();
    }

    public sealed class CondicionesInspectorViewModel
    {
        public int DocumentoId { get; set; }
        public int Version { get; set; }
        public string Estado { get; set; }
        public string Operador { get; set; }
        public string Aeronaves { get; set; }
        public string Modelos { get; set; }
        public string Aeropuertos { get; set; }
        public string Rutas { get; set; }
        public string Limitaciones { get; set; }
        public string Condiciones { get; set; }
        public bool Editable { get; set; }
        public bool PdfExiste { get; set; }
        public IList<VersionDocumentoViewModel> Versiones { get; set; } = new List<VersionDocumentoViewModel>();
    }

    public sealed class ObservacionDocumentoViewModel
    {
        public int ObservacionId { get; set; }
        public string TipoDocumento { get; set; }
        public string Seccion { get; set; }
        public string Campo { get; set; }
        public string Observacion { get; set; }
        public string Estado { get; set; }
        public int DocumentoOrigenId { get; set; }
        public int VersionOrigen { get; set; }
        public int PdfOrigenId { get; set; }
        public int DocumentoCorreccionId { get; set; }
        public int VersionCorreccion { get; set; }
        public DateTime Fecha { get; set; }
        public string Usuario { get; set; }
        public string Rol { get; set; }
    }

    public sealed class VersionDocumentoViewModel
    {
        public int DocumentoId { get; set; }
        public int Version { get; set; }
        public string Estado { get; set; }
        public DateTime Fecha { get; set; }
        public bool Vigente { get; set; }
        public string UrlPdf { get; set; }
    }

    public sealed class GuardarAocrInspectorRequest
    {
        public int SolicitudId { get; set; }
        public int DocumentoId { get; set; }
        public int VersionEsperada { get; set; }
        public string EstadoExplotador { get; set; }
        public DateTime? FechaVencimiento { get; set; }
    }

    public sealed class GuardarCondicionesInspectorRequest
    {
        public int SolicitudId { get; set; }
        public int DocumentoId { get; set; }
        public int VersionEsperada { get; set; }
        public string Limitaciones { get; set; }
        public string Condiciones { get; set; }
    }

    public sealed class RevisionDocumentosOperacionResult
    {
        public bool Ok { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public string Ruta { get; set; }
        public string Hash { get; set; }
        public long Tamanio { get; set; }
        public byte[] Contenido { get; set; }
    }
}
