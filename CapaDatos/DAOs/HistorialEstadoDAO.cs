using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO de Historial de Estados - AOCR
    /// Soporta tablas/columnas legacy y canónicas en PostgreSQL.
    /// </summary>
    public class HistorialEstadoDAO
    {
        private const string TablaCanonica = "aocr_tbhistorialestado";
        private const string TablaLegacy = "aocr_tbhistorial_estado";

        private const string SqlStateTablaNoExiste = "42P01";
        private const string SqlStateColumnaNoExiste = "42703";

        private sealed class HistorialSchema
        {
            public string Tabla;
            public string CodigoHistorial;
            public string CodigoSolicitud;
            public string EstadoAnterior;
            public string EstadoNuevo;
            public string CodigoUsuario;
            public string Observaciones;
            public string FechaCambio;
        }

        // =========================================================
        // Conexión
        // =========================================================
        private NpgsqlConnection CrearConexion()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en el config.");
            }

            return new NpgsqlConnection(cs);
        }

        // =========================================================
        // Mapeo
        // =========================================================
        private static HistorialEstado Map(IDataRecord r)
        {
            return new HistorialEstado
            {
                CodigoHistorial = r["codigohistorial"] != DBNull.Value ? Convert.ToInt32(r["codigohistorial"]) : 0,
                CodigoSolicitud = r["codigosolicitud"] != DBNull.Value ? Convert.ToInt32(r["codigosolicitud"]) : 0,
                EstadoAnterior = r["estadoanterior"] != DBNull.Value ? r["estadoanterior"].ToString() : null,
                EstadoNuevo = r["estadonuevo"] != DBNull.Value ? r["estadonuevo"].ToString() : null,
                CodigoUsuario = r["codigousuario"] != DBNull.Value ? Convert.ToInt32(r["codigousuario"]) : 0,
                Observaciones = r["observaciones"] != DBNull.Value ? r["observaciones"].ToString() : null,
                FechaCambio = r["fechacambio"] != DBNull.Value ? Convert.ToDateTime(r["fechacambio"]) : DateTime.Now
            };
        }

        private static bool EsErrorEstructuraHistorial(PostgresException ex)
        {
            return ex != null &&
                   (string.Equals(ex.SqlState, SqlStateTablaNoExiste, StringComparison.Ordinal) ||
                    string.Equals(ex.SqlState, SqlStateColumnaNoExiste, StringComparison.Ordinal));
        }

        private static void LogEstructuraHistorialInvalida(string operacion, string tabla, PostgresException ex)
        {
            Debug.WriteLine(
                $"[HistorialEstadoDAO] Estructura de historial no disponible/inválida ({tabla}). " +
                $"Operación omitida: {operacion}. SQLSTATE={ex.SqlState}");
        }

        // =========================================================
        // Resolución dinámica de esquema
        // =========================================================
        private static bool ExisteTabla(NpgsqlConnection cn, string tabla)
        {
            const string sql = "SELECT to_regclass(@tabla) IS NOT NULL;";
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value && Convert.ToBoolean(result);
            }
        }

        private static bool ExisteColumna(NpgsqlConnection cn, string tabla, string columna)
        {
            const string sql = @"
                SELECT 1
                FROM pg_attribute a
                WHERE a.attrelid = to_regclass(@tabla)
                  AND a.attname = @columna
                  AND a.attnum > 0
                  AND NOT a.attisdropped
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                cmd.Parameters.AddWithValue("@columna", columna);
                return cmd.ExecuteScalar() != null;
            }
        }

        private static string ResolverColumna(NpgsqlConnection cn, string tabla, string canonica, string legacy)
        {
            if (ExisteColumna(cn, tabla, canonica))
            {
                return canonica;
            }

            if (ExisteColumna(cn, tabla, legacy))
            {
                return legacy;
            }

            return canonica;
        }

        private static HistorialSchema ResolverSchema(NpgsqlConnection cn)
        {
            var tabla = ExisteTabla(cn, TablaCanonica)
                ? TablaCanonica
                : (ExisteTabla(cn, TablaLegacy) ? TablaLegacy : TablaCanonica);

            return new HistorialSchema
            {
                Tabla = tabla,
                CodigoHistorial = ResolverColumna(cn, tabla, "codigohistorial", "codigo_historial"),
                CodigoSolicitud = ResolverColumna(cn, tabla, "codigosolicitud", "codigo_solicitud"),
                EstadoAnterior = ResolverColumna(cn, tabla, "estadoanterior", "estado_anterior"),
                EstadoNuevo = ResolverColumna(cn, tabla, "estadonuevo", "estado_nuevo"),
                CodigoUsuario = ResolverColumna(cn, tabla, "codigousuario", "codigo_usuario"),
                Observaciones = "observaciones",
                FechaCambio = ResolverColumna(cn, tabla, "fechacambio", "fecha_cambio")
            };
        }

        // =========================================================
        // 1) Obtener todo el historial por solicitud
        // =========================================================
        public List<HistorialEstado> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var list = new List<HistorialEstado>();

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    var schema = ResolverSchema(cn);

                    var sql = $@"
                        SELECT
                            {schema.CodigoHistorial} AS codigohistorial,
                            {schema.CodigoSolicitud} AS codigosolicitud,
                            {schema.EstadoAnterior} AS estadoanterior,
                            {schema.EstadoNuevo} AS estadonuevo,
                            {schema.CodigoUsuario} AS codigousuario,
                            {schema.Observaciones} AS observaciones,
                            {schema.FechaCambio} AS fechacambio
                        FROM {schema.Tabla}
                        WHERE {schema.CodigoSolicitud} = @id
                        ORDER BY {schema.FechaCambio} DESC;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@id", codigoSolicitud);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                list.Add(Map(rd));
                            }
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsErrorEstructuraHistorial(ex))
            {
                LogEstructuraHistorialInvalida(nameof(ObtenerPorSolicitud), $"{TablaCanonica}/{TablaLegacy}", ex);
            }

            return list;
        }

        // =========================================================
        // 2) Obtener el último cambio de una solicitud
        // =========================================================
        public HistorialEstado ObtenerUltimoCambio(int codigoSolicitud)
        {
            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    var schema = ResolverSchema(cn);

                    var sql = $@"
                        SELECT
                            {schema.CodigoHistorial} AS codigohistorial,
                            {schema.CodigoSolicitud} AS codigosolicitud,
                            {schema.EstadoAnterior} AS estadoanterior,
                            {schema.EstadoNuevo} AS estadonuevo,
                            {schema.CodigoUsuario} AS codigousuario,
                            {schema.Observaciones} AS observaciones,
                            {schema.FechaCambio} AS fechacambio
                        FROM {schema.Tabla}
                        WHERE {schema.CodigoSolicitud} = @id
                        ORDER BY {schema.FechaCambio} DESC
                        LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@id", codigoSolicitud);

                        using (var rd = cmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                return Map(rd);
                            }
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsErrorEstructuraHistorial(ex))
            {
                LogEstructuraHistorialInvalida(nameof(ObtenerUltimoCambio), $"{TablaCanonica}/{TablaLegacy}", ex);
            }

            return null;
        }

        // =========================================================
        // 3) Obtener por estado nuevo
        // =========================================================
        public List<HistorialEstado> ObtenerPorEstado(string estadoNuevo)
        {
            var list = new List<HistorialEstado>();

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    var schema = ResolverSchema(cn);

                    var sql = $@"
                        SELECT
                            {schema.CodigoHistorial} AS codigohistorial,
                            {schema.CodigoSolicitud} AS codigosolicitud,
                            {schema.EstadoAnterior} AS estadoanterior,
                            {schema.EstadoNuevo} AS estadonuevo,
                            {schema.CodigoUsuario} AS codigousuario,
                            {schema.Observaciones} AS observaciones,
                            {schema.FechaCambio} AS fechacambio
                        FROM {schema.Tabla}
                        WHERE {schema.EstadoNuevo} = @estado
                        ORDER BY {schema.FechaCambio} DESC;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@estado", (object)estadoNuevo ?? DBNull.Value);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                list.Add(Map(rd));
                            }
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsErrorEstructuraHistorial(ex))
            {
                LogEstructuraHistorialInvalida(nameof(ObtenerPorEstado), $"{TablaCanonica}/{TablaLegacy}", ex);
            }

            return list;
        }

        // =========================================================
        // 4) Obtener por usuario que realizó el cambio
        // =========================================================
        public List<HistorialEstado> ObtenerPorUsuario(int codigoUsuario)
        {
            var list = new List<HistorialEstado>();

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    var schema = ResolverSchema(cn);

                    var sql = $@"
                        SELECT
                            {schema.CodigoHistorial} AS codigohistorial,
                            {schema.CodigoSolicitud} AS codigosolicitud,
                            {schema.EstadoAnterior} AS estadoanterior,
                            {schema.EstadoNuevo} AS estadonuevo,
                            {schema.CodigoUsuario} AS codigousuario,
                            {schema.Observaciones} AS observaciones,
                            {schema.FechaCambio} AS fechacambio
                        FROM {schema.Tabla}
                        WHERE {schema.CodigoUsuario} = @user
                        ORDER BY {schema.FechaCambio} DESC;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@user", codigoUsuario);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                list.Add(Map(rd));
                            }
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsErrorEstructuraHistorial(ex))
            {
                LogEstructuraHistorialInvalida(nameof(ObtenerPorUsuario), $"{TablaCanonica}/{TablaLegacy}", ex);
            }

            return list;
        }

        // =========================================================
        // 5) Obtener por rango de fechas
        // =========================================================
        public List<HistorialEstado> ObtenerPorFecha(DateTime desde, DateTime hasta)
        {
            var list = new List<HistorialEstado>();

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    var schema = ResolverSchema(cn);

                    var sql = $@"
                        SELECT
                            {schema.CodigoHistorial} AS codigohistorial,
                            {schema.CodigoSolicitud} AS codigosolicitud,
                            {schema.EstadoAnterior} AS estadoanterior,
                            {schema.EstadoNuevo} AS estadonuevo,
                            {schema.CodigoUsuario} AS codigousuario,
                            {schema.Observaciones} AS observaciones,
                            {schema.FechaCambio} AS fechacambio
                        FROM {schema.Tabla}
                        WHERE {schema.FechaCambio} >= @desde
                          AND {schema.FechaCambio} <= @hasta
                        ORDER BY {schema.FechaCambio} DESC;";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@desde", desde);
                        cmd.Parameters.AddWithValue("@hasta", hasta);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                list.Add(Map(rd));
                            }
                        }
                    }
                }
            }
            catch (PostgresException ex) when (EsErrorEstructuraHistorial(ex))
            {
                LogEstructuraHistorialInvalida(nameof(ObtenerPorFecha), $"{TablaCanonica}/{TablaLegacy}", ex);
            }

            return list;
        }

        public List<HistorialEstado> ObtenerPorFecha(DateTime fecha)
        {
            var desde = fecha.Date;
            var hasta = fecha.Date.AddDays(1).AddSeconds(-1);
            return ObtenerPorFecha(desde, hasta);
        }

        // =========================================================
        // 6) Registrar un cambio de estado
        // =========================================================
        public bool RegistrarCambio(
            int codigoSolicitud,
            string estadoAnterior,
            string estadoNuevo,
            int codigoUsuario,
            string observaciones)
        {
            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    var schema = ResolverSchema(cn);

                    var sql = $@"
                        INSERT INTO {schema.Tabla}
                        ({schema.CodigoSolicitud}, {schema.EstadoAnterior}, {schema.EstadoNuevo}, {schema.CodigoUsuario}, {schema.Observaciones}, {schema.FechaCambio})
                        VALUES
                        (@sol, @ant, @nuevo, @user, @obs, @fecha);";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@sol", codigoSolicitud);
                        cmd.Parameters.AddWithValue("@ant", (object)estadoAnterior ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@nuevo", (object)estadoNuevo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@user", codigoUsuario);
                        cmd.Parameters.AddWithValue("@obs", (object)observaciones ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fecha", DateTime.Now);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (PostgresException ex) when (EsErrorEstructuraHistorial(ex))
            {
                LogEstructuraHistorialInvalida(nameof(RegistrarCambio), $"{TablaCanonica}/{TablaLegacy}", ex);
                return false;
            }
        }

        // =========================================================
        // 7) Insertar (modelo completo)
        // =========================================================
        public bool Insertar(HistorialEstado modelo)
        {
            if (modelo == null)
            {
                return false;
            }

            try
            {
                using (var cn = CrearConexion())
                {
                    cn.Open();
                    var schema = ResolverSchema(cn);

                    var sql = $@"
                        INSERT INTO {schema.Tabla}
                        ({schema.CodigoSolicitud}, {schema.EstadoAnterior}, {schema.EstadoNuevo}, {schema.CodigoUsuario}, {schema.Observaciones}, {schema.FechaCambio})
                        VALUES
                        (@sol, @ant, @nuevo, @user, @obs, @fecha);";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@sol", modelo.CodigoSolicitud);
                        cmd.Parameters.AddWithValue("@ant", (object)modelo.EstadoAnterior ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@nuevo", (object)modelo.EstadoNuevo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@user", modelo.CodigoUsuario);
                        cmd.Parameters.AddWithValue("@obs", (object)modelo.Observaciones ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fecha", modelo.FechaCambio == DateTime.MinValue ? DateTime.Now : modelo.FechaCambio);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (PostgresException ex) when (EsErrorEstructuraHistorial(ex))
            {
                LogEstructuraHistorialInvalida(nameof(Insertar), $"{TablaCanonica}/{TablaLegacy}", ex);
                return false;
            }
        }
    }
}
