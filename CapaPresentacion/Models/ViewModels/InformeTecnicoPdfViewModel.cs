using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class InformeTecnicoPdfViewModel
    {
        public Inspeccion Inspeccion { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public InspeccionInformeTecnico Informe { get; set; }
        public bool EsVistaPrevia { get; set; }
        public bool EsDefinitivo { get; set; }
        public bool MostrarMarcaAguaBorrador { get; set; }
        public bool MostrarFirmas { get; set; }
        public bool MostrarFirmaInspector { get; set; }
        public bool MostrarFirmaDirector { get; set; }
        public string EstadoDocumento { get; set; }
    }
}
