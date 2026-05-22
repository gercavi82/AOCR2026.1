namespace CapaPresentacion.Models.ViewModels
{
    public class SidebarMenuContextViewModel
    {
        public bool EsAdministrador { get; set; }
        public bool RequiereOrden { get; set; }
        public bool TieneOrdenBorrador { get; set; }
        public bool TieneOrdenPendienteProceso { get; set; }
        public bool TieneOrdenPendienteComprobante { get; set; }
        public bool TieneAccesoSolicitudRt { get; set; }
        public bool EsRepresentanteRtRol { get; set; }
        public bool PuedeAdministracion { get; set; }
        public bool PuedeAprobarUsuarios { get; set; }
        public string MensajeBloqueoRtSidebar { get; set; }
    }
}
