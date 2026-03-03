using System;

namespace CapaDatos.Models
{
    public class FacturaPagoRegistroModel
    {
        public int OrdenId { get; set; }
        public int? PagoId { get; set; }
        public string NumeroFactura { get; set; }
        public string AutorizacionFactura { get; set; }
        public DateTime FechaEmision { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string Observaciones { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; }
        public string Fr3Estado { get; set; }
        public string Fr3Numero { get; set; }
        public decimal? Fr3Secuencial { get; set; }
        public string Fr3Aeropuerto { get; set; }
        public string Fr3Anio { get; set; }
        public string Fr3Error { get; set; }
    }
}
