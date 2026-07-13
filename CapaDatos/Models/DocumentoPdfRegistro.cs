using System;

namespace CapaDatos.Models
{
    public sealed class DocumentoPdfRegistro
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public int DocumentoOrigenId { get; set; }
        public string TipoDocumento { get; set; }
        public int Version { get; set; }
        public string Estado { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaLogica { get; set; }
        public string MimeType { get; set; }
        public long TamanoBytes { get; set; }
        public string HashSha256 { get; set; }
        public bool Vigente { get; set; }
        public bool Firmado { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public int UsuarioGeneradorId { get; set; }
        public DateTime? FechaFirma { get; set; }
        public int? UsuarioFirmaId { get; set; }
        public int VersionRegistro { get; set; }
        public string CodigoCompania { get; set; }
        public string ObservacionTecnica { get; set; }
    }

    public sealed class DocumentoPdfOrigenValidacion
    {
        public bool Existe { get; set; }
        public bool SolicitudActiva { get; set; }
        public bool InspectorAsignado { get; set; }
        public bool Firmado { get; set; }
        public string Estado { get; set; }
        public int Version { get; set; }
        public string CodigoCompania { get; set; }
    }
}
