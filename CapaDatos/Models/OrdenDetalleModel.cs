using System.ComponentModel.DataAnnotations;

namespace CapaDatos.Models
{
    public class OrdenDetalleModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El ID de orden es requerido")]
        public int OrdenId { get; set; }

        [Required(ErrorMessage = "El ID de concepto es requerido")]
        public int ConceptoId { get; set; }

        [Required(ErrorMessage = "El código de concepto es requerido")]
        [StringLength(60)]
        public string ConceptoCodigo { get; set; }

        [Required(ErrorMessage = "El nombre de concepto es requerido")]
        [StringLength(200)]
        public string ConceptoNombre { get; set; }

        public string Descripcion { get; set; }

        [Required(ErrorMessage = "La cantidad es requerida")]
        [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
        public decimal Cantidad { get; set; } = 1;

        [Required(ErrorMessage = "El valor unitario es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El valor unitario debe ser mayor o igual a 0")]
        public decimal ValorUnitario { get; set; }

        [Required(ErrorMessage = "El porcentaje de administración es requerido")]
        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100")]
        public decimal PorcentajeAdmin { get; set; }

        [Required(ErrorMessage = "El subtotal es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El subtotal debe ser mayor o igual a 0")]
        public decimal Subtotal { get; set; }

        [Required(ErrorMessage = "El valor de administración es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El valor de administración debe ser mayor o igual a 0")]
        public decimal Admin { get; set; }

        [Required(ErrorMessage = "El total de línea es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El total de línea debe ser mayor o igual a 0")]
        public decimal TotalLinea { get; set; }

        // Propiedades de navegación
        public OrdenRecaudacionModel Orden { get; set; }
        public ConceptoModel Concepto { get; set; }

        // Métodos de cálculo
        public void CalcularTotales()
        {
            Subtotal = Cantidad * ValorUnitario;
            Admin = Subtotal * (PorcentajeAdmin / 100);
            TotalLinea = Subtotal + Admin;
        }
    }
}