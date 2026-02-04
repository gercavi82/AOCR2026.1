using System.Collections.Generic;
using System.Web;
using CapaModelo;
using Newtonsoft.Json;

namespace CapaPresentacion.Models
{
    public class SolicitudAOCRViewModel
    {
        public SolicitudAOCR Solicitud { get; set; } = new SolicitudAOCR();

        // OJO: usa Aeronave (si tu modelo actual es Aeronave) o AeronaveSolicitud (si usas la tabla aocr_tbaeronave_solicitud)
        public List<AeronaveSolicitud> Aeronaves { get; set; } = new List<AeronaveSolicitud>();

        public List<Documento> DocumentosExistentes { get; set; } = new List<Documento>();

        // Comprobante (va en aocr_tbpago)
        public string Banco { get; set; }
        public string NumeroComprobante { get; set; }

        // Uploads - En JSON esto sera null, pero se puede ignorar
        [JsonIgnore]
        public IEnumerable<HttpPostedFileBase> ArchivosSubidos { get; set; }

        // Usuario logueado - En JSON esto sera null, pero se puede ignorar
        [JsonIgnore]
        public Usuario Usuario { get; set; }
    }
}
