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
}
