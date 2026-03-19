using System.ComponentModel.DataAnnotations;

namespace CapaModelo
{
    public class Tecnico
    {
        public int CodigoTecnico { get; set; }
        public string NombreCompleto { get; set; }
        public string Especialidad { get; set; }

        [StringLength(15, ErrorMessage = "Máximo 15 dígitos")]
        [RegularExpression(@"^\d{0,15}$", ErrorMessage = "El teléfono solo debe contener números")]
        public string Telefono { get; set; }

        public string Email { get; set; }
        public bool Activo { get; set; }
    }
}
