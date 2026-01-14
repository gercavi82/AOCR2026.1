using System;
using System.ComponentModel.DataAnnotations;

namespace CapaModelo
{
    public class ChecklistItem
    {
        // Llaves Primarias y Foráneas
        public int CodigoChecklist { get; set; } // De aocr_tbchecklist
        public int CodigoItem { get; set; }      // De aocr_tbchecklist_item
        public int CodigoSolicitud { get; set; } // De aocr_tbchecklist_solicitud

        // Datos del ítem
        [Required(ErrorMessage = "La sección es obligatoria.")]
        public string Seccion { get; set; }

        public int ItemNumero { get; set; }

        [Required(ErrorMessage = "La descripción no puede estar vacía.")]
        public string Descripcion { get; set; }

        // Resultado de la inspección
        public bool? Cumple { get; set; } // null = pendiente, true = Sí, false = No

        [StringLength(500)]
        public string Observaciones { get; set; }

        public string Criticidad { get; set; }

        // Auditoría
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        public string UsuarioRegistro { get; set; }
    }
}