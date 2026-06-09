using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace CapaPresentacion.Models
{
    public class CorreoPruebaViewModel
    {
        public CorreoPruebaViewModel()
        {
            PlantillasDisponibles = new List<SelectListItem>();
        }

        [Required]
        [Display(Name = "Plantilla de correo")]
        public string Plantilla { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Correo destino")]
        public string CorreoDestino { get; set; }

        [Display(Name = "Nombre destino")]
        public string NombreDestino { get; set; }

        [Display(Name = "Solicitud AOCR")]
        public int? SolicitudId { get; set; }

        [Display(Name = "Inspección")]
        public int? InspeccionId { get; set; }

        [Display(Name = "Orden de recaudación")]
        public int? OrdenId { get; set; }

        [Display(Name = "Observación")]
        public string Observacion { get; set; }

        public bool? ResultadoExitoso { get; set; }
        public string ResultadoMensaje { get; set; }
        public IEnumerable<SelectListItem> PlantillasDisponibles { get; set; }
    }
}