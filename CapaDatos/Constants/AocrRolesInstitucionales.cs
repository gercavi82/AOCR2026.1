using System;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Catálogo canónico institucional de los 7 roles reales del Sistema AOCR:
    /// 1. DIRCAV
    /// 2. DIRDAC
    /// 3. COORDINADOR
    /// 4. RT
    /// 5. FINANCIERO
    /// 6. INSPECTOR
    /// 7. ADMINISTRADOR
    /// DIRCAV y DIRDAC son autoridades independientes con bandejas, permisos y firmas segregadas.
    /// </summary>
    public static class AocrRolesInstitucionales
    {
        // =======================================================
        // 7 ROLES CANÓNICOS REALES
        // =======================================================
        public const string Dircav = "DIRCAV";
        public const string Dirdac = "DIRDAC";
        public const string Coordinador = "COORDINADOR";
        public const string RT = "RT";
        public const string Financiero = "FINANCIERO";
        public const string Inspector = "INSPECTOR";
        public const string Administrador = "ADMINISTRADOR";

        // Compatibilidad histórica estricta (no cruzada)
        public const string Dcav = "DIRCAV";
        public const string Coordinacion = "COORDINADOR";

        public const string RolesAccesoMvc = "DIRCAV,DCAV,DIRDAC,COORDINADOR,Coordinacion,Coordinador,CoordinadorInspecciones,INSPECTOR,Inspector,FINANCIERO,Financiero,ADMINISTRADOR,Administrador";

        public static readonly string[] RolesAcceso =
        {
            Dircav, Dirdac, Coordinador, Inspector, Financiero, Administrador,
            "Coordinacion", "Coordinador", "CoordinadorInspecciones", "DCAV"
        };

        // Tokens SQL exclusivos DIRCAV
        public static readonly string[] DircavSqlTokens =
        {
            "DIRCAV", "DCAV", "DIRECTOR_CERTIFICACIONES_DCAV", "DIRECTORCERTIFICACIONESDCAV"
        };
        public static readonly string[] DcavSqlTokens = DircavSqlTokens;
        public static readonly string[] DircavAliases = { Dircav, "DCAV", "DirectorCertificacionesDcav" };
        public static readonly string[] DcavAliases = DircavAliases;

        // Tokens SQL exclusivos DIRDAC
        public static readonly string[] DirdacSqlTokens =
        {
            "DIRDAC", "DIRECTOR_DIRDAC", "DIRECTORDIRDAC", "DIRECTORGENERAL", "DIRECTORDGAC", "DIRECCIONJEFATURATECNICA", "DIRECCION_JEFATURA_TECNICA"
        };
        public static readonly string[] DirdacAliases = { Dirdac, "DireccionJefaturaTecnica" };

        public static bool EsDircav(string rol)
        {
            return Coincide(rol, DircavSqlTokens);
        }

        public static bool EsDcav(string rol)
        {
            return EsDircav(rol);
        }

        // Tokens SQL exclusivos INSPECTOR
        public static readonly string[] InspectorSqlTokens =
        {
            "INSPECTOR", "INSPECTORTECNICO", "INSPECTOR_TECNICO", "TECNICO", "EVALUADORTECNICO", "EVALUADOR_TECNICO"
        };

        // Tokens SQL exclusivos COORDINADOR
        public static readonly string[] CoordinadorSqlTokens =
        {
            "COORDINADOR", "COORDINACION", "COORDINADORINSPECCIONES", "COORDINADOR_INSPECCIONES", "COORDINACIONLEGAL", "COORDINADORLEGAL"
        };

        public static bool EsInspector(string rol)
        {
            return Coincide(rol, InspectorSqlTokens);
        }

        public static bool EsCoordinador(string rol)
        {
            return Coincide(rol, CoordinadorSqlTokens);
        }

        public static readonly string[] AdministradorSqlTokens =
        {
            "ADMINISTRADOR", "ADMIN", "ADMINISTRADOR_SISTEMA", "ADMINISTRADORSISTEMA"
        };

        public static bool EsAdministrador(string rol)
        {
            return Coincide(rol, AdministradorSqlTokens);
        }

        public static readonly string[] RtSqlTokens =
        {
            "RT", "REPRESENTANTETECNICO", "REPRESENTANTE_TECNICO", "SOLICITANTE", "OPERADOR", "EXPLOTADOR"
        };

        public static bool EsRt(string rol)
        {
            return Coincide(rol, RtSqlTokens);
        }

        public static bool EsDirdac(string rol)
        {
            return Coincide(rol, DirdacSqlTokens);
        }

        private static bool Coincide(string rol, string[] permitidos)
        {
            var token = (rol ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
            return Array.Exists(permitidos, item => string.Equals(item.Replace("_", string.Empty), token, StringComparison.OrdinalIgnoreCase));
        }
    }
}
