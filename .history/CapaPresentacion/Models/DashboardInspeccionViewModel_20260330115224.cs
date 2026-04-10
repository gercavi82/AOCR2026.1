using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaPresentacion.Models
{
    public class DashboardInspeccionViewModel
    {
        public string CompaniaFiltro { get; set; }
        public string InspectorFiltro { get; set; }
        public string EstadoFiltro { get; set; }
        public string QuickFilter { get; set; }

        public List<string> CompaniasDisponibles { get; set; } = new List<string>();
        public List<string> InspectoresDisponibles { get; set; } = new List<string>();
        public List<string> EstadosDisponibles { get; set; } = new List<string>();

        public List<DashboardGestionIntegralItemViewModel> GestionIntegralAocr { get; set; } = new List<DashboardGestionIntegralItemViewModel>();
        public List<DashboardInspeccionSeguimientoItemViewModel> InspeccionesEnSeguimiento { get; set; } = new List<DashboardInspeccionSeguimientoItemViewModel>();
        public List<DashboardInspeccionDocumentoItemViewModel> ControlDocumental { get; set; } = new List<DashboardInspeccionDocumentoItemViewModel>();
        public List<DashboardInspeccionFirmaItemViewModel> PendientesFirma { get; set; } = new List<DashboardInspeccionFirmaItemViewModel>();
        public List<DashboardInspeccionNcItemViewModel> ObservacionesNc { get; set; } = new List<DashboardInspeccionNcItemViewModel>();
        public TableroAocrViewModel TableroAocr { get; set; } = new TableroAocrViewModel();

        public int TotalTramites
        {
            get { return GestionIntegralAocr.Count; }
        }

        public int TotalPendientes
        {
            get { return GestionIntegralAocr.Count(x => string.Equals(x.EstadoGeneral, "PENDIENTE", StringComparison.OrdinalIgnoreCase)); }
        }

        public int TotalEnProceso
        {
            get
            {
                return GestionIntegralAocr.Count(x =>
                    string.Equals(x.EstadoGeneral, "EN_PROCESO", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.EstadoGeneral, "EN_VERIFICACION", StringComparison.OrdinalIgnoreCase));
            }
        }

        public int TotalObservadas
        {
            get { return GestionIntegralAocr.Count(x => string.Equals(x.EstadoGeneral, "OBSERVADO", StringComparison.OrdinalIgnoreCase)); }
        }

        public int TotalListosFirma
        {
            get { return GestionIntegralAocr.Count(x => x.ListoParaFirma || string.Equals(x.EtapaActual, "FIRMA", StringComparison.OrdinalIgnoreCase)); }
        }

        public int TotalFinalizadas
        {
            get { return GestionIntegralAocr.Count(x => string.Equals(x.EstadoGeneral, "FINALIZADO", StringComparison.OrdinalIgnoreCase)); }
        }
    }

    public class TableroAocrViewModel
    {
        public List<DashboardGestionIntegralItemViewModel> Pendientes { get; set; } = new List<DashboardGestionIntegralItemViewModel>();
        public List<DashboardGestionIntegralItemViewModel> EnRevision { get; set; } = new List<DashboardGestionIntegralItemViewModel>();
        public List<DashboardGestionIntegralItemViewModel> EnInspeccion { get; set; } = new List<DashboardGestionIntegralItemViewModel>();
        public List<DashboardGestionIntegralItemViewModel> Observados { get; set; } = new List<DashboardGestionIntegralItemViewModel>();
        public List<DashboardGestionIntegralItemViewModel> ListosFirma { get; set; } = new List<DashboardGestionIntegralItemViewModel>();
        public List<DashboardGestionIntegralItemViewModel> Finalizados { get; set; } = new List<DashboardGestionIntegralItemViewModel>();
    }

    public class DashboardGestionIntegralItemViewModel
    {
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string Tipo { get; set; }
        public string EstadoGeneral { get; set; }
        public string EstadoDocumental { get; set; }
        public string EstadoInspeccion { get; set; }
        public string Inspector { get; set; }
        public string EtapaActual { get; set; }
        public bool FirmaInspector { get; set; }
        public bool FirmaDirdac { get; set; }
        public bool ListoParaFirma { get; set; }
        public DateTime? Fecha { get; set; }
        public string UrlDetalle { get; set; }
        public string UrlVerDocumento { get; set; }
        public string UrlDescargarPdf { get; set; }
        public string UrlAccionPrincipal { get; set; }
        public string TextoAccionPrincipal { get; set; }
        public string ColumnaKanban { get; set; }
        public string ColorKanban { get; set; }
        public bool EsUrgente { get; set; }
        public string ResumenKanban { get; set; }
    }

    public class DashboardInspeccionSeguimientoItemViewModel
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string InspectorAsignado { get; set; }
        public string Estado { get; set; }
        public string EstadoVisual { get; set; }
        public DateTime? FechaAsignacion { get; set; }
        public DateTime? UltimaActualizacion { get; set; }
        public string EtapaActual { get; set; }
        public string UrlDetalle { get; set; }
        public string UrlRevisar { get; set; }
        public string UrlContinuar { get; set; }
        public string TextoContinuar { get; set; }
    }

    public class DashboardInspeccionDocumentoItemViewModel
    {
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string Documento { get; set; }
        public string TipoDocumento { get; set; }
        public string EstadoDocumento { get; set; }
        public bool FirmadoInspector { get; set; }
        public bool FirmadoDirdac { get; set; }
        public DateTime? FechaUltimaActualizacion { get; set; }
        public string UrlVerDocumento { get; set; }
        public string UrlDescargarPdf { get; set; }
        public string UrlRevisar { get; set; }
        public string UrlFirmar { get; set; }
    }

    public class DashboardInspeccionFirmaItemViewModel
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string Documento { get; set; }
        public string FirmanteRequerido { get; set; }
        public string Estado { get; set; }
        public DateTime? FechaEnvio { get; set; }
        public string UrlAccion { get; set; }
        public string TextoAccion { get; set; }
    }

    public class DashboardInspeccionNcItemViewModel
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string TipoNc { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; }
        public string Responsable { get; set; }
        public DateTime? Fecha { get; set; }
        public string UrlAccion { get; set; }
    }
}