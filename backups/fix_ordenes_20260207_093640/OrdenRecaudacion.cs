using System;
using System.Collections.Generic;

namespace CapaModelo.OrdenRecaudacion
{
    public class OrdenRecaudacion
    {
        public int Id { get; set; }                         // aocr_or_orden.id
        public int CodigoUsuario { get; set; }              // codigo_usuario
        public string CodigoSolicitud { get; set; }         // codigo_solicitud (varchar 50)
        public string NumeroOrden { get; set; }             // numero_orden
        public DateTime FechaCreacion { get; set; }         // fecha_creacion
        public string Estado { get; set; }                  // BORRADOR, GENERADA, ENVIADA, PAGADA, ANULADA
        public string Observacion { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Admin { get; set; }
        public decimal Total { get; set; }

        public string LugarEmision { get; set; }
        public string Compania { get; set; }
        public string RucCedula { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public int? ConceptoId { get; set; }

        public List<OrdenRecaudacionDetalle> Detalles { get; set; } = new List<OrdenRecaudacionDetalle>();
    }
}
