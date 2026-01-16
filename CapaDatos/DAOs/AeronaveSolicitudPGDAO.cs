using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    // Lee aeronaves desde Postgres (aocr_tbaeronave_solicitud)
    public class AeronaveSolicitudPGDAO
    {
        public List<AeronaveSolicitud> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<AeronaveSolicitud>();

            using (var con = ConexionDAO.CrearConexion())
            {
                con.Open();

                // Ajusta nombres de columnas SEGUN TU TABLA REAL:
                // aocr_tbaeronave_solicitud: marca, modelo, serie, matricula, configuracion, etapa_ruido, fecha_registro, usuario_registro
                // OJO: peso_maximo / codigo_oaci solo si existen en esa tabla; si no, los dejamos en null.
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
                        usuario_registro
                    FROM public.aocr_tbaeronave_solicitud
                    WHERE codigosolicitud = @codigoSolicitud
                    ORDER BY codigo_aeronave_solicitud;";

                using (var cmd = new NpgsqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            var a = new AeronaveSolicitud();

                            a.CodigoAeronaveSolicitud = dr["codigo_aeronave_solicitud"] != DBNull.Value ? Convert.ToInt32(dr["codigo_aeronave_solicitud"]) : 0;
                            a.CodigoSolicitud = dr["codigosolicitud"] != DBNull.Value ? Convert.ToInt32(dr["codigosolicitud"]) : 0;

                            // ✅ FIX: NO existe Fabricante, usamos Marca
                            a.Marca = dr["marca"] != DBNull.Value ? dr["marca"].ToString() : null;
                            a.Modelo = dr["modelo"] != DBNull.Value ? dr["modelo"].ToString() : null;
                            a.Serie = dr["serie"] != DBNull.Value ? dr["serie"].ToString() : null;
                            a.Matricula = dr["matricula"] != DBNull.Value ? dr["matricula"].ToString() : null;
                            a.Configuracion = dr["configuracion"] != DBNull.Value ? dr["configuracion"].ToString() : null;
                            a.EtapaRuido = dr["etapa_ruido"] != DBNull.Value ? dr["etapa_ruido"].ToString() : null;

                            a.FechaRegistro = dr["fecha_registro"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(dr["fecha_registro"]) : null;
                            a.UsuarioRegistro = dr["usuario_registro"] != DBNull.Value ? dr["usuario_registro"].ToString() : null;

                            // Si tu tabla NO tiene estas columnas, se quedan null (no rompen)
                            a.PesoMaximo = null;
                            a.CodigoOACI = null;

                            lista.Add(a);
                        }
                    }
                }
            }

            return lista;
        }

        // Si también insertas/actualizas en PG, usa Marca (no Fabricante)
        public void Insertar(AeronaveSolicitud a)
        {
            if (a == null) return;

            using (var con = ConexionDAO.CrearConexion())
            {
                con.Open();

                string sql = @"
                    INSERT INTO public.aocr_tbaeronave_solicitud
                    (codigosolicitud, marca, modelo, serie, matricula, configuracion, etapa_ruido, fecha_registro, usuario_registro)
                    VALUES
                    (@codigosolicitud, @marca, @modelo, @serie, @matricula, @configuracion, @etapa_ruido, NOW(), @usuario_registro);";

                using (var cmd = new NpgsqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@codigosolicitud", a.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@marca", (object)(a.Marca ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@modelo", (object)(a.Modelo ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@serie", (object)(a.Serie ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@matricula", (object)(a.Matricula ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@configuracion", (object)(a.Configuracion ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@etapa_ruido", (object)(a.EtapaRuido ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario_registro", (object)(a.UsuarioRegistro ?? "sistema") ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
