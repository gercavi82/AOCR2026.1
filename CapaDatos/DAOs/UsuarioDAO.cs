using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.IO;
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

        private static void LogEliminarUsuarioError(int idUsuario, Exception ex)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var logDir = Path.Combine(baseDir, "App_Data", "Logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "eliminar-usuario.log");
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | id={idUsuario} | {ex}\r\n";
                File.AppendAllText(logPath, line);
            }
            catch
            {
                // No interrumpir el flujo si el log falla
            }
        }

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
        // ✅ LISTAR TODOS (ADMIN)
        // ==========================================
        public static List<Usuario> ListarTodos()
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"
                    SELECT 
                        idusuario     AS Id,
                        codigousuario AS CodigoUsuario,
                        codigousuario AS NombreUsuario,
                        correo        AS Email,
                        nombreusuario AS NombreCompleto,
                        apellidousuario AS ApellidoUsuario,
                        rol           AS Rol,
                        (estadoactividad = '1') AS Activo,
                        empresa_codigo AS EmpresaCodigo
                    FROM usuario
                    ORDER BY idusuario DESC;";

                return conn.Query<Usuario>(sql).AsList();
            }
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

        // ==========================================
        // ✅ ELIMINAR USUARIO (ADMIN)
        // ==========================================
        public static bool EliminarUsuario(int idUsuario, out string mensaje)
        {
            if (idUsuario <= 0)
            {
                mensaje = "Usuario inválido.";
                return false;
            }

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        bool TableExists(string tableName)
                        {
                            var fullName = tableName.Contains(".") ? tableName : $"public.{tableName}";
                            var exists = conn.ExecuteScalar<string>(
                                "SELECT to_regclass(@tbl)::text",
                                new { tbl = fullName }, tx);
                            return !string.IsNullOrWhiteSpace(exists);
                        }

                        bool ColumnExists(string tableName, string columnName)
                        {
                            var name = tableName.Contains(".") ? tableName.Split('.').Last() : tableName;
                            var exists = conn.ExecuteScalar<int>(@"
                                SELECT COUNT(*)
                                FROM information_schema.columns
                                WHERE table_schema = 'public'
                                  AND table_name = @tbl
                                  AND column_name = @col;",
                                new { tbl = name, col = columnName }, tx);
                            return exists > 0;
                        }

                        var codigo = conn.ExecuteScalar<string>(
                            "SELECT codigousuario FROM usuario WHERE idusuario = @id",
                            new { id = idUsuario }, tx);

                        if (string.IsNullOrWhiteSpace(codigo))
                        {
                            mensaje = "Usuario no encontrado.";
                            tx.Rollback();
                            return false;
                        }

                        // Eliminar roles (ambas tablas por compatibilidad)
                        if (TableExists("usuario_rol"))
                            conn.Execute("DELETE FROM usuario_rol WHERE codigousuario = @codigo", new { codigo }, tx);
                        if (TableExists("usuariorol"))
                            conn.Execute("DELETE FROM usuariorol WHERE codigousuario = @codigo", new { codigo }, tx);

                        // Eliminar dependencias por codigousuario (int) si aplica
                        if (int.TryParse(codigo, out var codigoInt))
                        {
                            if (TableExists("aocr_tbsesion"))
                                conn.Execute("DELETE FROM aocr_tbsesion WHERE codigousuario = @codigo", new { codigo = codigoInt }, tx);
                            if (TableExists("aocr_tbnotificacion"))
                            {
                                if (ColumnExists("aocr_tbnotificacion", "codigo_usuario"))
                                    conn.Execute("DELETE FROM aocr_tbnotificacion WHERE codigo_usuario = @codigo", new { codigo = codigoInt }, tx);
                                else if (ColumnExists("aocr_tbnotificacion", "codigousuario"))
                                    conn.Execute("DELETE FROM aocr_tbnotificacion WHERE codigousuario = @codigo", new { codigo = codigoInt }, tx);
                            }
                            if (TableExists("aocr_tbobservacion"))
                            {
                                if (ColumnExists("aocr_tbobservacion", "codigo_usuario"))
                                    conn.Execute("DELETE FROM aocr_tbobservacion WHERE codigo_usuario = @codigo", new { codigo = codigoInt }, tx);
                                else if (ColumnExists("aocr_tbobservacion", "codigousuario"))
                                    conn.Execute("DELETE FROM aocr_tbobservacion WHERE codigousuario = @codigo", new { codigo = codigoInt }, tx);
                            }
                            if (TableExists("aocr_tbhistorialestado"))
                            {
                                if (ColumnExists("aocr_tbhistorialestado", "codigo_usuario"))
                                    conn.Execute("DELETE FROM aocr_tbhistorialestado WHERE codigo_usuario = @codigo", new { codigo = codigoInt }, tx);
                                else if (ColumnExists("aocr_tbhistorialestado", "codigousuario"))
                                    conn.Execute("DELETE FROM aocr_tbhistorialestado WHERE codigousuario = @codigo", new { codigo = codigoInt }, tx);
                            }
                            if (TableExists("aocr_tbtecnico"))
                                conn.Execute("DELETE FROM aocr_tbtecnico WHERE codigousuario = @codigo", new { codigo = codigoInt }, tx);
                        }

                        // Desvincular órdenes de recaudación (FK a usuario) para permitir eliminación
                        if (TableExists("aocr_or_orden"))
                        {
                            if (ColumnExists("aocr_or_orden", "usuario_id"))
                                conn.Execute("UPDATE aocr_or_orden SET usuario_id = NULL WHERE usuario_id = @id", new { id = idUsuario }, tx);
                            else if (ColumnExists("aocr_or_orden", "codigo_usuario"))
                                conn.Execute("UPDATE aocr_or_orden SET codigo_usuario = NULL WHERE codigo_usuario = @id", new { id = idUsuario }, tx);
                            else if (ColumnExists("aocr_or_orden", "codigousuario") && int.TryParse(codigo, out var codigoInt2))
                                conn.Execute("UPDATE aocr_or_orden SET codigousuario = NULL WHERE codigousuario = @codigo", new { codigo = codigoInt2 }, tx);
                        }

                        // Eliminar historial RT y documentos RT asociados (si existen tablas)
                        if (TableExists("aocr_solicitud_rt_historial") && TableExists("aocr_solicitud_rt"))
                        {
                            conn.Execute(@"
                                DELETE FROM aocr_solicitud_rt_historial
                                WHERE solicitud_rt_id IN (
                                    SELECT id FROM aocr_solicitud_rt WHERE usuario_rt_id = @id
                                );", new { id = idUsuario }, tx);
                        }

                        // Eliminar historial RT donde el usuario figure como actor
                        if (TableExists("aocr_solicitud_rt_historial"))
                            conn.Execute("DELETE FROM aocr_solicitud_rt_historial WHERE usuario_id = @id", new { id = idUsuario }, tx);

                        if (TableExists("aocr_documento") && TableExists("aocr_solicitud_rt"))
                        {
                            conn.Execute(@"
                                DELETE FROM aocr_documento
                                WHERE solicitud_rt_id IN (
                                    SELECT id FROM aocr_solicitud_rt WHERE usuario_rt_id = @id
                                );", new { id = idUsuario }, tx);
                        }

                        if (TableExists("aocr_solicitud_rt"))
                            conn.Execute("DELETE FROM aocr_solicitud_rt WHERE usuario_rt_id = @id", new { id = idUsuario }, tx);

                        // Eliminar temporal de declaración por email
                        var correo = conn.ExecuteScalar<string>("SELECT correo FROM usuario WHERE idusuario = @id", new { id = idUsuario }, tx);
                        if (!string.IsNullOrWhiteSpace(correo))
                        {
                            conn.Execute("DELETE FROM aocr_declaracion_tmp WHERE LOWER(email) = LOWER(@email)",
                                new { email = correo }, tx);
                        }

                        // Eliminar usuario
                        conn.Execute("DELETE FROM usuario WHERE idusuario = @id", new { id = idUsuario }, tx);

                        tx.Commit();
                        mensaje = "Usuario eliminado correctamente.";
                        return true;
                    }
                    catch (PostgresException pex)
                    {
                        tx.Rollback();
                        mensaje = "No se pudo eliminar el usuario: " +
                                  (string.IsNullOrWhiteSpace(pex.MessageText) ? pex.Message : pex.MessageText);
                        if (!string.IsNullOrWhiteSpace(pex.Detail))
                            mensaje += " | Detalle: " + pex.Detail;
                        if (!string.IsNullOrWhiteSpace(pex.ConstraintName))
                            mensaje += " | Restricción: " + pex.ConstraintName;
                        if (!string.IsNullOrWhiteSpace(pex.TableName))
                            mensaje += " | Tabla: " + pex.TableName;
                        LogEliminarUsuarioError(idUsuario, pex);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        mensaje = "No se pudo eliminar el usuario: " + ex.Message;
                        LogEliminarUsuarioError(idUsuario, ex);
                        return false;
                    }
                }
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
        public static List<Usuario> ObtenerUsuariosPendientesDesignacion()
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"SELECT 
                                    idusuario AS ""Id"",
                                    idusuario AS ""IdUsuario"",
                                    codigousuario AS ""CodigoUsuario"",
                                    codigousuario AS ""NombreUsuario"",
                                    correo AS ""Email"",
                                    clave AS ""Contrasena"",
                                    nombreusuario AS ""NombreCompleto"",
                                    apellidousuario AS ""ApellidoUsuario"",
                                    rol AS ""Rol"",
                                    empresa_codigo AS ""EmpresaCodigo"",
                                    ruta_documento_legal AS ""RutaDocumentoLegal"",
                                    estado_designacion_rt AS ""EstadoDesignacionRT"",
                                    ruta_constancia_rt AS ""RutaConstanciaRT""
                               FROM usuario 
                               WHERE estado_designacion_rt IN ('pendiente', 'en_validacion')
                                 AND ruta_documento_legal IS NOT NULL";
                return conn.Query<Usuario>(sql).ToList();
            }
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

        public static void MarcarDesignacionEnValidacion(int idUsuario)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                string sql = @"UPDATE usuario
                               SET estado_designacion_rt = 'en_validacion',
                                   fecha_revision_designacion = NOW()
                               WHERE idusuario = @id";
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
    }
}
