using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CapaDatos.Models;

namespace CapaPresentacion.Models
{
    public class OrdenRecaudacionNuevaVM
    {
        public OrdenRecaudacionModel Orden { get; set; }

        // Este string es el que llenas con JSON desde la vista
        public string DetallesJson { get; set; }

        // Lista para pintar el combo (con Valor y %Admin reales)
        public List<ConceptoOptionVM> Conceptos { get; set; }

        public OrdenRecaudacionNuevaVM()
        {
            Orden = new OrdenRecaudacionModel();
            Conceptos = new List<ConceptoOptionVM>();
        }
    }

    public class ConceptoOptionVM
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public decimal Valor { get; set; }
        public decimal PorcentajeAdmin { get; set; }

        public string Label
        {
            get { return (Codigo ?? "") + " - " + (Nombre ?? ""); }
        }
    }

    // Esto es lo mínimo que debe venir en DetallesJson
    public class OrdenDetallePostVM
    {
        public int ConceptoId { get; set; }
        public decimal Cantidad { get; set; }
    }
}
