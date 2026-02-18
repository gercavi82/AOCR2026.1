using System;

namespace CapaPresentacion.Models.EmailTemplates
{
    public class OrdenAprobadaEmailVM
    {
        public string NumeroOrden { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NombreContribuyente { get; set; }
        public string RucCedula { get; set; }
        public decimal Total { get; set; }
        public string Observaciones { get; set; }
        public DateTime FechaAprobacion { get; set; }
        public string MetodoPago { get; set; }
        public string NumeroComprobante { get; set; }
    }
}
