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

        [Required(ErrorMessage = "Debe adjuntar el PDF de la declaración RT firmada")]
        public HttpPostedFileBase ArchivoPdf { get; set; }
    }

    public class SolicitudAceptacionRtVM
    {
        [Range(1, int.MaxValue)]
        public int SolicitudId { get; set; }

        [Required(ErrorMessage = "Ingrese el nombre del Gerente de Operaciones.")]
        [StringLength(150)]
        public string NombreGerente { get; set; }

        [Required]
        [StringLength(100)]
        public string CargoGerente { get; set; } = "Gerente de Operaciones";

        [Required(ErrorMessage = "Ingrese el lugar de emisión.")]
        [StringLength(100)]
        public string LugarEmision { get; set; }
    }
}
