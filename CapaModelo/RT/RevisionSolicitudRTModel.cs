using System;

namespace CapaModelo.RT
{
    public class RevisionSolicitudRTModel
    {
        public long Id { get; set; }
        public int SolicitudRtId { get; set; }
        public string InspectorUsuario { get; set; }
        public int CoordinadorUsuarioId { get; set; }
        public string Estado { get; set; }
        public string Resultado { get; set; }
        public string Observacion { get; set; }
        public DateTime FechaAsignacion { get; set; }
        public DateTime? FechaRevision { get; set; }
    }
}