using System.ComponentModel.DataAnnotations;

namespace CapaPresentacion.Models.ViewModels
{
    public class OrdenRecaudacionViewModel
    {
        [Required(ErrorMessage = "El concepto es obligatorio.")]
        public string ConceptoPrincipalCodigo { get; set; }

        public int? CodigoSolicitud { get; set; }

        [Range(0, 100, ErrorMessage = "Número de estaciones inválido.")]
        public int Estaciones { get; set; }

        [Range(0, 100, ErrorMessage = "Número de días inválido.")]
        public int Dias { get; set; }

        [StringLength(500, ErrorMessage = "La observación es demasiado larga.")]
        public string Observacion { get; set; }
    }
}