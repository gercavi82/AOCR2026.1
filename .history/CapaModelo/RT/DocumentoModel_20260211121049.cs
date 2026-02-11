using System;
using System.ComponentModel.DataAnnotations;

namespace CapaModelo.RT
{
    public class DocumentoModel
    {
        public int Id { get; set; }

        [Required]
        public int SolicitudRtId { get; set; }

        [Required]
        [StringLength(40)]
        public string Tipo { get; set; }

        [Required]
        [StringLength(255)]
        public string NombreArchivo { get; set; }

        [Required]
        [StringLength(500)]
        public string RutaStorage { get; set; }

        [Required]
        public long TamanoBytes { get; set; }

        [StringLength(64)]
        public string HashSha256 { get; set; }

        [StringLength(120)]
        public string CreatedBy { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
