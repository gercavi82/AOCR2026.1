using System.ComponentModel.DataAnnotations;
using CapaPresentacion.Models.Validation;

namespace CapaPresentacion.Models.RT
{
    public class RegistroRTVM
    {
        public int? SolicitudId { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Razón Social")]
        public string RazonSocial { get; set; }

        [Required]
        [RucCedulaValidation]
        [Display(Name = "RUC")]
        public string Ruc { get; set; }

        [Required]
        [StringLength(15, ErrorMessage = "Máximo 15 dígitos")]
        [RegularExpression(@"^\d{6,15}$", ErrorMessage = "El teléfono solo debe contener números")]
        [Display(Name = "Teléfono")]
        public string Telefono { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(120)]
        [Display(Name = "Email contacto")]
        public string Email { get; set; }

        [Display(Name = "Área contable (JSON)")]
        public string AreaContableJson { get; set; }
    }
}