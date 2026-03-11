using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class InformeTecnicoPdfViewModel
    {
        public Inspeccion Inspeccion { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public InspeccionInformeTecnico Informe { get; set; }
    }
}