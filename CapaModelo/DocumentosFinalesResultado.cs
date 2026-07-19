using System;
using System.Collections.Generic;

namespace CapaModelo
{
    public sealed class DocumentoFinalEnvioRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public int InspectorId { get; set; }
        public string InspectorNombre { get; set; }
        public long VersionConcurrencia { get; set; }
        public DocumentoFinalEvidencia Aocr { get; set; }
        public DocumentoFinalEvidencia Condiciones { get; set; }
        public string BaseUrl { get; set; }
        public bool RequiereAocr { get; set; } = true;
        public bool RequiereCondiciones { get; set; } = true;
    }

    public sealed class DocumentoFinalEvidencia
    {
        public int DocumentoId { get; set; }
        public int InspeccionId { get; set; }
        public int Version { get; set; }
        public string TipoDocumento { get; set; }
        public string RutaPdf { get; set; }
        public string HashPdf { get; set; }
        public long TamanioPdf { get; set; }
    }

    public sealed class DocumentoFinalFirmaRequest
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string Rol { get; set; }
        public string TipoDocumento { get; set; }
        public string RutaPdfFirmado { get; set; }
        public string HashPdfFirmado { get; set; }
        public long TamanioPdfFirmado { get; set; }
        public string NumeroAocr { get; set; }
        public string NombreArchivo { get; set; }
        public string CodigoQr { get; set; }
        public string SujetoCertificado { get; set; }
        public string NombreFirmante { get; set; }
        public string CargoFirmante { get; set; }
    }

    public sealed class DocumentosFinalesResultado
    {
        public bool Exitoso { get; set; }
        public bool Idempotente { get; set; }
        public bool Finalizado { get; set; }
        public string Mensaje { get; set; }
        public string EstadoExpediente { get; set; }
        public string EstadoAocr { get; set; }
        public string EstadoCondiciones { get; set; }
        public IList<string> Errores { get; set; } = new List<string>();
    }
}
