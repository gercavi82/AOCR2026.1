using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuarioInternoRTViewModel
    {
        [Required]
        [Display(Name = "Usuario interno")]
        [StringLength(64)]
        public string CodigoUsuarioBusqueda { get; set; }

        [Display(Name = "Codigo usuario")]
        [StringLength(64)]
        public string CodigoUsuario { get; set; }

        [Display(Name = "Ciudad (usucod9)")]
        [StringLength(10)]
        public string CiudadCodigo { get; set; }

        [Display(Name = "Codigo financiero (usuoid)")]
        public decimal? CodigoFinanciero { get; set; }

        [Required]
        [Display(Name = "OPCAR5 (aeropuerto)")]
        [StringLength(10)]
        public string Opcar5 { get; set; }

        [Display(Name = "OPCAER (aeropuerto)")]
        [StringLength(10)]
        public string Opcaer { get; set; }

        [Display(Name = "OPCOI3 (= usuoid)")]
        public decimal? Opcoi3 { get; set; }

        public IEnumerable<SelectListItem> Aeropuertos { get; set; } = new List<SelectListItem>();
    }
}
