using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using CapaModelo.Seguridad;

namespace CapaPresentacion.Models.AdminUsuarios
{
    public class AdminUsuarioTransferenciaViewModel
    {
        [Required]
        public int UsuarioOrigenId { get; set; }
        public string UsuarioOrigenCodigo { get; set; }
        public string UsuarioOrigenNombreCompleto { get; set; }
        public string UsuarioOrigenCorreo { get; set; }
        public bool UsuarioOrigenActivo { get; set; }

        [Required(ErrorMessage = "Seleccione un usuario destino.")]
        [Display(Name = "Usuario destino")]
        public int UsuarioDestinoId { get; set; }

        [Required(ErrorMessage = "El motivo de transferencia es obligatorio.")]
        [StringLength(500, MinimumLength = 8, ErrorMessage = "El motivo debe tener entre 8 y 500 caracteres.")]
        [Display(Name = "Motivo")]
        public string Motivo { get; set; }

        [Display(Name = "Confirmo la transferencia y desactivacion")]
        public bool ConfirmarTransferencia { get; set; }

        public IList<SelectListItem> UsuariosDestino { get; set; } = new List<SelectListItem>();
        public UsuarioTransferenciaPreviewDTO Impacto { get; set; } = new UsuarioTransferenciaPreviewDTO();
        public UsuarioTransferenciaResultadoDTO Resultado { get; set; }
    }
}
