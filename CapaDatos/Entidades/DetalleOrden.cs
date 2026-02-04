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

        /// <summary>
        /// Alias para ValorUnitario (compatibilidad con código existente)
        /// </summary>
        [NotMapped]
        public decimal PrecioUnitario
        {
            get { return ValorUnitario; }
            set { ValorUnitario = value; }
        }

        [Column("porcentaje_admin")]
        [Required]
        public decimal PorcentajeAdmin { get; set; }

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("admin")]
        public decimal Admin { get; set; }

        [Column("total_linea")]
        public decimal TotalLinea { get; set; }

        /// <summary>
        /// Alias para TotalLinea (compatibilidad)
        /// </summary>
        [NotMapped]
        public decimal Total
        {
            get { return TotalLinea; }
            set { TotalLinea = value; }
        }

        /// <summary>
        /// Valida la integridad básica del detalle
        /// </summary>
        public bool EsValido()
        {
            return ConceptoId.HasValue
                   && Cantidad > 0
                   && ValorUnitario >= 0
                   && Subtotal >= 0
                   && TotalLinea >= 0;
        }

        // Navegación
        [ForeignKey("OrdenId")]
        public virtual OrdenRecaudacion Orden { get; set; }
    }
}
