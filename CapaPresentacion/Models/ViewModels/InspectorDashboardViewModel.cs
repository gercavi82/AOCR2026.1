using System;
using System.Collections.Generic;

namespace CapaPresentacion.Models.ViewModels
{
    public class InspectorDashboardViewModel
    {
        public int CodigoInspector { get; set; }
        public string NombreInspector { get; set; }

        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string Estado { get; set; }
        public string Compania { get; set; }
        public int? CodigoSolicitud { get; set; }

        public int InspeccionesAsignadas { get; set; }
        public int InspeccionesPendientes { get; set; }
        public int InspeccionesConNc { get; set; }
        public int InspeccionesCerradas { get; set; }
        public int InspeccionesRequierenNueva { get; set; }
        public int DocumentosPendientesRevision { get; set; }
        public decimal TiempoPromedioAtencionHoras { get; set; }

        public List<InspectorInspeccionItemViewModel> UltimasInspecciones { get; set; }
        public List<InspectorAlertaViewModel> AlertasUrgentes { get; set; }

        public InspectorDashboardViewModel()
        {
            UltimasInspecciones = new List<InspectorInspeccionItemViewModel>();
            AlertasUrgentes = new List<InspectorAlertaViewModel>();
        }
    }

    public class InspectorInspeccionItemViewModel
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroInspeccion { get; set; }
        public string Estado { get; set; }
        public string Resultado { get; set; }
        public string Operador { get; set; }
        public DateTime? FechaProgramada { get; set; }
        public DateTime? UltimaActualizacion { get; set; }
        public bool TieneNoConformidadAbierta { get; set; }
    }

    public class InspectorAlertaViewModel
    {
        public string Tipo { get; set; }
        public string Titulo { get; set; }
        public string Mensaje { get; set; }
        public string UrlDestino { get; set; }
        public string Severidad { get; set; }
        public DateTime Fecha { get; set; }
    }
}
