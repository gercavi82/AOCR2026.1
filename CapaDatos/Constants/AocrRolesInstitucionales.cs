using System;

namespace CapaDatos.Constants
{
    public static class AocrRolesInstitucionales
    {
        public const string Inspector = "Inspector";
        public const string Dirdac = "DIRDAC";
        public const string Dcav = "DCAV";
        public const string Coordinacion = "Coordinacion";
        public const string Administrador = "Administrador";
        public const string RolesAccesoMvc = "Inspector,DirectorCertificacionesDcav,DCAV,Direccion,DireccionJefaturaTecnica,DIRDAC,JefaturaTecnica,Coordinacion,Coordinador,CoordinadorInspecciones,Administrador";

        public static readonly string[] RolesAcceso =
        {
            Inspector, "DirectorCertificacionesDcav", Dcav, "Direccion", "DireccionJefaturaTecnica", Dirdac, "JefaturaTecnica",
            Coordinacion, "Coordinador", "CoordinadorInspecciones", Administrador
        };

        public static readonly string[] DirdacAliases =
        {
            Dirdac, "Direccion", "DireccionJefaturaTecnica", "JefaturaTecnica"
        };

        public static readonly string[] DcavAliases =
        {
            Dcav, "DirectorCertificacionesDcav"
        };

        public static readonly string[] DirdacSqlTokens =
        {
            "DIRDAC", "DIRECCION", "DIRECCION_JEFATURA_TECNICA", "JEFATURA_TECNICA"
        };

        public static readonly string[] DcavSqlTokens =
        {
            "DCAV", "DIRECTOR_CERTIFICACIONES_DCAV", "DIRECTORCERTIFICACIONESDCAV"
        };

        public static bool EsDirdac(string rol)
        {
            return Coincide(rol, DirdacSqlTokens);
        }

        public static bool EsDcav(string rol)
        {
            return Coincide(rol, DcavSqlTokens);
        }

        private static bool Coincide(string rol, string[] permitidos)
        {
            var token = (rol ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
            return Array.Exists(permitidos, item => string.Equals(item.Replace("_", string.Empty), token, StringComparison.OrdinalIgnoreCase));
        }
    }
}
