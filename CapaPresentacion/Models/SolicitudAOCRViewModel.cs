using System.Collections.Generic;
using System.Web;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class SolicitudAOCRViewModel
    {
        public SolicitudAOCR Solicitud { get; set; } = new SolicitudAOCR();
        public List<AeronaveSolicitud> Aeronaves { get; set; } = new List<AeronaveSolicitud>();

        public List<Documento> DocumentosExistentes { get; set; } = new List<Documento>();

        // Comprobante (va en aocr_tbpago)
        public string Banco { get; set; }
        public string NumeroComprobante { get; set; }

        // Uploads
        public IEnumerable<HttpPostedFileBase> ArchivosSubidos { get; set; }

        // Usuario logueado
        public Usuario Usuario { get; set; }

        // ✅ AGREGAR ESTO (porque tu vista los usa)
        public string CorreoContactoEcuador { get; set; }
        public string DireccionOperadorVM { get; set; }
        public string TelefonoOperadorVM { get; set; }
        public string CorreoOperadorVM { get; set; }
    }
}
