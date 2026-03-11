using System;

namespace CapaModelo
{
    public class DocumentoInspeccion
    {
        public int CodigoDocumento { get; set; }
        public int CodigoInspeccion { get; set; }
        public int? CodigoInforme { get; set; }
        public int? CodigoDocumentoBase { get; set; }
        public int Version { get; set; }
        public string TipoDocumento { get; set; }
        public string NombreArchivoOriginal { get; set; }
        public string NombreArchivoStorage { get; set; }
        public string RutaArchivo { get; set; }
        public string HashArchivo { get; set; }
        public long? TamanoBytes { get; set; }
        public string ContentType { get; set; }
        public string Observacion { get; set; }
        public string SubidoPorRol { get; set; }
        public int? CodigoUsuario { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}