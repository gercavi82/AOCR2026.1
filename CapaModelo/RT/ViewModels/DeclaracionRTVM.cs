using System.ComponentModel.DataAnnotations;

namespace CapaModelo.RT.ViewModels
{
    public class DeclaracionRTVM
    {
        [Required]
        public int SolicitudId { get; set; }

        [Required]
        public string TextoDeclaracion { get; set; }

        [Required(ErrorMessage = "Debe aceptar la declaración de responsabilidad")]
        public bool Acepto { get; set; }

        public string Estado { get; set; }
    }
}
