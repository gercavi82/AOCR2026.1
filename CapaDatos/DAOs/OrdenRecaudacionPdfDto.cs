using System;
using System.Collections.Generic;

namespace CapaModelo.DTOs
{
    public class OrdenRecaudacionPdfDto
    {
        public int OrdenId { get; set; }

        public string NumeroOrden { get; set; }
        public DateTime FechaEmision { get; set; }
        public string LugarEmision { get; set; }

        public string NombreCompania { get; set; }
        public string Ruc { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }

        public string ConceptoPrincipal { get; set; }
        public decimal ValorBase { get; set; }

        public int Estaciones { get; set; }
        public int Dias { get; set; }

        public decimal ValorInspecciones { get; set; }
        public decimal ValorViaticos { get; set; }
        public decimal ValorGastosAdmin { get; set; }

        public decimal Total { get; set; }
        public string TotalEnLetras { get; set; }

        public string Referencia { get; set; }
        public string Observacion { get; set; }

        public string NombreRepresentante { get; set; }

        // Autoridad (puedes parametrizar en config)
        public string NombreInspector { get; set; }
        public string CargoInspector { get; set; }

        public void CalcularTotales()
        {
            ValorInspecciones = Estaciones * 500m;
            ValorViaticos = Dias * 80m;
            ValorGastosAdmin = ValorViaticos * 0.08m;
            Total = ValorBase + ValorInspecciones + ValorViaticos + ValorGastosAdmin;

            TotalEnLetras = NumeroALetrasService.Convertir(Total);
        }
    }

    /// <summary>
    /// Conversión a letras (ES) simple y segura para montos.
    /// </summary>
    public static class NumeroALetrasService
    {
        public static string Convertir(decimal numero)
        {
            if (numero < 0) numero = Math.Abs(numero);

            int parteEntera = (int)Math.Floor(numero);
            int centavos = (int)Math.Round((numero - parteEntera) * 100, 0);

            string letras = ConvertirEntero(parteEntera);
            if (string.IsNullOrWhiteSpace(letras)) letras = "cero";

            string dolares = (parteEntera == 1) ? "dólar americano" : "dólares americanos";
            return $"{PrimeraMayus(letras)} {dolares} con {centavos:00}/100 centavos";
        }

        private static string ConvertirEntero(int numero)
        {
            if (numero == 0) return "cero";

            string[] unidades = { "", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve" };
            string[] especiales = { "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve" };
            string[] decenas = { "", "", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
            string[] centenas = { "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos" };

            if (numero == 100) return "cien";

            var partes = new List<string>();

            // Miles
            if (numero >= 1000)
            {
                int miles = numero / 1000;
                if (miles == 1) partes.Add("mil");
                else partes.Add($"{ConvertirEntero(miles)} mil");
                numero %= 1000;
            }

            // Centenas
            if (numero >= 100)
            {
                int c = numero / 100;
                partes.Add(centenas[c]);
                numero %= 100;
            }

            // 10-19
            if (numero >= 10 && numero <= 19)
            {
                partes.Add(especiales[numero - 10]);
                numero = 0;
            }
            else if (numero >= 20)
            {
                int d = numero / 10;
                int u = numero % 10;

                if (d == 2 && u > 0)
                    partes.Add("veinti" + unidades[u]);
                else
                {
                    string dec = decenas[d];
                    if (u > 0) dec += " y " + unidades[u];
                    partes.Add(dec);
                }
                numero = 0;
            }

            if (numero > 0)
                partes.Add(unidades[numero]);

            return string.Join(" ", partes).Trim();
        }

        private static string PrimeraMayus(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
