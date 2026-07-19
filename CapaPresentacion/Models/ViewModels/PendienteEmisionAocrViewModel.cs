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
        public string NumeroSolicitud { get; set; }
        public string CompaniaRuc { get; set; }
        public string CompaniaNombre { get; set; }
        public string NumeroInspeccion { get; set; }
        public string TipoTramite { get; set; }
        public string InspectorAsignado { get; set; }
        public DateTime? FechaAprobacionDirdac { get; set; }
        public string EstadoAocr { get; set; }
        public string EstadoCondiciones { get; set; }
        public bool GenerarAocr { get; set; }
        public bool GenerarCondiciones { get; set; }
    }
}
