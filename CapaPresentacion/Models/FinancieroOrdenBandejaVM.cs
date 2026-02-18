using System;
using System.Collections.Generic;

namespace CapaPresentacion.Models
{
    public class FinancieroOrdenFiltroVM
    {
        public string Estado { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string Solicitante { get; set; }
        public string NumeroOrden { get; set; }
    }

    public class FinancieroOrdenBandejaVM
    {
        public FinancieroOrdenFiltroVM Filtro { get; set; } = new FinancieroOrdenFiltroVM();
        public List<OrdenValidacionFinancieraVM> Ordenes { get; set; } = new List<OrdenValidacionFinancieraVM>();
        public bool SinResultadosConFiltro { get; set; }
        public int TotalSinFiltro { get; set; }
    }
}
