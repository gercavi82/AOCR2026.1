using System;

namespace CapaDatos.Models
{
    public class AsignacionRTRegistro
    {
        public int Id { get; set; }
        public int CodigoSolicitud { get; set; }
        public string RtCedula { get; set; }
        public string RtNombre { get; set; }
        public string RtTipo { get; set; }
        public DateTime FechaAsignacion { get; set; }
        public string UsuarioAsigna { get; set; }
        public string Observacion { get; set; }
        public bool Activo { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
