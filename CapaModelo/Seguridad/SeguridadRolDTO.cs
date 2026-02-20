using System.ComponentModel.DataAnnotations;

namespace CapaModelo.Seguridad
{
    public class SeguridadRolDTO
    {
        public int CodigoRol { get; set; }

        [Required]
        [StringLength(100)]
        public string Descripcion { get; set; }

        public bool Activo { get; set; }
    }
}
