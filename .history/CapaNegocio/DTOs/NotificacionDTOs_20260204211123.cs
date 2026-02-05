using System;

namespace CapaNegocio.DTOs
{
    /// <summary>
    /// DTO para enviar notificaciones
    /// </summary>
    public class EnviarNotificacionRequest
    {
        public int OrdenId { get; set; }
        public string TipoNotificacion { get; set; }
        public string EmailDestino { get; set; }
        public string NombreDestino { get; set; }
        public bool AdjuntarPdf { get; set; }
        public byte[] AdjuntoPdf { get; set; }
        public string NombreAdjunto { get; set; }
    }
}
