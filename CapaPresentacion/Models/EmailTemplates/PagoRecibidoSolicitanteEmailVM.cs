using System;

namespace CapaPresentacion.Models.EmailTemplates
{
    public class PagoRecibidoSolicitanteEmailVM
    {
        public string NumeroOrden { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NombreContribuyente { get; set; }
        public decimal Monto { get; set; }
        public string MetodoPago { get; set; }
        public string NumeroComprobante { get; set; }
        public DateTime FechaPago { get; set; }
    }
}
