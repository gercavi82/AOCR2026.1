using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa un detalle de orden de recaudación (tabla aocr_or_orden_detalle)
    /// </summary>
    [Table("aocr_or_orden_detalle")]
    public class DetalleOrden
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("orden_id")]
        public int OrdenId { get; set; }

        [Column("concepto_id")]
        public int? ConceptoId { get; set; }

        [Column("concepto_codigo")]
        [StringLength(50)]
        public string ConceptoCodigo { get; set; }

        [Column("concepto_nombre")]
        [StringLength(200)]
        public string ConceptoNombre { get; set; }

        [Column("descripcion")]
        [StringLength(500)]
        public string Descripcion { get; set; }

        [Column("cantidad")]
        public int Cantidad { get; set; }

        [Column("valor_unitario")]
        public decimal ValorUnitario { get; set; }

        [Column("porcentaje_admin")]
        public decimal? PorcentajeAdmin { get; set; }

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("admin")]
        public decimal? Admin { get; set; }

        [Column("total_linea")]
        public decimal TotalLinea { get; set; }

        // Navegación
        [ForeignKey("OrdenId")]
        public virtual OrdenRecaudacion Orden { get; set; }
    }
}
