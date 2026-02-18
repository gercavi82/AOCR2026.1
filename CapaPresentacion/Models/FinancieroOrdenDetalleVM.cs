using System.Collections.Generic;
using PagoModelDatos = CapaDatos.Models.PagoModel;
using OrdenRecaudacionModelDatos = CapaDatos.Models.OrdenRecaudacionModel;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class FinancieroOrdenDetalleVM
    {
        public OrdenRecaudacionModelDatos Orden { get; set; }
        public PagoModelDatos Pago { get; set; }
        public List<HistorialEstado> Historial { get; set; } = new List<HistorialEstado>();
    }
}
