using System;

namespace CapaModelo.ReportesFinancieros
{
    public class FiltroReporteDTO
    {
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string Estado { get; set; }
        public int? UsuarioSolicitanteId { get; set; }
        public int? TipoTramiteId { get; set; }
        public string RolGestion { get; set; }
        public string Unidad { get; set; }

        public string EstadoNormalizado
        {
            get
            {
                return string.IsNullOrWhiteSpace(Estado)
                    ? null
                    : Estado.Trim().ToUpperInvariant();
            }
        }
    }
}
