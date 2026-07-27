using System.Collections.Generic;
using System.Web.Mvc;
using CapaModelo.Seguridad;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuariosIndexViewModel
    {
        public string TabActivo { get; set; } = "resumen";
        public string Filtro { get; set; }
        public bool? Activo { get; set; }
        public string TipoFiltro { get; set; }
        public string PerfilFiltro { get; set; }
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
        public IEnumerable<SelectListItem> RolesFiltro { get; set; } = new List<SelectListItem>();
    }
}
