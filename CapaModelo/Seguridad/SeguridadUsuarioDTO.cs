using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CapaModelo.Seguridad
{
    public class SeguridadUsuarioDTO
    {
        public int IdUsuario { get; set; }

        [Required]
        [StringLength(64)]
        public string CodigoUsuario { get; set; }

        [Required]
        [StringLength(120)]
        public string NombreUsuario { get; set; }

        [StringLength(120)]
        public string ApellidoUsuario { get; set; }

        [Required]
        [StringLength(160)]
        [EmailAddress]
        public string Correo { get; set; }

        public bool Activo { get; set; }

        public bool MustChangePassword { get; set; }

        public DateTime? UltimoLogin { get; set; }

        public string RolFallback { get; set; }

        public string RolesTexto { get; set; }

        public IList<int> RolesAsignados { get; set; } = new List<int>();
    }
}
