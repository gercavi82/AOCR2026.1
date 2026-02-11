using System;

namespace CapaModelo
{
    public class SolicitudRTModel
    {
        public int Id { get; set; }
        public int UsuarioRtId { get; set; }
        public int CompaniaId { get; set; }
        public string Estado { get; set; }
        public bool DeclaracionAceptada { get; set; }
        public string DeclaracionTexto { get; set; }
        public DateTime? FechaEnvio { get; set; }
        public string ObservacionCoordinador { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}