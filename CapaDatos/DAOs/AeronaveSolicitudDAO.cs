using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class AeronaveSolicitudDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        // =========================================================
        // CREAR (1 aeronave)
        // =========================================================
        public int Crear(AeronaveSolicitud a, string usuario = "sistema")
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.CodigoSolicitud <= 0) throw new Exception("Código de solicitud inválido.");
            if (string.IsNullOrWhiteSpace(a.Matricula)) throw new Exception("La matrícula es obligatoria.");

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var columnaSolicitud = ResolverColumnaCodigoSolicitud(cn);
                var columnaCodigoAeronave = ResolverColumnaCodigoAeronave(cn);
                var columnaFechaRegistro = ResolverColumnaFechaRegistro(cn);
                var columnaUsuarioRegistro = ResolverColumnaUsuarioRegistro(cn);
                var tieneCreatedAt = ExisteColumna(cn, "aocr_tbaeronave_solicitud", "created_at");

                var columnas = new List<string>
                {
                    columnaSolicitud,
                    "marca",
                    "modelo",
                    "serie",
                    "matricula",
                    "configuracion",
                    "etapa_ruido"
                };

                var valores = new List<string>
                {
                    "@codigo_solicitud",
                    "@marca",
                    "@modelo",
                    "@serie",
                    "@matricula",
                    "@configuracion",
                    "@etapa_ruido"
                };

                if (!string.IsNullOrWhiteSpace(columnaFechaRegistro))
                {
                    columnas.Add(columnaFechaRegistro);
                    valores.Add("@fecha_registro");
                }

                if (tieneCreatedAt)
                {
                    columnas.Add("created_at");
                    valores.Add("NOW()");
                }

                if (!string.IsNullOrWhiteSpace(columnaUsuarioRegistro))
                {
                    columnas.Add(columnaUsuarioRegistro);
                    valores.Add("@usuario_registro");
                }

                string sql = $@"
                    INSERT INTO aocr_tbaeronave_solicitud
                    ({string.Join(", ", columnas)})
                    VALUES
                    ({string.Join(", ", valores)})
                    RETURNING {columnaCodigoAeronave};";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", a.CodigoSolicitud);

                    cmd.Parameters.AddWithValue("@marca", (object)a.Marca ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@modelo", (object)a.Modelo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@serie", (object)a.Serie ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@matricula", (object)a.Matricula ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@configuracion", (object)a.Configuracion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@etapa_ruido", (object)a.EtapaRuido ?? DBNull.Value);

                    if (!string.IsNullOrWhiteSpace(columnaFechaRegistro))
                    {
                        cmd.Parameters.AddWithValue("@fecha_registro", (object)a.FechaRegistro ?? DateTime.Now);
                    }

                    if (!string.IsNullOrWhiteSpace(columnaUsuarioRegistro))
                    {
                        cmd.Parameters.AddWithValue("@usuario_registro", (object)(usuario ?? a.UsuarioRegistro ?? "sistema"));
                    }

                    try
                    {
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    catch (PostgresException ex) when (ex.SqlState == "42703" &&
                                                       string.Equals(ex.ColumnName, "created_at", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fallback defensivo: algunos entornos no tienen created_at pese a metadata inconsistente.
                        return CrearSinCreatedAt(cn, a, usuario, columnaSolicitud, columnaCodigoAeronave, columnaFechaRegistro, columnaUsuarioRegistro);
                    }
                }
            }
        }

        private static int CrearSinCreatedAt(
            NpgsqlConnection cn,
            AeronaveSolicitud a,
            string usuario,
            string columnaSolicitud,
            string columnaCodigoAeronave,
            string columnaFechaRegistro,
            string columnaUsuarioRegistro)
        {
            var columnas = new List<string>
            {
                columnaSolicitud,
                "marca",
                "modelo",
                "serie",
                "matricula",
                "configuracion",
                "etapa_ruido"
            };

            var valores = new List<string>
            {
                "@codigo_solicitud",
                "@marca",
                "@modelo",
                "@serie",
                "@matricula",
                "@configuracion",
                "@etapa_ruido"
            };

            if (!string.IsNullOrWhiteSpace(columnaFechaRegistro))
            {
                columnas.Add(columnaFechaRegistro);
                valores.Add("@fecha_registro");
            }

            if (!string.IsNullOrWhiteSpace(columnaUsuarioRegistro))
            {
                columnas.Add(columnaUsuarioRegistro);
                valores.Add("@usuario_registro");
            }

            var sql = $@"
                INSERT INTO aocr_tbaeronave_solicitud
                ({string.Join(", ", columnas)})
                VALUES
                ({string.Join(", ", valores)})
                RETURNING {columnaCodigoAeronave};";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", a.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@marca", (object)a.Marca ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modelo", (object)a.Modelo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@serie", (object)a.Serie ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@matricula", (object)a.Matricula ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@configuracion", (object)a.Configuracion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@etapa_ruido", (object)a.EtapaRuido ?? DBNull.Value);

                if (!string.IsNullOrWhiteSpace(columnaFechaRegistro))
                {
                    cmd.Parameters.AddWithValue("@fecha_registro", (object)a.FechaRegistro ?? DateTime.Now);
                }

                if (!string.IsNullOrWhiteSpace(columnaUsuarioRegistro))
                {
                    cmd.Parameters.AddWithValue("@usuario_registro", (object)(usuario ?? a.UsuarioRegistro ?? "sistema"));
                }

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // =========================================================
        // OBTENER POR SOLICITUD
        // =========================================================
        public List<AeronaveSolicitud> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<AeronaveSolicitud>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var columnaSolicitud = ResolverColumnaCodigoSolicitud(cn);
                var columnaCodigoAeronave = ResolverColumnaCodigoAeronave(cn);
                var columnaFechaRegistro = ResolverColumnaFechaRegistro(cn);
                var columnaUsuarioRegistro = ResolverColumnaUsuarioRegistro(cn);

                string sql = $@"
                    SELECT
                        {columnaCodigoAeronave} AS codigo_aeronave_solicitud,
                        {columnaSolicitud} AS codigosolicitud,
                        marca,
                        modelo,
                        serie,
                        matricula,
                        configuracion,
                        etapa_ruido,
                        {(string.IsNullOrWhiteSpace(columnaFechaRegistro) ? "NULL::timestamp" : columnaFechaRegistro)} AS fecha_registro,
                        {(string.IsNullOrWhiteSpace(columnaUsuarioRegistro) ? "NULL::text" : columnaUsuarioRegistro)} AS usuario_registro
                    FROM aocr_tbaeronave_solicitud
                    WHERE {columnaSolicitud} = @id
                    ORDER BY {columnaCodigoAeronave} DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoSolicitud);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            lista.Add(Mapear(rd));
                    }
                }
            }

            return lista;
        }

        // =========================================================
        // ELIMINAR (por id aeronave) - físico
        // =========================================================
        public bool Eliminar(int codigoAeronaveSolicitud)
        {
            if (codigoAeronaveSolicitud <= 0) return false;

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var columnaCodigoAeronave = ResolverColumnaCodigoAeronave(cn);

                string sql = $@"DELETE FROM aocr_tbaeronave_solicitud
                               WHERE {columnaCodigoAeronave} = @id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", codigoAeronaveSolicitud);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // =========================================================
        // ELIMINAR POR SOLICITUD - físico
        // =========================================================
        public bool EliminarPorSolicitud(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0) return false;

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                var columnaSolicitud = ResolverColumnaCodigoSolicitud(cn);

                string sql = $@"DELETE FROM aocr_tbaeronave_solicitud
                               WHERE {columnaSolicitud} = @sid;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@sid", codigoSolicitud);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // =========================================================
        // REEMPLAZAR POR SOLICITUD (lo que usa tu SolicitudAOCRController)
        // =========================================================
        /// <summary>
        /// Reemplaza la flota de la solicitud (borrar + insertar).
        /// Devuelve el número de aeronaves insertadas para que el llamador
        /// pueda confirmar la persistencia real antes de reportar éxito.
        /// </summary>
        public int ReemplazarPorSolicitud(int codigoSolicitud, List<AeronaveSolicitud> aeronaves, string usuario)
        {
            // 1) borrar todas las existentes
            EliminarPorSolicitud(codigoSolicitud);

            // 2) insertar nuevas
            if (aeronaves == null) return 0;

            var insertadas = 0;
            foreach (var a in aeronaves)
            {
                if (a == null) continue;
                if (string.IsNullOrWhiteSpace(a.Matricula)) continue;

                a.CodigoSolicitud = codigoSolicitud;
                a.FechaRegistro = a.FechaRegistro ?? DateTime.Now;
                a.UsuarioRegistro = a.UsuarioRegistro ?? usuario ?? "sistema";

                if (Crear(a, a.UsuarioRegistro) > 0)
                {
                    insertadas++;
                }
            }

            return insertadas;
        }

        // =========================================================
        // MAPEO (según tu modelo)
        // =========================================================
        private AeronaveSolicitud Mapear(IDataRecord rd)
        {
            return new AeronaveSolicitud
            {
                CodigoAeronaveSolicitud = rd["codigo_aeronave_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_aeronave_solicitud"]),
                CodigoSolicitud = rd["codigosolicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigosolicitud"]),

                Marca = rd["marca"] == DBNull.Value ? null : rd["marca"].ToString(),
                Modelo = rd["modelo"] == DBNull.Value ? null : rd["modelo"].ToString(),
                Serie = rd["serie"] == DBNull.Value ? null : rd["serie"].ToString(),
                Matricula = rd["matricula"] == DBNull.Value ? null : rd["matricula"].ToString(),

                Configuracion = rd["configuracion"] == DBNull.Value ? null : rd["configuracion"].ToString(),
                EtapaRuido = rd["etapa_ruido"] == DBNull.Value ? null : rd["etapa_ruido"].ToString(),

                FechaRegistro = rd["fecha_registro"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_registro"]),
                UsuarioRegistro = rd["usuario_registro"] == DBNull.Value ? null : rd["usuario_registro"].ToString()
            };
        }

        private static string ResolverColumnaCodigoSolicitud(NpgsqlConnection cn)
        {
            if (ExisteColumna(cn, "aocr_tbaeronave_solicitud", "codigosolicitud"))
            {
                return "codigosolicitud";
            }

            return "codigo_solicitud";
        }

        private static string ResolverColumnaCodigoAeronave(NpgsqlConnection cn)
        {
            if (ExisteColumna(cn, "aocr_tbaeronave_solicitud", "codigo_aeronave_solicitud"))
            {
                return "codigo_aeronave_solicitud";
            }

            if (ExisteColumna(cn, "aocr_tbaeronave_solicitud", "codigoaeronavesolicitud"))
            {
                return "codigoaeronavesolicitud";
            }

            return "codigo_aeronave_solicitud";
        }

        private static string ResolverColumnaFechaRegistro(NpgsqlConnection cn)
        {
            if (ExisteColumna(cn, "aocr_tbaeronave_solicitud", "fecha_registro"))
            {
                return "fecha_registro";
            }

            if (ExisteColumna(cn, "aocr_tbaeronave_solicitud", "fecharegistro"))
            {
                return "fecharegistro";
            }

            return string.Empty;
        }

        private static string ResolverColumnaUsuarioRegistro(NpgsqlConnection cn)
        {
            if (ExisteColumna(cn, "aocr_tbaeronave_solicitud", "created_by"))
            {
                return "created_by";
            }

            if (ExisteColumna(cn, "aocr_tbaeronave_solicitud", "usuario_registro"))
            {
                return "usuario_registro";
            }

            return string.Empty;
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
    }
}
