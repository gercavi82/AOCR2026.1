using System.ComponentModel.DataAnnotations;

namespace CapaDatos.Models
{
    public class ContribuyenteModel
    {
        public int IdContribuyente { get; set; }

        [Required(ErrorMessage = "El tipo de identificación es requerido")]
        [StringLength(2)]
        public string TipoIdentificacion { get; set; } // C: Cédula, R: RUC, P: Pasaporte

        [Required(ErrorMessage = "El número de identificación es requerido")]
        [StringLength(20)]
        public string NumeroIdentificacion { get; set; }

        [Required(ErrorMessage = "El nombre o razón social es requerido")]
        [StringLength(200)]
        public string NombreRazonSocial { get; set; }

        [StringLength(200)]
        public string NombreComercial { get; set; }

        [EmailAddress(ErrorMessage = "El formato del correo es inválido")]
        [StringLength(100)]
        public string Email { get; set; }

        [Phone(ErrorMessage = "El formato del teléfono es inválido")]
        [StringLength(20)]
        public string Telefono { get; set; }

        [StringLength(200)]
        public string Direccion { get; set; }

        public bool Activo { get; set; }

        public string IdentificacionCompleta => $"{TipoIdentificacion}-{NumeroIdentificacion}";
    }
}