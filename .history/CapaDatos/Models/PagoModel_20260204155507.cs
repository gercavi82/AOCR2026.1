using System;

namespace CapaDatos.Models
{
    public class PagoModel
    {
        public int CodigoPago { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroFactura { get; set; }
        public decimal Monto { get; set; }
        public string Moneda { get; set; }
        public string Concepto { get; set; }
        public string MetodoPago { get; set; }
        public string Banco { get; set; }
        public string Estado { get; set; }
        public DateTime? FechaPago { get; set; }
        public DateTime? FechaValidacion { get; set; }
        public string ValidadoPor { get; set; }
        public string Observaciones { get; set; }
        public string ComprobanteRuta { get; set; }
    }
}
