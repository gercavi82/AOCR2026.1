using System.Collections.Generic;
using System.Web.Mvc;

namespace CapaPresentacion.Models.RT
{
    public class GestionCompaniasRTViewModel
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; }
        public string Correo { get; set; }
        public string CodigoUsuario { get; set; }
        public string EstadoDesignacionRt { get; set; }

        public List<string> CompaniasSeleccionadas { get; set; } = new List<string>();
        public List<SelectListItem> CatalogoCompanias { get; set; } = new List<SelectListItem>();
    }
}
