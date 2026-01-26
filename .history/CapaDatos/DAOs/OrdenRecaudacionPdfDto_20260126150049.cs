using System;
using System.Collections.Generic;

namespace CapaModelo.DTOs
{
    public class OrdenRecaudacionPdfDto
    {
        public OrdenRecaudacionPdfDto()
        {
            Detalles = new List<OrdenRecaudacionPdfDetalleDto>();

            // Compatibilidad: evitar nulls
            ConceptoPrincipal = "";
            Estaciones = 0;
            Dias = 0;
            ValorBase = 0m;
            ValorInspecciones = 0m;
            ValorViaticos = 0m;
        }

        // =========================
        // CABECERA
        // =========================
        public int OrdenId { get; set; }
        public string NumeroOrden { get; set; }
        public DateTime FechaEmision { get; set; }
        public string LugarEmision { get; set; }

        public string NombreCompania { get; set; }
        public string Ruc { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }

        public string Referencia { get; set; }
        public string Observacion { get; set; }

        public string NombreInspector { get; set; }
        public string CargoInspector { get; set; }

        // =========================
        // COMPATIBILIDAD CON PdfGeneratorService (para que compile)
        // =========================
        public string ConceptoPrincipal { get; set; }      // antes lo usabas en PdfGeneratorService
        public decimal ValorBase { get; set; }             // antes lo usabas en PdfGeneratorService
        public int Estaciones { get; set; }                // antes lo usabas en PdfGeneratorService
        public decimal ValorInspecciones { get; set; }     // antes lo usabas en PdfGeneratorService
        public int Dias { get; set; }                      // antes lo usabas en PdfGeneratorService
        public decimal ValorViaticos { get; set; }         // antes lo usabas en PdfGeneratorService

        // =========================
        // DETALLES
        // =========================
        public List<OrdenRecaudacionPdfDetalleDto> Detalles { get; private set; }

        // =========================
        // TOTALES (solo lectura hacia afuera)
        // =========================
        public decimal Subtotal { get; private set; }
        public decimal ValorGastosAdmin { get; private set; }
        public decimal Total { get; private set; }
        public string TotalEnLetras { get; private set; }

        // =========================
        // CALCULAR
        // =========================
        public void CalcularTotales()
        {
            // Siempre calcular desde campos de compatibilidad para que coincida con lo mostrado en PDF
            Subtotal = Math.Round(ValorBase + ValorInspecciones + ValorViaticos, 2);
            ValorGastosAdmin = Math.Round(ValorViaticos * 0.08m, 2); // 8% sobre viáticos
            Total = Math.Round(ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin, 2);

            // Si tienes conversor a letras real, cámbialo aquí.
            TotalEnLetras = Total.ToString("N2");
        }
    }

    public class OrdenRecaudacionPdfDetalleDto
    {
        public string CodigoConcepto { get; set; }
        public string NombreConcepto { get; set; }

        public int Cantidad { get; set; }
        public decimal ValorUnitario { get; set; }
        public decimal PorcentajeAdmin { get; set; }

        public decimal SubtotalLinea { get; set; }
        public decimal AdminLinea { get; set; }
        public decimal ValorTotal { get; set; }
    }
}
