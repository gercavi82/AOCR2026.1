using System.Collections.Generic;
using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class DocumentoWorkflowActionsVm
    {
        public string UrlVerDocumentacion { get; set; }
        public string UrlRevisionDocumental { get; set; }
        public bool MostrarRevisionDocumental { get; set; }
        public bool PuedeAbrirRevisionDocumental { get; set; }
        public string TituloBloqueoRevisionDocumental { get; set; }
        public string TextoAccesoConsulta { get; set; }
        public string TextoAyudaConsulta { get; set; }
        public string TextoAccesoRevision { get; set; }
        public string TextoAyudaRevision { get; set; }
        public string TextoAccesoRevisionBloqueada { get; set; }
        public string TextoAyudaRevisionBloqueada { get; set; }
        public string ContainerClass { get; set; }
        public bool AbrirEnNuevaPestana { get; set; }

        public DocumentoWorkflowActionsVm()
        {
            UrlVerDocumentacion = string.Empty;
            UrlRevisionDocumental = string.Empty;
            TituloBloqueoRevisionDocumental = string.Empty;
            TextoAccesoConsulta = "Ver documentación cargada";
            TextoAyudaConsulta = "Consulta del expediente en solo lectura, con vista previa y descarga.";
            TextoAccesoRevision = "Abrir revisión documental";
            TextoAyudaRevision = "Aceptar, devolver y cerrar la fase documental antes de continuar con la fase operativa.";
            TextoAccesoRevisionBloqueada = "Revisión documental";
            TextoAyudaRevisionBloqueada = "Habilitada únicamente cuando el expediente esté en una etapa documental previa a inspección.";
            ContainerClass = string.Empty;
            AbrirEnNuevaPestana = false;
        }
    }

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
        
        // Propiedades de NC
        public bool RequiereNoConformidad { get; set; }
        public CapaModelo.NoConformidad NoConformidad { get; set; }
        public bool PuedeFirmarNoConformidad { get; set; }

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
        public DocumentoWorkflowActionsVm AccesosDocumentales { get; set; }
        public string MensajeBloqueoEdicion { get; set; }
        public bool PuedeConfirmarRevisionDocumental { get; set; }
        public bool TieneDocumentosObservados { get; set; }
        public bool TieneDocumentosSubsanadosPendientes { get; set; }
        public bool DocumentacionAprobada { get; set; }
        public bool PuedeAbrirLvEae { get; set; }
        public string MensajeBloqueoDocumental { get; set; }

        public ListaVerificacionOperacionalEaePanelVm()
        {
            Solicitud = new SolicitudAOCR();
            ListaVerificacion = new ListaVerificacionOperacionalEae();
            UrlVisualizacion = string.Empty;
            UrlDescarga = string.Empty;
            UrlDocumentosSolicitud = string.Empty;
            AccesosDocumentales = new DocumentoWorkflowActionsVm();
            MensajeBloqueoEdicion = string.Empty;
            MensajeBloqueoDocumental = string.Empty;
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
        public FirmaInspectorPanelVm FirmaInspectorPanel { get; set; }
        public bool TieneDocumentosObservados { get; set; }
        public bool TieneDocumentosSubsanadosPendientes { get; set; }
        public bool DocumentacionAprobada { get; set; }
        public bool PuedeAbrirLvEae { get; set; }
        public bool PuedeAbrirInformeTecnico { get; set; }
        public string MensajeBloqueoDocumental { get; set; }

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
            FirmaInspectorPanel = null;
            MensajeBloqueoDocumental = string.Empty;
        }
    }

    public class CoordinadorPanelVm
    {
        public int CodigoInspeccion { get; set; }
        public InspeccionInformeTecnico InformeTecnico { get; set; }
        public bool EsCoordinador { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string RutaInformeVisual { get; set; }
        
        // Propiedades de NC
        public bool RequiereNoConformidad { get; set; }
        public CapaModelo.NoConformidad NoConformidad { get; set; }

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
        public bool EsRolDireccionOJefatura { get; set; }
        public bool EsInspectorTecnico { get; set; }
        public string RolUsuarioActual { get; set; }
        public bool PuedeEnviarADirdac { get; set; }
        public bool PuedeReintentarNotificacionDirdac { get; set; }
        public bool PuedeFirmarDirdac { get; set; }
        public bool PuedeTomarDecisionInstitucionalFinal { get; set; }
        public bool InformeEnviadoADirdac { get; set; }
        public bool InformeAprobadoDireccion { get; set; }
        public bool InformeDevueltoDireccion { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string RutaInformeVisual { get; set; }

        public DireccionJefaturaPanelVm()
        {
            InformeTecnico = new InspeccionInformeTecnico();
            EstadoInformeTecnico = "BORRADOR";
            RolUsuarioActual = string.Empty;
            RutaInformeVisual = string.Empty;
        }
    }

    public class HistorialInformeViewModel
    {
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Observacion { get; set; }
        public string Origen { get; set; }
        public string UsuarioNombre { get; set; }
        public System.DateTime FechaCambio { get; set; }

        public HistorialInformeViewModel()
        {
            EstadoAnterior = string.Empty;
            EstadoNuevo = string.Empty;
            Observacion = string.Empty;
            Origen = string.Empty;
            UsuarioNombre = string.Empty;
        }
    }

    public class PendienteRevisionDireccionItemViewModel
    {
        public int CodigoInforme { get; set; }
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NombreOperadora { get; set; }
        public string NombreInspector { get; set; }
        public System.DateTime? FechaFirmaInspector { get; set; }
        public string ResultadoTecnicoFinal { get; set; }
        public string EstadoInforme { get; set; }
        public bool NotificacionFormalEnviada { get; set; }
        public string UrlRevision { get; set; }
        public string EtiquetaAccionPrincipal { get; set; }
        public string IconoAccionPrincipal { get; set; }
        public string AccionSiguienteDireccion { get; set; }
        public string MotivoAccionSiguiente { get; set; }
        public string UrlPdfInformeFirmadoInspector { get; set; }

        public PendienteRevisionDireccionItemViewModel()
        {
            NumeroSolicitud = string.Empty;
            NombreOperadora = string.Empty;
            NombreInspector = string.Empty;
            ResultadoTecnicoFinal = string.Empty;
            EstadoInforme = string.Empty;
            UrlRevision = string.Empty;
            EtiquetaAccionPrincipal = "Abrir tramite";
            IconoAccionPrincipal = "fas fa-route";
            AccionSiguienteDireccion = string.Empty;
            MotivoAccionSiguiente = string.Empty;
            UrlPdfInformeFirmadoInspector = string.Empty;
        }
    }

    public class RevisionInformeTecnicoDireccionViewModel
    {
        public int CodigoInforme { get; set; }
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NombreOperadora { get; set; }
        public string NombreInspector { get; set; }
        public System.DateTime? FechaFirmaInspector { get; set; }
        public string EstadoSolicitud { get; set; }
        public string EstadoInspeccion { get; set; }
        public string EstadoInforme { get; set; }
        public string ResultadoTecnicoFinal { get; set; }
        public string TipoResultadoInsatisfactorio { get; set; }
        public string Antecedentes { get; set; }
        public string Objetivo { get; set; }
        public string Alcance { get; set; }
        public string DesarrolloTecnico { get; set; }
        public string Hallazgos { get; set; }
        public string ObservacionesInspector { get; set; }
        public string Conclusiones { get; set; }
        public string Recomendaciones { get; set; }
        public string UrlPdfInformeFirmadoInspector { get; set; }
        public string UrlDescargarPdfInformeFirmadoInspector { get; set; }
        public bool InformeFirmadoInspectorDisponible { get; set; }
        public string MensajePdfFirmadoInspector { get; set; }
        public bool PuedeAprobarDecisionFinal { get; set; }
        public bool PuedeDevolverConObservacion { get; set; }
        public bool PuedeReenviarNotificacionRt { get; set; }
        public bool RequiereFirmaDireccion { get; set; }
        public bool NotificadoRt { get; set; }
        public System.DateTime? FechaNotificacionRt { get; set; }
        public string EstadoAocr { get; set; }
        public bool PuedeGenerarAocr { get; set; }
        public bool AocrYaGenerada { get; set; }
        public string MotivoBloqueoGenerarAocr { get; set; }
        public string UrlDetalleSolicitudAocr { get; set; }
        public string UrlVistaPreviaAocr { get; set; }
        public string ObservacionDireccion { get; set; }
        public string UrlVolverPendientes { get; set; }
        public string RolUsuarioActual { get; set; }
        public IList<HistorialInformeViewModel> Historial { get; set; }

        public RevisionInformeTecnicoDireccionViewModel()
        {
            NumeroSolicitud = string.Empty;
            NombreOperadora = string.Empty;
            NombreInspector = string.Empty;
            EstadoSolicitud = string.Empty;
            EstadoInspeccion = string.Empty;
            EstadoInforme = string.Empty;
            ResultadoTecnicoFinal = string.Empty;
            TipoResultadoInsatisfactorio = string.Empty;
            Antecedentes = string.Empty;
            Objetivo = string.Empty;
            Alcance = string.Empty;
            DesarrolloTecnico = string.Empty;
            Hallazgos = string.Empty;
            ObservacionesInspector = string.Empty;
            Conclusiones = string.Empty;
            Recomendaciones = string.Empty;
            UrlPdfInformeFirmadoInspector = string.Empty;
            UrlDescargarPdfInformeFirmadoInspector = string.Empty;
            MensajePdfFirmadoInspector = string.Empty;
            EstadoAocr = string.Empty;
            MotivoBloqueoGenerarAocr = string.Empty;
            UrlDetalleSolicitudAocr = string.Empty;
            UrlVistaPreviaAocr = string.Empty;
            ObservacionDireccion = string.Empty;
            UrlVolverPendientes = string.Empty;
            RolUsuarioActual = string.Empty;
            Historial = new List<HistorialInformeViewModel>();
        }
    }
}
