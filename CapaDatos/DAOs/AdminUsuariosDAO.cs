using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using Npgsql;
using CapaModelo.Seguridad;

namespace CapaDatos.DAOs
{
    public class AdminUsuariosDAO
    {
        private static NpgsqlConnection CrearConexion()
        {
            return ConexionDAO.CrearConexion();
        }

        public List<SeguridadUsuarioDTO> BuscarUsuarios(string filtro, bool? activo)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();

                var tieneMustChangePassword = ExisteColumna(cn, "usuario", "must_change_password");
                var selectMustChangePassword = tieneMustChangePassword
                    ? "COALESCE(u.must_change_password, FALSE) AS \"MustChangePassword\""
                    : "FALSE AS \"MustChangePassword\"";
                var filtroUsuarioRolActivo = ExisteColumna(cn, "usuario_rol", "activo")
                    ? "AND COALESCE(ur.activo, TRUE) = TRUE"
                    : string.Empty;

                var sql = @"
SELECT
    u.idusuario AS ""IdUsuario"",
    u.codigousuario AS ""CodigoUsuario"",
    COALESCE(u.nombreusuario, '') AS ""NombreUsuario"",
    COALESCE(u.apellidousuario, '') AS ""ApellidoUsuario"",
    COALESCE(u.correo, '') AS ""Correo"",
    (COALESCE(NULLIF(TRIM(u.estadoactividad), ''), '1') = '1') AS ""Activo"",
    " + selectMustChangePassword + @",
    u.fechaultimaconexion AS ""UltimoLogin"",
    COALESCE(u.rol, '') AS ""RolFallback"",
    COALESCE(string_agg(DISTINCT r.descripcion, ', ' ORDER BY r.descripcion), '') AS ""RolesTexto""
FROM usuario u
LEFT JOIN usuario_rol ur
    ON ur.codigousuario = u.codigousuario
   " + filtroUsuarioRolActivo + @"
LEFT JOIN rol r
    ON r.codigorol = ur.codigorol
   AND r.activo = TRUE
WHERE (@filtro IS NULL
       OR u.codigousuario ILIKE @filtroLike
       OR u.nombreusuario ILIKE @filtroLike
       OR u.apellidousuario ILIKE @filtroLike
       OR u.correo ILIKE @filtroLike)
  AND (@activo IS NULL OR (COALESCE(NULLIF(TRIM(u.estadoactividad), ''), '1') = '1') = @activo)
GROUP BY
    u.idusuario,
    u.codigousuario,
    u.nombreusuario,
    u.apellidousuario,
    u.correo,
    u.estadoactividad,
    " + (tieneMustChangePassword ? "u.must_change_password," : string.Empty) + @"
    u.fechaultimaconexion,
    u.rol
ORDER BY u.idusuario DESC;";

                var filtroNormalizado = string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim();
                return cn.Query<SeguridadUsuarioDTO>(sql, new
                {
                    filtro = filtroNormalizado,
                    filtroLike = filtroNormalizado != null ? "%" + filtroNormalizado + "%" : null,
                    activo = activo
                }).ToList();
            }
        }

        public SeguridadUsuarioDTO ObtenerUsuarioPorId(int idUsuario)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();

                var tieneMustChangePassword = ExisteColumna(cn, "usuario", "must_change_password");
                var selectMustChangePassword = tieneMustChangePassword
                    ? "COALESCE(u.must_change_password, FALSE) AS \"MustChangePassword\""
                    : "FALSE AS \"MustChangePassword\"";

                var sql = @"
SELECT
    u.idusuario AS ""IdUsuario"",
    u.codigousuario AS ""CodigoUsuario"",
    COALESCE(u.nombreusuario, '') AS ""NombreUsuario"",
    COALESCE(u.apellidousuario, '') AS ""ApellidoUsuario"",
    COALESCE(u.correo, '') AS ""Correo"",
    (COALESCE(NULLIF(TRIM(u.estadoactividad), ''), '1') = '1') AS ""Activo"",
    " + selectMustChangePassword + @",
    u.fechaultimaconexion AS ""UltimoLogin"",
    COALESCE(u.rol, '') AS ""RolFallback""
FROM usuario u
WHERE u.idusuario = @idUsuario
LIMIT 1;";

                var usuario = cn.QueryFirstOrDefault<SeguridadUsuarioDTO>(sql, new { idUsuario = idUsuario });
                if (usuario == null)
                {
                    return null;
                }

                usuario.RolesAsignados = ObtenerRolesUsuario(idUsuario);
                return usuario;
            }
        }

        public bool ExisteCodigoUsuario(string codigoUsuario, int? excluirIdUsuario = null)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var sql = @"
