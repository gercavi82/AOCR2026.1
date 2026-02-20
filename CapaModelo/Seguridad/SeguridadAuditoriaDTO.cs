using System;

namespace CapaModelo.Seguridad
{
    public class SeguridadAuditoriaDTO
    {
        public long IdAuditoria { get; set; }
        public int? ActorUsuarioId { get; set; }
        public string ActorCodigoUsuario { get; set; }
        public string Accion { get; set; }
        public string ObjetivoTipo { get; set; }
        public string ObjetivoId { get; set; }
        public string DetalleJson { get; set; }
        public DateTime Fecha { get; set; }
        public string Ip { get; set; }
    }
}
