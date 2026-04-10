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

        public List<string> CompaniasDisponibles { get; set; } = new List<string>();
        public List<string> InspectoresDisponibles { get; set; } = new List<string>();
        public List<string> EstadosDisponibles { get; set; } = new List<string>();

        public List<DashboardInspeccionSeguimientoItemViewModel> InspeccionesEnSeguimiento { get; set; } = new List<DashboardInspeccionSeguimientoItemViewModel>();
        public List<DashboardInspeccionDocumentoItemViewModel> ControlDocumental { get; set; } = new List<DashboardInspeccionDocumentoItemViewModel>();
        public List<DashboardInspeccionFirmaItemViewModel> PendientesFirma { get; set; } = new List<DashboardInspeccionFirmaItemViewModel>();
        public List<DashboardInspeccionNcItemViewModel> ObservacionesNc { get; set; } = new List<DashboardInspeccionNcItemViewModel>();

        public int TotalInspecciones
        {
            get { return InspeccionesEnSeguimiento.Count; }
        }

        public int TotalPendientes
        {
            get { return InspeccionesEnSeguimiento.Count(x => !string.Equals(x.EstadoVisual, "FINALIZADA", StringComparison.OrdinalIgnoreCase)); }
        }

        public int TotalObservadas
        {
            get { return InspeccionesEnSeguimiento.Count(x => string.Equals(x.EstadoVisual, "OBSERVADA", StringComparison.OrdinalIgnoreCase)); }
        }

        public int TotalFinalizadas
        {
            get { return InspeccionesEnSeguimiento.Count(x => string.Equals(x.EstadoVisual, "FINALIZADA", StringComparison.OrdinalIgnoreCase)); }
        }
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