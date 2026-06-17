using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using Npgsql;
using Dapper;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public static class UsuarioDAO
    {
        private static readonly Lazy<string> _connectionString = new Lazy<string>(ResolveConnectionString);

        private static string GetConnectionString()
        {
            return _connectionString.Value;
        }

        private static string ResolveConnectionString()
        {
            var envConnection = Environment.GetEnvironmentVariable("AOCR_CONNSTR_AOCRCONNECTION");
            if (!string.IsNullOrWhiteSpace(envConnection))
            {
                return envConnection;
            }

            var configConnection = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                                   ?? ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString
                                   ?? ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString;
            if (!string.IsNullOrWhiteSpace(configConnection))
            {
                return configConnection;
            }

            throw new InvalidOperationException("No se encontró cadena de conexión PostgreSQL para UsuarioDAO.");
        }

        // ==========================================
        // LOGIN: por usuario o correo
        // ==========================================
        public static Usuario ObtenerPorNombreUsuario(string loginInput)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                var hasMustChangePassword = ExisteColumna(conn, "usuario", "must_change_password");
                var selectMustChangePassword = hasMustChangePassword
                    ? "COALESCE(must_change_password, FALSE) AS MustChangePassword,"
                    : "FALSE AS MustChangePassword,";
                var selectRuc = ConstruirSelectRuc(conn);

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
                        " + selectMustChangePassword + @"
                        " + selectRuc + @"
                        empresa_codigo AS EmpresaCodigo,
                        ruta_documento_legal AS RutaDocumentoLegal,
                        estado_designacion_rt AS EstadoDesignacionRT,
                        ruta_constancia_rt AS RutaConstanciaRT,
                        fecha_revision_designacion AS FechaRevisionDesignacion
                    FROM usuario
                    WHERE (
                        UPPER(TRIM(codigousuario)) = UPPER(TRIM(@p1))
                        OR UPPER(TRIM(correo)) = UPPER(TRIM(@p1))
                    )
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
                conn.Open();
                var hasMustChangePassword = ExisteColumna(conn, "usuario", "must_change_password");
                var selectMustChangePassword = hasMustChangePassword
                    ? "COALESCE(must_change_password, FALSE) AS MustChangePassword,"
                    : "FALSE AS MustChangePassword,";
                var selectRuc = ConstruirSelectRuc(conn);

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
                        " + selectMustChangePassword + @"
                        " + selectRuc + @"
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

        public static string ObtenerIdentificacionPrincipal(int idUsuario)
        {
            if (idUsuario <= 0)
            {
                return string.Empty;
            }

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();

                var expresiones = new List<string>();

                // Priorizamos cédula; si no existe, usamos RUC/identificación tributaria.
                if (ExisteColumna(conn, "usuario", "cedulaidentificacion"))
                {
                    expresiones.Add("NULLIF(TRIM(cedulaidentificacion), '')");
                }

                if (ExisteColumna(conn, "usuario", "identificaciontributaria"))
                {
                    expresiones.Add("NULLIF(TRIM(identificaciontributaria), '')");
                }

                if (ExisteColumna(conn, "usuario", "ruc"))
                {
                    expresiones.Add("NULLIF(TRIM(ruc), '')");
                }

                if (ExisteColumna(conn, "usuario", "numeroruc"))
                {
                    expresiones.Add("NULLIF(TRIM(numeroruc), '')");
                }

                if (expresiones.Count == 0)
                {
                    return string.Empty;
                }

                var sql = @"
                    SELECT COALESCE(" + string.Join(", ", expresiones) + @", '')
                    FROM usuario
                    WHERE idusuario = @id
                    LIMIT 1;";

                return (conn.ExecuteScalar<string>(sql, new { id = idUsuario }) ?? string.Empty).Trim();
            }
        }

        public static string ObtenerNombreCompletoPrincipal(int idUsuario)
        {
            if (idUsuario <= 0)
            {
                return string.Empty;
            }

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();

                var tieneNombreUsuario = ExisteColumna(conn, "usuario", "nombreusuario");
                var tieneApellidoUsuario = ExisteColumna(conn, "usuario", "apellidousuario");
                var tieneNombres = ExisteColumna(conn, "usuario", "nombres");
                var tienePrimerNombre = ExisteColumna(conn, "usuario", "primer_nombre");
                var tieneSegundoNombre = ExisteColumna(conn, "usuario", "segundo_nombre");
                var tieneApellidoPaterno = ExisteColumna(conn, "usuario", "apellidopaterno") || ExisteColumna(conn, "usuario", "apellido_paterno");
                var tieneApellidoMaterno = ExisteColumna(conn, "usuario", "apellidomaterno") || ExisteColumna(conn, "usuario", "apellido_materno");
                var tieneNombreCompleto = ExisteColumna(conn, "usuario", "nombrecompleto") || ExisteColumna(conn, "usuario", "nombre_completo");

                var segmentos = new List<string>();

                if (tieneNombres || tienePrimerNombre || tieneSegundoNombre)
                {
                    if (tieneNombres)
                    {
                        segmentos.Add("NULLIF(TRIM(nombres), '')");
                    }
                    else
                    {
                        if (tienePrimerNombre)
                        {
                            segmentos.Add("NULLIF(TRIM(primer_nombre), '')");
                        }
                        if (tieneSegundoNombre)
                        {
                            segmentos.Add("NULLIF(TRIM(segundo_nombre), '')");
                        }
                    }

                    if (tieneApellidoPaterno)
                    {
                        if (ExisteColumna(conn, "usuario", "apellidopaterno"))
                            segmentos.Add("NULLIF(TRIM(apellidopaterno), '')");
                        else if (ExisteColumna(conn, "usuario", "apellido_paterno"))
                            segmentos.Add("NULLIF(TRIM(apellido_paterno), '')");
                    }

                    if (tieneApellidoMaterno)
                    {
                        if (ExisteColumna(conn, "usuario", "apellidomaterno"))
                            segmentos.Add("NULLIF(TRIM(apellidomaterno), '')");
                        else if (ExisteColumna(conn, "usuario", "apellido_materno"))
                            segmentos.Add("NULLIF(TRIM(apellido_materno), '')");
                    }
                }
                else
                {
                    if (tieneNombreUsuario)
                    {
                        segmentos.Add("NULLIF(TRIM(nombreusuario), '')");
                    }

                    if (tieneApellidoUsuario)
                    {
                        segmentos.Add("NULLIF(TRIM(apellidousuario), '')");
                    }
                }

                if (segmentos.Count == 0)
                {
                    if (tieneNombreCompleto)
                    {
                        var columna = ExisteColumna(conn, "usuario", "nombrecompleto") ? "nombrecompleto" : "nombre_completo";
                        var sqlNombreCompleto = @"
                            SELECT COALESCE(NULLIF(TRIM(" + columna + @"), ''), '')
                            FROM usuario
                            WHERE idusuario = @id
                            LIMIT 1;";

                        return NormalizarEspacios(conn.ExecuteScalar<string>(sqlNombreCompleto, new { id = idUsuario }));
                    }

                    return string.Empty;
                }

                var sql = @"
                    SELECT COALESCE(NULLIF(TRIM(CONCAT_WS(' ', " + string.Join(", ", segmentos) + @")), ''), '')
                    FROM usuario
                    WHERE idusuario = @id
                    LIMIT 1;";

                return NormalizarEspacios(conn.ExecuteScalar<string>(sql, new { id = idUsuario }));
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

                // Si no te mandan CodigoUsuario pero sí NombreUsuario, lo usamos como fallback
                if (string.IsNullOrWhiteSpace(usuario.CodigoUsuario) && !string.IsNullOrWhiteSpace(usuario.NombreUsuario))
                    usuario.CodigoUsuario = usuario.NombreUsuario;

                var codigoUsuario = (usuario.CodigoUsuario ?? string.Empty).Trim();
                var nombreUsuario = (usuario.NombreUsuario ?? string.Empty).Trim();
                var apellidoUsuario = (usuario.ApellidoUsuario ?? string.Empty).Trim();
                var nombreCompleto = (usuario.NombreCompleto ?? string.Empty).Trim();

                // Compatibilidad legacy: si antes se guardaba todo en NombreCompleto/nombreusuario,
                // intentamos separar para poblar nombre y apellido.
                if (!string.IsNullOrWhiteSpace(nombreCompleto))
                {
                    if (string.IsNullOrWhiteSpace(nombreUsuario) ||
                        string.Equals(nombreUsuario, codigoUsuario, StringComparison.OrdinalIgnoreCase))
                    {
                        string nombreSeparado;
                        string apellidoSeparado;
                        SepararNombreCompleto(nombreCompleto, out nombreSeparado, out apellidoSeparado);
                        if (string.IsNullOrWhiteSpace(nombreUsuario))
                        {
                            nombreUsuario = nombreSeparado;
                        }
                        if (string.IsNullOrWhiteSpace(apellidoUsuario))
                        {
                            apellidoUsuario = apellidoSeparado;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(apellidoUsuario))
                    {
                        string nombreSeparado;
                        string apellidoSeparado;
                        SepararNombreCompleto(nombreCompleto, out nombreSeparado, out apellidoSeparado);
                        if (string.IsNullOrWhiteSpace(apellidoSeparado) == false)
                        {
                            apellidoUsuario = apellidoSeparado;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(nombreUsuario))
                {
                    nombreUsuario = !string.IsNullOrWhiteSpace(nombreCompleto)
                        ? nombreCompleto
                        : codigoUsuario;
                }

                if (string.IsNullOrWhiteSpace(nombreCompleto))
                {
                    nombreCompleto = string.Format("{0} {1}", nombreUsuario, apellidoUsuario).Trim();
                }

                var columnas = new List<string>
                {
                    "codigousuario",
                    "clave",
                    "correo",
                    "estadoactividad",
                    "nombreusuario",
                    "rol",
                    "empresa_codigo",
                    "ruta_documento_legal",
                    "fechacreado"
                };

                var valores = new List<string>
                {
                    "@CodigoUsuario",
                    "@Contrasena",
                    "@Email",
                    "'1'",
                    "@NombreUsuario",
                    "@Rol",
                    "@EmpresaCodigo",
                    "@RutaDocumentoLegal",
                    "NOW()"
                };

                if (ExisteColumna(conn, "usuario", "apellidousuario"))
                {
                    columnas.Add("apellidousuario");
                    valores.Add("@ApellidoUsuario");
                }

                var sql = string.Format(
                    "INSERT INTO usuario ({0}) VALUES ({1}) RETURNING idusuario;",
                    string.Join(", ", columnas),
                    string.Join(", ", valores));

                return conn.ExecuteScalar<int>(sql, new
                {
                    CodigoUsuario = codigoUsuario,
                    Contrasena = usuario.Contrasena,
                    Email = usuario.Email,
                    NombreUsuario = nombreUsuario,
                    ApellidoUsuario = string.IsNullOrWhiteSpace(apellidoUsuario) ? null : apellidoUsuario,
                    Rol = usuario.Rol,
                    EmpresaCodigo = usuario.EmpresaCodigo,
                    RutaDocumentoLegal = usuario.RutaDocumentoLegal
                });
            }
        }

        public static bool ActualizarEmpresaCodigoPrincipal(int idUsuario, string empresaCodigo)
        {
            if (idUsuario <= 0)
            {
                return false;
            }

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                const string sql = @"
UPDATE usuario
SET empresa_codigo = @empresaCodigo
WHERE idusuario = @id;";

                var rows = conn.Execute(sql, new
                {
                    id = idUsuario,
                    empresaCodigo = (empresaCodigo ?? string.Empty).Trim()
                });

                return rows > 0;
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

                var setList = new List<string>
                {
                    "clave = @clave",
                    "fechaultimaconexion = NULL"
                };

                if (ExisteColumna(conn, "usuario", "must_change_password"))
                {
                    setList.Add("must_change_password = TRUE");
                }

                if (ExisteColumna(conn, "usuario", "password_changed_at"))
                {
                    setList.Add("password_changed_at = NOW()");
                }

                string sql = "UPDATE usuario SET " + string.Join(", ", setList) + " WHERE LOWER(correo) = LOWER(@correo);";

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

                var setList = new List<string>
                {
                    "clave = @clave"
                };

                if (ExisteColumna(conn, "usuario", "must_change_password"))
                {
                    setList.Add("must_change_password = FALSE");
                }

                if (ExisteColumna(conn, "usuario", "password_changed_at"))
                {
                    setList.Add("password_changed_at = NOW()");
                }

                string sql = "UPDATE usuario SET " + string.Join(", ", setList) + " WHERE idusuario = @id;";

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
                conn.Open();

                var columnas = new List<string>();
                if (ExisteColumna(conn, "usuario", "ruc"))
                {
                    columnas.Add("ruc");
                }

                if (ExisteColumna(conn, "usuario", "numeroruc"))
                {
                    columnas.Add("numeroruc");
                }

                if (!columnas.Any())
                {
                    return false;
                }

                var condiciones = columnas
                    .Select(c => string.Format("NULLIF(TRIM({0}), '') = @ruc", c));

                var sql = "SELECT COUNT(*) FROM usuario WHERE " + string.Join(" OR ", condiciones) + ";";
                int count = conn.ExecuteScalar<int>(sql, new { ruc = ruc.Trim() });
                return count > 0;
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
                                    fechacreado::timestamp AS ""FechaCreacion"",
                                    COALESCE((SELECT COUNT(*)
                                              FROM aocr_or_orden o
                                              WHERE o.codigo_usuario = usuario.idusuario), 0) AS ""OrdenesRecaudacionCount"",
                                    COALESCE((SELECT COUNT(*)
                                              FROM aocr_tbdocumento_subsanacion ds
                                              WHERE ds.codigo_usuario_carga = usuario.idusuario), 0) AS ""DocumentosSubsanacionCount"",
                                    COALESCE((SELECT COUNT(*)
                                              FROM aocr_tbsubsanacion s
                                              WHERE s.codigo_usuario_solicitante = usuario.idusuario
                                                 OR s.codigo_usuario_respuesta = usuario.idusuario), 0) AS ""SubsanacionesCount"",
                                    (
                                        COALESCE((SELECT COUNT(*) FROM aocr_or_orden o WHERE o.codigo_usuario = usuario.idusuario), 0)
                                        + COALESCE((SELECT COUNT(*) FROM aocr_tbdocumento_subsanacion ds WHERE ds.codigo_usuario_carga = usuario.idusuario), 0)
                                        + COALESCE((SELECT COUNT(*) FROM aocr_tbsubsanacion s
                                                    WHERE s.codigo_usuario_solicitante = usuario.idusuario
                                                       OR s.codigo_usuario_respuesta = usuario.idusuario), 0)
                                    ) AS ""TotalRelacionesBloqueantes""
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

        public static bool EliminarUsuarioRT(int idUsuario, out string mensaje, bool permitirPurgaDatosPruebas = false)
        {
            mensaje = string.Empty;

            using (var conn = new NpgsqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    string codigoUsuario = string.Empty;
                    int idEncontrado = idUsuario;
                    PurgadoDatosUsuarioPruebas purgado = null;

                    try
                    {
                        var usuario = conn.QueryFirstOrDefault(
                            @"SELECT idusuario AS id, codigousuario AS codigo
                              FROM usuario
                              WHERE idusuario = @id;",
                            new { id = idUsuario },
                            tx);

                        if (usuario == null)
                        {
                            tx.Rollback();
                            mensaje = "Usuario no encontrado.";
                            return false;
                        }

                        codigoUsuario = Convert.ToString(usuario.codigo ?? string.Empty);
                        idEncontrado = Convert.ToInt32(usuario.id);

                        var relaciones = ObtenerRelacionesBloqueantesUsuario(conn, tx, idEncontrado);
                        if (relaciones.Total > 0)
                        {
                            if (permitirPurgaDatosPruebas)
                            {
                                purgado = PurgarDatosUsuarioParaPruebas(conn, tx, idEncontrado);
                                relaciones = ObtenerRelacionesBloqueantesUsuario(conn, tx, idEncontrado);
                            }

                            if (relaciones.Total > 0)
                            {
                                tx.Rollback();
                                mensaje = ConstruirMensajeRelacionesBloqueantes(relaciones);
                                return false;
                            }
                        }

                        EliminarRelacionesUsuario(conn, tx, "usuariorol", idEncontrado, codigoUsuario);
                        EliminarRelacionesUsuario(conn, tx, "usuario_rol", idEncontrado, codigoUsuario);

                        int rows = conn.Execute(
                            "DELETE FROM usuario WHERE idusuario = @id;",
                            new { id = idEncontrado },
                            tx);

                        if (rows > 0)
                        {
                            tx.Commit();
                            if (purgado != null && purgado.Total > 0)
                            {
                                mensaje = "Usuario eliminado correctamente. Se depuraron datos de prueba asociados: " + purgado.Resumen + ".";
                            }
                            else
                            {
                                mensaje = "Usuario eliminado correctamente.";
                            }
                            return true;
                        }

                        tx.Rollback();
                        mensaje = "No se pudo eliminar el usuario.";
                        return false;
                    }
                    catch (PostgresException ex) when (ex.SqlState == "23503")
                    {
                        tx.Rollback();

                        if (string.Equals(ex.ConstraintName, "fk_orden_usuario", StringComparison.OrdinalIgnoreCase))
                        {
                            int totalOrdenes = 0;
                            try
                            {
                                totalOrdenes = conn.ExecuteScalar<int>(
                                    @"SELECT COUNT(*)
                                      FROM aocr_or_orden
                                      WHERE codigo_usuario = @id;",
                                    new { id = idEncontrado });
                            }
                            catch
                            {
                                // Si falla el conteo no bloqueamos el mensaje principal.
                            }

                            mensaje = totalOrdenes > 0
                                ? $"No se puede eliminar porque el usuario tiene {totalOrdenes} orden(es) de recaudacion asociadas."
                                : "No se puede eliminar porque el usuario tiene ordenes de recaudacion asociadas.";
                        }
                        else
                        {
                            mensaje = "No se puede eliminar porque el usuario tiene informacion relacionada. Use Inactivar si desea bloquear el acceso.";
                        }

                        return false;
                    }
                    catch (PostgresException ex)
                    {
                        tx.Rollback();
                        mensaje = $"Error BD al eliminar usuario ({ex.SqlState}): {ex.MessageText}";
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

        private sealed class RelacionesBloqueantesUsuario
        {
            public int Ordenes { get; set; }
            public int DocumentosSubsanacion { get; set; }
            public int Subsanaciones { get; set; }
            public int Total => Ordenes + DocumentosSubsanacion + Subsanaciones;
        }

        private sealed class PurgadoDatosUsuarioPruebas
        {
            public int Ordenes { get; set; }
            public int OrdenesDetalle { get; set; }
            public int FacturasPago { get; set; }
            public int EmailsQueue { get; set; }
            public int DocumentosSubsanacion { get; set; }
            public int Subsanaciones { get; set; }

            public int Total => Ordenes + OrdenesDetalle + FacturasPago + EmailsQueue + DocumentosSubsanacion + Subsanaciones;

            public string Resumen
            {
                get
                {
                    var partes = new List<string>();
                    if (Ordenes > 0) partes.Add($"{Ordenes} orden(es)");
                    if (OrdenesDetalle > 0) partes.Add($"{OrdenesDetalle} detalle(s) de orden");
                    if (FacturasPago > 0) partes.Add($"{FacturasPago} factura(s)/pago(s)");
                    if (EmailsQueue > 0) partes.Add($"{EmailsQueue} correo(s) en cola");
                    if (DocumentosSubsanacion > 0) partes.Add($"{DocumentosSubsanacion} documento(s) de subsanación");
                    if (Subsanaciones > 0) partes.Add($"{Subsanaciones} subsanación(es)");
                    return partes.Count == 0 ? "sin registros" : string.Join(", ", partes);
                }
            }
        }

        private static RelacionesBloqueantesUsuario ObtenerRelacionesBloqueantesUsuario(NpgsqlConnection conn, NpgsqlTransaction tx, int idUsuario)
        {
            var relaciones = new RelacionesBloqueantesUsuario();

            relaciones.Ordenes = ContarSiTablaYColumnaExiste(conn, tx, "aocr_or_orden", "codigo_usuario", idUsuario);
            relaciones.DocumentosSubsanacion = ContarSiTablaYColumnaExiste(conn, tx, "aocr_tbdocumento_subsanacion", "codigo_usuario_carga", idUsuario);

            var tieneTablaSubsanacion = ExisteTabla(conn, tx, "aocr_tbsubsanacion");
            if (tieneTablaSubsanacion)
            {
                var tieneSolicitante = ExisteColumna(conn, tx, "aocr_tbsubsanacion", "codigo_usuario_solicitante");
                var tieneRespuesta = ExisteColumna(conn, tx, "aocr_tbsubsanacion", "codigo_usuario_respuesta");

                if (tieneSolicitante && tieneRespuesta)
                {
                    relaciones.Subsanaciones = conn.ExecuteScalar<int>(
                        @"SELECT COUNT(*)
                          FROM aocr_tbsubsanacion
                          WHERE codigo_usuario_solicitante = @id
                             OR codigo_usuario_respuesta = @id;",
                        new { id = idUsuario },
                        tx);
                }
                else if (tieneSolicitante)
                {
                    relaciones.Subsanaciones = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM aocr_tbsubsanacion WHERE codigo_usuario_solicitante = @id;",
                        new { id = idUsuario },
                        tx);
                }
                else if (tieneRespuesta)
                {
                    relaciones.Subsanaciones = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM aocr_tbsubsanacion WHERE codigo_usuario_respuesta = @id;",
                        new { id = idUsuario },
                        tx);
                }
            }

            return relaciones;
        }

        private static PurgadoDatosUsuarioPruebas PurgarDatosUsuarioParaPruebas(NpgsqlConnection conn, NpgsqlTransaction tx, int idUsuario)
        {
            var purgado = new PurgadoDatosUsuarioPruebas();

            if (ExisteTabla(conn, tx, "aocr_or_orden"))
            {
                if (ExisteTabla(conn, tx, "email_queue") && ExisteColumna(conn, tx, "email_queue", "orden_id"))
                {
                    purgado.EmailsQueue = conn.Execute(
                        @"DELETE FROM email_queue
                          WHERE orden_id IN (SELECT id FROM aocr_or_orden WHERE codigo_usuario = @id);",
                        new { id = idUsuario },
                        tx);
                }

                if (ExisteTabla(conn, tx, "aocr_tb_factura_pago") && ExisteColumna(conn, tx, "aocr_tb_factura_pago", "orden_id"))
                {
                    purgado.FacturasPago = conn.Execute(
                        @"DELETE FROM aocr_tb_factura_pago
                          WHERE orden_id IN (SELECT id FROM aocr_or_orden WHERE codigo_usuario = @id);",
                        new { id = idUsuario },
                        tx);
                }

                if (ExisteTabla(conn, tx, "aocr_or_orden_detalle") && ExisteColumna(conn, tx, "aocr_or_orden_detalle", "orden_id"))
                {
                    purgado.OrdenesDetalle = conn.Execute(
                        @"DELETE FROM aocr_or_orden_detalle
                          WHERE orden_id IN (SELECT id FROM aocr_or_orden WHERE codigo_usuario = @id);",
                        new { id = idUsuario },
                        tx);
                }

                if (ExisteColumna(conn, tx, "aocr_or_orden", "codigo_usuario"))
                {
                    purgado.Ordenes = conn.Execute(
                        "DELETE FROM aocr_or_orden WHERE codigo_usuario = @id;",
                        new { id = idUsuario },
                        tx);
                }
            }

            if (ExisteTabla(conn, tx, "aocr_tbdocumento_subsanacion") && ExisteColumna(conn, tx, "aocr_tbdocumento_subsanacion", "codigo_usuario_carga"))
            {
                purgado.DocumentosSubsanacion += conn.Execute(
                    "DELETE FROM aocr_tbdocumento_subsanacion WHERE codigo_usuario_carga = @id;",
                    new { id = idUsuario },
                    tx);
            }

            if (ExisteTabla(conn, tx, "aocr_tbsubsanacion"))
            {
                // Siempre eliminar documentos vinculados a subsanaciones del usuario antes de borrar las subsanaciones.
                var tieneCodigoSubsanacionEnDocs = ExisteTabla(conn, tx, "aocr_tbdocumento_subsanacion") &&
                                                   ExisteColumna(conn, tx, "aocr_tbdocumento_subsanacion", "codigo_subsanacion");
                var tieneSolicitante = ExisteColumna(conn, tx, "aocr_tbsubsanacion", "codigo_usuario_solicitante");
                var tieneRespuesta = ExisteColumna(conn, tx, "aocr_tbsubsanacion", "codigo_usuario_respuesta");

                if (tieneCodigoSubsanacionEnDocs && (tieneSolicitante || tieneRespuesta))
                {
                    var filtro = tieneSolicitante && tieneRespuesta
                        ? "s.codigo_usuario_solicitante = @id OR s.codigo_usuario_respuesta = @id"
                        : (tieneSolicitante ? "s.codigo_usuario_solicitante = @id" : "s.codigo_usuario_respuesta = @id");

                    purgado.DocumentosSubsanacion += conn.Execute(
                        @"DELETE FROM aocr_tbdocumento_subsanacion d
                          USING aocr_tbsubsanacion s
                          WHERE d.codigo_subsanacion = s.codigo_subsanacion
                            AND (" + filtro + ");",
                        new { id = idUsuario },
                        tx);
                }

                if (tieneSolicitante || tieneRespuesta)
                {
                    string where = tieneSolicitante && tieneRespuesta
                        ? "codigo_usuario_solicitante = @id OR codigo_usuario_respuesta = @id"
                        : (tieneSolicitante ? "codigo_usuario_solicitante = @id" : "codigo_usuario_respuesta = @id");

                    purgado.Subsanaciones = conn.Execute(
                        "DELETE FROM aocr_tbsubsanacion WHERE " + where + ";",
                        new { id = idUsuario },
                        tx);
                }
            }

            return purgado;
        }

        private static int ContarSiTablaYColumnaExiste(NpgsqlConnection conn, NpgsqlTransaction tx, string tableName, string columnName, int idUsuario)
        {
            if (!ExisteTabla(conn, tx, tableName) || !ExisteColumna(conn, tx, tableName, columnName))
            {
                return 0;
            }

            return conn.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @id;",
                new { id = idUsuario },
                tx);
        }

        private static string ConstruirMensajeRelacionesBloqueantes(RelacionesBloqueantesUsuario relaciones)
        {
            var partes = new List<string>();

            if (relaciones.Ordenes > 0)
                partes.Add($"{relaciones.Ordenes} orden(es) de recaudación");

            if (relaciones.DocumentosSubsanacion > 0)
                partes.Add($"{relaciones.DocumentosSubsanacion} documento(s) de subsanación");

            if (relaciones.Subsanaciones > 0)
                partes.Add($"{relaciones.Subsanaciones} registro(s) de subsanación");

            if (partes.Count == 0)
            {
                return "No se puede eliminar porque el usuario tiene información relacionada. Use Inactivar si desea bloquear el acceso.";
            }

            return "No se puede eliminar porque el usuario tiene " + string.Join(", ", partes) + " asociadas. Use Inactivar si desea bloquear el acceso.";
        }

        private static void EliminarRelacionesUsuario(NpgsqlConnection conn, NpgsqlTransaction tx, string tabla, int idUsuario, string codigoUsuario)
        {
            var columnas = conn.Query<string>(
                @"SELECT column_name
                  FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = @tabla;",
                new { tabla },
                tx)
                .Select(c => c.Trim().ToLowerInvariant())
                .ToHashSet();

            if (columnas.Count == 0)
            {
                return;
            }

            if (columnas.Contains("codigousuario"))
            {
                conn.Execute(
                    $"DELETE FROM {tabla} WHERE codigousuario::text = @codigo;",
                    new { codigo = codigoUsuario },
                    tx);
                return;
            }

            if (columnas.Contains("idusuario"))
            {
                conn.Execute(
                    $"DELETE FROM {tabla} WHERE idusuario = @id;",
                    new { id = idUsuario },
                    tx);
                return;
            }

            if (columnas.Contains("usuario_id"))
            {
                conn.Execute(
                    $"DELETE FROM {tabla} WHERE usuario_id = @id;",
                    new { id = idUsuario },
                    tx);
                return;
            }

            if (columnas.Contains("id_usuario"))
            {
                conn.Execute(
                    $"DELETE FROM {tabla} WHERE id_usuario = @id;",
                    new { id = idUsuario },
                    tx);
            }
        }

        private static string ConstruirSelectRuc(IDbConnection conn)
        {
            var expresiones = new List<string>();

            if (ExisteColumna(conn, "usuario", "ruc"))
            {
                expresiones.Add("NULLIF(TRIM(ruc), '')");
            }

            if (ExisteColumna(conn, "usuario", "numeroruc"))
            {
                expresiones.Add("NULLIF(TRIM(numeroruc), '')");
            }

            if (ExisteColumna(conn, "usuario", "cedulaidentificacion"))
            {
                expresiones.Add("NULLIF(TRIM(cedulaidentificacion), '')");
            }

            if (ExisteColumna(conn, "usuario", "identificaciontributaria"))
            {
                expresiones.Add("NULLIF(TRIM(identificaciontributaria), '')");
            }

            if (expresiones.Count == 0)
            {
                return "'' AS Ruc,";
            }

            return "COALESCE(" + string.Join(", ", expresiones) + ", '') AS Ruc,";
        }

        private static bool ExisteColumna(IDbConnection conn, string tableName, string columnName)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @tableName
  AND column_name = @columnName;";

            return conn.ExecuteScalar<int>(sql, new
            {
                tableName = tableName,
                columnName = columnName
            }) > 0;
        }

        private static bool ExisteColumna(IDbConnection conn, IDbTransaction tx, string tableName, string columnName)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @tableName
  AND column_name = @columnName;";

            return conn.ExecuteScalar<int>(sql, new
            {
                tableName = tableName,
                columnName = columnName
            }, tx) > 0;
        }

        private static bool ExisteTabla(IDbConnection conn, IDbTransaction tx, string tableName)
        {
            const string sql = @"
SELECT COUNT(1)
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @tableName;";

            return conn.ExecuteScalar<int>(sql, new { tableName }, tx) > 0;
        }

        private static void SepararNombreCompleto(string nombreCompleto, out string nombres, out string apellidos)
        {
            nombres = string.Empty;
            apellidos = string.Empty;

            var limpio = (nombreCompleto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(limpio))
            {
                return;
            }

            var partes = limpio
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (partes.Count <= 1)
            {
                nombres = limpio;
                return;
            }

            if (partes.Count == 2)
            {
                nombres = partes[0];
                apellidos = partes[1];
                return;
            }

            if (partes.Count == 3)
            {
                nombres = string.Join(" ", partes.Take(2));
                apellidos = partes[2];
                return;
            }

            nombres = string.Join(" ", partes.Take(partes.Count - 2));
            apellidos = string.Join(" ", partes.Skip(partes.Count - 2));
        }

        private static string NormalizarEspacios(string texto)
        {
            return string.Join(" ",
                (texto ?? string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()));
        }
    }
}
