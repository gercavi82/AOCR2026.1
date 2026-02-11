using System;

namespace CapaModelo
{
    public class DocumentoModel
    {
        public int Id { get; set; }
        public int SolicitudRtId { get; set; }
        public string Tipo { get; set; }
        public string NombreArchivo { get; set; }
        public string RutaStorage { get; set; }
        public long TamanoBytes { get; set; }
        public string HashSha256 { get; set; }
        public DateTime CreatedAt { get; set; }
    }
