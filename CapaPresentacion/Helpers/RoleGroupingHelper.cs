using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapaPresentacion.Helpers
{
    /// <summary>
    /// Helper para unificación y segregación de los 7 roles canónicos del Sistema AOCR:
    /// 1. DIRCAV
    /// 2. DIRDAC
    /// 3. COORDINADOR
    /// 4. RT
    /// 5. FINANCIERO
    /// 6. INSPECTOR
    /// 7. ADMINISTRADOR
    /// DIRCAV y DIRDAC son roles completamente diferentes con bandejas, permisos y firmas segregadas.
    /// </summary>
    public static class RoleGroupingHelper
    {
        // 7 ROLES CANÓNICOS
        public const string Administrador = "ADMINISTRADOR";
        public const string Dircav = "DIRCAV";
        public const string Dirdac = "DIRDAC";
        public const string Coordinador = "COORDINADOR";
        public const string Solicitante = "RT";
        public const string Financiero = "FINANCIERO";
        public const string InspectorTecnico = "INSPECTOR";

        // Aliases legacy para compatibilidad hacia atrás
        public const string Dcav = "DCAV";
        public const string Coordinacion = "COORDINADOR";
        public const string DireccionJefaturaTecnica = "DireccionJefaturaTecnica";

        private static readonly string[] UnifiedRoleOrder =
        {
            Administrador,
            Dircav,
            Dirdac,
            Coordinador,
            InspectorTecnico,
            Financiero,
            Solicitante
        };

        private static readonly HashSet<string> HiddenRawRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RECEPCION"
        };

        private static readonly HashSet<string> TechnicalRawRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TECNICO",
            "TÉCNICO",
            "TECNICA",
            "TÉCNICA",
            "INSPECTOR",
            "INSPECTORTECNICO",
            "INSPECTOR TÉCNICO",
            "INSPECTOR TECNICO",
            "EVALUADORTECNICO",
            "EVALUADOR TECNICO",
            "EVALUADOR TÉCNICO"
        };

        private static readonly HashSet<string> ForcedCoordinacionUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GEN_COORDINACION"
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

        public static IList<string> SanitizeRawRolesForUser(string username, IEnumerable<string> rawRoles)
        {
            var roles = (rawRoles ?? Enumerable.Empty<string>())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!IsForcedCoordinacionUser(username))
            {
                return roles;
            }

            var filtered = roles
                .Where(role => !TechnicalRawRoles.Contains(Simplify(role)))
                .ToList();

            if (!filtered.Any(role => NormalizeSelectedRole(role).Equals(Coordinador, StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Add(Coordinador);
            }

            return filtered
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string ResolveSelectedRoleForUser(string username, IEnumerable<string> unifiedRoles, string selectedRole)
        {
            var roles = (unifiedRoles ?? Enumerable.Empty<string>())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (IsForcedCoordinacionUser(username) &&
                roles.Contains(Coordinador, StringComparer.OrdinalIgnoreCase))
            {
                return Coordinador;
            }

            var normalizedSelected = NormalizeSelectedRole(selectedRole);
            return !string.IsNullOrWhiteSpace(normalizedSelected) &&
                   roles.Contains(normalizedSelected, StringComparer.OrdinalIgnoreCase)
                ? normalizedSelected
                : (roles.FirstOrDefault() ?? string.Empty);
        }

        public static bool IsForcedCoordinacionUser(string username)
        {
            return !string.IsNullOrWhiteSpace(username) && ForcedCoordinacionUsers.Contains(username.Trim());
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

            // DIRCAV canónico (no confundir con DIRDAC)
            if (Matches(normalized,
                "DIRCAV",
                "DCAV",
                "DIRECTORDCAV",
                "DIRECTORDIRCAV",
                "DIRECTORCERTIFICACIONESDCAV"))
            {
                return Dircav;
            }

            // DIRDAC canónico (no confundir con DIRCAV ni con Dirección genérica)
            if (Matches(normalized,
                "DIRDAC",
                "DIRECTORDIRDAC",
                "DIRECTORGENERAL",
                "DIRECTORDGAC",
                "DGAC"))
            {
                return Dirdac;
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
                "FINANCIERO",
                "COORDINADORFINANCIERO",
                "COORDINACIONFINANCIERA",
                "DIRECTORFINANCIERO"))
            {
                return Financiero;
            }

            if (Matches(normalized,
                "COORDINACION",
                "COORDINADOR",
                "COORDINADORINSPECCIONES",
                "COORDINADORDEINSPECCIONES",
                "COORDINACIONINSPECCIONES",
                "COORDINACIONLEGAL",
                "COORDINADORLEGAL",
                "JEFATURATECNICA"))
            {
                return Coordinador;
            }

            return string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();
        }

        public static string ToDisplayName(string role)
        {
            switch (NormalizeSelectedRole(role))
            {
                case Administrador:
                    return "Administrador";
                case Dircav:
                    return "Director DIRCAV";
                case Dirdac:
                    return "Director General (DIRDAC)";
                case Coordinador:
                    return "Coordinador";
                case Solicitante:
                    return "Representante Técnico (RT)";
                case InspectorTecnico:
                    return "Inspector";
                case Financiero:
                    return "Financiero";
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

        public static bool IsDircav(string role)
        {
            return NormalizeSelectedRole(role).Equals(Dircav, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDcav(string role)
        {
            return IsDircav(role);
        }

        public static bool IsDirdac(string role)
        {
            return NormalizeSelectedRole(role).Equals(Dirdac, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDireccionJefaturaTecnica(string role)
        {
            // Solo para retrocompatibilidad controlada: no debe usarse para suplantar
            var normalized = NormalizeSelectedRole(role);
            return normalized.Equals(Dircav, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(Dirdac, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFinanciero(string role)
        {
            return NormalizeSelectedRole(role).Equals(Financiero, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCoordinacion(string role)
        {
            return NormalizeSelectedRole(role).Equals(Coordinador, StringComparison.OrdinalIgnoreCase);
        }

        public static bool RolRequiereCompaniaActiva(string role)
        {
            return IsSolicitante(role);
        }

        public static bool EsRolInstitucional(string role)
        {
            var normalized = NormalizeSelectedRole(role);
            return normalized.Equals(Administrador, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(Coordinador, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(Dircav, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(Dirdac, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(Financiero, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(InspectorTecnico, StringComparison.OrdinalIgnoreCase);
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
