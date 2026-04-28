using System.Collections.Generic;

namespace CapaPresentacion.Models
{
    public class SeleccionCompaniaViewModel
    {
        public string CompaniaSeleccionada { get; set; }
        public string NuevaCompaniaCodigo { get; set; }
        public string NuevaCompaniaNombre { get; set; }
        public bool MostrarAgregarCompania { get; set; }
        public string ReturnUrl { get; set; }
        public List<CompaniaAsignadaViewModel> Companias { get; set; } = new List<CompaniaAsignadaViewModel>();
    }

    public class CompaniaAsignadaViewModel
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
    }
}
