using System;
using System.Collections.Generic;
using System.IO;

namespace CapaNegocio.DTOs.DocumentosPdf
{
    public sealed class GenerarPdfRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public int DocumentoOrigenId { get; set; }
        public string TipoDocumento { get; set; }
        public int VersionOrigen { get; set; }
        public int VersionRegistroEsperada { get; set; }
        public int UsuarioId { get; set; }
        public string Rol { get; set; }
        public string EstadoEsperado { get; set; }
        public string CodigoCompania { get; set; }
        public IList<string> CamposFaltantes { get; set; } = new List<string>();
        public Func<byte[]> Generador { get; set; }
        public string CorrelationId { get; set; }
        public string Ip { get; set; }
    }

    public sealed class ResultadoGeneracionPdf
    {
        public bool Exitoso { get; set; }
        public bool YaProcesado { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public DocumentoPdfDto Documento { get; set; }
    }

    public sealed class DocumentoPdfDto
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
        public bool Eliminado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        public int VersionRegistro { get; set; }
        public string CodigoCompania { get; set; }
    }

    public sealed class ResultadoValidacionPdf
    {
        public bool Valido { get; set; }
        public int Codigo { get; set; }
        public string Mensaje { get; set; }
        public string HashCalculado { get; set; }
        public long TamanoCalculado { get; set; }
    }

    public sealed class ArchivoPdfAutorizado : IDisposable
    {
        public DocumentoPdfDto Documento { get; set; }
        public Stream Contenido { get; set; }
        public void Dispose(){if(Contenido!=null)Contenido.Dispose();}
    }

    public sealed class DocumentoPdfConsistenciaResultado
    {
        public int RegistrosAnalizados { get; set; }
        public int ArchivosAnalizados { get; set; }
        public IList<DocumentoPdfConsistenciaHallazgo> Hallazgos { get; set; } = new List<DocumentoPdfConsistenciaHallazgo>();
        public bool Consistente { get { return Hallazgos.Count == 0; } }
    }

    public sealed class DocumentoPdfConsistenciaHallazgo
    {
        public string Codigo { get; set; }
        public int? DocumentoPdfId { get; set; }
        public string RutaLogica { get; set; }
        public string Detalle { get; set; }
    }
}
