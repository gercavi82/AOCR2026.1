using System;

namespace CapaModelo
{
    public class AocrFirmaDocumento
    {
        public int CodigoFirma { get; set; }
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public string TipoDocumento { get; set; }
        public string NumeroAocr { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaDocumento { get; set; }
        public string HashDocumento { get; set; }
        public string CodigoQr { get; set; }
        public string SujetoCertificado { get; set; }
        public string NombreFirmante { get; set; }
        public string CargoFirmante { get; set; }
        public DateTime FechaFirma { get; set; }
        public int? CodigoUsuario { get; set; }
        public string UsuarioNombre { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
