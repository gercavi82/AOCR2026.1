using System.ComponentModel.DataAnnotations;

namespace CapaModelo.Seguridad
{
    public class SeguridadPermisoDTO
    {
        public int IdPermiso { get; set; }

        [Required]
        [StringLength(80)]
        public string Codigo { get; set; }

        [Required]
        [StringLength(180)]
        public string Nombre { get; set; }

        [StringLength(80)]
        public string Modulo { get; set; }

        [StringLength(30)]
        public string TipoAccion { get; set; }

        [StringLength(300)]
        public string Descripcion { get; set; }

        public bool Activo { get; set; }
    }
}
