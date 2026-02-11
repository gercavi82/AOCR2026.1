using System.ComponentModel.DataAnnotations;

namespace CapaPresentacion.Models.RT
{
    public class DeclaracionRTVM
    {
        public int SolicitudId { get; set; }

        [Display(Name = "Texto de la declaración")]
        public string TextoDeclaracion { get; set; }

        [Required(ErrorMessage = "Debe aceptar la declaración para continuar")]
        [Display(Name = "Acepto")] 
        public bool Acepto { get; set; }
    }
}