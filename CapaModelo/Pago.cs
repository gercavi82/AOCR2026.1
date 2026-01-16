using System;

namespace CapaModelo
{
    public class Pago
    {
        public int CodigoPago { get; set; }
        public int CodigoSolicitud { get; set; }

        // En tu sistema a veces le dicen NumeroTransaccion, aquí usamos NumeroFactura
        public string NumeroFactura { get; set; }

        public decimal? Monto { get; set; }
        public string Moneda { get; set; }

        public string Concepto { get; set; }
        public string MetodoPago { get; set; }
        public string Estado { get; set; }

        public DateTime? FechaPago { get; set; }
        public DateTime? FechaValidacion { get; set; }

        // Campo real
        public string ValidadoPor { get; set; }

        // Campo real
        public string Observaciones { get; set; }

        // Campo real (ruta del comprobante)
        public string ComprobanteRuta { get; set; }

        // =========================================================
        // ✅ ALIAS (COMPATIBILIDAD con controllers/BL antiguos)
        // =========================================================
        public string UsuarioValidacion
        {
            get => ValidadoPor;
            set => ValidadoPor = value;
        }

        public string ObservacionesValidacion
        {
            get => Observaciones;
            set => Observaciones = value;
        }

        // Algunos lugares usan RutaComprobante
        public string RutaComprobante
        {
            get => ComprobanteRuta;
            set => ComprobanteRuta = value;
        }
    }
}
