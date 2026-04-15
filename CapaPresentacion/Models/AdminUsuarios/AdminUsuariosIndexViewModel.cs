using System.Collections.Generic;
using CapaModelo.Seguridad;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuariosIndexViewModel
    {
        public string Filtro { get; set; }
        public bool? Activo { get; set; }
        public string TipoFiltro { get; set; }
        public int TotalUsuarios { get; set; }
        public int UsuariosActivos { get; set; }
        public int UsuariosInactivos { get; set; }
        public int UsuariosConRoles { get; set; }
        public int UsuariosSinRoles { get; set; }
        public int RolesActivos { get; set; }
        public int PendientesDesignacionRt { get; set; }
        public int UsuariosConAccesoReciente { get; set; }
        public IList<SeguridadUsuarioDTO> Usuarios { get; set; } = new List<SeguridadUsuarioDTO>();
        public IList<SeguridadUsuarioDTO> UsuariosRecientes { get; set; } = new List<SeguridadUsuarioDTO>();
    }
}
