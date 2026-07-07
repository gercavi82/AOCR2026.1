using System;

namespace CapaPresentacion.Models.ViewModels
{
    public class AocrResumenEstadoActualViewModel
    {
        public int SolicitudId { get; set; }
        public string EstadoActual { get; set; }
        public string EtapaActual { get; set; }
        public string RolResponsable { get; set; }
        public string Responsable { get; set; }
        public string SiguienteAccion { get; set; }
        public string FechaEstado { get; set; }
    }
}
