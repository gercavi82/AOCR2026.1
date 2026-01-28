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
            if (Detalles != null && Detalles.Count > 0)
            {
                decimal sub = 0m;
                decimal adm = 0m;
                decimal tot = 0m;

                foreach (var d in Detalles)
                {
                    sub += d.SubtotalLinea;
                    adm += d.AdminLinea;
                    tot += d.ValorTotal;
                }

                Subtotal = Math.Round(sub, 2);
                ValorGastosAdmin = Math.Round(adm, 2);
                Total = Math.Round(tot, 2);
            }
            else
            {
                // Compatibilidad con campos antiguos
                Subtotal = Math.Round(ValorBase + ValorInspecciones + ValorViaticos, 2);
                ValorGastosAdmin = Math.Round(ValorViaticos * 0.08m, 2);
                Total = Math.Round(ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin, 2);
            }

            TotalEnLetras = NumeroEnLetras(Total);
        }

        private static string NumeroEnLetras(decimal valor)
        {
            var entero = (long)Math.Floor(valor);
            var centavos = (int)Math.Round((valor - entero) * 100m, 0);

            var letras = NumeroATexto(entero).Trim();
            if (string.IsNullOrWhiteSpace(letras))
                letras = "CERO";

            return string.Format("{0} DOLARES AMERICANOS CON {1:00}/100 CENTAVOS", letras, centavos);
        }

        private static string NumeroATexto(long numero)
        {
            if (numero == 0) return "CERO";
            if (numero < 0) return "MENOS " + NumeroATexto(Math.Abs(numero));

            string[] unidades = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ",
                "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISEIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE" };
            string[] decenas = { "", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
            string[] centenas = { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

            if (numero == 100) return "CIEN";

            if (numero < 20) return unidades[(int)numero];

            if (numero < 100)
            {
                var d = numero / 10;
                var r = numero % 10;
                if (d == 2 && r > 0) return "VEINTI" + unidades[(int)r];
                return decenas[(int)d] + (r > 0 ? " Y " + unidades[(int)r] : "");
            }

            if (numero < 1000)
            {
                var c = numero / 100;
                var r = numero % 100;
                return centenas[(int)c] + (r > 0 ? " " + NumeroATexto(r) : "");
            }

            if (numero < 1000000)
            {
                var m = numero / 1000;
                var r = numero % 1000;
                var miles = m == 1 ? "MIL" : NumeroATexto(m) + " MIL";
                return miles + (r > 0 ? " " + NumeroATexto(r) : "");
            }

            if (numero < 1000000000)
            {
                var m = numero / 1000000;
                var r = numero % 1000000;
                var millones = m == 1 ? "UN MILLON" : NumeroATexto(m) + " MILLONES";
                return millones + (r > 0 ? " " + NumeroATexto(r) : "");
            }

            var b = numero / 1000000000;
            var resto = numero % 1000000000;
            var milesMillones = b == 1 ? "MIL MILLONES" : NumeroATexto(b) + " MIL MILLONES";
            return milesMillones + (resto > 0 ? " " + NumeroATexto(resto) : "");
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
