using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuarioInternoRTViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Buscar por cédula o nombre")]
        [StringLength(64)]
        public string CodigoUsuarioBusqueda { get; set; }

        [Display(Name = "Código de usuario")]
        [StringLength(64)]
        public string CodigoUsuario { get; set; }

        [Display(Name = "Cédula")]
        [StringLength(20)]
        public string Cedula { get; set; }

        [Display(Name = "Nombre completo")]
        [StringLength(100)]
        public string NombreCompleto { get; set; }

        [Display(Name = "Tipo inspector")]
        [StringLength(10)]
        public string TipoInspector { get; set; }

        [Display(Name = "Ciudad (usucod9)")]
        [StringLength(10)]
        public string CiudadCodigo { get; set; }

        [Display(Name = "Código financiero (usuoid)")]
        public decimal? CodigoFinanciero { get; set; }

        [Display(Name = "OPCOI3 (= usuoid)")]
        public decimal? Opcoi3 { get; set; }

        [Display(Name = "OPCAR5 (aeropuerto)")]
        [StringLength(10)]
        public string Opcar5 { get; set; }

        [Required]
        [Display(Name = "Rol interno")]
        [StringLength(100)]
        public string RolInterno { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Correo institucional")]
        [StringLength(200)]
        public string CorreoInstitucional { get; set; }

        [Display(Name = "Observaciones")]
        [StringLength(1000)]
        public string Observaciones { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        public IEnumerable<SelectListItem> RolesInternos { get; set; }
    }
}
