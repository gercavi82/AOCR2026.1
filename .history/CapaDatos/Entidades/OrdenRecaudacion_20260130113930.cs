using System;
using System.Collections.Generic;

namespace CapaDatos.Entidades
{
    /// <summary>
    /// Entidad que representa una orden de recaudación
    /// </summary>
    public class OrdenRecaudacion
    {
        public int Id { get; set; }
        public string NumeroOrden { get; set; }
        public int SolicitudId { get; set; }
        public int ConceptoId { get; set; }
        public int ContribuyenteId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string Observaciones { get; set; }
        public string Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public string UsuarioModificacion { get; set; }
        public bool Activo { get; set; }
        public string NombreContribuyente { get; set; }
        public string RucContribuyente { get; set; }
        public string EmailContribuyente { get; set; }
        public string ConceptoNombre { get; set; }
        public virtual ICollection<DetalleOrden> Detalles { get; set; }
        public virtual ICollection<Pago> Pagos { get; set; }

        public OrdenRecaudacion()
        {
            Detalles = new List<DetalleOrden>();
            Pagos = new List<Pago>();
            Activo = true;
            FechaCreacion = DateTime.Now;
        }
    }
}
