using System;

namespace CapaDatos.Models
{
    public class AocrProcesoEstadoRecord
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int? OrdenRecaudacionId { get; set; }
        public int? InspeccionId { get; set; }
        public int? InformeId { get; set; }
        public string EstadoActual { get; set; }
        public string EtapaActual { get; set; }
        public string RolResponsable { get; set; }
        public int? UsuarioResponsableId { get; set; }
        public string SiguienteAccion { get; set; }
        public string Observacion { get; set; }
        public DateTime FechaEstado { get; set; }
        public bool Activo { get; set; }
    }
}
