using System;

namespace CapaModelo
{
    public class AocrDocumentoGenerado
    {
        public int CodigoDocumento { get; set; }
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public string TipoDocumento { get; set; }
        public string NumeroAocr { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaDocumento { get; set; }
        public long? TamanioPdf { get; set; }
        public string Estado { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public int? CodigoUsuario { get; set; }
        public string UsuarioNombre { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string HashPdfFirmado { get; set; }
        public long? TamanioPdfFirmado { get; set; }
        public DateTime? FechaLiberacion { get; set; }
        public bool DisponibleRt { get; set; }
        public DateTime? FechaDisponibleRt { get; set; }
        public int VersionDocumento { get; set; }
        public bool Vigente { get; set; }
        public bool Completo { get; set; }
        public bool Bloqueado { get; set; }
        public string HashPdf { get; set; }
        public string RutaPdfFirmado { get; set; }
        public int? CodigoUsuarioFirma { get; set; }
        public string RolFirma { get; set; }
        public DateTime? FechaFirma { get; set; }
        public long VersionConcurrencia { get; set; }
    }
}