SELECT COUNT(1)
FROM usuario
WHERE UPPER(TRIM(codigousuario)) = UPPER(TRIM(@codigoUsuario))
  AND (@excluirIdUsuario IS NULL OR idusuario <> @excluirIdUsuario);";

                return cn.ExecuteScalar<int>(sql, new
                {
                    codigoUsuario = codigoUsuario,
                    excluirIdUsuario = excluirIdUsuario
                }) > 0;
            }
        }

        public bool ExisteCorreo(string correo, int? excluirIdUsuario = null)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var sql = @"
SELECT COUNT(1)
FROM usuario
WHERE UPPER(TRIM(correo)) = UPPER(TRIM(@correo))
  AND (@excluirIdUsuario IS NULL OR idusuario <> @excluirIdUsuario);";

                return cn.ExecuteScalar<int>(sql, new
                {
                    correo = correo,
                    excluirIdUsuario = excluirIdUsuario
                }) > 0;
            }
        }

        public int CrearUsuarioConRoles(
            SeguridadUsuarioDTO usuario,
            string passwordHash,
            IEnumerable<int> roles,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var tieneMustChangePassword = ExisteColumna(cn, tx, "usuario", "must_change_password");
                    var tienePasswordChangedAt = ExisteColumna(cn, tx, "usuario", "password_changed_at");
                    var tieneRol = ExisteColumna(cn, tx, "usuario", "rol");
                    var tieneCodigoRol = ExisteColumna(cn, tx, "usuario", "codigorol");
                    var tieneUsuarioCreado = ExisteColumna(cn, tx, "usuario", "usuariocreado");
                    var tieneFechaCreado = ExisteColumna(cn, tx, "usuario", "fechacreado");
                    var tieneFechaUltimaConexion = ExisteColumna(cn, tx, "usuario", "fechaultimaconexion");

                    var rolesNormalizados = (roles ?? Enumerable.Empty<int>())
                        .Where(r => r > 0)
                        .Distinct()
                        .ToList();

                    var rolPrincipal = ObtenerRolPrincipal(cn, tx, rolesNormalizados.FirstOrDefault());

                    var columnas = new List<string>
                    {
                        "codigousuario",
                        "nombreusuario",
                        "apellidousuario",
                        "correo",
                        "clave",
                        "estadoactividad"
                    };

                    var valores = new List<string>
                    {
                        "@codigoUsuario",
                        "@nombreUsuario",
                        "@apellidoUsuario",
                        "@correo",
                        "@clave",
                        "@estadoActividad"
                    };

                    if (tieneRol)
                    {
                        columnas.Add("rol");
                        valores.Add("@rolFallback");
                    }

                    if (tieneCodigoRol)
                    {
                        columnas.Add("codigorol");
                        valores.Add("@codigoRolFallback");
                    }

                    if (tieneUsuarioCreado)
                    {
                        columnas.Add("usuariocreado");
                        valores.Add("@usuarioCreado");
                    }

                    if (tieneFechaCreado)
                    {
                        columnas.Add("fechacreado");
                        valores.Add("NOW()");
                    }

                    if (tieneFechaUltimaConexion)
                    {
                        columnas.Add("fechaultimaconexion");
                        valores.Add("NULL");
                    }

                    if (tieneMustChangePassword)
                    {
                        columnas.Add("must_change_password");
                        valores.Add("TRUE");
                    }

                    if (tienePasswordChangedAt)
                    {
                        columnas.Add("password_changed_at");
                        valores.Add("NOW()");
                    }

                    var sqlInsert = string.Format(
                        "INSERT INTO usuario ({0}) VALUES ({1}) RETURNING idusuario;",
                        string.Join(", ", columnas),
                        string.Join(", ", valores));

                    var nuevoId = cn.ExecuteScalar<int>(sqlInsert, new
                    {
                        codigoUsuario = usuario.CodigoUsuario,
                        nombreUsuario = usuario.NombreUsuario,
                        apellidoUsuario = NullIfWhite(usuario.ApellidoUsuario),
                        correo = usuario.Correo,
                        clave = passwordHash,
                        estadoActividad = usuario.Activo ? "1" : "0",
                        rolFallback = rolPrincipal != null ? rolPrincipal.Descripcion : NullIfWhite(usuario.RolFallback),
                        codigoRolFallback = rolPrincipal != null ? (int?)rolPrincipal.CodigoRol : null,
                        usuarioCreado = actorCodigoUsuario
                    }, tx);

                    ReemplazarRolesInterno(cn, tx, nuevoId, usuario.CodigoUsuario, rolesNormalizados, actorCodigoUsuario);

                    RegistrarAuditoria(
                        cn,
                        tx,
                        actorUsuarioId,
                        actorCodigoUsuario,
                        "CREAR_USUARIO",
                        "USUARIO",
                        nuevoId.ToString(),
                        new
                        {
                            usuario.CodigoUsuario,
                            usuario.NombreUsuario,
                            usuario.ApellidoUsuario,
                            usuario.Correo,
                            usuario.Activo,
                            Roles = rolesNormalizados
                        },
                        ip);

                    tx.Commit();
                    return nuevoId;
                }
            }
        }

        public bool ActualizarUsuario(
            SeguridadUsuarioDTO usuario,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var setParts = new List<string>
                    {
                        "nombreusuario = @nombreUsuario",
                        "apellidousuario = @apellidoUsuario",
                        "correo = @correo",
                        "estadoactividad = @estadoActividad"
                    };

                    if (ExisteColumna(cn, tx, "usuario", "usuariomodificado"))
                    {
                        setParts.Add("usuariomodificado = @usuarioModificado");
                    }

                    if (ExisteColumna(cn, tx, "usuario", "fechamodificado"))
                    {
                        setParts.Add("fechamodificado = NOW()");
                    }

                    var sql = "UPDATE usuario SET " + string.Join(", ", setParts) + " WHERE idusuario = @idUsuario;";

                    var rows = cn.Execute(sql, new
                    {
                        idUsuario = usuario.IdUsuario,
                        nombreUsuario = usuario.NombreUsuario,
                        apellidoUsuario = NullIfWhite(usuario.ApellidoUsuario),
                        correo = usuario.Correo,
                        estadoActividad = usuario.Activo ? "1" : "0",
                        usuarioModificado = actorCodigoUsuario
                    }, tx);

                    if (rows <= 0)
                    {
                        tx.Rollback();
                        return false;
                    }

                    RegistrarAuditoria(
                        cn,
                        tx,
                        actorUsuarioId,
                        actorCodigoUsuario,
                        "ACTUALIZAR_USUARIO",
                        "USUARIO",
                        usuario.IdUsuario.ToString(),
                        new
                        {
                            usuario.NombreUsuario,
                            usuario.ApellidoUsuario,
                            usuario.Correo,
                            usuario.Activo
                        },
                        ip);

                    tx.Commit();
                    return true;
                }
            }
        }

        public bool CambiarEstadoUsuario(
            int idUsuario,
            bool activo,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var setParts = new List<string>
                    {
                        "estadoactividad = @estadoActividad"
                    };

                    if (ExisteColumna(cn, tx, "usuario", "usuariomodificado"))
                    {
                        setParts.Add("usuariomodificado = @usuarioModificado");
                    }

                    if (ExisteColumna(cn, tx, "usuario", "fechamodificado"))
                    {
                        setParts.Add("fechamodificado = NOW()");
                    }

                    var sql = "UPDATE usuario SET " + string.Join(", ", setParts) + " WHERE idusuario = @idUsuario;";

                    var rows = cn.Execute(sql, new
                    {
                        idUsuario = idUsuario,
                        estadoActividad = activo ? "1" : "0",
                        usuarioModificado = actorCodigoUsuario
                    }, tx);

                    if (rows <= 0)
                    {
                        tx.Rollback();
                        return false;
                    }

                    RegistrarAuditoria(
                        cn,
                        tx,
                        actorUsuarioId,
                        actorCodigoUsuario,
                        activo ? "ACTIVAR_USUARIO" : "DESACTIVAR_USUARIO",
                        "USUARIO",
                        idUsuario.ToString(),
                        new { Activo = activo },
                        ip);

                    tx.Commit();
                    return true;
                }
            }
        }

        public bool ResetPassword(
            int idUsuario,
            string passwordHash,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var tieneMustChangePassword = ExisteColumna(cn, tx, "usuario", "must_change_password");
                    var tienePasswordChangedAt = ExisteColumna(cn, tx, "usuario", "password_changed_at");
                    var tieneUsuarioModificado = ExisteColumna(cn, tx, "usuario", "usuariomodificado");
                    var tieneFechaModificado = ExisteColumna(cn, tx, "usuario", "fechamodificado");
                    var tieneFechaUltimaConexion = ExisteColumna(cn, tx, "usuario", "fechaultimaconexion");

                    var setParts = new List<string>
                    {
                        "clave = @clave"
                    };

                    if (tieneFechaUltimaConexion)
                    {
                        setParts.Add("fechaultimaconexion = NULL");
                    }

                    if (tieneUsuarioModificado)
                    {
                        setParts.Add("usuariomodificado = @usuarioModificado");
                    }

                    if (tieneFechaModificado)
                    {
                        setParts.Add("fechamodificado = NOW()");
                    }

                    if (tieneMustChangePassword)
                    {
                        setParts.Add("must_change_password = TRUE");
                    }

                    if (tienePasswordChangedAt)
                    {
                        setParts.Add("password_changed_at = NOW()");
                    }

                    var sql = "UPDATE usuario SET " + string.Join(", ", setParts) + " WHERE idusuario = @idUsuario;";
                    var rows = cn.Execute(sql, new
                    {
                        idUsuario = idUsuario,
                        clave = passwordHash,
                        usuarioModificado = actorCodigoUsuario
                    }, tx);

                    if (rows <= 0)
                    {
                        tx.Rollback();
                        return false;
                    }

                    RegistrarAuditoria(
                        cn,
                        tx,
                        actorUsuarioId,
                        actorCodigoUsuario,
                        "RESET_PASSWORD",
                        "USUARIO",
                        idUsuario.ToString(),
                        new { ResetBy = actorCodigoUsuario },
                        ip);

                    tx.Commit();
                    return true;
                }
            }
        }

        public List<SeguridadRolDTO> ObtenerRolesActivos()
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var sql = @"
SELECT
    codigorol AS ""CodigoRol"",
    descripcion AS ""Descripcion"",
    activo AS ""Activo""
