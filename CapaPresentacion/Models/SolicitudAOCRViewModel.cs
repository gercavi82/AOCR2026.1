using System;
using System.Collections.Generic;
using System.Web;
using CapaModelo;
using Newtonsoft.Json;

namespace CapaPresentacion.Models
{
    public class SolicitudAOCRViewModel
    {
        public SolicitudAOCR Solicitud { get; set; } = new SolicitudAOCR();

        // OJO: usa Aeronave (si tu modelo actual es Aeronave) o AeronaveSolicitud (si usas la tabla aocr_tbaeronave_solicitud)
        public List<AeronaveSolicitud> Aeronaves { get; set; } = new List<AeronaveSolicitud>();

        public List<Documento> DocumentosExistentes { get; set; } = new List<Documento>();

        // Comprobante (va en aocr_tbpago)
        public string Banco { get; set; }
        public string NumeroComprobante { get; set; }

        // Uploads - En JSON esto sera null, pero se puede ignorar
        [JsonIgnore]
        public IEnumerable<HttpPostedFileBase> ArchivosSubidos { get; set; }

        // Metadatos de documentos enviados desde UI (tipo/si obligatorio/concepto)
        public List<DocumentoCargaVM> DocumentosCarga { get; set; } = new List<DocumentoCargaVM>();

        // Catálogo de compañías para selector en el wizard
        public List<CompaniaCatalogoVM> CompaniasDisponibles { get; set; } = new List<CompaniaCatalogoVM>();

        // Datos pre-resueltos para la vista de Solicitud AOCR (evita usar username por error)
        public string NombreRepresentanteTecnico { get; set; }
        public string IdentificacionUsuario { get; set; }
        public string CompaniaActivaCodigo { get; set; }
        public string CompaniaActivaNombre { get; set; }
        public bool EsModoSubsanacionObservada { get; set; }
        public List<string> DocumentosPendientesSubsanacionInputIds { get; set; } = new List<string>();
        public List<string> DocumentosPendientesSubsanacionEtiquetas { get; set; } = new List<string>();
        public List<string> DocumentosPendientesSubsanacionNoGestionables { get; set; } = new List<string>();

        // Usuario logueado - En JSON esto sera null, pero se puede ignorar
        [JsonIgnore]
        public Usuario Usuario { get; set; }
    }

    public class DocumentoCargaVM
    {
        public string InputId { get; set; }
        public string TipoDocumento { get; set; }
        public string Concepto { get; set; }
        public bool Obligatorio { get; set; }
    }

    public class CompaniaCatalogoVM
    {
        public string CodigoOaci { get; set; }
        public string CodigoIata { get; set; }
        public string CodigoNumeroCia { get; set; }
        public string Nombre { get; set; }
    }

    public class SubsanacionViewModel
    {
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public DateTime? FechaSolicitud { get; set; }
        public string Estado { get; set; }
        public string InspectorNombre { get; set; }

        public string ObservacionesInspector { get; set; }
        public List<HistorialObservacionVM> HistorialObservaciones { get; set; } = new List<HistorialObservacionVM>();
        public List<DocumentoSubsanacionVM> DocumentosObservados { get; set; } = new List<DocumentoSubsanacionVM>();
        public List<DocumentoSubsanacionVM> DocumentosBloqueados { get; set; } = new List<DocumentoSubsanacionVM>();
    }

    public class HistorialObservacionVM
    {
        public DateTime? Fecha { get; set; }
        public string Observacion { get; set; }
        public string Usuario { get; set; }
    }

    public class DocumentoSubsanacionVM
    {
        public int CodigoDocumento { get; set; }
        public string TipoDocumento { get; set; }
        public string NombreArchivo { get; set; }
        public string Estado { get; set; }
        public string Observaciones { get; set; }
        public DateTime? FechaCarga { get; set; }
        public int? Version { get; set; }
        public bool PuedeSubsanar { get; set; }
        public bool EsBloqueado { get; set; }
    }
}
