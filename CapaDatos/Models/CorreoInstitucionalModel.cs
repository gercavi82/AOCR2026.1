using System;

namespace CapaDatos.Models
{
    public class CorreoInstitucionalModel
    {
        public int CodigoCorreo { get; set; }
        public string CodigoArea { get; set; }
        public string NombreArea { get; set; }
        public string CorreoPrincipal { get; set; }
        public string CorreosCc { get; set; }
        public string CorreosBcc { get; set; }
        public string Descripcion { get; set; }
        public bool Activo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class CorreoInstitucionalHistorialModel
    {
        public int CodigoHistorial { get; set; }
        public int CodigoCorreo { get; set; }
        public string CodigoArea { get; set; }
        public string CorreoAnterior { get; set; }
        public string CorreoNuevo { get; set; }
        public string CcAnterior { get; set; }
        public string CcNuevo { get; set; }
        public string BccAnterior { get; set; }
        public string BccNuevo { get; set; }
        public string UsuarioModificacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        public string Accion { get; set; }
    }
}