FROM rol
WHERE activo = TRUE
ORDER BY descripcion;";

                return cn.Query<SeguridadRolDTO>(sql).ToList();
            }
        }

        public List<int> ObtenerRolesUsuario(int idUsuario)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var filtroActivo = ExisteColumna(cn, "usuario_rol", "activo")
                    ? "AND COALESCE(ur.activo, TRUE) = TRUE"
                    : string.Empty;

                var sql = @"
SELECT ur.codigorol
FROM usuario_rol ur
INNER JOIN usuario u ON u.codigousuario = ur.codigousuario
WHERE u.idusuario = @idUsuario
  " + filtroActivo + @"
ORDER BY ur.codigorol;";

                return cn.Query<int>(sql, new { idUsuario = idUsuario }).ToList();
            }
        }

        public bool ReemplazarRolesUsuario(
            int idUsuario,
            IEnumerable<int> roles,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    var codigoUsuario = cn.QueryFirstOrDefault<string>(
                        "SELECT codigousuario FROM usuario WHERE idusuario = @idUsuario LIMIT 1;",
                        new { idUsuario = idUsuario },
                        tx);

                    if (string.IsNullOrWhiteSpace(codigoUsuario))
                    {
                        tx.Rollback();
                        return false;
                    }

                    var rolesNormalizados = (roles ?? Enumerable.Empty<int>())
                        .Where(r => r > 0)
                        .Distinct()
                        .ToList();

                    ReemplazarRolesInterno(cn, tx, idUsuario, codigoUsuario, rolesNormalizados, actorCodigoUsuario);

                    RegistrarAuditoria(
                        cn,
                        tx,
                        actorUsuarioId,
                        actorCodigoUsuario,
                        "ASIGNAR_ROLES",
                        "USUARIO",
                        idUsuario.ToString(),
                        new { Roles = rolesNormalizados },
                        ip);

                    tx.Commit();
                    return true;
                }
            }
        }

        public List<SeguridadPermisoDTO> ObtenerPermisos(bool soloActivos)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                if (!ExisteTabla(cn, "seguridad_permiso"))
                {
                    return new List<SeguridadPermisoDTO>();
                }

                var sql = @"
