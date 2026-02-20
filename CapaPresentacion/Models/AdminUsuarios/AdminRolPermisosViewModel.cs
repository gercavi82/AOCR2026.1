using System.Collections.Generic;
using System.Web.Mvc;
using CapaModelo.Seguridad;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminRolPermisosViewModel
    {
        public int CodigoRolSeleccionado { get; set; }
        public string NombreRolSeleccionado { get; set; }
        public bool InfraestructuraPermisosDisponible { get; set; }

        public IList<int> PermisosSeleccionados { get; set; } = new List<int>();
        public IList<SeguridadPermisoDTO> PermisosDisponibles { get; set; } = new List<SeguridadPermisoDTO>();
        public IEnumerable<SelectListItem> RolesDisponibles { get; set; } = new List<SelectListItem>();
    }
}
