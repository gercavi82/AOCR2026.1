using System;

namespace CapaDatos.Models
{
    public class SolicitudTransicionRequest
    {
        public int SolicitudId { get; set; }
        public int? InspeccionId { get; set; }
        public int? OrdenRecaudacionId { get; set; }
        public int? InformeId { get; set; }
        public int UsuarioId { get; set; }
        public string Rol { get; set; }
        public string EstadoEsperado { get; set; }
        public string EstadoDestino { get; set; }
        public string Accion { get; set; }
        public string Observacion { get; set; }
        public string ClaveIdempotencia { get; set; }
        public long? VersionRegistro { get; set; }
        public string Ip { get; set; }
        public string CorrelationId { get; set; }
    }
}
