using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace CapaDatos.Models
{
    public class PagoModel
    {
        public int CodigoPago { get; set; }

        [Required(ErrorMessage = "El código de solicitud es requerido")]
        public int CodigoSolicitud { get; set; }

        [StringLength(50)]
        public string NumeroFactura { get; set; }

        [Required(ErrorMessage = "El monto es requerido")]
        [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        [StringLength(3)]
        public string Moneda { get; set; } = "USD";

        public string Concepto { get; set; }

        [StringLength(50)]
        public string MetodoPago { get; set; }

        [StringLength(50)]
        public string Estado { get; set; } = "Pendiente";

        public DateTime? FechaPago { get; set; }
        public DateTime? FechaValidacion { get; set; }

        [StringLength(100)]
        public string ValidadoPor { get; set; }

        public string Observaciones { get; set; }

        [StringLength(255)]
        public string ComprobanteRuta { get; set; }

        // Propiedad para manejo de archivos
        public HttpPostedFileBase ComprobanteArchivo { get; set; }

        // Relación opcional
        public OrdenRecaudacionModel Orden { get; set; }
    }
}
