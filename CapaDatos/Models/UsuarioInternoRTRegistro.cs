using System;

namespace CapaDatos.Models
{
    public class UsuarioInternoRTRegistro
    {
        public int Id { get; set; }
        public int? UsuarioId { get; set; }
        public int? TecnicoId { get; set; }
        public string CodigoUsuario { get; set; }
        public string Identificacion { get; set; }
        public string Cedula
        {
            get { return Identificacion; }
            set { Identificacion = value; }
        }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string NombreCompleto { get; set; }
        public string Tipo { get; set; }
        public string EstadoAs400 { get; set; }
        public string CiudadCodigo { get; set; }
        public decimal CodigoFinanciero { get; set; }
        public string Opcar5 { get; set; }
        public string Opcaer { get; set; }
        public decimal Opcoi3 { get; set; }
        public string CorreoInstitucional { get; set; }
        public string RolInterno { get; set; }
        public string Observaciones { get; set; }
        public bool Activo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }

        public string UsuarioLogin
        {
            get
            {
                return string.IsNullOrWhiteSpace(CodigoUsuario) ? Identificacion : CodigoUsuario;
            }
        }

        public string NombreVisual
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NombreCompleto))
                {
                    return NombreCompleto;
                }

                return (string.Format("{0} {1}", Nombres ?? string.Empty, Apellidos ?? string.Empty)).Trim();
            }
        }
    }
}
