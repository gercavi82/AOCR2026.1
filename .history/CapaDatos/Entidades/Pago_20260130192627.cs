using System;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa un pago de orden de recaudación
    /// </summary>
    public class Pago
    {
        public int Id { get; set; }
        public int OrdenId { get; set; }
        public string NumeroComprobante { get; set; }
        public decimal MontoPagado { get; set; }
        public DateTime FechaPago { get; set; }
        public string MetodoPago { get; set; }
        public string BancoOrigen { get; set; }
        public string Observaciones { get; set; }
        public string RutaComprobante { get; set; }
        public string Estado { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string UsuarioRegistro { get; set; }
        public DateTime? FechaValidacion { get; set; }
        public string UsuarioValidacion { get; set; }

        // Navegación
        public virtual OrdenRecaudacion Orden { get; set; }

        // ============================
        // Alias / compatibilidad
        // ============================
        // Algunos servicios usan NumeroTransaccion / CodigoSolicitud
        public string NumeroTransaccion
        {
            get { return NumeroComprobante; }
            set { NumeroComprobante = value; }
        }

        public string NumeroFactura
        {
            get { return NumeroComprobante; }
            set { NumeroComprobante = value; }
        }

        // Alias para OrdenRecaudacionOrchestrator que usa Observacion (singular)
        public string Observacion
        {
            get { return Observaciones; }
            set { Observaciones = value; }
        }

        // En el flujo financiero previo se usa CodigoSolicitud (puede mapearse vía Orden)
        public int CodigoSolicitud { get; set; }
    }
}
