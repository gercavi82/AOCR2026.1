using System.Collections.Generic;
using CapaModelo.Seguridad;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuariosIndexViewModel
    {
        public string Filtro { get; set; }
        public bool? Activo { get; set; }
        public IList<SeguridadUsuarioDTO> Usuarios { get; set; } = new List<SeguridadUsuarioDTO>();
    }
}
