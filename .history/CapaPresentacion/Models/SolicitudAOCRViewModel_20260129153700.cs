using System.Collections.Generic;
using System.Web;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class SolicitudAOCRViewModel
    {
        public SolicitudAOCR Solicitud { get; set; } = new SolicitudAOCR();

        // Lista de aeronaves asociadas a la solicitud
        public List<AeronaveSolicitud> Aeronaves { get; set; } = new List<AeronaveSolicitud>();

        // Documentos existentes
        public List<Documento> DocumentosExistentes { get; set; } = new List<Documento>();

        // Información de pago/comprobante
        public string Banco { get; set; }
        public string NumeroComprobante { get; set; }

        // Archivos subidos desde la vista
        public IEnumerable<HttpPostedFileBase> ArchivosSubidos { get; set; }

        // Usuario logueado
        public Usuario Usuario { get; set; }

        // Datos adicionales para formularios específicos
        public string CartaTexto { get; set; }
        public List<string> CompaniasSeleccionadas { get; set; } = new List<string>();
    }
}
