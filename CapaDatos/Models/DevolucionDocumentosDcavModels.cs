using System;

namespace CapaDatos.Models
{
    public sealed class ObservacionDocumentoDcavRegistro
    {
        public int ObservacionId { get; set; }
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string TipoDocumento { get; set; }
        public int DocumentoOrigenId { get; set; }
        public int VersionOrigen { get; set; }
        public int PdfOrigenId { get; set; }
        public string Seccion { get; set; }
        public string Campo { get; set; }
        public string Texto { get; set; }
        public int UsuarioDcavId { get; set; }
        public string UsuarioDcav { get; set; }
        public string RolDcav { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; }
        public int DocumentoCorreccionId { get; set; }
        public int VersionCorreccion { get; set; }
        public string CodigoCompania { get; set; }
    }

    public sealed class VersionCorreccionDcavRegistro
    {
        public int DocumentoId { get; set; }
        public int Version { get; set; }
    }
}