SELECT
    id_permiso AS ""IdPermiso"",
    codigo AS ""Codigo"",
    nombre AS ""Nombre"",
    modulo AS ""Modulo"",
    activo AS ""Activo""
FROM seguridad_permiso
WHERE (@soloActivos = FALSE OR activo = TRUE)
ORDER BY modulo, codigo;";

                return cn.Query<SeguridadPermisoDTO>(sql, new { soloActivos = soloActivos }).ToList();
            }
        }

        public List<int> ObtenerPermisosPorRol(int codigoRol)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                if (!ExisteTabla(cn, "seguridad_rol_permiso"))
                {
                    return new List<int>();
                }

                var sql = @"
SELECT id_permiso
FROM seguridad_rol_permiso
WHERE codigorol = @codigoRol
  AND activo = TRUE
ORDER BY id_permiso;";

                return cn.Query<int>(sql, new { codigoRol = codigoRol }).ToList();
            }
        }

        public bool ReemplazarPermisosRol(
            int codigoRol,
            IEnumerable<int> permisos,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    if (!ExisteTabla(cn, tx, "seguridad_rol_permiso"))
                    {
                        tx.Rollback();
                        return false;
                    }

                    var permisosNormalizados = (permisos ?? Enumerable.Empty<int>())
                        .Where(p => p > 0)
                        .Distinct()
                        .ToList();

                    cn.Execute(
                        "UPDATE seguridad_rol_permiso SET activo = FALSE, actualizado_en = NOW(), actualizado_por = @actor WHERE codigorol = @codigoRol;",
                        new { codigoRol = codigoRol, actor = actorCodigoUsuario },
                        tx);

                    foreach (var permisoId in permisosNormalizados)
                    {
                        cn.Execute(@"
INSERT INTO seguridad_rol_permiso
    (codigorol, id_permiso, activo, creado_en, creado_por, actualizado_en, actualizado_por)
VALUES
    (@codigoRol, @permisoId, TRUE, NOW(), @actor, NOW(), @actor)
ON CONFLICT (codigorol, id_permiso)
DO UPDATE SET
    activo = TRUE,
    actualizado_en = NOW(),
    actualizado_por = EXCLUDED.actualizado_por;",
                            new
                            {
                                codigoRol = codigoRol,
                                permisoId = permisoId,
                                actor = actorCodigoUsuario
                            },
                            tx);
                    }

                    RegistrarAuditoria(
                        cn,
                        tx,
                        actorUsuarioId,
                        actorCodigoUsuario,
                        "ASIGNAR_PERMISOS_ROL",
                        "ROL",
                        codigoRol.ToString(),
                        new { Permisos = permisosNormalizados },
                        ip);

                    tx.Commit();
                    return true;
                }
            }
        }

        private void ReemplazarRolesInterno(
            IDbConnection cn,
            IDbTransaction tx,
            int idUsuario,
            string codigoUsuario,
            IList<int> rolesNormalizados,
            string actorCodigoUsuario)
        {
            var tieneFechaAsignacion = ExisteColumna(cn, tx, "usuario_rol", "fechaasignacion");
            var tieneUsuarioCreado = ExisteColumna(cn, tx, "usuario_rol", "usuariocreado");
            var tieneActivo = ExisteColumna(cn, tx, "usuario_rol", "activo");

            if (tieneActivo)
            {
                cn.Execute(
                    @"UPDATE usuario_rol
SET activo = FALSE
WHERE codigousuario = @codigoUsuario;",
                    new { codigoUsuario = codigoUsuario },
                    tx);
            }
            else
            {
                cn.Execute(
                    "DELETE FROM usuario_rol WHERE codigousuario = @codigoUsuario;",
                    new { codigoUsuario = codigoUsuario },
                    tx);
            }

            foreach (var codigoRol in rolesNormalizados)
            {
                var rows = cn.Execute(@"
UPDATE usuario_rol
SET
    activo = TRUE
WHERE codigousuario = @codigoUsuario
  AND codigorol = @codigoRol;",
                    new { codigoUsuario = codigoUsuario, codigoRol = codigoRol },
                    tx);

                if (rows <= 0)
                {
                    var columnas = new List<string> { "codigousuario", "codigorol" };
                    var valores = new List<string> { "@codigoUsuario", "@codigoRol" };

                    if (tieneFechaAsignacion)
                    {
                        columnas.Add("fechaasignacion");
                        valores.Add("NOW()");
                    }

                    if (tieneUsuarioCreado)
                    {
                        columnas.Add("usuariocreado");
                        valores.Add("@actor");
                    }

                    if (tieneActivo)
                    {
                        columnas.Add("activo");
                        valores.Add("TRUE");
                    }

                    var insertSql = string.Format(
                        "INSERT INTO usuario_rol ({0}) VALUES ({1});",
                        string.Join(", ", columnas),
                        string.Join(", ", valores));

                    cn.Execute(insertSql, new
                    {
                        codigoUsuario = codigoUsuario,
                        codigoRol = codigoRol,
                        actor = actorCodigoUsuario
                    }, tx);
                }
                else
                {
                    var setParts = new List<string>();
                    if (tieneFechaAsignacion)
                    {
                        setParts.Add("fechaasignacion = NOW()");
                    }

                    if (tieneUsuarioCreado)
                    {
                        setParts.Add("usuariocreado = @actor");
                    }

                    if (setParts.Count > 0)
                    {
                        var updateSql = "UPDATE usuario_rol SET " + string.Join(", ", setParts) +
                                        " WHERE codigousuario = @codigoUsuario AND codigorol = @codigoRol;";
                        cn.Execute(updateSql, new
                        {
                            codigoUsuario = codigoUsuario,
                            codigoRol = codigoRol,
                            actor = actorCodigoUsuario
                        }, tx);
                    }
                }
            }

            var rolPrincipal = ObtenerRolPrincipal(cn, tx, rolesNormalizados.FirstOrDefault());
            var setUsuario = new List<string>();
            if (ExisteColumna(cn, tx, "usuario", "codigorol"))
            {
                setUsuario.Add("codigorol = @codigoRol");
            }

            if (ExisteColumna(cn, tx, "usuario", "rol"))
            {
                setUsuario.Add("rol = @rolDescripcion");
            }

            if (setUsuario.Count > 0)
            {
                var updateUsuarioSql = "UPDATE usuario SET " + string.Join(", ", setUsuario) + " WHERE idusuario = @idUsuario;";
                cn.Execute(updateUsuarioSql,
                    new
                    {
                        idUsuario = idUsuario,
                        codigoRol = rolPrincipal != null ? (int?)rolPrincipal.CodigoRol : null,
                        rolDescripcion = rolPrincipal != null ? rolPrincipal.Descripcion : null
                    },
                    tx);
            }
        }

        private SeguridadRolDTO ObtenerRolPrincipal(IDbConnection cn, IDbTransaction tx, int codigoRol)
        {
            if (codigoRol <= 0)
            {
                return null;
            }

            return cn.QueryFirstOrDefault<SeguridadRolDTO>(@"
SELECT
    codigorol AS ""CodigoRol"",
    descripcion AS ""Descripcion"",
    activo AS ""Activo""
FROM rol
WHERE codigorol = @codigoRol
LIMIT 1;",
                new { codigoRol = codigoRol },
                tx);
        }

        private void RegistrarAuditoria(
            IDbConnection cn,
            IDbTransaction tx,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string accion,
            string objetivoTipo,
            string objetivoId,
            object detalle,
            string ip)
        {
            if (!ExisteTabla(cn, tx, "auditoria_seguridad"))
            {
                return;
            }

            var detalleJson = detalle == null
                ? null
                : JsonConvert.SerializeObject(detalle);

            var sql = @"
INSERT INTO auditoria_seguridad
    (actor_usuario_id, actor_codigo_usuario, accion, objetivo_tipo, objetivo_id, detalle_json, fecha, ip)
VALUES
    (@actorUsuarioId, @actorCodigoUsuario, @accion, @objetivoTipo, @objetivoId, CAST(@detalleJson AS jsonb), NOW(), @ip);";

            cn.Execute(sql, new
            {
                actorUsuarioId = actorUsuarioId,
                actorCodigoUsuario = NullIfWhite(actorCodigoUsuario),
                accion = accion,
                objetivoTipo = objetivoTipo,
                objetivoId = objetivoId,
                detalleJson = detalleJson,
                ip = NullIfWhite(ip)
            }, tx);
        }

        private static string NullIfWhite(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private bool ExisteTabla(IDbConnection cn, string tableName)
        {
            return ExisteTabla(cn, null, tableName);
        }

        private bool ExisteTabla(IDbConnection cn, IDbTransaction tx, string tableName)
        {
            var sql = @"
SELECT COUNT(1)
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @tableName;";

            return cn.ExecuteScalar<int>(sql, new { tableName = tableName }, tx) > 0;
        }

        private bool ExisteColumna(IDbConnection cn, string tableName, string columnName)
        {
            return ExisteColumna(cn, null, tableName, columnName);
        }

        private bool ExisteColumna(IDbConnection cn, IDbTransaction tx, string tableName, string columnName)
        {
            var sql = @"
SELECT COUNT(1)
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @tableName
  AND column_name = @columnName;";

            return cn.ExecuteScalar<int>(sql, new
            {
                tableName = tableName,
                columnName = columnName
            }, tx) > 0;
        }
    }
}
