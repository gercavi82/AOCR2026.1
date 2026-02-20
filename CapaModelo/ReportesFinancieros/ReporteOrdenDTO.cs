using System;

namespace CapaModelo.ReportesFinancieros
{
    public class ReporteOrdenDTO
    {
        public int OrdenId { get; set; }
        public string NumeroOrden { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaPago { get; set; }
        public string Estado { get; set; }

        public int UsuarioSolicitanteId { get; set; }
        public string UsuarioSolicitante { get; set; }
        public int? TipoTramiteId { get; set; }
        public string TipoTramite { get; set; }
        public string RolGestion { get; set; }
        public string Unidad { get; set; }

        public string Compania { get; set; }
        public string RucCedula { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Administracion { get; set; }
        public decimal Total { get; set; }
        public decimal MontoPagado { get; set; }
        public decimal SaldoPendiente { get; set; }

        public string Observacion { get; set; }
        public string MotivoAnulacion { get; set; }
    }
}
