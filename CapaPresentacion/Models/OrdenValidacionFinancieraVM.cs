using CapaDatos.Models;

namespace CapaPresentacion.Models
{
    public class OrdenValidacionFinancieraVM
    {
        public OrdenRecaudacionModel Orden { get; set; }
        public PagoModel Pago { get; set; }
        public string Fr3Estado { get; set; }
        public string Fr3Numero { get; set; }
        public string Fr3Error { get; set; }
        public bool PuedeReintentarFr3 { get; set; }
    }
}
