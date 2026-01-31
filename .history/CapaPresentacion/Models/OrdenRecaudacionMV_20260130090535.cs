using System;
using System.ComponentModel.DataAnnotations;

namespace CapaPresentacion.Models.ViewModels
{
    public class OrdenRecaudacionViewModel
    {
        public int Id { get; set; } // ✅ AGREGAR ESTA PROPIEDAD

        [Required(ErrorMessage = "El código de solicitud es requerido")]
        [Display(Name = "Código de Solicitud")]
        public string CodigoSolicitud { get; set; }

        [Required(ErrorMessage = "El concepto es requerido")]
        [Display(Name = "Concepto")]
        public int ConceptoId { get; set; }

        [Display(Name = "Código del Concepto Principal")]
        public string ConceptoPrincipalCodigo { get; set; }

        [Required(ErrorMessage = "El número de estaciones es requerido")]
        [Range(0, 50, ErrorMessage = "Las estaciones deben estar entre 0 y 50")]
        [Display(Name = "Número de Estaciones")]
        public int Estaciones { get; set; }

        [Required(ErrorMessage = "El número de días es requerido")]
        [Range(0, 30, ErrorMessage = "Los días deben estar entre 0 y 30")]
        [Display(Name = "Número de Días")]
        public int Dias { get; set; }

        [Display(Name = "Observaciones")]
        [StringLength(500, ErrorMessage = "Las observaciones no pueden exceder los 500 caracteres")]
        public string Observacion { get; set; }

        // ✅ AGREGAR ESTAS PROPIEDADES PARA DETALLE:
        [Display(Name = "Número de Orden")]
        public string NumeroOrden { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; }

        [Display(Name = "Estado")]
        public string Estado { get; set; }

        [Display(Name = "Subtotal")]
        public decimal Subtotal { get; set; }

        [Display(Name = "Administración")]
        public decimal Admin { get; set; }

        [Display(Name = "Total")]
        public decimal Total { get; set; }

        // Propiedades para mostrar en la vista
        [Display(Name = "Valor Base")]
        public decimal ValorBase { get; set; }

        [Display(Name = "Inspección")]
        public decimal Inspeccion { get; set; }

        [Display(Name = "Viáticos")]
        public decimal Viaticos { get; set; }

        [Display(Name = "Gastos Administrativos")]
        public decimal GastosAdmin { get; set; }
    }
}