using System.Collections.Generic;
using System.Web.Mvc;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuarioRolesViewModel
    {
        public int IdUsuario { get; set; }
        public string CodigoUsuario { get; set; }
        public string NombreCompleto { get; set; }
        public bool Activo { get; set; }
        public IList<int> RolesSeleccionados { get; set; } = new List<int>();
        public IEnumerable<SelectListItem> RolesDisponibles { get; set; } = new List<SelectListItem>();
    }
}
