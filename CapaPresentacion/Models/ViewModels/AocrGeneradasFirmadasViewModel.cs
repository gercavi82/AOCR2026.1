using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace CapaPresentacion.Models.ViewModels
{
    public class AocrGeneradasFirmadasFiltroViewModel
    {
        public string Search { get; set; }
        public string EstadoFinal { get; set; }
        public string EstadoFirma { get; set; }
        public string TipoTramite { get; set; }
        public string SoloConPdf { get; set; }
        public string DocumentoPendiente { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 15;
    }

    public class AocrGeneradasFirmadasRowViewModel
    {
        public int SolicitudId { get; set; }
        public int? InspeccionId { get; set; }
        public int? InformeId { get; set; }
        public int? CertificadoId { get; set; }
        public int? FirmaCondicionesId { get; set; }
        public int? FirmaReconocimientoId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NumeroAocr { get; set; }
        public string TipoTramite { get; set; }
        public string NombreExplotador { get; set; }
        public string InspectorNombre { get; set; }
        public string CoordinadorNombre { get; set; }
        public string EstadoInformeTecnico { get; set; }
        public string ResultadoTecnicoFinal { get; set; }
        public string EstadoAocr { get; set; }
        public string EstadoCondiciones { get; set; }
        public string EstadoFirma { get; set; }
        public string EstadoFinal { get; set; }
        public string BadgeEstadoAocrCss { get; set; }
        public string BadgeEstadoCondicionesCss { get; set; }
        public string BadgeEstadoFirmaCss { get; set; }
        public string BadgeEstadoFinalCss { get; set; }
        public DateTime? FechaSolicitud { get; set; }
        public DateTime? FechaUltimoHito { get; set; }
        public string NombreFirmante { get; set; }
        public bool UsaFlujoCondiciones { get; set; }
        public bool TienePdfPreliminar { get; set; }
        public bool TienePdfFirmado { get; set; }
        public string UrlDetalleSolicitud { get; set; }
        public string UrlDetalleInspeccion { get; set; }
        public string UrlHistorial { get; set; }
        public string UrlPreliminar { get; set; }
        public string UrlFinal { get; set; }
        public string UrlGestion { get; set; }
        public string UrlValidacion { get; set; }
    }

    public class AocrGeneradasFirmadasViewModel
    {
        public AocrGeneradasFirmadasFiltroViewModel Filtros { get; set; } = new AocrGeneradasFirmadasFiltroViewModel();
        public IList<AocrGeneradasFirmadasRowViewModel> Items { get; set; } = new List<AocrGeneradasFirmadasRowViewModel>();
        public IList<SelectListItem> EstadosFinales { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> EstadosFirma { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> TiposTramite { get; set; } = new List<SelectListItem>();
        public int TotalRegistros { get; set; }
        public int TotalFirmadas { get; set; }
        public int TotalPendientesFirma { get; set; }
        public int TotalObservadas { get; set; }
        public int TotalConPdf { get; set; }
        public int PaginaActual { get; set; }
        public int TotalPaginas { get; set; }
        public int PageSize { get; set; }
        public bool EsAdministrador { get; set; }
        public bool EsSolicitante { get; set; }
        public bool EsInspector { get; set; }
        public bool EsCoordinacion { get; set; }
        public bool EsDireccion { get; set; }
        public string EmptyStateTitle { get; set; } = "No existen AOCR generadas o firmadas para los criterios seleccionados.";
        public string EmptyStateMessage { get; set; } = "La consulta no elimina trámites por falta de PDF; si una fila no aparece es porque no se encuentra en la etapa AOCR visible para su rol o quedó fuera de los filtros activos.";
        public bool TieneResultados => Items != null && Items.Count > 0;
    }
}
