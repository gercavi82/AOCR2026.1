using System;

namespace CapaModelo
{
    public class OrdenRecaudacionModel
    {
        public int Id { get; set; }
        public string NumeroOrden { get; set; }
        public string Estado { get; set; }
        public decimal Total { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string NombreContribuyente { get; set; }
        public int CodigoUsuario { get; set; }
        public string CodigoSolicitud { get; set; }
        public string LugarEmision { get; set; }
        public string Compania { get; set; }
        public string RucCedula { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public string Observacion { get; set; }

        // Agregado: lista de detalles de la orden
        public System.Collections.Generic.List<CapaDatos.Models.OrdenDetalleModel> Detalles { get; set; } = new System.Collections.Generic.List<CapaDatos.Models.OrdenDetalleModel>();
    }
}
