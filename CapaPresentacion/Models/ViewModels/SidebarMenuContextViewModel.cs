using System.Collections.Generic;

namespace CapaPresentacion.Models.ViewModels
{
    public class SidebarMenuContextViewModel
    {
        public bool EsAdministrador { get; set; }
        public bool EsSolicitanteRol { get; set; }
        public bool RequiereOrden { get; set; }
        public bool TieneOrdenBorrador { get; set; }
        public bool TieneOrdenPendienteProceso { get; set; }
        public bool TieneOrdenPendienteComprobante { get; set; }
        public bool TieneAccesoSolicitudRt { get; set; }
        public bool EsRepresentanteRtRol { get; set; }
        public bool EsInspectorRol { get; set; }
        public bool EsCoordinadorRol { get; set; }
        public bool EsFinancieroRol { get; set; }
        public bool EsDireccionRol { get; set; }
        public bool EsLegalRol { get; set; }
        public bool PuedeAdministracion { get; set; }
        public bool PuedeAprobarUsuarios { get; set; }
        public string MensajeBloqueoRtSidebar { get; set; }
    }

    public class SidebarMenuViewModel
    {
        public SidebarMenuViewModel()
        {
            Companias = new List<SidebarCompanyOptionViewModel>();
            QuickActions = new List<SidebarMenuItemViewModel>();
            Groups = new List<SidebarMenuGroupViewModel>();
            FooterItems = new List<SidebarMenuItemViewModel>();
            EmptyStateTitle = "Sin navegación disponible";
            EmptyStateMessage = "La sesión actual no tiene un rol con accesos cargados para este menú.";
        }

        public string UserName { get; set; }
        public string ActiveRoleKey { get; set; }
        public string UserRoleDisplay { get; set; }
        public string UserEmail { get; set; }
        public int AvailableRoleCount { get; set; }
        public string ActiveCompanyCode { get; set; }
        public string ActiveCompanyName { get; set; }
        public bool ShowCompanySelector { get; set; }
        public bool ShowCompanyContext { get; set; }
        public bool ShowSearch { get; set; }
        public string NavigationSectionTitle { get; set; }
        public string CompanyChangeUrl { get; set; }
        public string ReturnUrl { get; set; }
        public string ActiveRoleSummary { get; set; }
        public string EmptyStateTitle { get; set; }
        public string EmptyStateMessage { get; set; }
        public bool HasNavigation { get; set; }
        public SidebarStatusCardViewModel OrderStatusCard { get; set; }
        public IList<SidebarCompanyOptionViewModel> Companias { get; private set; }
        public IList<SidebarMenuItemViewModel> QuickActions { get; private set; }
        public IList<SidebarMenuGroupViewModel> Groups { get; private set; }
        public IList<SidebarMenuItemViewModel> FooterItems { get; private set; }
    }

    public class SidebarStatusCardViewModel
    {
        public bool Visible { get; set; }
        public string ToneClass { get; set; }
        public string IconClass { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string LinkText { get; set; }
        public string LinkUrl { get; set; }
    }

    public class SidebarCompanyOptionViewModel
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public bool Selected { get; set; }
    }

    public class SidebarMenuGroupViewModel
    {
        public SidebarMenuGroupViewModel()
        {
            Items = new List<SidebarMenuItemViewModel>();
        }

        public string Key { get; set; }
        public string Title { get; set; }
        public string IconClass { get; set; }
        public string Description { get; set; }
        public string AccentClass { get; set; }
        public string CollapseId { get; set; }
        public bool Expanded { get; set; }
        public bool Visible { get; set; }
        public int BadgeCount { get; set; }
        public bool ShowBadge { get; set; }
        public IList<SidebarMenuItemViewModel> Items { get; private set; }
    }

    public class SidebarMenuItemViewModel
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconClass { get; set; }
        public string Url { get; set; }
        public bool Visible { get; set; }
        public bool Enabled { get; set; }
        public bool PermissionGranted { get; set; }
        public bool IsActive { get; set; }
        public bool IsQuickAction { get; set; }
        public int BadgeCount { get; set; }
        public bool ShowBadge { get; set; }
        public string BadgeToneClass { get; set; }
        public string Tooltip { get; set; }
        public string CssClass { get; set; }
    }
}
