using System.ComponentModel.DataAnnotations;
using System.Web;

namespace CapaModelo.RT.ViewModels
{
    public class DesignacionUploadVM
    {
        [Required]
        public int SolicitudId { get; set; }

        public string NombreArchivoActual { get; set; }

        public string Estado { get; set; }

        [Required(ErrorMessage = "Debe adjuntar el PDF de Designación RT legalizada")]
        public HttpPostedFileBase ArchivoPdf { get; set; }
    }
}
