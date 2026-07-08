using System;

namespace CapaDatos.Models
{
    public class OrdenDetalleModel
    {
        public int Id { get; set; }
        public int OrdenId { get; set; }
        public int ConceptoId { get; set; }
        public string ConceptoCodigo { get; set; }
        public string ConceptoNombre { get; set; }
        public string Descripcion { get; set; }

        public decimal Cantidad { get; set; }
        public decimal ValorUnitario { get; set; }
        public decimal PorcentajeAdmin { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Admin { get; set; }
        public decimal TotalLinea { get; set; }

        public string LugarInspeccion { get; set; }
        public string ProvinciaInspeccion { get; set; }
        public bool InspeccionExtObligatoria { get; set; }
        public bool ViaticosInspectorObligatorios { get; set; }
        public int? UsuarioReglaInspeccion { get; set; }
        public DateTime? FechaReglaInspeccion { get; set; }
    }
}
