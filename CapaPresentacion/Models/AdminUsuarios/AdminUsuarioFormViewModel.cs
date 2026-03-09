using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuarioFormViewModel
    {
        public int IdUsuario { get; set; }

        [Required]
        [Display(Name = "Usuario")]
        [StringLength(64)]
        public string CodigoUsuario { get; set; }

        [Required]
        [Display(Name = "Nombres")]
        [StringLength(120)]
        public string NombreUsuario { get; set; }

        [Display(Name = "Apellidos")]
        [StringLength(120)]
        public string ApellidoUsuario { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Correo")]
        [StringLength(160)]
        public string Correo { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        [Display(Name = "Generar contrasena temporal")]
        public bool GenerarPassword { get; set; } = true;

        [Display(Name = "Contrasena inicial")]
        [DataType(DataType.Password)]
        public string PasswordInicial { get; set; }

        public IList<int> RolesSeleccionados { get; set; } = new List<int>();

        public IEnumerable<SelectListItem> RolesDisponibles { get; set; } = new List<SelectListItem>();

        [Display(Name = "Companias RT")]
        public IList<string> CompaniasSeleccionadas { get; set; } = new List<string>();

        public IEnumerable<SelectListItem> CatalogoCompanias { get; set; } = new List<SelectListItem>();

        public bool IsEditMode
        {
            get { return IdUsuario > 0; }
        }
    }
}

