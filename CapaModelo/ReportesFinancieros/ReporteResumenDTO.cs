using System;
using System.Collections.Generic;

namespace CapaModelo.ReportesFinancieros
{
    public class ReporteResumenDTO
    {
        public int TotalOrdenesGeneradas { get; set; }
        public int TotalOrdenesPagadas { get; set; }
        public int TotalPendientes { get; set; }
        public int TotalAnuladas { get; set; }

        public decimal TotalRecaudado { get; set; }
        public decimal TotalPendientePorCobrar { get; set; }

        public decimal TotalFiltrado { get; set; }
        public decimal SubtotalFiltrado { get; set; }
        public decimal AdministracionFiltrada { get; set; }

        public IList<SerieMensualDTO> IngresosPorMes { get; set; } = new List<SerieMensualDTO>();
        public IList<EstadoTotalDTO> TotalesPorEstado { get; set; } = new List<EstadoTotalDTO>();
        public IList<RecaudacionPorTramiteDTO> RecaudacionPorTramite { get; set; } = new List<RecaudacionPorTramiteDTO>();
        public IList<RecaudacionPorUnidadDTO> RecaudacionPorUnidad { get; set; } = new List<RecaudacionPorUnidadDTO>();
        public IList<ReporteAnulacionDTO> Anulaciones { get; set; } = new List<ReporteAnulacionDTO>();
    }

    public class SerieMensualDTO
    {
        public string Etiqueta { get; set; }
        public decimal Total { get; set; }
    }

    public class EstadoTotalDTO
    {
        public string Estado { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }

    public class RecaudacionPorTramiteDTO
    {
        public string Tramite { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }

    public class RecaudacionPorUnidadDTO
    {
        public string Unidad { get; set; }
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }

    public class ReporteAnulacionDTO
    {
        public string NumeroOrden { get; set; }
        public DateTime Fecha { get; set; }
        public string Unidad { get; set; }
        public string Motivo { get; set; }
        public string RolGestion { get; set; }
        public string Observaciones { get; set; }
    }

    public class FiltroOpcionDTO
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }
}
