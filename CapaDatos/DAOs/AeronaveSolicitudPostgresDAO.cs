using System.Collections.Generic;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class AeronaveSolicitudPostgresDAO
    {
        public List<AeronaveSolicitud> ListarPorCodigoSolicitud(int codigoSolicitud)
        {
            NpgsqlConnection con = null;
            try
            {
                con = ConexionDAO.ObtenerConexion();

                var sql = @"
                    SELECT
                        marca,
                        modelo,
                        serie,
                        matricula,
                        configuracion,
                        etapa_ruido,
                        usuario_registro,
                        fecha_registro
                    FROM public.aocr_tbaeronave_solicitud
                    WHERE codigosolicitud = @codigo
                    ORDER BY codigo_aeronave_solicitud;
                ";

                var lista = new List<AeronaveSolicitud>();

                using (var cmd = new NpgsqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@codigo", codigoSolicitud);

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new AeronaveSolicitud
                            {
                                Marca = dr["marca"] == System.DBNull.Value ? "" : dr["marca"].ToString(),
                                Modelo = dr["modelo"] == System.DBNull.Value ? "" : dr["modelo"].ToString(),
                                Serie = dr["serie"] == System.DBNull.Value ? "" : dr["serie"].ToString(),
                                Matricula = dr["matricula"] == System.DBNull.Value ? "" : dr["matricula"].ToString(),
                                Configuracion = dr["configuracion"] == System.DBNull.Value ? "" : dr["configuracion"].ToString(),
                                EtapaRuido = dr["etapa_ruido"] == System.DBNull.Value ? "" : dr["etapa_ruido"].ToString()
                            });
                        }
                    }
                }

                return lista;
            }
            finally
            {
                ConexionDAO.CerrarConexion(con);
            }
        }
    }
}
