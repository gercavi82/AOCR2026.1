using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa un concepto de orden de recaudación (tabla aocr_or_concepto)
    /// </summary>
    [Table("aocr_or_concepto")]
    public class OrdenRecaudacionConcepto
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("codigo")]
        [StringLength(50)]
        public string Codigo { get; set; }

        [Column("nombre")]
        [StringLength(200)]
        public string Nombre { get; set; }

        [Column("tipo_calculo")]
        [StringLength(50)]
        public string TipoCalculo { get; set; }

        [Column("valor_base")]
        public decimal? ValorBase { get; set; }

        [Column("porcentaje_admin")]
        public decimal? PorcentajeAdmin { get; set; }

        [Column("activo")]
        public bool Activo { get; set; }

        [Column("orden")]
        public int? Orden { get; set; }

        [Column("descripcion")]
        [StringLength(500)]
        public string Descripcion { get; set; }

        [Column("por_estacion")]
        public bool PorEstacion { get; set; }

        [Column("por_dia")]
        public bool PorDia { get; set; }

        [Column("es_viatico")]
        public bool EsViatico { get; set; }
    }
}
