using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Npgsql;
using Dapper;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public static class UsuarioDAO
    {
        private static readonly string Host = "172.20.16.55";
        private static readonly int Puerto = 5432;
        private static readonly string BaseDatos = "dgac_des";
        private static readonly string UsuarioDB = "root";
        private static readonly string Clave = "control";

        private static string GetConnectionString() =>
            $"Host={Host};Port={Puerto};Database={BaseDatos};Username={UsuarioDB};Password={Clave};";

        // ==========================================
        // LOGIN: por usuario o correo
        // ==========================================
        public static Usuario ObtenerPorNombreUsuario(string loginInput)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"
                    SELECT 
                        idusuario     AS Id,
                        codigousuario AS CodigoUsuario,
                        codigousuario AS NombreUsuario,
                        correo        AS Email,
                        clave         AS Contrasena,
                        nombreusuario AS NombreCompleto,
                        rol           AS Rol,
                        (estadoactividad = '1') AS Activo,
                        fechacreado::timestamp AS FechaCreacion,
                        fechaultimaconexion AS FechaUltimaConexion,
                        empresa_codigo AS EmpresaCodigo,
                        ruta_documento_legal AS RutaDocumentoLegal,
                        estado_designacion_rt AS EstadoDesignacionRT,
                        ruta_constancia_rt AS RutaConstanciaRT,
                        fecha_revision_designacion AS FechaRevisionDesignacion
                    FROM usuario
                    WHERE (codigousuario = @p1 OR correo = @p1)
                    LIMIT 1;";

                var u = conn.QueryFirstOrDefault<Usuario>(sql, new { p1 = loginInput });

                // (Opcional) Completar NombreCompleto si en tu DB existe apellido y no viene concatenado
                // Si nombreusuario ya es el nombre completo, puedes ignorar esto.
                return u;
            }
        }

        // ==========================================
        // ✅ OBTENER POR ID (lo necesitabas en el Controller)
        // ==========================================
        public static Usuario ObtenerPorId(int idUsuario)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"
                    SELECT 
                        idusuario     AS Id,
                        codigousuario AS CodigoUsuario,
                        codigousuario AS NombreUsuario,
                        correo        AS Email,
                        clave         AS Contrasena,
                        nombreusuario AS NombreCompleto,
                        apellidousuario AS ApellidoUsuario,
                        rol           AS Rol,
                        (estadoactividad = '1') AS Activo,
                        fechacreado::timestamp AS FechaCreacion,
                        fechaultimaconexion AS FechaUltimaConexion,
                        empresa_codigo AS EmpresaCodigo,
                        ruta_documento_legal AS RutaDocumentoLegal,
                        estado_designacion_rt AS EstadoDesignacionRT,
                        ruta_constancia_rt AS RutaConstanciaRT,
                        fecha_revision_designacion AS FechaRevisionDesignacion
                    FROM usuario
                    WHERE idusuario = @id
                    LIMIT 1;";

                return conn.QueryFirstOrDefault<Usuario>(sql, new { id = idUsuario });
            }
        }

        // ==========================================
        // ROLES: tabla usuario_rol + rol (fallback a usuario.rol)
        // ==========================================
        public static List<string> ObtenerRoles(int idUsuario)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"
                    SELECT r.descripcion
                    FROM usuario u
                    INNER JOIN usuario_rol ur ON u.codigousuario = ur.codigousuario
                    INNER JOIN rol r ON r.codigorol = ur.codigorol
                    WHERE u.idusuario = @id
                      AND ur.activo = true
                      AND r.activo = true;";

                var roles = conn.Query<string>(sql, new { id = idUsuario }).AsList();

                if (roles.Count == 0)
                {
                    string sqlFallback = "SELECT rol FROM usuario WHERE idusuario = @id";
                    var rolBasico = conn.QueryFirstOrDefault<string>(sqlFallback, new { id = idUsuario });
                    if (!string.IsNullOrWhiteSpace(rolBasico))
                        roles.Add(rolBasico);
                }

                return roles;
            }
        }

        // ==========================================
        // LISTAR POR ROL (tu método)
        // ==========================================
        public static List<Usuario> ListarPorRol(string rol)
        {
            var lista = new List<Usuario>();
            using (var cn = new NpgsqlConnection(GetConnectionString()))
            {
                cn.Open();
                string sql = @"SELECT * FROM usuario WHERE LOWER(rol) = LOWER(@rol)";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@rol", rol);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(new Usuario
                            {
                                // OJO: tu modelo tiene Id e IdUsuario. Usa Id como estándar.
                                Id = rd["idusuario"] == DBNull.Value ? 0 : Convert.ToInt32(rd["idusuario"]),
                                CodigoUsuario = rd["codigousuario"] == DBNull.Value ? "" : rd["codigousuario"].ToString(),
                                NombreUsuario = rd["nombreusuario"] == DBNull.Value ? "" : rd["nombreusuario"].ToString(),
                                ApellidoUsuario = rd["apellidousuario"] == DBNull.Value ? "" : rd["apellidousuario"].ToString(),
                                Rol = rd["rol"] == DBNull.Value ? "" : rd["rol"].ToString(),
                                Email = rd["correo"] == DBNull.Value ? "" : rd["correo"].ToString()
                            });
                        }
                    }
                }
            }
            return lista;
        }
        // ==========================================
        // ✅ CREAR USUARIO (lo necesitaba UsuarioBL)
        // ==========================================
        public static int Crear(Usuario usuario)
        {
            if (usuario == null) throw new ArgumentNullException(nameof(usuario));

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();

                string sql = @"
            INSERT INTO usuario
                (codigousuario, clave, correo, estadoactividad, nombreusuario, rol, 
                 empresa_codigo, ruta_documento_legal, fechacreado)
            VALUES
                (@CodigoUsuario, @Contrasena, @Email, '1', @NombreCompleto, @Rol,
                 @EmpresaCodigo, @RutaDocumentoLegal, NOW())
            RETURNING idusuario;";

                // Si no te mandan CodigoUsuario pero sí NombreUsuario, lo usamos como fallback
                if (string.IsNullOrWhiteSpace(usuario.CodigoUsuario) && !string.IsNullOrWhiteSpace(usuario.NombreUsuario))
                    usuario.CodigoUsuario = usuario.NombreUsuario;

                return conn.ExecuteScalar<int>(sql, usuario);
            }
        }

        // ==========================================
        // ✅ RESTABLECER CONTRASEÑA (lo necesitaba UsuarioBL)
        // ==========================================
        public static bool RestablecerContrasena(string email, string nuevaClave, out string mensaje)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                mensaje = "Debe indicar un correo.";
                return false;
            }

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();

                  string sql = @"UPDATE usuario
                         SET clave = @clave,
                             fechaultimaconexion = NULL
                         WHERE LOWER(correo) = LOWER(@correo);";

                int rows = conn.Execute(sql, new { clave = nuevaClave, correo = email.Trim() });

                if (rows > 0)
                {
                    mensaje = "Contraseña restablecida con éxito.";
                    return true;
                }

                mensaje = "El correo no existe.";
                return false;
            }
        }

        // ==========================================
        // ✅ ACTUALIZAR CONTRASEÑA POR ID
        // ==========================================
        public static bool ActualizarContrasena(int idUsuario, string nuevaClaveHash, out string mensaje)
        {
            if (idUsuario <= 0)
            {
                mensaje = "Usuario inválido.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(nuevaClaveHash))
            {
                mensaje = "La contraseña no puede estar vacía.";
                return false;
            }

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();

                string sql = @"UPDATE usuario
                       SET clave = @clave
                       WHERE idusuario = @id;";

                int rows = conn.Execute(sql, new { clave = nuevaClaveHash, id = idUsuario });

                if (rows > 0)
                {
                    mensaje = "Contraseña actualizada con éxito.";
                    return true;
                }

                mensaje = "No se pudo actualizar la contraseña.";
                return false;
            }
        }

        // ==========================================
        // ✅ ACTUALIZAR ÚLTIMA CONEXIÓN (lo necesitaba SesionBL y UsuarioBL)
        // ==========================================
        public static void ActualizarUltimaConexion(int idUsuario)
        {
            if (idUsuario <= 0) return;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();

                // Si tu columna se llama distinto (ej: fechaultimaconexion), aquí ya está contemplada
                string sql = @"UPDATE usuario
                       SET fechaultimaconexion = NOW()
                       WHERE idusuario = @id;";

                conn.Execute(sql, new { id = idUsuario });
            }
        }

        // Forzar activación de un usuario por correo (uso: cuentas de emergencia / superadmin permanente)
        public static void ActivarPorCorreo(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo)) return;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = @"UPDATE usuario SET estadoactividad = '1' WHERE LOWER(correo) = LOWER(@correo);";
                conn.Execute(sql, new { correo = correo.Trim() });
            }
        }

        // ==========================================
        // ✅ VALIDACIONES PARA MODAL DE REGISTRO
        // ==========================================
        
        public static bool ExisteCorreo(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo)) return false;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = "SELECT COUNT(*) FROM usuario WHERE LOWER(correo) = LOWER(@correo)";
                int count = conn.ExecuteScalar<int>(sql, new { correo = correo.Trim() });
                return count > 0;
            }
        }

        public static bool ExisteIdentificacion(string identificacion)
        {
            if (string.IsNullOrWhiteSpace(identificacion)) return false;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = "SELECT COUNT(*) FROM usuario WHERE codigousuario = @identificacion";
                int count = conn.ExecuteScalar<int>(sql, new { identificacion = identificacion.Trim() });
                return count > 0;
            }
        }

        public static bool ExisteRUC(string ruc)
        {
            if (string.IsNullOrWhiteSpace(ruc)) return false;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                // Asumiendo que existe una columna 'ruc' en la tabla usuario
                // Si no existe, ajusta la consulta según tu esquema de BD
                string sql = "SELECT COUNT(*) FROM usuario WHERE ruc = @ruc";
                
                try
                {
                    int count = conn.ExecuteScalar<int>(sql, new { ruc = ruc.Trim() });
                    return count > 0;
                }
                catch
                {
                    // Si la columna no existe, retornar false por ahora
                    return false;
                }
            }
        }

        // ==========================================
        // DESIGNACIÓN RT: REVISIÓN POR COORDINADOR
        // ==========================================
        public static List<Usuario> ObtenerUsuariosRTParaRevision(bool soloPendientes = false)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string filtroPendientes = soloPendientes
                    ? "AND COALESCE(NULLIF(TRIM(estado_designacion_rt), ''), 'pendiente') = 'pendiente'"
                    : string.Empty;

                string sql = $@"SELECT 
                                    idusuario AS ""Id"",
                                    idusuario AS ""IdUsuario"",
                                    codigousuario AS ""CodigoUsuario"",
                                    codigousuario AS ""NombreUsuario"",
                                    correo AS ""Email"",
                                    clave AS ""Contrasena"",
                                    nombreusuario AS ""NombreCompleto"",
                                    apellidousuario AS ""ApellidoUsuario"",
                                    rol AS ""Rol"",
                                    (estadoactividad = '1') AS ""Activo"",
                                    empresa_codigo AS ""EmpresaCodigo"",
                                    ruta_documento_legal AS ""RutaDocumentoLegal"",
                                    COALESCE(NULLIF(TRIM(estado_designacion_rt), ''), 'pendiente') AS ""EstadoDesignacionRT"",
                                    ruta_constancia_rt AS ""RutaConstanciaRT"",
                                    fechacreado::timestamp AS ""FechaCreacion""
                               FROM usuario 
                               WHERE (
                                    ruta_documento_legal IS NOT NULL
                                    OR estado_designacion_rt IS NOT NULL
                                    OR LOWER(COALESCE(rol, '')) IN ('solicitante', 'operador', 'rt')
                               )
                               {filtroPendientes}
                               ORDER BY fechacreado DESC NULLS LAST, idusuario DESC;";

                return conn.Query<Usuario>(sql).ToList();
            }
        }

        public static List<Usuario> ObtenerUsuariosPendientesDesignacion()
        {
            return ObtenerUsuariosRTParaRevision(true);
        }

        public static void AceptarDesignacionRT(int idUsuario, string rutaConstancia)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"UPDATE usuario SET estado_designacion_rt = 'aceptado', fecha_revision_designacion = NOW(), ruta_constancia_rt = @ruta WHERE idusuario = @id";
                conn.Execute(sql, new { id = idUsuario, ruta = rutaConstancia });
            }
        }

        public static void RechazarDesignacionRT(int idUsuario)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"UPDATE usuario SET estado_designacion_rt = 'rechazado', fecha_revision_designacion = NOW() WHERE idusuario = @id";
                conn.Execute(sql, new { id = idUsuario });
            }
        }

        public static void ActualizarDesignacionRT(int idUsuario, string rutaDocumento)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"UPDATE usuario
                               SET ruta_documento_legal = @ruta,
                                   estado_designacion_rt = 'pendiente',
                                   fecha_revision_designacion = NULL,
                                   ruta_constancia_rt = NULL
                               WHERE idusuario = @id";
                conn.Execute(sql, new { id = idUsuario, ruta = rutaDocumento });
            }
        }

        public static bool ActualizarEstadoActividad(int idUsuario, bool activo)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"UPDATE usuario
                               SET estadoactividad = @estado
                               WHERE idusuario = @id";
                string estado = activo ? "1" : "0";
                int rows = conn.Execute(sql, new { id = idUsuario, estado });
                return rows > 0;
            }
        }

        public static bool EliminarUsuarioRT(int idUsuario, out string mensaje)
        {
            mensaje = string.Empty;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        var codigoUsuario = conn.ExecuteScalar<string>(
                            "SELECT codigousuario FROM usuario WHERE idusuario = @id;",
                            new { id = idUsuario }, tx);

                        if (string.IsNullOrWhiteSpace(codigoUsuario))
                        {
                            tx.Rollback();
                            mensaje = "Usuario no encontrado.";
                            return false;
                        }

                        try
                        {
                            conn.Execute(
                                "DELETE FROM usuariorol WHERE codigousuario::text = @codigo;",
                                new { codigo = codigoUsuario }, tx);
                        }
                        catch (PostgresException ex) when (ex.SqlState == "42P01")
                        {
                            // Tabla no existe en este ambiente.
                        }

                        try
                        {
                            conn.Execute(
                                "DELETE FROM usuario_rol WHERE codigousuario::text = @codigo;",
                                new { codigo = codigoUsuario }, tx);
                        }
                        catch (PostgresException ex) when (ex.SqlState == "42P01")
                        {
                            // Tabla no existe en este ambiente.
                        }

                        int rows = conn.Execute(
                            "DELETE FROM usuario WHERE idusuario = @id;",
                            new { id = idUsuario }, tx);

                        if (rows <= 0)
                        {
                            tx.Rollback();
                            mensaje = "No se pudo eliminar el usuario.";
                            return false;
                        }

                        tx.Commit();
                        mensaje = "Usuario eliminado correctamente.";
                        return true;
                    }
                    catch (PostgresException ex) when (ex.SqlState == "23503")
                    {
                        tx.Rollback();
                        mensaje = "No se puede eliminar porque el usuario tiene informacion relacionada. Inactive la cuenta en su lugar.";
                        return false;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        mensaje = "Error al eliminar usuario: " + ex.Message;
                        return false;
                    }
                }
            }
        }
    }
}
