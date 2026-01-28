using CapaDatos.Models;

namespace CapaPresentacion.Models
{
    public class OrdenValidacionFinancieraVM
    {
        public OrdenRecaudacionModel Orden { get; set; }
        public PagoModel Pago { get; set; }
    }
}
