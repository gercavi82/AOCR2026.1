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

                // Ajusta nombres de tabla/columnas si difieren en tu BD
                string sql = @"
                    INSERT INTO aocr_tbaeronave_solicitud
                    (codigo_solicitud, marca, modelo, serie, matricula, configuracion, etapa_ruido,
                     fecha_registro, created_at, created_by)
                    VALUES
                    (@codigo_solicitud, @marca, @modelo, @serie, @matricula, @configuracion, @etapa_ruido,
                     @fecha_registro, NOW(), @created_by)
                    RETURNING codigo_aeronave_solicitud;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", a.CodigoSolicitud);

                    cmd.Parameters.AddWithValue("@marca", (object)a.Marca ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@modelo", (object)a.Modelo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@serie", (object)a.Serie ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@matricula", (object)a.Matricula ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@configuracion", (object)a.Configuracion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@etapa_ruido", (object)a.EtapaRuido ?? DBNull.Value);

                    // si no viene, le ponemos now
                    cmd.Parameters.AddWithValue("@fecha_registro", (object)a.FechaRegistro ?? DateTime.Now);

                    cmd.Parameters.AddWithValue("@created_by", (object)(usuario ?? a.UsuarioRegistro ?? "sistema"));

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
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

                string sql = @"
                    SELECT
                        codigo_aeronave_solicitud,
                        codigosolicitud,
                        marca,
                        modelo,
                        serie,
                        matricula,
                        configuracion,
                        etapa_ruido,
                        fecha_registro,
                        created_by
                    FROM aocr_tbaeronave_solicitud
                    WHERE codigosolicitud = @id
                    ORDER BY codigo_aeronave_solicitud DESC;";

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

                string sql = @"DELETE FROM aocr_tbaeronave_solicitud
                               WHERE codigo_aeronave_solicitud = @id;";

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

                string sql = @"DELETE FROM aocr_tbaeronave_solicitud
                               WHERE codigo_solicitud = @sid;";

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
        public void ReemplazarPorSolicitud(int codigoSolicitud, List<AeronaveSolicitud> aeronaves, string usuario)
        {
            // 1) borrar todas las existentes
            EliminarPorSolicitud(codigoSolicitud);

            // 2) insertar nuevas
            if (aeronaves == null) return;

            foreach (var a in aeronaves)
            {
                if (a == null) continue;
                if (string.IsNullOrWhiteSpace(a.Matricula)) continue;

                a.CodigoSolicitud = codigoSolicitud;
                a.FechaRegistro = a.FechaRegistro ?? DateTime.Now;
                a.UsuarioRegistro = a.UsuarioRegistro ?? usuario ?? "sistema";

                Crear(a, a.UsuarioRegistro);
            }
        }

        // =========================================================
        // MAPEO (según tu modelo)
        // =========================================================
        private AeronaveSolicitud Mapear(IDataRecord rd)
        {
            return new AeronaveSolicitud
            {
                CodigoAeronaveSolicitud = rd["codigo_aeronave_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_aeronave_solicitud"]),
                CodigoSolicitud = rd["codigo_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_solicitud"]),

                Marca = rd["marca"] == DBNull.Value ? null : rd["marca"].ToString(),
                Modelo = rd["modelo"] == DBNull.Value ? null : rd["modelo"].ToString(),
                Serie = rd["serie"] == DBNull.Value ? null : rd["serie"].ToString(),
                Matricula = rd["matricula"] == DBNull.Value ? null : rd["matricula"].ToString(),

                Configuracion = rd["configuracion"] == DBNull.Value ? null : rd["configuracion"].ToString(),
                EtapaRuido = rd["etapa_ruido"] == DBNull.Value ? null : rd["etapa_ruido"].ToString(),

                FechaRegistro = rd["fecha_registro"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_registro"]),
                UsuarioRegistro = rd["created_by"] == DBNull.Value ? null : rd["created_by"].ToString()
            };
        }
    }
}
