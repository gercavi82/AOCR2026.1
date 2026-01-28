using CapaDatos.Models;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class PagoFinancieroVM
    {
        public Pago Pago { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public OrdenRecaudacionModel Orden { get; set; }
    }
}
