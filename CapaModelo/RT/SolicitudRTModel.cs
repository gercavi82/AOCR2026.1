using System;
using System.ComponentModel.DataAnnotations;

namespace CapaModelo.RT
{
    public class SolicitudRTModel
    {
        public int Id { get; set; }

        [Required]
        public int UsuarioRtId { get; set; }

        [Required]
        public int CompaniaId { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "BORRADOR";

        public bool DeclaracionAceptada { get; set; }

        [Required]
        public string DeclaracionTexto { get; set; }

        public DateTime? FechaEnvio { get; set; }

        public string ObservacionCoordinador { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
