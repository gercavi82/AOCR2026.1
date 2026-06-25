using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CapaDatos.Constants;

namespace CapaDatos.Models
{
    public class OrdenRecaudacionModel
    {
        public int Id { get; set; }
        public string NumeroOrden { get; set; }
        public string Estado { get; set; }
        public decimal Total { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string NombreContribuyente { get; set; }
        public int CodigoUsuario { get; set; }
        public string CodigoSolicitud { get; set; }
        public string LugarEmision { get; set; }
        public string Compania { get; set; }
        public string RucCedula { get; set; }
        public string Correo { get; set; }

        [StringLength(15, ErrorMessage = "Máximo 15 dígitos")]
        [RegularExpression(@"^\d{0,15}$", ErrorMessage = "El teléfono solo debe contener números")]
        public string Telefono { get; set; }

        public string Observacion { get; set; }
        public string Observaciones { get { return Observacion; } set { Observacion = value; } }
        public decimal Admin { get; set; }

        public List<OrdenDetalleModel> Detalles { get; set; } = new List<OrdenDetalleModel>();

        // Props de apoyo UI (NO DB)
        public string NombreUsuario { get; set; }
        public string UsuarioNombre { get; set; }
        public string NumeroFr3 { get; set; }
        public string CorreoUsuario { get; set; }
        public string CreadoPor { get; set; }
        public DateTime? FechaCreacionRegistro { get; set; }
        public string NumeroSolicitud { get; set; }

        // ✅ NUEVO: Propiedad usada por la vista Index.cshtml
        // Regla por defecto: VENCE a los 30 días desde FechaCreacion,
        // y solo se considera vencida si NO está PAGADA o ANULADA.
        public bool EstaVencida
        {
            get
            {
                var est = EstadoOrden.NormalizarEstado(Estado);

                if (est == EstadoOrden.Pagada ||
                    est == EstadoOrden.Anulada ||
                    est == EstadoOrden.Facturada ||
                    est == EstadoOrden.Completada)
                    return false;

                if (FechaCreacion == default(DateTime))
                    return false;

                var fechaVence = FechaCreacion.Date.AddDays(30); // <-- cambia 30 si tu regla es otra
                return DateTime.Now.Date > fechaVence;
            }
        }

        public bool PuedeEditar() => string.Equals(EstadoOrden.NormalizarEstado(Estado), EstadoOrden.Borrador, StringComparison.OrdinalIgnoreCase);
        public bool PuedeGenerar() => PuedeEditar() && Total > 0;
        public bool PuedeAnular()
        {
            var estado = EstadoOrden.NormalizarEstado(Estado);
            return string.Equals(estado, EstadoOrden.Borrador, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, EstadoOrden.Generada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, EstadoOrden.Pendiente, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, EstadoOrden.Enviada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, EstadoOrden.EnRevisionFinanciera, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, EstadoOrden.Devuelta, StringComparison.OrdinalIgnoreCase);
        }
        public string ConceptoNombre
        {
            get
            {
                return Detalles != null && Detalles.Any()
                    ? Detalles.First().ConceptoNombre
                    : null;
            }
        }

        // Propiedad para determinar el color del badge según el estado
        public string EstadoColor
        {
            get
            {
                var estado = EstadoOrden.NormalizarEstado(Estado);
                switch (estado)
                {
                    case EstadoOrden.Borrador:
                        return "secondary"; // Gris
                    case EstadoOrden.Pendiente:
                    case EstadoOrden.Generada:
                        return "warning"; // Amarillo
                    case EstadoOrden.Enviada:
                        return "info"; // Azul
                    case EstadoOrden.EnRevisionFinanciera:
                        return "info"; // Azul claro
                    case EstadoOrden.Devuelta:
                        return "danger"; // Rojo
                    case EstadoOrden.Facturada:
                    case EstadoOrden.Completada:
                    case EstadoOrden.Pagada:
                        return "success"; // Verde
                    case EstadoOrden.Anulada:
                        return "danger"; // Rojo
                    default:
                        return "secondary"; // Gris por defecto
                }
            }
        }
    }

    // PagoModel ya no se define aquí - usar el de CapaDatos.Models existente o CapaModelo
}
