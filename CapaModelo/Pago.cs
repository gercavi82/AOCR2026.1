using System;

namespace CapaModelo
{
    public class Pago
    {
        public int CodigoPago { get; set; }
        public int CodigoSolicitud { get; set; }

        // Campo real principal (puede representar comprobante o transacción según tu BD)
        public string NumeroFactura { get; set; }

        public decimal? Monto { get; set; }
        public string Moneda { get; set; }

        public string Concepto { get; set; }
        public string MetodoPago { get; set; }   // tu campo actual
        public string Estado { get; set; }

        public DateTime? FechaPago { get; set; }
        public DateTime? FechaValidacion { get; set; }

        public string ValidadoPor { get; set; }
        public string Observaciones { get; set; }

        public string ComprobanteRuta { get; set; }

        // ============================
        // ✅ CAMPOS QUE EL DAO ESPERA
        // ============================
        public string Banco { get; set; }
        public DateTime? FechaAnulacion { get; set; }
        public string UsuarioRegistro { get; set; }
        public string UsuarioAnulacion { get; set; }

        // ============================
        // ✅ ALIAS / COMPATIBILIDAD
        // ============================

        // DAO usa NumeroComprobante
        public string NumeroComprobante
        {
            get => NumeroFactura;
            set => NumeroFactura = value;
        }

        // DAO usa FormaPago, pero tú tenías MetodoPago
        public string FormaPago
        {
            get => MetodoPago;
            set => MetodoPago = value;
        }

        // En otras partes usan NumeroTransaccion
        public string NumeroTransaccion
        {
            get => NumeroFactura;
            set => NumeroFactura = value;
        }

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

        public string RutaComprobante
        {
            get => ComprobanteRuta;
            set => ComprobanteRuta = value;
        }

        // Rechazo
        public DateTime? FechaRechazo { get; set; }
        public string UsuarioRechazo { get; set; }
    }
}
