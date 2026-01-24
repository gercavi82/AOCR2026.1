using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapaDatos.Models
{
    public class OrdenRecaudacionModel
    {
        // =========================
        // Identificación
        // =========================
        public int Id { get; set; }

        [Required(ErrorMessage = "El código de usuario es requerido")]
        public int CodigoUsuario { get; set; }

        [StringLength(50)]
        public string CodigoSolicitud { get; set; }

        [Required(ErrorMessage = "El número de orden es requerido")]
        [StringLength(30)]
        public string NumeroOrden { get; set; }

        // =========================
        // Fechas
        // =========================
        [Required(ErrorMessage = "La fecha de creación es requerida")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaVencimiento { get; set; }

        public DateTime? FechaPago { get; set; }

        // =========================
        // Estado / Observación
        // =========================
        [Required(ErrorMessage = "El estado es requerido")]
        [StringLength(20)]
        public string Estado { get; set; } = "BORRADOR"; // BORRADOR, GENERADA, ENVIADA, PAGADA, ANULADA

        public string Observacion { get; set; }

        // =========================
        // Valores
        // =========================
        [Required(ErrorMessage = "El subtotal es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El subtotal debe ser mayor o igual a 0")]
        public decimal Subtotal { get; set; }

        [Required(ErrorMessage = "El valor de administración es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El valor de administración debe ser mayor o igual a 0")]
        public decimal Admin { get; set; }

        [Required(ErrorMessage = "El total es requerido")]
        [Range(0, double.MaxValue, ErrorMessage = "El total debe ser mayor o igual a 0")]
        public decimal Total { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El monto pagado debe ser mayor o igual a 0")]
        public decimal MontoPagado { get; set; }

        public string ReferenciaPago { get; set; }

        [NotMapped]
        public decimal SaldoPendiente => Math.Max(0, Total - MontoPagado);

        // =========================
        // Emisión / Compañía
        // =========================
        [StringLength(100)]
        public string LugarEmision { get; set; } = "Quito";

        [Required(ErrorMessage = "La compañía es requerida")]
        [StringLength(100)]
        public string Compania { get; set; }

        [Required(ErrorMessage = "El RUC/Cédula es requerido")]
        [StringLength(20)]
        public string RucCedula { get; set; }

        [EmailAddress(ErrorMessage = "El formato del correo es inválido")]
        [StringLength(100)]
        public string Correo { get; set; }

        [StringLength(20)]
        public string Telefono { get; set; }

        // =========================
        // Concepto
        // =========================
        public int? ConceptoId { get; set; }
        public ConceptoModel Concepto { get; set; }

        // Si quieres guardar texto del concepto aparte (opcional):
        [StringLength(200)]
        public string ConceptoTexto { get; set; }

        // =========================
        // Contribuyente
        // =========================
        public int ContribuyenteId { get; set; }
        public string NombreContribuyente { get; set; }
        public string EmailContribuyente { get; set; }
        public ContribuyenteModel Contribuyente { get; set; }

        // =========================
        // Navegación
        // =========================
        public List<OrdenDetalleModel> Detalles { get; set; } = new List<OrdenDetalleModel>();
        public List<DocumentoModel> Documentos { get; set; } = new List<DocumentoModel>();

        // =========================
        // Calculadas / UI
        // =========================
        [NotMapped]
        public bool TieneSaldoPendiente => !string.Equals(Estado, "PAGADA", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public string EstadoColor
        {
            get
            {
                switch ((Estado ?? "").ToUpperInvariant())
                {
                    case "GENERADA": return "success";
                    case "BORRADOR": return "warning";
                    case "ENVIADA": return "info";
                    case "PAGADA": return "primary";
                    case "ANULADA": return "danger";
                    default: return "secondary";
                }
            }
        }

        // =========================
        // Info del usuario (si aplica)
        // =========================
        public string NombreUsuario { get; set; }
        public string CorreoUsuario { get; set; }

        // ==========================================================
        // ALIAS (compatibilidad con nombres anteriores)
        // ==========================================================
        [NotMapped]
        [Obsolete("Usa FechaCreacion")]
        public DateTime FechaOrden
        {
            get => FechaCreacion;
            set => FechaCreacion = value;
        }

        [NotMapped]
        [Obsolete("Usa Total")]
        public decimal MontoTotal
        {
            get => Total;
            set => Total = value;
        }

        // Antes tenías 'Concepto' string; ahora queda como alias de ConceptoTexto
        [NotMapped]
        [Obsolete("Usa ConceptoTexto o la navegación Concepto")]
        public string ConceptoNombre
        {
            get => ConceptoTexto;
            set => ConceptoTexto = value;
        }
    }
}
