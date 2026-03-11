using System;

namespace CapaDatos.Models
{
    public class UsuarioInternoRTRegistro
    {
        public int Id { get; set; }
        public int? UsuarioId { get; set; }
        public string CodigoUsuario { get; set; }
        public string CiudadCodigo { get; set; }
        public decimal CodigoFinanciero { get; set; }
        public string Opcar5 { get; set; }
        public string Opcaer { get; set; }
        public decimal Opcoi3 { get; set; }
        public bool Activo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }
}
