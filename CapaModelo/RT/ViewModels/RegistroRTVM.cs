using System.ComponentModel.DataAnnotations;

namespace CapaModelo.RT.ViewModels
{
    public class RegistroRTVM
    {
        public int? SolicitudId { get; set; }

        [Required(ErrorMessage = "La razón social es requerida")]
        [StringLength(200)]
        public string RazonSocial { get; set; }

        [Required(ErrorMessage = "El RUC es requerido")]
        [StringLength(20)]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "El RUC debe tener 13 dígitos")]
        public string Ruc { get; set; }

        [Required(ErrorMessage = "El teléfono es requerido")]
        [StringLength(30)]
        public string Telefono { get; set; }

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [StringLength(120)]
        public string Email { get; set; }

        // Pendiente de confirmación: estructura flexible
        public string AreaContableJson { get; set; }
    }
}
