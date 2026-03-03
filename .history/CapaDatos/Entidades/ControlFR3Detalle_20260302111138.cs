using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa el detalle de un control FR3 (tabla aocr_control_fr3_detalle)
    /// Migrada desde SistemaGestionCalidad (tabla OPCAR6 en DB2/AS400) a PostgreSQL
    /// </summary>
    [Table("aocr_control_fr3_detalle")]
    public class ControlFR3Detalle
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("control_fr3_id")]
        public int ControlFR3Id { get; set; }

        [Column("secuencial")]
        public decimal Secuencial { get; set; }

        [Column("aeropuerto")]
        [StringLength(10)]
        public string Aeropuerto { get; set; }

        [Column("anio")]
        [StringLength(4)]
        public string Anio { get; set; }

        [Column("secuencial_detalle")]
        public decimal SecuencialDetalle { get; set; }

        [Column("tipo_cobro")]
        [StringLength(20)]
        public string TipoCobro { get; set; }

        [Column("oid_formulario")]
        public decimal OidFormulario { get; set; }

        [Column("codigo_contable")]
        [StringLength(50)]
        public string CodigoContable { get; set; }

        [Column("descripcion")]
        [StringLength(500)]
        public string Descripcion { get; set; }

        [Column("cantidad")]
        public decimal Cantidad { get; set; }

        [Column("valor")]
        public decimal Valor { get; set; }

        [Column("hacer_descuento")]
        [StringLength(5)]
        public string HacerDescuento { get; set; }

        [Column("cobrar_impuesto")]
        [StringLength(5)]
        public string CobrarImpuesto { get; set; }

        [Column("ingresar_cantidad")]
        [StringLength(5)]
        public string IngresarCantidad { get; set; }

        [Column("descripcion_cuenta")]
        [StringLength(200)]
        public string DescripcionCuenta { get; set; }

        [Column("codigo")]
        [StringLength(20)]
        public string Codigo { get; set; }

        [Column("total")]
        public decimal Total { get; set; }

        // =============================
        // Campos de auditoría PostgreSQL
        // =============================
        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Column("activo")]
        public bool Activo { get; set; } = true;

        // Navegación
        [ForeignKey("ControlFR3Id")]
        public virtual ControlFR3 ControlFR3 { get; set; }

        /// <summary>
        /// Valida integridad básica del detalle
        /// </summary>
        public bool EsValido()
        {
            return !string.IsNullOrWhiteSpace(Aeropuerto)
                && !string.IsNullOrWhiteSpace(CodigoContable)
                && Cantidad >= 0
                && Valor >= 0;
        }
    }
}
