using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapaPresentacion.Helpers
{
    public static class RoleGroupingHelper
    {
        public const string Administrador = "Administrador";
        public const string Solicitante = "Solicitante";
        public const string InspectorTecnico = "InspectorTecnico";
        public const string DireccionJefaturaTecnica = "DireccionJefaturaTecnica";
        public const string Financiero = "Financiero";
        public const string Coordinacion = "Coordinacion";

        private static readonly string[] UnifiedRoleOrder =
        {
            Administrador,
            Solicitante,
            InspectorTecnico,
            DireccionJefaturaTecnica,
            Financiero,
            Coordinacion
        };

        private static readonly HashSet<string> HiddenRawRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RECEPCION"
        };

        public static IList<string> ExtractRoles(object rolesObject, string extraRole = null)
        {
            var roles = new List<string>();

            if (rolesObject is string)
            {
                roles.Add((string)rolesObject);
            }
            else if (rolesObject is string[])
            {
                roles.AddRange((string[])rolesObject);
            }
            else if (rolesObject is List<string>)
            {
                roles.AddRange((List<string>)rolesObject);
            }
            else if (rolesObject is IEnumerable<string>)
            {
                roles.AddRange((IEnumerable<string>)rolesObject);
            }

            if (!string.IsNullOrWhiteSpace(extraRole))
            {
                roles.Add(extraRole);
            }

            return roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IList<string> BuildUnifiedRoles(IEnumerable<string> rawRoles)
        {
            var mappedRoles = (rawRoles ?? Enumerable.Empty<string>())
                .Where(role => !HiddenRawRoles.Contains(Simplify(role)))
                .Select(NormalizeSelectedRole)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var orderedRoles = new List<string>();
            foreach (var unifiedRole in UnifiedRoleOrder)
            {
                if (mappedRoles.Contains(unifiedRole, StringComparer.OrdinalIgnoreCase))
                {
                    orderedRoles.Add(unifiedRole);
                }
            }

            foreach (var role in mappedRoles)
            {
                if (!orderedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    orderedRoles.Add(role);
                }
            }

            return orderedRoles;
        }

        public static string NormalizeSelectedRole(string role)
        {
            var normalized = Simplify(role);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (Matches(normalized, "ADMIN", "ADMINISTRADOR"))
            {
                return Administrador;
            }

            if (Matches(normalized,
                "SOLICITANTE",
                "OPERADOR",
                "REPRESENTANTETECNICO",
                "REPRESENTANTELEGAL",
                "RT"))
            {
                return Solicitante;
            }

            if (Matches(normalized,
                "INSPECTOR",
                "TECNICO",
                "EVALUADORTECNICO",
                "INSPECTORTECNICO"))
            {
                return InspectorTecnico;
            }

            if (Matches(normalized,
                "DIRECCION",
                "JEFATURATECNICA",
                "DIRDAC",
                "DIRECTORGENERAL",
                "DIRECCIONJEFATURATECNICA"))
            {
                return DireccionJefaturaTecnica;
            }

            if (Matches(normalized,
                "FINANCIERO",
                "COORDINADORFINANCIERO",
                "DIRECTORFINANCIERO"))
            {
                return Financiero;
            }

            if (Matches(normalized,
                "COORDINACION",
                "COORDINADOR",
                "COORDINADORINSPECCIONES",
                "COORDINACIONLEGAL",
                "COORDINADORLEGAL"))
            {
                return Coordinacion;
            }

            return string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();
        }

        public static string ToDisplayName(string role)
        {
            switch (NormalizeSelectedRole(role))
            {
                case Administrador:
                    return "Administrador";
                case Solicitante:
                    return "Solicitante";
                case InspectorTecnico:
                    return "Inspector / Técnico";
                case DireccionJefaturaTecnica:
                    return "Dirección / Jefatura técnica";
                case Financiero:
                    return "Financiero";
                case Coordinacion:
                    return "Coordinación";
                default:
                    return string.IsNullOrWhiteSpace(role) ? "Perfil institucional" : role.Trim();
            }
        }

        public static bool IsAdministrador(string role)
        {
            return NormalizeSelectedRole(role).Equals(Administrador, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSolicitante(string role)
        {
            return NormalizeSelectedRole(role).Equals(Solicitante, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsInspectorTecnico(string role)
        {
            return NormalizeSelectedRole(role).Equals(InspectorTecnico, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDireccionJefaturaTecnica(string role)
        {
            return NormalizeSelectedRole(role).Equals(DireccionJefaturaTecnica, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFinanciero(string role)
        {
            return NormalizeSelectedRole(role).Equals(Financiero, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCoordinacion(string role)
        {
            return NormalizeSelectedRole(role).Equals(Coordinacion, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasAnyRawRole(IEnumerable<string> rawRoles, params string[] aliases)
        {
            var roleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawRole in rawRoles ?? Enumerable.Empty<string>())
            {
                var simplified = Simplify(rawRole);
                if (!string.IsNullOrWhiteSpace(simplified))
                {
                    roleSet.Add(simplified);
                }
            }

            foreach (var alias in aliases ?? new string[0])
            {
                if (roleSet.Contains(Simplify(alias)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Matches(string normalizedRole, params string[] aliases)
        {
            return aliases.Any(alias => normalizedRole.Equals(alias, StringComparison.OrdinalIgnoreCase));
        }

        private static string Simplify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToUpperInvariant();
            normalized = normalized
                .Replace('Á', 'A')
                .Replace('É', 'E')
                .Replace('Í', 'I')
                .Replace('Ó', 'O')
                .Replace('Ú', 'U')
                .Replace('Ü', 'U')
                .Replace('Ñ', 'N');

            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}