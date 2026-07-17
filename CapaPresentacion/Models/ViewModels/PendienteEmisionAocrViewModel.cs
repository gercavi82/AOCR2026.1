using System;
using System.Collections.Generic;
using CapaModelo;

namespace CapaPresentacion.Models.ViewModels
{
    public class PendienteEmisionAocrViewModel
    {
        public IList<PendienteEmisionAocrItemViewModel> Inspecciones { get; set; } = new List<PendienteEmisionAocrItemViewModel>();
    }

    public class PendienteEmisionAocrItemViewModel
    {
        public int InspeccionId { get; set; }
        public int SolicitudId { get; set; }
        public string CompaniaRuc { get; set; }
        public string CompaniaNombre { get; set; }
        public string TramiteAocr { get; set; }
        public DateTime FechaAsignacion { get; set; }
        public string EstadoNormalizado { get; set; }
        public bool PuedeGenerarAocr { get; set; }
        public bool CondicionesRedactadas { get; set; }
        public string MotivoBloqueoAocr { get; set; }
    }
}
