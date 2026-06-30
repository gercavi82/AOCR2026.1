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
    }
}
