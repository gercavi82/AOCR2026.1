using System.Collections.Generic;
using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class NoConformidadPdfViewModel
    {
        public Inspeccion Inspeccion { get; set; }
        public SolicitudAOCR Solicitud { get; set; }
        public NoConformidad NoConformidad { get; set; }
        public InspeccionInformeTecnico Informe { get; set; }
        
        public bool EsVistaPrevia { get; set; }
        public bool MostrarMarcaAguaBorrador { get; set; }
        public bool MostrarFirmas { get; set; }
        public bool MostrarFirmaDirector { get; set; }
    }
}
