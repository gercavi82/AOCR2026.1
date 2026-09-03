using System;

namespace CapaModelo.RT
{
    /// <summary>
    /// Resultado de la operación de devolución de postulación provisional RT (AC-01).
    /// </summary>
    public class ResultadoDevolucionRT
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public bool YaEstabaDevuelto { get; set; }
        public int UsuarioId { get; set; }
        public string CorreoOriginal { get; set; }
        public string NombreCompleto { get; set; }
        public string CodigoUsuario { get; set; }
        public int? SolicitudRtId { get; set; }
        public bool CorreoLiberado { get; set; }
    }
}
