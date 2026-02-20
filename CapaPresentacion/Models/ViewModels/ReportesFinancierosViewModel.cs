using System.Collections.Generic;
using System.Web.Mvc;
using CapaModelo.ReportesFinancieros;

namespace CapaPresentacion.Models.ViewModels
{
    public class ReportesFinancierosViewModel
    {
        public FiltroReporteDTO Filtros { get; set; } = new FiltroReporteDTO();
        public ReporteResumenDTO Resumen { get; set; } = new ReporteResumenDTO();
        public IList<ReporteOrdenDTO> Ordenes { get; set; } = new List<ReporteOrdenDTO>();

        public IList<SelectListItem> EstadosDisponibles { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> UsuariosDisponibles { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> TramitesDisponibles { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> RolesGestionDisponibles { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> UnidadesDisponibles { get; set; } = new List<SelectListItem>();
    }
}
