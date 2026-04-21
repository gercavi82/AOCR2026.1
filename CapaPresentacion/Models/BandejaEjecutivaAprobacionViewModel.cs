using System.Collections.Generic;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class BandejaEjecutivaAprobacionViewModel
    {
        public BandejaEjecutivaAprobacionViewModel()
        {
            Solicitudes = new List<SolicitudAOCR>();
            SolicitudesFiltradas = new List<SolicitudAOCR>();
            FiltroActivo = "todas";
            FiltroEsExplicito = false;
        }

        public List<SolicitudAOCR> Solicitudes { get; set; }
        public List<SolicitudAOCR> SolicitudesFiltradas { get; set; }
        public string FiltroActivo { get; set; }

        /// <summary>
        /// true si el usuario eligió el filtro explícitamente via URL; false si fue seleccionado automáticamente.
        /// </summary>
        public bool FiltroEsExplicito { get; set; }

        public int Total { get; set; }
        public int TotalEnRevision { get; set; }
        public int TotalObservadas { get; set; }
        public int TotalSubsanadas { get; set; }
        public int TotalJefatura { get; set; }
        public int TotalLegal { get; set; }
        public int TotalFiltradas { get; set; }

        public bool TieneDatos
        {
            get
            {
                return Solicitudes != null && Solicitudes.Count > 0;
            }
        }

        public bool TieneDatosFiltrados
        {
            get
            {
                return SolicitudesFiltradas != null && SolicitudesFiltradas.Count > 0;
            }
        }

        /// <summary>
        /// Etiqueta legible del filtro activo para mostrar en mensajes contextuales.
        /// </summary>
        public string FiltroActivoEtiqueta
        {
            get
            {
                switch (FiltroActivo)
                {
                    case "enrevision":  return "En revisión";
                    case "observadas":  return "Observadas";
                    case "subsanadas":  return "Subsanadas";
                    case "jefatura":    return "Jefatura técnica";
                    case "legal":       return "Revisión legal";
                    default:            return "Todas";
                }
            }
        }
    }
}
