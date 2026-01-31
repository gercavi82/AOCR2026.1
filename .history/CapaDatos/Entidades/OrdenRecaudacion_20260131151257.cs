using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa una orden de recaudación (tabla aocr_or_orden)
    /// </summary>
    [Table("aocr_or_orden")]
    public class OrdenRecaudacion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("codigo_usuario")]
        [StringLength(50)]
        public string CodigoUsuario { get; set; }

        [Column("codigo_solicitud")]
        [StringLength(50)]
        public string CodigoSolicitud { get; set; }

        [Column("numero_orden")]
        [StringLength(50)]
        public string NumeroOrden { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("estado")]
        [StringLength(50)]
        public string Estado { get; set; }

        [Column("observacion")]
        [StringLength(500)]
        public string Observacion { get; set; }

        [Column("subtotal")]
        public decimal? Subtotal { get; set; }

        [Column("admin")]
        public decimal? Admin { get; set; }

        [Column("total")]
        public decimal? Total { get; set; }

        [Column("lugar_emision")]
        [StringLength(200)]
        public string LugarEmision { get; set; }

        [Column("compania")]
        [StringLength(200)]
        public string Compania { get; set; }

        [Column("ruc_cedula")]
        [StringLength(20)]
        public string RucCedula { get; set; }

        [Column("correo")]
        [StringLength(100)]
        public string Correo { get; set; }

        [Column("telefono")]
        [StringLength(20)]
        public string Telefono { get; set; }

        [Column("concepto_id")]
        public int? ConceptoId { get; set; }

        // Propiedad de navegación para el concepto (si se usa Entity Framework)
        [ForeignKey("ConceptoId")]
        public virtual OrdenRecaudacionConcepto Concepto { get; set; }

        // Propiedad calculada para obtener el nombre del concepto
        [NotMapped]
        public string ConceptoNombre
        {
            get
            {
                if (Concepto != null)
                {
                    return Concepto.Nombre;
                }
                return null;
            }
        }

        public OrdenRecaudacion()
        {
            Detalles = new List<DetalleOrden>();
            Pagos = new List<Pago>();
            Activo = true;
            FechaCreacion = DateTime.Now;
        }
    }
}
