using System;

namespace CapaModelo.OrdenRecaudacion
{
    public class HistorialEstadoOrden
    {
        public int Id { get; set; }
        public int OrdenId { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public int? CodigoUsuario { get; set; }
        public string Observaciones { get; set; }
        public DateTime FechaCambio { get; set; }
    }
}
