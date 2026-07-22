using System;

namespace CapaNegocio.Services
{
    public class AocrRtFlujoEstadoViewModel
    {
        public int UsuarioId { get; set; }
        public string CompaniaCodigo { get; set; }
        public string CompaniaNombre { get; set; }

        public bool TieneCompaniaActiva { get; set; }

        public bool TieneOrdenVigente { get; set; }
        public int? OrdenRecaudacionId { get; set; }
        public string NumeroOrden { get; set; }
        public string EstadoOrden { get; set; }

        public bool TieneComprobante { get; set; }
        public bool PagoAprobado { get; set; }
        public bool Fr3Vinculado { get; set; }

        public bool SolicitudAocrHabilitada { get; set; }
        public bool SolicitudAocrCreada { get; set; }
        public int? SolicitudAocrId { get; set; }
        public string EstadoSolicitudAocr { get; set; }

        public string SiguientePaso { get; set; }
        public string UrlDestino { get; set; }
        public string MensajeUsuario { get; set; }
    }
}
