using System;

namespace CapaDatos.Models
{
    public class UsuarioAs400Record
    {
        public string CodigoUsuario { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string TipoIdentificacion { get; set; }
        public string Identificacion { get; set; }
        public string Correo { get; set; }
        public string ClaveHash { get; set; }
        public string Estado { get; set; }
        public string TipoApp { get; set; }
        public string TipoTributario { get; set; }
        public string NumeroRuc { get; set; }
        public string RolCodigo { get; set; }
        public string CiudadCodigo { get; set; }
        public string DependenciaCodigo { get; set; }
        public string UsuarioAuditoria { get; set; }
        public string Dispositivo { get; set; }

        // Datos adicionales (USUAR1)
        public string Titulo1 { get; set; }
        public string Titulo2 { get; set; }
        public string NombreCorto { get; set; }
        public string Cargo { get; set; }
        public string Telefono1 { get; set; }
        public string Telefono2 { get; set; }
        public string CorreoAdicional { get; set; }
        public decimal? OidCentroContable { get; set; }
        public string CiudadCodigoAdicional { get; set; }

        public static UsuarioAs400Record CrearBasico(
            string codigoUsuario,
            string nombres,
            string apellidos,
            string tipoIdentificacion,
            string identificacion,
            string correo,
            string claveHash,
            string usuarioAuditoria)
        {
            return new UsuarioAs400Record
            {
                CodigoUsuario = codigoUsuario,
                Nombres = nombres,
                Apellidos = apellidos,
                TipoIdentificacion = tipoIdentificacion,
                Identificacion = identificacion,
                Correo = correo,
                ClaveHash = claveHash,
                Estado = "AC",
                TipoApp = "WEB",
                UsuarioAuditoria = string.IsNullOrWhiteSpace(usuarioAuditoria) ? "AOCR" : usuarioAuditoria,
                Dispositivo = "WEB"
            };
        }
    }
}
