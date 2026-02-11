using System.ComponentModel.DataAnnotations;
using System.Web;

namespace CapaPresentacion.Models.RT
{
    public class DesignacionUploadVM
    {
        public int SolicitudId { get; set; }

        [Display(Name = "Archivo PDF de designación (legalizado)")]
        public HttpPostedFileBase ArchivoPdf { get; set; }

        public string NombreArchivoActual { get; set; }
        public bool TieneArchivo => !string.IsNullOrWhiteSpace(NombreArchivoActual);
    }
}