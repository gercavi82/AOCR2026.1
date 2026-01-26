using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaDatos.Models
{
    public class OrdenRecaudacionModel
    {
        public int Id { get; set; }
        public int CodigoUsuario { get; set; }
        public string CodigoSolicitud { get; set; }

        public string NumeroOrden { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Estado { get; set; }

        public string Observacion { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Admin { get; set; }
        public decimal Total { get; set; }

        public string LugarEmision { get; set; }
        public string Compania { get; set; }
        public string RucCedula { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public int? ConceptoId { get; set; }

        public string NombreContribuyente { get; set; }

        public List<OrdenDetalleModel> Detalles { get; set; } = new List<OrdenDetalleModel>();

        // Props de apoyo UI (NO DB)
        public string NombreUsuario { get; set; }
        public string CorreoUsuario { get; set; }
        public string CreadoPor { get; set; }
        public DateTime? FechaCreacionRegistro { get; set; }

        // ✅ NUEVO: Propiedad usada por la vista Index.cshtml
        // Regla por defecto: VENCE a los 30 días desde FechaCreacion,
        // y solo se considera vencida si NO está PAGADA o ANULADA.
        public bool EstaVencida
        {
            get
            {
                var est = (Estado ?? "").Trim().ToUpperInvariant();

                if (est == "PAGADA" || est == "ANULADA")
                    return false;

                if (FechaCreacion == default(DateTime))
                    return false;

                var fechaVence = FechaCreacion.Date.AddDays(30); // <-- cambia 30 si tu regla es otra
                return DateTime.Now.Date > fechaVence;
            }
        }

        public bool PuedeEditar() => string.Equals(Estado, "BORRADOR", StringComparison.OrdinalIgnoreCase);
        public bool PuedeGenerar() => PuedeEditar() && Total > 0;
        public bool PuedeAnular() =>
            string.Equals(Estado, "BORRADOR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Estado, "GENERADA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Estado, "ENVIADA", StringComparison.OrdinalIgnoreCase);
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
                var estado = (Estado ?? "").Trim().ToUpperInvariant();
                switch (estado)
                {
                    case "BORRADOR":
                        return "secondary"; // Gris
                    case "GENERADA":
                        return "primary"; // Azul
                    case "ENVIADA":
                        return "info"; // Azul claro
                    case "PAGADA":
                        return "success"; // Verde
                    case "ANULADA":
                        return "danger"; // Rojo
                    default:
                        return "secondary"; // Gris por defecto
                }
            }
        }
