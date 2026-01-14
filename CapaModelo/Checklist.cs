using System;
using System.ComponentModel.DataAnnotations;

namespace CapaModelo
{
    public class Checklist
    {
        public int CodigoChecklist { get; set; }

        public int? CodigoInspeccion { get; set; }

        [Display(Name = "Sección")]
        public string Seccion { get; set; }

        [Display(Name = "Ítem N°")]
        public string ItemNumero { get; set; }

        [Display(Name = "Descripción")]
        public string Descripcion { get; set; }

        [Display(Name = "Cumple")]
        public string Cumple { get; set; } // "Si", "No", "N/A"

        [Display(Name = "Observaciones")]
        public string Observaciones { get; set; }

        public string Criticidad { get; set; }

        public int CodigoSolicitud { get; set; }

        // Auditoría
        public DateTime? CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }

        public bool Activo { get; set; } = true; // útil si haces borrado lógico
    }
}
