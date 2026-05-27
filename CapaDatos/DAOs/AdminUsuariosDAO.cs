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

        public SeguridadUsuarioDTO ObtenerUsuarioPorCodigoUsuario(string codigoUsuario)
        {
            var codigoNormalizado = (codigoUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(codigoNormalizado))
            {
                return null;
            }

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
WHERE UPPER(TRIM(u.codigousuario)) = UPPER(TRIM(@codigoUsuario))
LIMIT 1;";

                var usuario = cn.QueryFirstOrDefault<SeguridadUsuarioDTO>(sql, new { codigoUsuario = codigoNormalizado });
                if (usuario == null)
                {
                    return null;
                }

                usuario.RolesAsignados = ObtenerRolesUsuario(usuario.IdUsuario);
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

        public List<SeguridadUsuarioDTO> ObtenerUsuariosActivosParaTransferencia(int excluirIdUsuario)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();

                var sql = @"
SELECT
    u.idusuario AS ""IdUsuario"",
    u.codigousuario AS ""CodigoUsuario"",
    COALESCE(u.nombreusuario, '') AS ""NombreUsuario"",
    COALESCE(u.apellidousuario, '') AS ""ApellidoUsuario"",
    COALESCE(u.correo, '') AS ""Correo"",
    (COALESCE(NULLIF(TRIM(u.estadoactividad), ''), '1') = '1') AS ""Activo""
FROM usuario u
WHERE u.idusuario <> @excluirIdUsuario
  AND (COALESCE(NULLIF(TRIM(u.estadoactividad), ''), '1') = '1')
ORDER BY COALESCE(u.nombreusuario, ''), COALESCE(u.apellidousuario, ''), u.codigousuario;";

                return cn.Query<SeguridadUsuarioDTO>(sql, new { excluirIdUsuario = excluirIdUsuario }).ToList();
            }
        }

        public UsuarioTransferenciaPreviewDTO ObtenerImpactoTransferencia(int idUsuarioOrigen)
        {
            var preview = new UsuarioTransferenciaPreviewDTO
            {
                UsuarioOrigenId = idUsuarioOrigen
            };

            if (idUsuarioOrigen <= 0)
            {
                return preview;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();

                var usuarioOrigen = ObtenerUsuarioTransferible(cn, null, idUsuarioOrigen);
                if (usuarioOrigen == null)
                {
                    return preview;
                }

                preview.UsuarioOrigenCodigo = usuarioOrigen.CodigoUsuario;

                var reglas = ConstruirReglasTransferencia(cn, null);
                foreach (var regla in reglas)
                {
                    if (!ExisteTabla(cn, regla.Tabla) || !ExisteColumna(cn, regla.Tabla, regla.Campo))
                    {
                        continue;
                    }

                    var infoColumna = ObtenerInfoColumna(cn, null, regla.Tabla, regla.Campo);
                    if (infoColumna == null)
                    {
                        continue;
                    }

                    var registros = ContarRegistrosRegla(cn, null, regla, infoColumna, usuarioOrigen.IdUsuario, usuarioOrigen.CodigoUsuario);
                    if (registros <= 0)
                    {
                        continue;
                    }

                    preview.Referencias.Add(new UsuarioReferenciaImpactoDTO
                    {
                        Grupo = regla.Grupo,
                        Tabla = regla.Tabla,
                        Campo = regla.Campo,
                        Descripcion = regla.Descripcion,
                        Estrategia = regla.Estrategia,
                        Transferible = regla.Transferible,
                        RegistrosDetectados = registros,
                        RegistrosAfectados = 0,
                        Observacion = regla.Transferible
                            ? "Se puede reasignar al usuario destino."
                            : "Registro historico, no se transfiere."
                    });
                }
            }

            preview.TotalRegistrosDetectados = preview.Referencias.Sum(r => r.RegistrosDetectados);
            preview.TotalRegistrosTransferibles = preview.Referencias
                .Where(r => r.Transferible)
                .Sum(r => r.RegistrosDetectados);
            preview.TotalRegistrosHistoricos = preview.Referencias
                .Where(r => !r.Transferible)
                .Sum(r => r.RegistrosDetectados);

            return preview;
        }

        public bool TransferirYDesactivarUsuario(
            int idUsuarioOrigen,
            int idUsuarioDestino,
            string motivo,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string ip,
            out UsuarioTransferenciaResultadoDTO resultado)
        {
            resultado = new UsuarioTransferenciaResultadoDTO
            {
                Ok = false,
                UsuarioOrigenId = idUsuarioOrigen,
                UsuarioDestinoId = idUsuarioDestino,
                Mensaje = "No se pudo completar la transferencia."
            };

            if (idUsuarioOrigen <= 0 || idUsuarioDestino <= 0)
            {
                resultado.Mensaje = "Los usuarios origen y destino son obligatorios.";
                return false;
            }

            if (idUsuarioOrigen == idUsuarioDestino)
            {
                resultado.Mensaje = "El usuario destino no puede ser el mismo usuario origen.";
                return false;
            }

            using (var cn = CrearConexion())
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        var auditoriaTransferenciaDisponible = EnsureTablasTransferencia(cn, tx);

                        var usuarioOrigen = ObtenerUsuarioTransferible(cn, tx, idUsuarioOrigen);
                        if (usuarioOrigen == null)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "Usuario origen no encontrado.";
                            return false;
                        }

                        var usuarioDestino = ObtenerUsuarioTransferible(cn, tx, idUsuarioDestino);
                        if (usuarioDestino == null)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "Usuario destino no encontrado.";
                            return false;
                        }

                        if (!usuarioDestino.Activo)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "El usuario destino esta inactivo.";
                            return false;
                        }

                        if (EsUsuarioCriticoNoTransferible(usuarioOrigen))
                        {
                            tx.Rollback();
                            resultado.Mensaje = "El usuario seleccionado es critico del sistema y no puede ser eliminado o transferido.";
                            return false;
                        }

                        if (actorUsuarioId.HasValue && actorUsuarioId.Value == idUsuarioOrigen)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "No se permite transferir/desactivar su propio usuario.";
                            return false;
                        }

                        var reglas = ConstruirReglasTransferencia(cn, tx);
                        var impactos = new List<UsuarioReferenciaImpactoDTO>();
                        var totalDetectados = 0;
                        var totalTransferidos = 0;

                        foreach (var regla in reglas)
                        {
                            if (!ExisteTabla(cn, tx, regla.Tabla) || !ExisteColumna(cn, tx, regla.Tabla, regla.Campo))
                            {
                                continue;
                            }

                            var infoColumna = ObtenerInfoColumna(cn, tx, regla.Tabla, regla.Campo);
                            if (infoColumna == null)
                            {
                                continue;
                            }

                            var detectados = ContarRegistrosRegla(cn, tx, regla, infoColumna, usuarioOrigen.IdUsuario, usuarioOrigen.CodigoUsuario);
                            if (detectados <= 0)
                            {
                                continue;
                            }

                            var item = new UsuarioReferenciaImpactoDTO
                            {
                                Grupo = regla.Grupo,
                                Tabla = regla.Tabla,
                                Campo = regla.Campo,
                                Descripcion = regla.Descripcion,
                                Estrategia = regla.Estrategia,
                                Transferible = regla.Transferible,
                                RegistrosDetectados = detectados,
                                RegistrosAfectados = 0
                            };

                            totalDetectados += detectados;

                            if (regla.Transferible)
                            {
                                var afectados = EjecutarTransferenciaRegla(
                                    cn,
                                    tx,
                                    regla,
                                    infoColumna,
                                    usuarioOrigen.IdUsuario,
                                    usuarioOrigen.CodigoUsuario,
                                    usuarioDestino.IdUsuario,
                                    usuarioDestino.CodigoUsuario);

                                item.RegistrosAfectados = afectados;
                                totalTransferidos += afectados;
                                item.Observacion = regla.EliminarEnLugarTransferir
                                    ? "Registros operativos invalidados para seguridad."
                                    : "Referencia operativa transferida al usuario destino.";
                            }
                            else
                            {
                                item.Observacion = "Registro historico conservado para auditoria.";
                            }

                            impactos.Add(item);
                        }

                        var filasUsuario = DesactivarUsuarioTransferido(cn, tx, usuarioOrigen.IdUsuario, actorCodigoUsuario);
                        if (filasUsuario <= 0)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "No se pudo desactivar el usuario origen.";
                            return false;
                        }

                        long transferenciaId = 0;
                        if (auditoriaTransferenciaDisponible)
                        {
                            transferenciaId = RegistrarTransferenciaUsuario(
                                cn,
                                tx,
                                usuarioOrigen.IdUsuario,
                                usuarioDestino.IdUsuario,
                                actorUsuarioId,
                                actorCodigoUsuario,
                                motivo,
                                ip,
                                totalDetectados,
                                totalTransferidos,
                                impactos);

                            RegistrarTransferenciaDetalle(cn, tx, transferenciaId, impactos);
                        }

                        try
                        {
                            RegistrarAuditoria(
                                cn,
                                tx,
                                actorUsuarioId,
                                actorCodigoUsuario,
                                "TRANSFERIR_ELIMINAR_USUARIO",
                                "USUARIO",
                                usuarioOrigen.IdUsuario.ToString(),
                                new
                                {
                                    UsuarioOrigen = usuarioOrigen.CodigoUsuario,
                                    UsuarioDestino = usuarioDestino.CodigoUsuario,
                                    Motivo = motivo,
                                    TotalDetectados = totalDetectados,
                                    TotalTransferidos = totalTransferidos
                                },
                                ip);
                        }
                        catch (Exception exAudit)
                        {
                            System.Diagnostics.Debug.WriteLine("AdminUsuariosDAO.TransferirYDesactivarUsuario - auditoria no registrada: " + exAudit.Message);
                        }

                        tx.Commit();

                        resultado.Ok = true;
                        resultado.TransferenciaId = transferenciaId;
                        resultado.UsuarioOrigenId = usuarioOrigen.IdUsuario;
                        resultado.UsuarioDestinoId = usuarioDestino.IdUsuario;
                        resultado.TotalRegistrosDetectados = totalDetectados;
                        resultado.TotalRegistrosTransferidos = totalTransferidos;
                        resultado.UsuarioOrigenDesactivado = true;
                        resultado.Referencias = impactos;
                        resultado.Mensaje = "Transferencia completada y usuario origen desactivado correctamente.";
                        return true;
                    }
                    catch (PostgresException exPg)
                    {
                        tx.Rollback();
                        resultado.Mensaje = string.Format(
                            "Error de base de datos en transferencia (SQLSTATE {0}): {1}",
                            exPg.SqlState,
                            exPg.MessageText);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        resultado.Mensaje = "Error en transferencia de usuario: " + ex.Message;
                        return false;
                    }
                }
            }
        }

        private bool EnsureTablasTransferencia(IDbConnection cn, IDbTransaction tx)
        {
            try
            {
                cn.Execute(@"
CREATE TABLE IF NOT EXISTS aocr_usuario_transferencia
(
    id_transferencia BIGSERIAL PRIMARY KEY,
    usuario_origen_id INT NOT NULL,
    usuario_destino_id INT NOT NULL,
    ejecutado_por_usuario_id INT NULL,
    ejecutado_por_codigo VARCHAR(100) NULL,
    motivo VARCHAR(500) NULL,
    ip VARCHAR(64) NULL,
    fecha TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    total_registros_detectados INT NOT NULL DEFAULT 0,
    total_registros_transferidos INT NOT NULL DEFAULT 0,
    resumen_json JSONB NULL
);", transaction: tx);

                cn.Execute(@"
CREATE TABLE IF NOT EXISTS aocr_usuario_transferencia_detalle
(
    id_detalle BIGSERIAL PRIMARY KEY,
    transferencia_id BIGINT NOT NULL REFERENCES aocr_usuario_transferencia(id_transferencia) ON DELETE CASCADE,
    grupo VARCHAR(30) NOT NULL,
    tabla VARCHAR(128) NOT NULL,
    campo VARCHAR(128) NOT NULL,
    descripcion VARCHAR(300) NULL,
    estrategia VARCHAR(300) NULL,
    transferible BOOLEAN NOT NULL DEFAULT FALSE,
    registros_detectados INT NOT NULL DEFAULT 0,
    registros_afectados INT NOT NULL DEFAULT 0,
    observacion VARCHAR(500) NULL
);", transaction: tx);

                cn.Execute("CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_fecha ON aocr_usuario_transferencia(fecha DESC);", transaction: tx);
                cn.Execute("CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_origen ON aocr_usuario_transferencia(usuario_origen_id);", transaction: tx);
                cn.Execute("CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_destino ON aocr_usuario_transferencia(usuario_destino_id);", transaction: tx);
                cn.Execute("CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_detalle_transferencia ON aocr_usuario_transferencia_detalle(transferencia_id);", transaction: tx);
            }
            catch (PostgresException exPg) when (exPg.SqlState == "42501")
            {
                // Sin permisos DDL: continuar sin persistir tabla de trazabilidad de transferencias.
                System.Diagnostics.Debug.WriteLine("AdminUsuariosDAO.EnsureTablasTransferencia - sin permisos para crear tablas: " + exPg.MessageText);
            }
            catch (Exception ex)
            {
                // No bloquear transferencia operativa por fallas de infraestructura de auditoría.
                System.Diagnostics.Debug.WriteLine("AdminUsuariosDAO.EnsureTablasTransferencia - error al asegurar tablas: " + ex.Message);
            }

            return ExisteTabla(cn, tx, "aocr_usuario_transferencia")
                   && ExisteTabla(cn, tx, "aocr_usuario_transferencia_detalle");
        }

        private UsuarioTransferibleInfo ObtenerUsuarioTransferible(IDbConnection cn, IDbTransaction tx, int idUsuario)
        {
            if (idUsuario <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT
    u.idusuario AS ""IdUsuario"",
    COALESCE(u.codigousuario, '') AS ""CodigoUsuario"",
    COALESCE(u.nombreusuario, '') AS ""NombreUsuario"",
    COALESCE(u.apellidousuario, '') AS ""ApellidoUsuario"",
    COALESCE(u.correo, '') AS ""Correo"",
    (COALESCE(NULLIF(TRIM(u.estadoactividad), ''), '1') = '1') AS ""Activo""
FROM usuario u
WHERE u.idusuario = @idUsuario
LIMIT 1;";

            return cn.QueryFirstOrDefault<UsuarioTransferibleInfo>(sql, new { idUsuario = idUsuario }, tx);
        }

        private List<UsuarioTransferRule> ConstruirReglasTransferencia(IDbConnection cn, IDbTransaction tx)
        {
            var reglas = new List<UsuarioTransferRule>
            {
                // Grupo A: transferibles operativos
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_tbsolicitud",
                    Campo = "codigo_usuario",
                    Descripcion = "Propietario operativo de la solicitud AOCR",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_tbsolicitud",
                    Campo = "codigo_tecnico",
                    Descripcion = "Tecnico responsable actual de la solicitud",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_or_orden",
                    Campo = "codigo_usuario",
                    Descripcion = "Responsable operativo de ordenes",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_tbinspeccion",
                    Campo = "codigo_inspector",
                    Descripcion = "Inspector asignado",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_tbnotificacion",
                    Campo = "codigousuario",
                    Descripcion = "Notificaciones operativas del usuario",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_solicitud_rt",
                    Campo = "usuario_rt_id",
                    Descripcion = "Vinculo RT activo de solicitudes",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_usuario_compania_rt",
                    Campo = "usuario_id",
                    Descripcion = "Relaciones activas usuario-compania RT",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },
                new UsuarioTransferRule
                {
                    Grupo = "A",
                    Tabla = "aocr_tbsubsanacion",
                    Campo = "codigo_usuario_solicitante",
                    Descripcion = "Responsable solicitante de subsanacion",
                    Estrategia = "Transferir a usuario destino",
                    Transferible = true
                },

                // Grupo B: historicos no transferibles
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_tbhistorialestado",
                    Campo = "codigousuario",
                    Descripcion = "Historial de estados",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_tblog",
                    Campo = "codigo_usuario",
                    Descripcion = "Bitacora de auditoria del sistema",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_audit_trail",
                    Campo = "usuario_id",
                    Descripcion = "Auditoria tecnica",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "auditoria_seguridad",
                    Campo = "actor_usuario_id",
                    Descripcion = "Auditoria de seguridad",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_solicitud_rt_historial",
                    Campo = "usuario_id",
                    Descripcion = "Historial de decisiones RT",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_tbsolicitud",
                    Campo = "created_by",
                    Descripcion = "Autor de creacion de solicitud",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_tbsolicitud",
                    Campo = "updated_by",
                    Descripcion = "Autor de actualizacion de solicitud",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_tbsubsanacion",
                    Campo = "created_by",
                    Descripcion = "Autor de creacion de subsanacion",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_tbsubsanacion",
                    Campo = "updated_by",
                    Descripcion = "Autor de actualizacion de subsanacion",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "B",
                    Tabla = "aocr_tbdocumento_subsanacion",
                    Campo = "codigo_usuario_carga",
                    Descripcion = "Autor de carga documental historica",
                    Estrategia = "Conservar historial",
                    Transferible = false
                },

                // Grupo C: mixtos
                new UsuarioTransferRule
                {
                    Grupo = "C",
                    Tabla = "aocr_tbobservacion",
                    Campo = "codigousuario",
                    Descripcion = "Observaciones del proceso",
                    Estrategia = "Mantener autor historico; transferir solo flujo operativo",
                    Transferible = false
                },
                new UsuarioTransferRule
                {
                    Grupo = "C",
                    Tabla = "aocr_tbsubsanacion",
                    Campo = "codigo_usuario_respuesta",
                    Descripcion = "Usuario que responde subsanacion",
                    Estrategia = "Mantener historial; no sobreescribir autor de respuesta",
                    Transferible = false
                }
            };

            if (cn == null)
            {
                return reglas;
            }

            var existentes = new HashSet<string>(
                reglas.Select(r => string.Format("{0}.{1}", r.Tabla, r.Campo)),
                StringComparer.OrdinalIgnoreCase);

            const string sqlDescubrimiento = @"
SELECT
    table_name AS ""Tabla"",
    column_name AS ""Campo""
FROM information_schema.columns
WHERE table_schema = 'public'
  AND (
        column_name LIKE '%usuario%'
        OR column_name LIKE '%creado_por%'
        OR column_name LIKE '%aprobado_por%'
        OR column_name LIKE '%asignado%'
        OR column_name LIKE '%responsable%'
      );";

            var detectadas = cn.Query<ColumnaDetectadaUsuario>(sqlDescubrimiento, transaction: tx).ToList();
            foreach (var detectada in detectadas)
            {
                if (detectada == null ||
                    string.IsNullOrWhiteSpace(detectada.Tabla) ||
                    string.IsNullOrWhiteSpace(detectada.Campo))
                {
                    continue;
                }

                if (detectada.Tabla.StartsWith("aocr_usuario_transferencia", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var llave = string.Format("{0}.{1}", detectada.Tabla, detectada.Campo);
                if (existentes.Contains(llave))
                {
                    continue;
                }

                reglas.Add(new UsuarioTransferRule
                {
                    Grupo = "C",
                    Tabla = detectada.Tabla,
                    Campo = detectada.Campo,
                    Descripcion = "Referencia detectada automaticamente",
                    Estrategia = "Revision manual recomendada; se conserva para auditoria",
                    Transferible = false
                });

                existentes.Add(llave);
            }

            return reglas;
        }

        private ColumnaTransferInfo ObtenerInfoColumna(IDbConnection cn, IDbTransaction tx, string tabla, string campo)
        {
            const string sql = @"
SELECT
    data_type AS ""DataType"",
    udt_name AS ""UdtName"",
    (is_nullable = 'YES') AS ""Nullable""
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @tabla
  AND column_name = @campo
LIMIT 1;";

            return cn.QueryFirstOrDefault<ColumnaTransferInfo>(sql, new
            {
                tabla = tabla,
                campo = campo
            }, tx);
        }

        private int ContarRegistrosRegla(
            IDbConnection cn,
            IDbTransaction tx,
            UsuarioTransferRule regla,
            ColumnaTransferInfo infoColumna,
            int idUsuarioOrigen,
            string codigoUsuarioOrigen)
        {
            var tablaSql = QuoteIdentifier(regla.Tabla);
            var campoSql = QuoteIdentifier(regla.Campo);

            if (infoColumna.EsNumerica)
            {
                var sql = string.Format("SELECT COUNT(1) FROM {0} WHERE {1} = @idUsuarioOrigen;", tablaSql, campoSql);
                return cn.ExecuteScalar<int>(sql, new { idUsuarioOrigen = idUsuarioOrigen }, tx);
            }

            if (string.IsNullOrWhiteSpace(codigoUsuarioOrigen))
            {
                return 0;
            }

            var sqlTexto = string.Format(
                "SELECT COUNT(1) FROM {0} WHERE UPPER(TRIM(COALESCE({1}::text, ''))) = UPPER(TRIM(@codigoUsuarioOrigen));",
                tablaSql,
                campoSql);

            return cn.ExecuteScalar<int>(sqlTexto, new { codigoUsuarioOrigen = codigoUsuarioOrigen }, tx);
        }

        private int EjecutarTransferenciaRegla(
            IDbConnection cn,
            IDbTransaction tx,
            UsuarioTransferRule regla,
            ColumnaTransferInfo infoColumna,
            int idUsuarioOrigen,
            string codigoUsuarioOrigen,
            int idUsuarioDestino,
            string codigoUsuarioDestino)
        {
            var tablaSql = QuoteIdentifier(regla.Tabla);
            var campoSql = QuoteIdentifier(regla.Campo);

            if (regla.EliminarEnLugarTransferir)
            {
                if (!infoColumna.Nullable)
                {
                    return 0;
                }

                if (infoColumna.EsNumerica)
                {
                    var sqlNull = string.Format(
                        "UPDATE {0} SET {1} = NULL WHERE {1} = @idUsuarioOrigen;",
                        tablaSql,
                        campoSql);
                    return cn.Execute(sqlNull, new { idUsuarioOrigen = idUsuarioOrigen }, tx);
                }

                var sqlNullTexto = string.Format(
                    "UPDATE {0} SET {1} = NULL WHERE UPPER(TRIM(COALESCE({1}::text, ''))) = UPPER(TRIM(@codigoUsuarioOrigen));",
                    tablaSql,
                    campoSql);

                return cn.Execute(sqlNullTexto, new { codigoUsuarioOrigen = codigoUsuarioOrigen }, tx);
            }

            if (infoColumna.EsNumerica)
            {
                var sql = string.Format(
                    "UPDATE {0} SET {1} = @idUsuarioDestino WHERE {1} = @idUsuarioOrigen;",
                    tablaSql,
                    campoSql);

                return cn.Execute(sql, new
                {
                    idUsuarioDestino = idUsuarioDestino,
                    idUsuarioOrigen = idUsuarioOrigen
                }, tx);
            }

            if (string.IsNullOrWhiteSpace(codigoUsuarioDestino))
            {
                return 0;
            }

            var sqlTexto = string.Format(
                "UPDATE {0} SET {1} = @codigoUsuarioDestino WHERE UPPER(TRIM(COALESCE({1}::text, ''))) = UPPER(TRIM(@codigoUsuarioOrigen));",
                tablaSql,
                campoSql);

            return cn.Execute(sqlTexto, new
            {
                codigoUsuarioDestino = codigoUsuarioDestino,
                codigoUsuarioOrigen = codigoUsuarioOrigen
            }, tx);
        }

        private int DesactivarUsuarioTransferido(IDbConnection cn, IDbTransaction tx, int idUsuarioOrigen, string actorCodigoUsuario)
        {
            var setParts = new List<string>();
            if (ExisteColumna(cn, tx, "usuario", "estadoactividad"))
            {
                setParts.Add("estadoactividad = '0'");
            }

            if (ExisteColumna(cn, tx, "usuario", "activo"))
            {
                setParts.Add("activo = FALSE");
            }

            if (ExisteColumna(cn, tx, "usuario", "usuariomodificado"))
            {
                setParts.Add("usuariomodificado = @actor");
            }

            if (ExisteColumna(cn, tx, "usuario", "usuarioactualizado"))
            {
                setParts.Add("usuarioactualizado = @actor");
            }

            if (ExisteColumna(cn, tx, "usuario", "fechamodificado"))
            {
                setParts.Add("fechamodificado = NOW()");
            }

            if (ExisteColumna(cn, tx, "usuario", "fechaactualizado"))
            {
                setParts.Add("fechaactualizado = NOW()");
            }

            if (ExisteColumna(cn, tx, "usuario", "fechabaja"))
            {
                setParts.Add("fechabaja = NOW()");
            }

            if (setParts.Count == 0)
            {
                return 0;
            }

            var sql = "UPDATE usuario SET " + string.Join(", ", setParts) + " WHERE idusuario = @idUsuarioOrigen;";
            return cn.Execute(sql, new
            {
                idUsuarioOrigen = idUsuarioOrigen,
                actor = NullIfWhite(actorCodigoUsuario)
            }, tx);
        }

        private long RegistrarTransferenciaUsuario(
            IDbConnection cn,
            IDbTransaction tx,
            int usuarioOrigenId,
            int usuarioDestinoId,
            int? actorUsuarioId,
            string actorCodigoUsuario,
            string motivo,
            string ip,
            int totalDetectados,
            int totalTransferidos,
            IList<UsuarioReferenciaImpactoDTO> impactos)
        {
            var resumenJson = JsonConvert.SerializeObject(impactos ?? new List<UsuarioReferenciaImpactoDTO>());

            const string sql = @"
INSERT INTO aocr_usuario_transferencia
(
    usuario_origen_id,
    usuario_destino_id,
    ejecutado_por_usuario_id,
    ejecutado_por_codigo,
    motivo,
    ip,
    fecha,
    total_registros_detectados,
    total_registros_transferidos,
    resumen_json
)
VALUES
(
    @usuarioOrigenId,
    @usuarioDestinoId,
    @actorUsuarioId,
    @actorCodigoUsuario,
    @motivo,
    @ip,
    NOW(),
    @totalDetectados,
    @totalTransferidos,
    CAST(@resumenJson AS jsonb)
)
RETURNING id_transferencia;";

            return cn.ExecuteScalar<long>(sql, new
            {
                usuarioOrigenId = usuarioOrigenId,
                usuarioDestinoId = usuarioDestinoId,
                actorUsuarioId = actorUsuarioId,
                actorCodigoUsuario = NullIfWhite(actorCodigoUsuario),
                motivo = NullIfWhite(motivo),
                ip = NullIfWhite(ip),
                totalDetectados = totalDetectados,
                totalTransferidos = totalTransferidos,
                resumenJson = resumenJson
            }, tx);
        }

        private void RegistrarTransferenciaDetalle(
            IDbConnection cn,
            IDbTransaction tx,
            long transferenciaId,
            IEnumerable<UsuarioReferenciaImpactoDTO> impactos)
        {
            var items = impactos != null ? impactos.ToList() : new List<UsuarioReferenciaImpactoDTO>();
            if (!items.Any())
            {
                return;
            }

            const string sql = @"
INSERT INTO aocr_usuario_transferencia_detalle
(
    transferencia_id,
    grupo,
    tabla,
    campo,
    descripcion,
    estrategia,
    transferible,
    registros_detectados,
    registros_afectados,
    observacion
)
VALUES
(
    @transferenciaId,
    @grupo,
    @tabla,
    @campo,
    @descripcion,
    @estrategia,
    @transferible,
    @registrosDetectados,
    @registrosAfectados,
    @observacion
);";

            foreach (var item in items)
            {
                cn.Execute(sql, new
                {
                    transferenciaId = transferenciaId,
                    grupo = NullIfWhite(item.Grupo) ?? "N/A",
                    tabla = NullIfWhite(item.Tabla) ?? "N/A",
                    campo = NullIfWhite(item.Campo) ?? "N/A",
                    descripcion = NullIfWhite(item.Descripcion),
                    estrategia = NullIfWhite(item.Estrategia),
                    transferible = item.Transferible,
                    registrosDetectados = item.RegistrosDetectados,
                    registrosAfectados = item.RegistrosAfectados,
                    observacion = NullIfWhite(item.Observacion)
                }, tx);
            }
        }

        private bool EsUsuarioCriticoNoTransferible(UsuarioTransferibleInfo usuario)
        {
            if (usuario == null)
            {
                return false;
            }

            var codigo = (usuario.CodigoUsuario ?? string.Empty).Trim();
            var correo = (usuario.Correo ?? string.Empty).Trim();

            if (usuario.IdUsuario == 1)
            {
                return true;
            }

            if (string.Equals(codigo, "USU_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(codigo, "ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(correo, "gercavi82@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "\"" + (identifier ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private sealed class UsuarioTransferRule
        {
            public string Grupo { get; set; }
            public string Tabla { get; set; }
            public string Campo { get; set; }
            public string Descripcion { get; set; }
            public string Estrategia { get; set; }
            public bool Transferible { get; set; }
            public bool EliminarEnLugarTransferir { get; set; }
        }

        private sealed class ColumnaTransferInfo
        {
            public string DataType { get; set; }
            public string UdtName { get; set; }
            public bool Nullable { get; set; }

            public bool EsNumerica
            {
                get
                {
                    var dataType = (DataType ?? string.Empty).ToLowerInvariant();
                    var udt = (UdtName ?? string.Empty).ToLowerInvariant();
                    return dataType.Contains("integer")
                           || dataType.Contains("numeric")
                           || dataType.Contains("decimal")
                           || dataType.Contains("bigint")
                           || udt == "int2"
                           || udt == "int4"
                           || udt == "int8"
                           || udt == "numeric";
                }
            }
        }

        private sealed class UsuarioTransferibleInfo
        {
            public int IdUsuario { get; set; }
            public string CodigoUsuario { get; set; }
            public string NombreUsuario { get; set; }
            public string ApellidoUsuario { get; set; }
            public string Correo { get; set; }
            public bool Activo { get; set; }
        }

        private sealed class ColumnaDetectadaUsuario
        {
            public string Tabla { get; set; }
            public string Campo { get; set; }
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

