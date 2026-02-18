using System;

namespace CapaPresentacion.Models.EmailTemplates
{
    public class OrdenRechazadaEmailVM
    {
        public string NumeroOrden { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NombreContribuyente { get; set; }
        public string RucCedula { get; set; }
        public decimal Total { get; set; }
        public string Motivo { get; set; }
        public DateTime FechaRechazo { get; set; }
        public string MetodoPago { get; set; }
        public string NumeroComprobante { get; set; }
    }
}
