using System;

namespace CapaModelo
{
    public class PagoModel
    {
        public int Id { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroFactura { get; set; }
        public decimal Monto { get; set; }
        public string Moneda { get; set; }
        public string MetodoPago { get; set; }
        public string Estado { get; set; }
        public DateTime FechaPago { get; set; }
        public string Observaciones { get; set; }
        public string ComprobanteRuta { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string UsuarioRegistro { get; set; }
    }
}
