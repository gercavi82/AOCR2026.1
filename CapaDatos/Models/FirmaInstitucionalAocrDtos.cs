using System;
using System.Collections.Generic;

namespace CapaDatos.Models
{
    public class FirmaInstitucionalAocrFilaDto
    {
        public int SolicitudId { get; set; }
        public int InspeccionId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Explotador { get; set; }
        public string TipoTramite { get; set; }
        public string Inspector { get; set; }
        public string UsuarioDcav { get; set; }
        public DateTime FechaAprobacion { get; set; }
        public string Estado { get; set; }
        public long VersionExpediente { get; set; }
        public int AocrId { get; set; }
        public int VersionAocr { get; set; }
        public int AocrPdfId { get; set; }
        public string EstadoAocr { get; set; }
        public int CondicionesId { get; set; }
        public int VersionCondiciones { get; set; }
        public int CondicionesPdfId { get; set; }
        public string EstadoCondiciones { get; set; }
        public int InformeId { get; set; }
        public string InformeRuta { get; set; }
        public int LvEaeId { get; set; }
        public string LvEaeRuta { get; set; }
    }

    public sealed class FirmaInstitucionalAocrDetalleDto : FirmaInstitucionalAocrFilaDto
    {
        public IList<HistorialDocumentoDcavDto> Historial { get; set; } = new List<HistorialDocumentoDcavDto>();
    }
}
