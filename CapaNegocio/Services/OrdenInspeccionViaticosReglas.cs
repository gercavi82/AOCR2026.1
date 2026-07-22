using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Reglas puras para inspeccion externa y viaticos de Orden de Recaudacion.
    /// No consulta base de datos para que frontend, controlador y pruebas compartan
    /// una unica definicion de la regla institucional.
    /// </summary>
    public static class OrdenInspeccionViaticosReglas
    {
        public const string InspeccionExterna = "INSPECCION_EXT";
        public const string ViaticosInspector = "VIATICOS_INSPECTOR";
        public const string OtraProvincia = "OTRA_PROVINCIA";

        private static readonly HashSet<string> TramitesConInspeccionObligatoria =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "EMI_AOCR",
                "REN_AOCR",
                "MOD_AOCR_INC"
            };

        private static readonly HashSet<string> LugaresSinViaticos =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "QUITO",
                "LATACUNGA"
            };

        private static readonly HashSet<string> LugaresPermitidos =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "QUITO",
                "LATACUNGA",
                "GUAYAQUIL",
                "MANTA",
                OtraProvincia
            };

        private static readonly Regex LocalidadSegura = new Regex(
            @"^[\p{L}\p{M}\d\s.,#()'/-]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool RequiereInspeccionExterna(string codigoTramite)
        {
            return TramitesConInspeccionObligatoria.Contains(Normalizar(codigoTramite));
        }

        public static bool EsLugarSinViaticos(string lugarInspeccion)
        {
            var lugares = SepararLugares(lugarInspeccion);
            return lugares.Count > 0 && lugares.All(LugaresSinViaticos.Contains);
        }

        public static bool EsLugarPermitido(string lugarInspeccion)
        {
            var lugares = SepararLugares(lugarInspeccion);
            return lugares.Count > 0 && lugares.All(LugaresPermitidos.Contains);
        }

        public static bool RequiereProvinciaLocalidad(string lugarInspeccion)
        {
            return SepararLugares(lugarInspeccion).Contains(OtraProvincia, StringComparer.OrdinalIgnoreCase);
        }

        public static bool TieneLugarConViaticos(string lugarInspeccion)
        {
            return SepararLugares(lugarInspeccion).Any(lugar => !LugaresSinViaticos.Contains(lugar));
        }

        public static List<string> SepararLugares(string lugarInspeccion)
        {
            return (lugarInspeccion ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalizar)
                .Where(valor => valor.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string UnirLugares(IEnumerable<string> lugares)
        {
            return string.Join(",", (lugares ?? Enumerable.Empty<string>())
                .Select(Normalizar)
                .Where(valor => valor.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        public static bool EsProvinciaLocalidadSegura(string provinciaLocalidad)
        {
            var valor = (provinciaLocalidad ?? string.Empty).Trim();
            return valor.Length > 0 && valor.Length <= 150 && LocalidadSegura.IsMatch(valor);
        }

        public static int CalcularDiasPagadosViatico(int numeroDiasInspeccion)
        {
            return Math.Max(numeroDiasInspeccion - 1, 0);
        }

        public static decimal CalcularSubtotalViatico(int numeroDiasInspeccion, decimal valorDiario)
        {
            if (numeroDiasInspeccion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numeroDiasInspeccion));
            }

            if (valorDiario < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(valorDiario));
            }

            return CalcularDiasPagadosViatico(numeroDiasInspeccion) * valorDiario;
        }

        public static string Normalizar(string valor)
        {
            return (valor ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
