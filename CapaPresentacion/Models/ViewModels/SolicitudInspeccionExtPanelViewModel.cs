using System.Collections.Generic;
using CapaDatos.Models;

namespace CapaPresentacion.Models.ViewModels
{
    public class SolicitudInspeccionExtPanelViewModel
    {
        public int OrdenId { get; set; }
        public string EstadoOrden { get; set; }
        public bool TieneInspeccionExt { get; set; }
        public string EstadoDocumentoSolicitudInspeccion { get; set; }
        public string AeropuertosSolicitados { get; set; }
        public bool TienePdfGenerado { get; set; }
        public bool TienePdfFirmado { get; set; }
        public bool PuedeEditarSolicitudInspeccionExt { get; set; }
        public bool PuedeGenerarSolicitud { get; set; }
        public bool PuedeDescargarSolicitud { get; set; }
        public bool PuedeSubirSolicitudFirmada { get; set; }
        public bool PuedeVerSolicitudFirmada { get; set; }
        public bool PuedeContinuarConOrden { get; set; }
        public bool EsNuevaOrden { get; set; }
        public bool MostrarSoloLecturaSinFirmado { get; set; }
        public string UrlGenerarSolicitud { get; set; }
        public string UrlVerSolicitudFirmada { get; set; }
        public string UrlDescargarSolicitudGenerada { get; set; }
        public string UrlSubirSolicitudFirmada { get; set; }
        public string ClaseEstadoCss { get; set; }
        public string MensajeEstado { get; set; }
        public string MensajeSoloLectura { get; set; }
    }

    public class OrdenRecaudacionDetallesViewModel
    {
        public OrdenRecaudacionModel Orden { get; set; } = new OrdenRecaudacionModel();
        public List<DocumentoModel> Documentos { get; set; } = new List<DocumentoModel>();
        public List<PagoModel> Pagos { get; set; } = new List<PagoModel>();
        public FacturaPagoRegistroModel FacturaPago { get; set; }
        public bool TieneComprobanteValido { get; set; }
        public string MensajeComprobante { get; set; }
        public bool AbrirModalPago { get; set; }
        public SolicitudInspeccionExtPanelViewModel SolicitudInspeccionPanel { get; set; } = new SolicitudInspeccionExtPanelViewModel();
    }
}