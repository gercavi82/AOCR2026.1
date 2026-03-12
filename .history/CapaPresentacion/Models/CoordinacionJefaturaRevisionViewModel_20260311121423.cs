using System.Collections.Generic;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class CoordinacionJefaturaRevisionViewModel
    {
        public List<SolicitudAOCR> SolicitudesControlDocumental { get; set; } = new List<SolicitudAOCR>();
        public List<SolicitudAOCR> SolicitudesAocrRevision { get; set; } = new List<SolicitudAOCR>();
        public List<Inspeccion> InspeccionesSeguimiento { get; set; } = new List<Inspeccion>();
    }
}