using System.Collections.Generic;

namespace CapaPresentacion.Models
{
    public class DashboardViewModel
    {
        public string NombreUsuario { get; set; }
        public string RolUsuario { get; set; }
        public int SolicitudesPendientes { get; set; }
        public int TramitesEnCurso { get; set; }
        public int NotificacionesNuevas { get; set; }
        public bool MostrarModuloOperador { get; set; }
        public bool MostrarModuloFinanciero { get; set; }
        public bool MostrarModuloCertificacion { get; set; }
        public bool MostrarModuloInspector { get; set; }
        public bool MostrarDashboardOrdenes { get; set; }
        public bool MostrarDashboardFinanciero { get; set; }
        public bool MostrarDashboardInspector { get; set; }
        public bool MostrarDashboardGerencial { get; set; }
        public bool MostrarDashboardAdministracion { get; set; }
        public bool MostrarSyncRt { get; set; }
        public bool MostrarAprobacionRt { get; set; }
        public IList<DashboardShortcutViewModel> AccesosDashboards { get; set; } = new List<DashboardShortcutViewModel>();
        public IList<DashboardShortcutViewModel> AccionesInstitucionales { get; set; } = new List<DashboardShortcutViewModel>();
    }

    public class DashboardShortcutViewModel
    {
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Icono { get; set; }
        public string Controlador { get; set; }
        public string Accion { get; set; }
        public string Estilo { get; set; }
    }
}
