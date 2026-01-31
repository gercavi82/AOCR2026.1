using System;
using CapaNegocio.DTOs;

namespace CapaPresentacion.Models
{
    // Placeholders para mantener compatibilidad de firmas en Controllers con los DTOs reales.
    public class CrearOrdenViewModel : CrearOrdenRequest { }
    public class RegistrarPagoViewModel : RegistrarPagoRequest { }
    public class ValidarPagoViewModel : ValidarPagoRequest { }

    // ViewModels faltantes usados en controladores/vistas antiguas
    public class EventoOrdenViewModel { }
    public class OrdenRecaudacionIndexViewModel { }
    public class OrdenRecaudacionResumenViewModel { }

    // VM liviano usado por vistas Obligatoria/TodasOrdenes
    public class OrdenRecaudacionViewModel
    {
        public int Id { get; set; }
        public string NumeroOrden { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Estado { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Admin { get; set; }
        public decimal Total { get; set; }
        public string Observacion { get; set; }
        public string Compania { get; set; }
        public string RucCedula { get; set; }
        public string Correo { get; set; }
        public string ConceptoNombre { get; set; }
    }
}
