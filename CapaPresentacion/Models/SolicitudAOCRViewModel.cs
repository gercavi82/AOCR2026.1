using System.Collections.Generic;
using System.Web;
using System.ComponentModel.DataAnnotations;
using CapaModelo;

namespace CapaPresentacion.Models
{
    public class SolicitudAOCRViewModel
    {
        // ==========================================================
        // 1. CONSTRUCTOR (Vital para evitar NullReferenceException)
        // ==========================================================
        public SolicitudAOCRViewModel()
        {
            Solicitud = new SolicitudAOCR();
            Aeronaves = new List<Aeronave>();
            DocumentosExistentes = new List<Documento>();
            ArchivosSubidos = new List<HttpPostedFileBase>();
        }

        // ==========================================================
        // 2. ENTIDADES PRINCIPALES
        // ==========================================================
        public SolicitudAOCR Solicitud { get; set; }
        public List<Aeronave> Aeronaves { get; set; }

        // ==========================================================
        // 3. DATOS DE FACTURACIÓN (Validación del lado del servidor)
        // ==========================================================

        [Required(ErrorMessage = "Debe seleccionar el tipo de identificación.")]
        [Display(Name = "Tipo de Identificación")]
        public string TipoIdentificacionFactura { get; set; }

        [Required(ErrorMessage = "El número de identificación es obligatorio.")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "El número debe tener entre 3 y 20 caracteres.")]
        [Display(Name = "RUC / Cédula / ID")]
        // Nota: RegularExpression opcional para permitir solo números si es necesario
        // [RegularExpression("^[0-9]+$", ErrorMessage = "Solo se permiten números.")]
        public string NumeroIdentificacionFactura { get; set; }

        [Required(ErrorMessage = "La razón social es obligatoria.")]
        [StringLength(150, ErrorMessage = "La razón social no puede exceder 150 caracteres.")]
        [Display(Name = "Razón Social")]
        public string RazonSocialFactura { get; set; }

        [Required(ErrorMessage = "Debe seleccionar la forma de pago.")]
        [Display(Name = "Forma de Pago")]
        public string FormaPago { get; set; }

        [StringLength(100, ErrorMessage = "El nombre del banco es muy largo.")]
        [Display(Name = "Banco Emisor")]
        public string Banco { get; set; }

        [StringLength(50, ErrorMessage = "El número de comprobante es muy largo.")]
        [Display(Name = "N° Comprobante / Referencia")]
        public string NumeroComprobante { get; set; }

        // ==========================================================
        // 4. METADATA Y CONTROL
        // ==========================================================

        [Required(ErrorMessage = "Debe especificar el tipo de solicitud.")]
        [Display(Name = "Tipo de Solicitud")]
        public string TipoSolicitud { get; set; }

        // ==========================================================
        // 5. GESTIÓN DE ARCHIVOS
        // ==========================================================

        // Archivos que ya existen en base de datos (para mostrar enlaces de descarga)
        public List<Documento> DocumentosExistentes { get; set; }

        // Archivos nuevos que el usuario está subiendo (HttpPostedFileBase es seguro para MVC)
        [Display(Name = "Adjuntar Documentos")]
        public List<HttpPostedFileBase> ArchivosSubidos { get; set; }
    }
}