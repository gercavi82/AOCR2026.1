using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;

namespace CapaNegocio
{
    public static class SeguridadBL
    {
        private static readonly SeguridadDAO _dao = new SeguridadDAO();

        private static readonly Dictionary<string, string[]> _permisosFallback =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "ADM_GESTION_USUARIOS", new[] { "Administrador", "Direccion", "JefaturaTecnica" } },
                { "ADM_ROLES_PERMISOS", new[] { "Administrador", "Direccion", "JefaturaTecnica" } },
                { "ADM_RESET_PASSWORD", new[] { "Administrador", "Direccion", "JefaturaTecnica" } },
                { "FIN_VER_PAGOS", new[] { "Administrador", "Financiero", "CoordinadorFinanciero", "DirectorFinanciero" } },
                { "FIN_APROBAR_PAGO", new[] { "Administrador", "Financiero", "CoordinadorFinanciero", "DirectorFinanciero" } },
                { "LEGAL_REVISAR_SOLICITUD", new[] { "Administrador", "CoordinacionLegal", "CoordinadorLegal" } },
                { "LEGAL_GENERAR_CERTIFICADO", new[] { "Administrador", "CoordinacionLegal", "CoordinadorLegal" } },
                { "ORD_ANULAR", new[] { "Administrador", "Solicitante", "Operador" } }
            };

        public static bool InfraestructuraPermisosDisponible()
        {
            return _dao.InfraestructuraPermisosDisponible();
        }

        public static bool UsuarioTienePermiso(string codigoUsuario, string codigoPermiso, IEnumerable<string> rolesUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoPermiso))
            {
                return true;
            }

            var roles = (rolesUsuario ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roles.Any(r => r.Equals("Administrador", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (InfraestructuraPermisosDisponible())
            {
                var permisoExiste = _dao.ExistePermiso(codigoPermiso);
                if (permisoExiste && !string.IsNullOrWhiteSpace(codigoUsuario))
                {
                    if (_dao.UsuarioTienePermiso(codigoUsuario, codigoPermiso))
                    {
                        return true;
                    }
                }
            }

            return PermisoPorRolesFallback(codigoPermiso, roles);
        }

        public static List<string> ObtenerPermisosUsuario(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return new List<string>();
            }

            if (!InfraestructuraPermisosDisponible())
            {
                return new List<string>();
            }

            return _dao.ObtenerPermisosUsuario(codigoUsuario);
        }

        private static bool PermisoPorRolesFallback(string codigoPermiso, IList<string> rolesUsuario)
        {
            if (rolesUsuario == null || rolesUsuario.Count == 0)
            {
                return false;
            }

            string[] rolesPermitidos;
            if (!_permisosFallback.TryGetValue(codigoPermiso, out rolesPermitidos))
            {
                return false;
            }

            return rolesPermitidos.Any(rolPermitido =>
                rolesUsuario.Any(rolUsuario => rolUsuario.Equals(rolPermitido, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
