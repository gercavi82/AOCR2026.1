using System;
using System.ComponentModel.DataAnnotations;


namespace CapaDatos.Models
{
    public class DocumentoModel
    {
        public int CodigoDocumento { get; set; }

        [Required(ErrorMessage = "El código de solicitud es requerido")]
        public int CodigoSolicitud { get; set; }

        [Required(ErrorMessage = "El tipo de documento es requerido")]
        [StringLength(100)]
        public string TipoDocumento { get; set; }

        [Required(ErrorMessage = "El nombre del archivo es requerido")]
        [StringLength(255)]
        public string NombreArchivo { get; set; }

        [StringLength(500)]
        public string RutaGuardada { get; set; }

        [StringLength(100)]
        public string Tipo { get; set; }

        [StringLength(500)]
        public string HashArchivo { get; set; }

        public long? TamanoBytes { get; set; }

        [StringLength(20)]
        public string Extension { get; set; }

        [StringLength(50)]
        public string Estado { get; set; } = "Cargado";

        public bool? Validado { get; set; } = false;

        public DateTime? FechaCarga { get; set; } = DateTime.Now;
        public DateTime? FechaValidacion { get; set; }

        [StringLength(100)]
        public string ValidadoPor { get; set; }

        public string Observaciones { get; set; }

        public int? Version { get; set; } = 1;
        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string CreatedBy { get; set; }

        // Propiedad para manejo de archivos
        public System.Web.HttpPostedFileBase Archivo { get; set; }

        // Propiedades calculadas
        public string TamanoFormateado
        {
            get
            {
                if (!TamanoBytes.HasValue) return "N/A";

                long bytes = TamanoBytes.Value;
                if (bytes < 1024) return $"{bytes} bytes";
                if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
                return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
            }
        }

        // Propiedades alias para compatibilidad
        public string TipoArchivo => Extension;
        public string Descripcion => Observaciones;
        public string RutaArchivo => RutaGuardada;
    }
}