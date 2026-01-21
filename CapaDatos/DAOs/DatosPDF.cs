using System;
using System.Collections.Generic;
namespace CapaDatos.Models
{
    public class DatosPDF
    {
        public string NumeroOrden { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Compania { get; set; }
        public string RucCedula { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Admin { get; set; }
        public decimal Total { get; set; }
        public string LugarEmision { get; set; }
        public string Observacion { get; set; }
        public string UsuarioNombre { get; set; }
        public string UsuarioEmail { get; set; }
        public List<DetalleOrden> Detalles { get; set; } = new List<DetalleOrden>();
    }

    public class DetalleOrden
    {
        public int Id { get; set; }
        public string CodigoConcepto { get; set; }
        public string NombreConcepto { get; set; }
        public int Cantidad { get; set; }
        public decimal ValorUnitario { get; set; }
        public decimal ValorTotal { get; set; }
        public string Observacion { get; set; }
    }
}