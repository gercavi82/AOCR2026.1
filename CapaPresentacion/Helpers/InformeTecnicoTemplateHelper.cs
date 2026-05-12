using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaPresentacion.Helpers
{
    public static class InformeTecnicoTemplateHelper
    {
        public sealed class ServicioEstacionFila
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public string Uio { get; set; }
            public string Gye { get; set; }
            public string Mec { get; set; }
            public string Ltx { get; set; }
        }

        private sealed class ServicioEstacionDef
        {
            public ServicioEstacionDef(string key, string label)
            {
                Key = key;
                Label = label;
            }

            public string Key { get; private set; }
            public string Label { get; private set; }
        }

        private static readonly ServicioEstacionDef[] ServicioDefs = new[]
        {
            new ServicioEstacionDef("supervisor_responsable_estacion", "Supervisor / Responsable de Estacion"),
            new ServicioEstacionDef("servicio_rampa", "Servicio de Rampa"),
            new ServicioEstacionDef("servicio_despacho_aeronaves", "Servicio Despacho Aeronaves"),
            new ServicioEstacionDef("servicio_seguridad_aeroportuaria", "Servicio Seguridad Aeroportuaria"),
            new ServicioEstacionDef("servicio_atencion_pasajeros", "Servicio Atencion de Pasajeros"),
            new ServicioEstacionDef("servicio_provision_combustible", "Servicio Provision de combustible"),
            new ServicioEstacionDef("servicio_mantenimiento_linea", "Servicio Mantenimiento en Linea"),
            new ServicioEstacionDef("servicio_procesamiento_carga", "Servicio Procesamiento de Carga"),
            new ServicioEstacionDef("instalaciones", "Instalaciones.")
        };

        private static readonly string[] DocumentosAdjuntosBase = new[]
        {
            "LISTA DE VERIFICACION",
            "REPORTE DE INFRACCION",
            "REPORTE DE SUSPENSION DE FUNCIONES",
            "EVIDENCIAS DE LA INSPECCION"
        };

        public static IList<ServicioEstacionFila> GetServicioRows(string serialized)
        {
            var values = ParseServicioRows(serialized);
            var rows = new List<ServicioEstacionFila>();

            foreach (var def in ServicioDefs)
            {
                string[] cells;
                if (!values.TryGetValue(def.Key, out cells) || cells == null)
                {
                    cells = new string[4];
                }

                rows.Add(new ServicioEstacionFila
                {
                    Key = def.Key,
                    Label = def.Label,
                    Uio = GetCell(cells, 0),
                    Gye = GetCell(cells, 1),
                    Mec = GetCell(cells, 2),
                    Ltx = GetCell(cells, 3)
                });
            }

            return rows;
        }

        public static IList<string> GetDocumentosAdjuntosBase()
        {
            return new List<string>(DocumentosAdjuntosBase);
        }

        public static IDictionary<string, List<string>> ParseDocumentosAdjuntosArchivos(string serialized)
        {
            var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return values;
            }

            var normalized = serialized.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length == 0)
                {
                    continue;
                }

                var key = CleanLine(parts[0]);
                var fileName = parts.Length > 1 ? NormalizeFileName(parts[1]) : null;
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                List<string> fileNames;
                if (!values.TryGetValue(key, out fileNames) || fileNames == null)
                {
                    fileNames = new List<string>();
                    values[key] = fileNames;
                }

                if (!fileNames.Any(existing => string.Equals(existing, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    fileNames.Add(fileName);
                }
            }

            return values;
        }

        public static string SerializeDocumentosAdjuntosArchivos(IDictionary<string, List<string>> values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }

            var lines = new List<string>();

            foreach (var label in DocumentosAdjuntosBase)
            {
                List<string> fileNames;
                if (!values.TryGetValue(label, out fileNames) || fileNames == null)
                {
                    continue;
                }

                var normalizedFileNames = fileNames
                    .Select(NormalizeFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (normalizedFileNames.Count == 0)
                {
                    continue;
                }

                foreach (var normalizedFileName in normalizedFileNames)
                {
                    lines.Add(NormalizeCell(label) + "|" + normalizedFileName);
                }
            }

            foreach (var pair in values.Where(x => !DocumentosAdjuntosBase.Contains(x.Key, StringComparer.OrdinalIgnoreCase)))
            {
                var key = NormalizeCell(pair.Key);
                var normalizedFileNames = (pair.Value ?? new List<string>())
                    .Select(NormalizeFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (string.IsNullOrWhiteSpace(key) || normalizedFileNames.Count == 0)
                {
                    continue;
                }

                foreach (var normalizedFileName in normalizedFileNames)
                {
                    lines.Add(key + "|" + normalizedFileName);
                }
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        public static string SerializeServicioRows(IDictionary<string, string[]> values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }

            var lines = new List<string>();

            foreach (var def in ServicioDefs)
            {
                string[] cells;
                if (!values.TryGetValue(def.Key, out cells) || cells == null)
                {
                    cells = new string[4];
                }

                var uio = NormalizeCell(GetCell(cells, 0));
                var gye = NormalizeCell(GetCell(cells, 1));
                var mec = NormalizeCell(GetCell(cells, 2));
                var ltx = NormalizeCell(GetCell(cells, 3));

                if (string.IsNullOrWhiteSpace(uio) &&
                    string.IsNullOrWhiteSpace(gye) &&
                    string.IsNullOrWhiteSpace(mec) &&
                    string.IsNullOrWhiteSpace(ltx))
                {
                    continue;
                }

                lines.Add(def.Key + "|" + uio + "|" + gye + "|" + mec + "|" + ltx);
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        public static IList<string> SplitLines(string value)
        {
            var items = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return items;
            }

            var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            var parts = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var cleaned = CleanLine(part);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    items.Add(cleaned);
                }
            }

            return items;
        }

        public static string SerializeLines(IEnumerable<string> values)
        {
            if (values == null)
            {
                return null;
            }

            var items = new List<string>();
            foreach (var value in values)
            {
                var cleaned = CleanLine(value);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    items.Add(cleaned);
                }
            }

            return items.Count == 0 ? null : string.Join("\n", items);
        }

        public static string GetResultadoLabel(string resultado)
        {
            var normalized = NormalizeResultadoInformeTecnico(resultado);
            if (normalized == "SATISFACTORIO")
            {
                return "Satisfactorio";
            }

            if (normalized == "INSATISFACTORIO")
            {
                return "Insatisfactorio";
            }

            if (normalized == "OBSERVADO")
            {
                return "Observado";
            }

            if (normalized == "REQUIERE_ACCION_CORRECTIVA")
            {
                return "Requiere acción correctiva";
            }

            if (normalized == "NO_APLICA")
            {
                return "No aplica";
            }

            return string.IsNullOrWhiteSpace(resultado) ? "Pendiente" : resultado.Trim().Replace("_", " ");
        }

        public static bool IsResultadoSatisfactorio(string resultado)
        {
            return NormalizeResultadoInformeTecnico(resultado) == "SATISFACTORIO";
        }

        public static bool IsResultadoInsatisfactorio(string resultado)
        {
            return NormalizeResultadoInformeTecnico(resultado) == "INSATISFACTORIO";
        }

        public static string GetTipoResultadoInsatisfactorioLabel(string tipoResultadoInsatisfactorio)
        {
            var normalized = NormalizeTipoResultadoInsatisfactorio(tipoResultadoInsatisfactorio);
            if (normalized == "CON_INSPECCION")
            {
                return "Con inspección";
            }

            if (normalized == "SIN_INSPECCION")
            {
                return "Sin inspección";
            }

            return string.Empty;
        }

        public static string NormalizeResultadoInformeTecnico(string resultado)
        {
            var normalized = NormalizeResultado(resultado);
            switch (normalized)
            {
                case "NO_SATISFACTORIO":
                    return "INSATISFACTORIO";
                case "OBSERVACION_DOCUMENTAL":
                    return "OBSERVADO";
                case "NO_APLICABLE":
                case "N/A":
                    return "NO_APLICA";
                default:
                    return normalized;
            }
        }

        public static string NormalizeTipoResultadoInsatisfactorio(string tipoResultadoInsatisfactorio)
        {
            var normalized = NormalizeResultado(tipoResultadoInsatisfactorio);
            switch (normalized)
            {
                case "CON_INSPECCION":
                case "SIN_INSPECCION":
                    return normalized;
                default:
                    return string.Empty;
            }
        }

        private static Dictionary<string, string[]> ParseServicioRows(string serialized)
        {
            var values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in SplitLines(serialized))
            {
                var parts = line.Split('|');
                if (parts.Length == 0)
                {
                    continue;
                }

                var key = CleanLine(parts[0]);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var cells = new string[4];
                for (var i = 0; i < 4; i++)
                {
                    cells[i] = parts.Length > (i + 1) ? CleanLine(parts[i + 1]) : string.Empty;
                }

                values[key] = cells;
            }

            return values;
        }

        private static string NormalizeResultado(string resultado)
        {
            return string.IsNullOrWhiteSpace(resultado) ? string.Empty : resultado.Trim().ToUpperInvariant();
        }

        private static string NormalizeCell(string value)
        {
            var cleaned = CleanLine(value);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return string.Empty;
            }

            cleaned = cleaned.Replace("|", "/");
            return cleaned.Length > 160 ? cleaned.Substring(0, 160) : cleaned;
        }

        private static string NormalizeFileName(string value)
        {
            var cleaned = CleanLine(value);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return null;
            }

            cleaned = cleaned.Replace("|", "_");
            return cleaned.Length > 240 ? cleaned.Substring(0, 240) : cleaned;
        }

        private static string CleanLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var cleaned = value.Replace("\0", string.Empty).Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }

        private static string GetCell(string[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                return string.Empty;
            }

            return values[index] ?? string.Empty;
        }
    }
}
