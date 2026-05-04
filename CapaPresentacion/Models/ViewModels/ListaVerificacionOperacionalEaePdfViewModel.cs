using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class ListaVerificacionOperacionalEaePdfViewModel
    {
        public Inspeccion Inspeccion { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public ListaVerificacionOperacionalEae ListaVerificacion { get; set; }
        public bool MostrarMarcaAguaBorrador { get; set; }
        public bool MostrarFirmas { get; set; }
        public string EstadoDocumento { get; set; }
    }
}