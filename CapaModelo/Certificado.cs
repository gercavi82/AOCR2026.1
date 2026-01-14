using System; // Soluciona el error CS0246 (DateTime)
using System.ComponentModel.DataAnnotations; // Necesario para seguridad y validación

namespace CapaModelo
{
    /// <summary>
    /// Modelo de datos para el Certificado de Operador Aéreo (AOCR).
    /// Incluye validaciones para integridad de datos en producción.
    /// </summary>
    public class Certificado
    {
        // ==========================================
        // IDENTIFICADORES
        // ==========================================
        [Key]
        public int CodigoCertificado { get; set; }

        [Required(ErrorMessage = "El código de solicitud es obligatorio.")]
        public int CodigoSolicitud { get; set; }

        [Required(ErrorMessage = "El número de certificado es obligatorio.")]
        [StringLength(50, ErrorMessage = "El número no puede exceder 50 caracteres.")]
        [Display(Name = "N° Certificado")]
        public string NumeroCertificado { get; set; }

        // ==========================================
        // VIGENCIA
        // ==========================================
        [Required]
        [Display(Name = "Fecha de Emisión")]
        [DataType(DataType.Date)]
        public DateTime FechaEmision { get; set; }

        [Required]
        [Display(Name = "Fecha de Vencimiento")]
        [DataType(DataType.Date)]
        public DateTime FechaVencimiento { get; set; }

        [Range(1, 10, ErrorMessage = "La vigencia debe ser entre 1 y 10 años.")]
        public int VigenciaAnios { get; set; } = 2; // Valor por defecto

        // ==========================================
        // ESTADO Y DETALLES
        // ==========================================
        [Required]
        [StringLength(20)]
        public string Estado { get; set; } // EMITIDO, REVOCADO, SUSPENDIDO

        [DataType(DataType.MultilineText)]
        [Display(Name = "Condiciones Especiales")]
        public string CondicionesEspeciales { get; set; }

        // ==========================================
        // SEGURIDAD Y FIRMA DIGITAL
        // ==========================================
        [Required(ErrorMessage = "La firma de la autoridad es obligatoria.")]
        [StringLength(100)]
        [Display(Name = "Firmado Por")]
        public string FirmadoPor { get; set; } // Nombre de la autoridad (Director General)

        [StringLength(255)]
        public string RutaPdf { get; set; } // Ubicación física del archivo generado

        [Required]
        [StringLength(64)] // Longitud típica de un hash SHA-256 o UUID
        public string CodigoVerificacion { get; set; } // Para el código QR

        // ==========================================
        // AUDITORÍA (Opcional pero recomendado)
        // ==========================================
        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }

        // Constructor para evitar nulos en cadenas
        public Certificado()
        {
            Estado = "EMITIDO";
            CondicionesEspeciales = "Ninguna";
        }
    }
}