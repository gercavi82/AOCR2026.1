using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Dapper;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public static class UsuarioDAO
    {
        // ==========================================
        // CONEXION (desde config)
        // ==========================================
        private static string GetConnectionString()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en el config.");
            return cs;
        }

        private static NpgsqlConnection GetConnection() => new NpgsqlConnection(GetConnectionString());

        // ==========================================
        // AUTENTICACION: OBTENER POR LOGIN (usuario o correo)
        // ==========================================
        public static Usuario ObtenerPorNombreUsuario(string loginInput)
        {
            if (string.IsNullOrWhiteSpace(loginInput)) return null;

            using (var conn = GetConnection())
            {
                // NOTA: NO regreses clave al front. Solo si tu Auth la necesita internamente.
                const string sql = @"
                    SELECT
                        idusuario              AS IdUsuario,
                        idusuario              AS Id,
                        codigousuario          AS CodigoUsuario,
                        nombreusuario          AS NombreUsuario,
                        apellidousuario        AS ApellidoUsuario,
                        correo                 AS Email,
                        clave                  AS Contrasena,
                        rol                    AS Rol,
                        CASE
                            WHEN estadoactividad IS NULL THEN FALSE
                            WHEN estadoactividad::text IN ('1','true','TRUE','t','T') THEN TRUE
                            ELSE FALSE
                        END                    AS Activo
                    FROM usuario
                    WHERE (codigousuario = @p1 OR LOWER(correo) = LOWER(@p1))
                    LIMIT 1;";

                var u = conn.QueryFirstOrDefault<Usuario>(sql, new { p1 = loginInput.Trim() });

                if (u != null)
                    u.NombreCompleto = $"{u.NombreUsuario} {u.ApellidoUsuario}".Trim();

                return u;
            }
        }

        // ==========================================
        // ✅ NUEVO: OBTENER POR ID (para auto-rellenar formulario)
        // ==========================================
        public static Usuario ObtenerPorId(int idUsuario)
        {
            if (idUsuario <= 0) return null;

            using (var conn = GetConnection())
            {
                const string sql = @"
                    SELECT
                        idusuario              AS IdUsuario,
                        idusuario              AS Id,
                        codigousuario          AS CodigoUsuario,
                        nombreusuario          AS NombreUsuario,
                        apellidousuario        AS ApellidoUsuario,
                        correo                 AS Email,
                        tipoidentificacion     AS TipoIdentificacion,
                        cedulaidentificacion   AS CedulaIdentificacion,
                        numeroruc              AS NumeroRuc,
                        cargo                  AS Cargo,
                        rol                    AS Rol,
                        CASE
                            WHEN estadoactividad IS NULL THEN FALSE
                            WHEN estadoactividad::text IN ('1','true','TRUE','t','T') THEN TRUE
                            ELSE FALSE
                        END                    AS Activo
                    FROM usuario
                    WHERE idusuario = @id
                    LIMIT 1;";

                var u = conn.QueryFirstOrDefault<Usuario>(sql, new { id = idUsuario });

                if (u != null)
                    u.NombreCompleto = $"{u.NombreUsuario} {u.ApellidoUsuario}".Trim();

                return u;
            }
        }

        // ==========================================
        // OBTENER ROLES (Sincronizado con BL)
        // ==========================================
        public static List<string> ObtenerRoles(int idUsuario)
        {
            if (idUsuario <= 0) return new List<string>();

            using (var conn = GetConnection())
            {
                const string sql = @"
                    SELECT r.descripcion
                    FROM usuario u
                    INNER JOIN usuario_rol ur ON u.codigousuario = ur.codigousuario
                    INNER JOIN rol r ON r.codigorol = ur.codigorol
                    WHERE u.idusuario = @id
                      AND ur.activo = true
                      AND r.activo = true;";

                var roles = conn.Query<string>(sql, new { id = idUsuario }).ToList();

                if (roles.Count == 0)
                {
                    const string sqlFallback = "SELECT rol FROM usuario WHERE idusuario = @id";
                    var rolBasico = conn.QueryFirstOrDefault<string>(sqlFallback, new { id = idUsuario });
                    if (!string.IsNullOrWhiteSpace(rolBasico))
                        roles.Add(rolBasico);
                }

                return roles;
            }
        }

        // ==========================================
        // CREAR USUARIO (si lo usas)
        // ==========================================
        public static int Crear(Usuario usuario)
        {
            if (usuario == null) return 0;

            using (var conn = GetConnection())
            {
                const string sql = @"
                    INSERT INTO usuario (codigousuario, clave, correo, estadoactividad, nombreusuario, apellidousuario, rol)
                    VALUES (@CodigoUsuario, @Contrasena, @Email, '1', @NombreUsuario, @ApellidoUsuario, @Rol)
                    RETURNING idusuario;";

                return conn.ExecuteScalar<int>(sql, usuario);
            }
        }

        // ==========================================
        // RESTABLECER CONTRASENA
        // ==========================================
        public static bool RestablecerContrasena(string email, string nuevaClave, out string mensaje)
        {
            mensaje = "";

            if (string.IsNullOrWhiteSpace(email)) { mensaje = "Correo inválido."; return false; }
            if (string.IsNullOrWhiteSpace(nuevaClave)) { mensaje = "Clave inválida."; return false; }

            using (var conn = GetConnection())
            {
                const string sql = "UPDATE usuario SET clave = @clave WHERE LOWER(correo) = LOWER(@correo)";
                var rows = conn.Execute(sql, new { clave = nuevaClave, correo = email.Trim() });

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
        // ACTUALIZAR ULTIMA CONEXION
        // ==========================================
        public static void ActualizarUltimaConexion(int idUsuario)
        {
            if (idUsuario <= 0) return;

            using (var conn = GetConnection())
            {
                const string sql = "UPDATE usuario SET fechaultimaconexion = CURRENT_TIMESTAMP WHERE idusuario = @id";
                conn.Execute(sql, new { id = idUsuario });
            }
        }

        // ==========================================
        // OBTENER USUARIOS POR ROL
        // ==========================================
        public static List<Usuario> ListarPorRol(string rol)
        {
            if (string.IsNullOrWhiteSpace(rol)) return new List<Usuario>();

            using (var conn = GetConnection())
            {
                const string sql = @"
                    SELECT
                        idusuario        AS IdUsuario,
                        idusuario        AS Id,
                        codigousuario    AS CodigoUsuario,
                        nombreusuario    AS NombreUsuario,
                        apellidousuario  AS ApellidoUsuario,
                        rol              AS Rol
                    FROM usuario
                    WHERE LOWER(rol) = LOWER(@rol);";

                var lista = conn.Query<Usuario>(sql, new { rol = rol.Trim() }).ToList();

                foreach (var u in lista)
                    u.NombreCompleto = $"{u.NombreUsuario} {u.ApellidoUsuario}".Trim();

                return lista;
            }
        }
    }
}
