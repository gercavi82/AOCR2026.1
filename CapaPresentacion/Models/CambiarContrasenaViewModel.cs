using System.ComponentModel.DataAnnotations;

namespace CapaPresentacion.Models
{
    public class CambiarContrasenaViewModel
    {
        [Required(ErrorMessage = "Ingrese la contraseña actual.")]
        [DataType(DataType.Password)]
        public string ContrasenaActual { get; set; }

        [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
        [DataType(DataType.Password)]
        public string NuevaContrasena { get; set; }

        [Required(ErrorMessage = "Confirme la nueva contraseña.")]
        [DataType(DataType.Password)]
        public string ConfirmarContrasena { get; set; }
    }
}
