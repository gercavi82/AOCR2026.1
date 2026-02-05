using System;

namespace CapaModelo
{
    public class Parametro
    {
        public int CodigoParametro { get; set; }
        public string Clave { get; set; }
        public string Valor { get; set; }
        public string Descripcion { get; set; }
        public bool Activo { get; set; }

        // Nuevas propiedades del esquema dinámico
        public string CodigoParametroStr { get; set; }
        public decimal? ValorParametro { get; set; }
        public string DescripcionParametro { get; set; }
        public bool? ActivoNuevo { get; set; }
        public DateTime? CreatedAtNuevo { get; set; }
        public DateTime? UpdatedAtNuevo { get; set; }
        public string CreatedByStr { get; set; }
        public string UpdatedByStr { get; set; }
        public DateTime? DeletedAtNuevo { get; set; }
        public string DeletedByStr { get; set; }

        // Propiedades legacy
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }
    }
}
