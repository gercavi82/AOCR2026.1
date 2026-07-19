using System.Collections.Generic;

namespace CapaPresentacion.Models
{
    public class DashboardGerencialViewModel
    {
        public int TotalSolicitudes { get; set; }
        public int SolicitudesPendientes { get; set; }
        public int SolicitudesObservadas { get; set; }
        public int SolicitudesAceptadasDocumental { get; set; }
        public int InspeccionesPendientes { get; set; }
        public int InspeccionesEnCurso { get; set; }
        public int InspeccionesFinalizadas { get; set; }
        public int AocrEnRevision { get; set; }
        public int AocrValidados { get; set; }
        public int AocrLegalizados { get; set; }
        public int AocrEmitidosRecibidos { get; set; }
        public int InformesPendientesDirdac { get; set; }
        public int AocrPendientesFirmaDirdac { get; set; }
        public int DocumentosInstitucionalesFirmados { get; set; }
        public List<EstadoResumenItem> EstadosSolicitud { get; set; } = new List<EstadoResumenItem>();
        public List<EstadoResumenItem> CuellosBotella { get; set; } = new List<EstadoResumenItem>();
    }

    public class EstadoResumenItem
    {
        public string Estado { get; set; }
        public int Total { get; set; }
    }
}
