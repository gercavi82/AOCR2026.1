using System;

namespace CapaDatos.Models
{
    public class AocrProcesoEstadoHistorialRecord
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int? OrdenRecaudacionId { get; set; }
        public int? InspeccionId { get; set; }
        public int? InformeId { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Etapa { get; set; }
        public string Accion { get; set; }
        public string RolUsuario { get; set; }
        public int? UsuarioId { get; set; }
        public string RolResponsable { get; set; }
        public int? UsuarioResponsableId { get; set; }
        public string Observacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Ip { get; set; }
        public string CorrelationId { get; set; }
        public string ClaveIdempotencia { get; set; }
        public string Resultado { get; set; }

        // Mapped values for views
        public string UsuarioNombre { get; set; }
        public string ResponsableNombre { get; set; }
    }
}
