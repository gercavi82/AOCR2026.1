using System.Collections.Generic;
using System.Linq;
using Dapper;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class SeguridadDAO
    {
        private static NpgsqlConnection CrearConexion()
        {
            return ConexionDAO.CrearConexion();
        }

        public bool InfraestructuraPermisosDisponible()
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                const string sql = @"
SELECT COUNT(1)
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN ('seguridad_permiso', 'seguridad_rol_permiso', 'usuario_rol');";

                return cn.ExecuteScalar<int>(sql) == 3;
            }
        }

        public bool ExistePermiso(string codigoPermiso)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                if (!InfraestructuraPermisosDisponibleInterno(cn))
                {
                    return false;
                }

                const string sql = @"
SELECT COUNT(1)
FROM seguridad_permiso
WHERE UPPER(TRIM(codigo)) = UPPER(TRIM(@codigoPermiso))
  AND activo = TRUE;";

                return cn.ExecuteScalar<int>(sql, new { codigoPermiso = codigoPermiso }) > 0;
            }
        }

        public bool UsuarioTienePermiso(string codigoUsuario, string codigoPermiso)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                if (!InfraestructuraPermisosDisponibleInterno(cn))
                {
                    return false;
                }

                const string sql = @"
SELECT COUNT(1)
FROM usuario_rol ur
INNER JOIN seguridad_rol_permiso rp
    ON rp.codigorol = ur.codigorol
   AND rp.activo = TRUE
INNER JOIN seguridad_permiso p
    ON p.id_permiso = rp.id_permiso
   AND p.activo = TRUE
WHERE ur.codigousuario = @codigoUsuario
  AND COALESCE(ur.activo, TRUE) = TRUE
  AND UPPER(TRIM(p.codigo)) = UPPER(TRIM(@codigoPermiso));";

                return cn.ExecuteScalar<int>(sql, new
                {
                    codigoUsuario = codigoUsuario,
                    codigoPermiso = codigoPermiso
                }) > 0;
            }
        }

        public List<string> ObtenerPermisosUsuario(string codigoUsuario)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                if (!InfraestructuraPermisosDisponibleInterno(cn))
                {
                    return new List<string>();
                }

                const string sql = @"
SELECT DISTINCT p.codigo
FROM usuario_rol ur
INNER JOIN seguridad_rol_permiso rp
    ON rp.codigorol = ur.codigorol
   AND rp.activo = TRUE
INNER JOIN seguridad_permiso p
    ON p.id_permiso = rp.id_permiso
   AND p.activo = TRUE
WHERE ur.codigousuario = @codigoUsuario
  AND COALESCE(ur.activo, TRUE) = TRUE
ORDER BY p.codigo;";

                return cn.Query<string>(sql, new { codigoUsuario = codigoUsuario }).ToList();
            }
        }

        private bool InfraestructuraPermisosDisponibleInterno(NpgsqlConnection cn)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN ('seguridad_permiso', 'seguridad_rol_permiso', 'usuario_rol');";

            return cn.ExecuteScalar<int>(sql) == 3;
        }
    }
}
