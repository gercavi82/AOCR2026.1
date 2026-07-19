using System;

namespace CapaModelo.OrdenRecaudacion
{
    public class OrdenRecaudacionDetalle
    {
        public int Id { get; set; }                 // aocr_or_orden_detalle.id
        public int OrdenId { get; set; }            // orden_id
        public int ConceptoId { get; set; }         // concepto_id
        public string ConceptoCodigo { get; set; }
        public string ConceptoNombre { get; set; }
        public string Descripcion { get; set; }

        public decimal Cantidad { get; set; } = 1;
        public decimal ValorUnitario { get; set; } = 0;
        public decimal PorcentajeAdmin { get; set; } = 0;

        public decimal Subtotal { get; set; } = 0;
        public decimal Admin { get; set; }
        public decimal TotalLinea { get; set; }

        public string LugarInspeccion { get; set; }
        public string ProvinciaInspeccion { get; set; }
    }
}
