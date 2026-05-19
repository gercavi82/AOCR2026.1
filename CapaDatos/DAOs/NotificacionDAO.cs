using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO de Notificación AOCR – PostgreSQL (Npgsql)
    /// Tabla sugerida: aocr_tbnotificacion
    /// Columnas esperadas:
    ///   codigonotificacion (PK, serial/int)
    ///   codigousuario      (int)
    ///   titulo             (varchar)
    ///   mensaje            (text)
    ///   tipo               (varchar)
    ///   url                (varchar)
    ///   fechacreacion      (timestamp)
    ///   leida              (boolean)
    /// </summary>
    public class NotificacionDAO
    {
        // ==============================
        // Conexión
        // ==============================
        private static NpgsqlConnection CrearConexion()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en el config.");

            return new NpgsqlConnection(cs);
        }

        // ==============================
        // Mapeo
        // ==============================
        private static Notificacion Map(IDataRecord r)
        {
            var not = new Notificacion();

            // Asumiendo propiedades del modelo:
            // int CodigoNotificacion, int CodigoUsuario, string Titulo, string Mensaje,
            // string Tipo, string Url, DateTime? FechaCreacion, bool Leida

            if (r["codigonotificacion"] != DBNull.Value)
                not.CodigoNotificacion = Convert.ToInt32(r["codigonotificacion"]);

            if (r["codigousuario"] != DBNull.Value)
                not.CodigoUsuario = Convert.ToInt32(r["codigousuario"]);

            not.Titulo = r["titulo"] != DBNull.Value ? r["titulo"].ToString() : null;
            not.Mensaje = r["mensaje"] != DBNull.Value ? r["mensaje"].ToString() : null;
            not.Tipo = r["tipo"] != DBNull.Value ? r["tipo"].ToString() : null;
            not.Url = r["url"] != DBNull.Value ? r["url"].ToString() : null;

            if (r["fechacreacion"] != DBNull.Value)
                not.FechaCreacion = Convert.ToDateTime(r["fechacreacion"]);

            if (r["leida"] != DBNull.Value)
                not.Leida = Convert.ToBoolean(r["leida"]);

            var modulo = GetFieldValue(r, "modulo");
            if (modulo != null && modulo != DBNull.Value)
                not.Modulo = modulo.ToString();

            var entidadId = GetFieldValue(r, "entidad_id");
            if (entidadId != null && entidadId != DBNull.Value)
                not.EntidadId = Convert.ToInt32(entidadId);

            var tipoEntidad = GetFieldValue(r, "tipo_entidad");
            if (tipoEntidad != null && tipoEntidad != DBNull.Value)
                not.TipoEntidad = tipoEntidad.ToString();

            var fechaLectura = GetFieldValue(r, "fecha_lectura");
            if (fechaLectura != null && fechaLectura != DBNull.Value)
                not.FechaLectura = Convert.ToDateTime(fechaLectura);

            return not;
        }

        // ==============================
        // Insertar
        // ==============================
        public static bool Insertar(Notificacion notificacion)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();

                var tieneModulo = ExisteColumna(cn, "aocr_tbnotificacion", "modulo");
                var tieneEntidadId = ExisteColumna(cn, "aocr_tbnotificacion", "entidad_id");
                var tieneTipoEntidad = ExisteColumna(cn, "aocr_tbnotificacion", "tipo_entidad");
                var tieneFechaLectura = ExisteColumna(cn, "aocr_tbnotificacion", "fecha_lectura");

                var columnas = new List<string>
                {
                    "codigousuario", "titulo", "mensaje", "tipo", "url", "fechacreacion", "leida"
                };
                var valores = new List<string>
                {
                    "@user", "@tit", "@msg", "@tipo", "@url", "@fec", "@leida"
                };

                if (tieneModulo)
                {
                    columnas.Add("modulo");
                    valores.Add("@modulo");
                }

                if (tieneEntidadId)
                {
                    columnas.Add("entidad_id");
                    valores.Add("@entidad_id");
                }

                if (tieneTipoEntidad)
                {
                    columnas.Add("tipo_entidad");
                    valores.Add("@tipo_entidad");
                }

                if (tieneFechaLectura)
                {
                    columnas.Add("fecha_lectura");
                    valores.Add("@fecha_lectura");
                }

                var sql = "INSERT INTO aocr_tbnotificacion (" + string.Join(", ", columnas) + ") VALUES (" + string.Join(", ", valores) + ") RETURNING codigonotificacion;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@user", notificacion.CodigoUsuario);
                    cmd.Parameters.AddWithValue("@tit", (object)notificacion.Titulo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@msg", (object)notificacion.Mensaje ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo", (object)notificacion.Tipo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@url", (object)notificacion.Url ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fec", (object)notificacion.FechaCreacion ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@leida", notificacion.Leida);

                    if (tieneModulo)
                    {
                        cmd.Parameters.AddWithValue("@modulo", (object)notificacion.Modulo ?? DBNull.Value);
                    }

                    if (tieneEntidadId)
                    {
                        cmd.Parameters.AddWithValue("@entidad_id", (object)notificacion.EntidadId ?? DBNull.Value);
                    }

                    if (tieneTipoEntidad)
                    {
                        cmd.Parameters.AddWithValue("@tipo_entidad", (object)notificacion.TipoEntidad ?? DBNull.Value);
                    }

                    if (tieneFechaLectura)
                    {
                        cmd.Parameters.AddWithValue("@fecha_lectura", (object)notificacion.FechaLectura ?? DBNull.Value);
                    }

                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                        notificacion.CodigoNotificacion = Convert.ToInt32(obj);

                    return notificacion.CodigoNotificacion > 0;
                }
            }
        }

        // ==============================
        // Marcar como leída
        // ==============================
        public static bool MarcarComoLeida(int codigoNotificacion)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();

                var tieneFechaLectura = ExisteColumna(cn, "aocr_tbnotificacion", "fecha_lectura");
                var sql = tieneFechaLectura
                    ? @"
                        UPDATE aocr_tbnotificacion
                        SET leida = TRUE,
                            fecha_lectura = COALESCE(fecha_lectura, NOW())
                        WHERE codigonotificacion = @id;"
                    : @"
                        UPDATE aocr_tbnotificacion
                        SET leida = TRUE
                        WHERE codigonotificacion = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoNotificacion);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public static bool MarcarComoLeida(int codigoNotificacion, int codigoUsuario)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();

                var tieneFechaLectura = ExisteColumna(cn, "aocr_tbnotificacion", "fecha_lectura");
                var sql = tieneFechaLectura
                    ? @"
                        UPDATE aocr_tbnotificacion
                        SET leida = TRUE,
                            fecha_lectura = COALESCE(fecha_lectura, NOW())
                        WHERE codigonotificacion = @id
                          AND codigousuario = @user;"
                    : @"
                        UPDATE aocr_tbnotificacion
                        SET leida = TRUE
                        WHERE codigonotificacion = @id
                          AND codigousuario = @user;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoNotificacion);
                    cmd.Parameters.AddWithValue("@user", codigoUsuario);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public static bool MarcarTodasComoLeidas(int codigoUsuario)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();

                var tieneFechaLectura = ExisteColumna(cn, "aocr_tbnotificacion", "fecha_lectura");
                var sql = tieneFechaLectura
                    ? @"
                        UPDATE aocr_tbnotificacion
                        SET leida = TRUE,
                            fecha_lectura = COALESCE(fecha_lectura, NOW())
                        WHERE codigousuario = @user
                          AND leida = FALSE;"
                    : @"
                        UPDATE aocr_tbnotificacion
                        SET leida = TRUE
                        WHERE codigousuario = @user
                          AND leida = FALSE;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@user", codigoUsuario);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ==============================
        // Eliminar
        // ==============================
        public static bool Eliminar(int codigoNotificacion)
        {
            const string sql = @"
                DELETE FROM aocr_tbnotificacion
                WHERE codigonotificacion = @id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoNotificacion);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public static bool EliminarTodasLeidas(int codigoUsuario)
        {
            const string sql = @"
                DELETE FROM aocr_tbnotificacion
                WHERE codigousuario = @user
                  AND leida = TRUE;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@user", codigoUsuario);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ==============================
        // Consultas
        // ==============================
        public static List<Notificacion> ObtenerPorUsuario(int codigoUsuario)
        {
            var list = new List<Notificacion>();

            using (var cn = CrearConexion())
            {
                cn.Open();

                var sql = @"
                    SELECT " + ObtenerColumnasConsulta(cn) + @"
                    FROM aocr_tbnotificacion
                    WHERE codigousuario = @user
                    ORDER BY fechacreacion DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@user", codigoUsuario);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(Map(rd));
                    }
                }
            }

            return list;
        }

        public static List<Notificacion> ObtenerNoLeidas(int codigoUsuario)
        {
            var list = new List<Notificacion>();

            using (var cn = CrearConexion())
            {
                cn.Open();

                var sql = @"
                    SELECT " + ObtenerColumnasConsulta(cn) + @"
                    FROM aocr_tbnotificacion
                    WHERE codigousuario = @user
                      AND leida = FALSE
                    ORDER BY fechacreacion DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@user", codigoUsuario);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(Map(rd));
                    }
                }
            }

            return list;
        }

        public static int ContarNoLeidas(int codigoUsuario)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM aocr_tbnotificacion
                WHERE codigousuario = @user
                  AND leida = FALSE;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@user", codigoUsuario);

                cn.Open();
                var obj = cmd.ExecuteScalar();
                return (obj != null && obj != DBNull.Value) ? Convert.ToInt32(obj) : 0;
            }
        }

        // ==============================
        // 🔹 FALTABA: ObtenerPorTipo
        // ==============================
        public static List<Notificacion> ObtenerPorTipo(int codigoUsuario, string tipo)
        {
            var list = new List<Notificacion>();

            using (var cn = CrearConexion())
            {
                cn.Open();

                var sql = @"
                    SELECT " + ObtenerColumnasConsulta(cn) + @"
                    FROM aocr_tbnotificacion
                    WHERE codigousuario = @user
                      AND UPPER(tipo) = UPPER(@tipo)
                    ORDER BY fechacreacion DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@user", codigoUsuario);
                    cmd.Parameters.AddWithValue("@tipo", (object)tipo ?? DBNull.Value);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(Map(rd));
                    }
                }
            }

            return list;
        }

        // ==============================
        // 🔹 FALTABA: ObtenerRecientes
        // ==============================
        public static List<Notificacion> ObtenerRecientes(int codigoUsuario, int cantidad)
        {
            var list = new List<Notificacion>();

            using (var cn = CrearConexion())
            {
                cn.Open();

                var sql = @"
                    SELECT " + ObtenerColumnasConsulta(cn) + @"
                    FROM aocr_tbnotificacion
                    WHERE codigousuario = @user
                    ORDER BY fechacreacion DESC
                    LIMIT @cant;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@user", codigoUsuario);
                    cmd.Parameters.AddWithValue("@cant", cantidad);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(Map(rd));
                    }
                }
            }

            return list;
        }

        // ==============================
        // Mantenimiento: limpiar antiguas
        // ==============================
        public static bool LimpiarNotificacionesAntiguas(int diasAntiguedad)
        {
            DateTime fechaLimite = DateTime.Now.AddDays(-diasAntiguedad);

            const string sql = @"
                DELETE FROM aocr_tbnotificacion
                WHERE fechacreacion < @fechaLimite;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@fechaLimite", fechaLimite);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private static object GetFieldValue(IDataRecord r, string field)
        {
            try
            {
                return r[field];
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private static bool ExisteColumna(NpgsqlConnection cn, string tabla, string columna)
        {
            const string sql = @"
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tabla
                  AND column_name = @columna
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla.Replace("public.", string.Empty));
                cmd.Parameters.AddWithValue("@columna", columna);
                return cmd.ExecuteScalar() != null;
            }
        }

        private static string ObtenerColumnasConsulta(NpgsqlConnection cn)
        {
            var columnas = new List<string>
            {
                "codigonotificacion",
                "codigousuario",
                "titulo",
                "mensaje",
                "tipo",
                "url",
                "fechacreacion",
                "leida"
            };

            if (ExisteColumna(cn, "aocr_tbnotificacion", "modulo"))
            {
                columnas.Add("modulo");
            }

            if (ExisteColumna(cn, "aocr_tbnotificacion", "entidad_id"))
            {
                columnas.Add("entidad_id");
            }

            if (ExisteColumna(cn, "aocr_tbnotificacion", "tipo_entidad"))
            {
                columnas.Add("tipo_entidad");
            }

            if (ExisteColumna(cn, "aocr_tbnotificacion", "fecha_lectura"))
            {
                columnas.Add("fecha_lectura");
            }

            return string.Join(", ", columnas);
        }
    }
}
