using System.Collections.Generic;
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

    public class InformeTecnicoModalVm
    {
        public int CodigoInspeccion { get; set; }
        public Inspeccion Inspeccion { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public ListaVerificacionOperacionalEae ListaVerificacion { get; set; }
        public IList<DocumentoInspeccion> DocumentosSolicitante { get; set; }
        public bool UsaFlujoListaVerificacionOperacionalEae { get; set; }
        public bool LvEaeFinalizada { get; set; }
        public bool PuedeGestionarInformeTecnico { get; set; }
        public bool PuedeEditarInformeTecnico { get; set; }
        public bool ExisteInformeTecnico { get; set; }
        public bool ExistePdfInformeTecnico { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string EstadoListaVerificacion { get; set; }
        public int? CodigoInformeTecnico { get; set; }
        public string MensajeBloqueo { get; set; }
        public string UrlGuardar { get; set; }
        public string UrlPrevisualizar { get; set; }
        public string UrlVerPdf { get; set; }
        public string UrlDescargarPdf { get; set; }

        public InformeTecnicoModalVm()
        {
            Inspeccion = new Inspeccion();
            Solicitud = new SolicitudAOCR();
            InformeTecnico = new InspeccionInformeTecnico();
            ListaVerificacion = new ListaVerificacionOperacionalEae();
            DocumentosSolicitante = new List<DocumentoInspeccion>();
            EstadoInformeTecnico = "BORRADOR_INFORME";
            EstadoListaVerificacion = "LV_BORRADOR";
            MensajeBloqueo = string.Empty;
            UrlGuardar = string.Empty;
            UrlPrevisualizar = string.Empty;
            UrlVerPdf = string.Empty;
            UrlDescargarPdf = string.Empty;
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
