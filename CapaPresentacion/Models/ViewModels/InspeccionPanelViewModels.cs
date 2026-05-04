using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class FirmaInspectorPanelVm
    {
        public int CodigoInspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public bool UsaFlujoListaVerificacionEae { get; set; }
        public bool PuedeFirmarInspector { get; set; }
        public bool PuedeEnviarADirdac { get; set; }
        public bool PuedeReintentarNotificacionDirdac { get; set; }
        public bool InformeEnviadoADirdac { get; set; }
        public bool InformeDevueltoCoordinador { get; set; }
        public bool InformeDevueltoDireccion { get; set; }
        public bool InformeAprobadoDireccion { get; set; }
        public string EstadoInformeTecnico { get; set; }

        public FirmaInspectorPanelVm()
        {
            InformeTecnico = new InspeccionInformeTecnico();
            EstadoInformeTecnico = "BORRADOR";
        }
    }

    public class ListaVerificacionOperacionalEaePanelVm
    {
        public int CodigoInspeccion { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public ListaVerificacionOperacionalEae ListaVerificacion { get; set; }
        public bool PuedeEditar { get; set; }
        public bool PuedeFirmar { get; set; }
        public bool EstaFirmada { get; set; }
        public string UrlVisualizacion { get; set; }
        public string UrlDescarga { get; set; }
        public string UrlDocumentosSolicitud { get; set; }
        public string MensajeBloqueoEdicion { get; set; }
        public bool PuedeConfirmarRevisionDocumental { get; set; }

        public ListaVerificacionOperacionalEaePanelVm()
        {
            Solicitud = new SolicitudAOCR();
            ListaVerificacion = new ListaVerificacionOperacionalEae();
            UrlVisualizacion = string.Empty;
            UrlDescarga = string.Empty;
            UrlDocumentosSolicitud = string.Empty;
            MensajeBloqueoEdicion = string.Empty;
        }
    }

    public class CoordinadorPanelVm
    {
        public int CodigoInspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public bool EsCoordinador { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string RutaInformeVisual { get; set; }

        public CoordinadorPanelVm()
        {
            InformeTecnico = new InspeccionInformeTecnico();
            EstadoInformeTecnico = "BORRADOR";
            RutaInformeVisual = string.Empty;
        }
    }

    public class DireccionJefaturaPanelVm
    {
        public int CodigoInspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public bool EsDirdac { get; set; }
        public bool PuedeFirmarDirdac { get; set; }
        public bool InformeEnviadoADirdac { get; set; }
        public bool InformeAprobadoDireccion { get; set; }
        public bool InformeDevueltoDireccion { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string RutaInformeVisual { get; set; }

        public DireccionJefaturaPanelVm()
        {
            InformeTecnico = new InspeccionInformeTecnico();
            EstadoInformeTecnico = "BORRADOR";
            RutaInformeVisual = string.Empty;
        }
    }
}
