using System;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class ContactoEcuadorPGDAO
    {
        public ContactoEcuadorPG ObtenerPorSolicitud(int codigoSolicitud)
        {
            const string sql = @"
                SELECT codigo_solicitud,
                       nombre_representante,
                       ruc_representante,
                       direccion,
                       telefono,
                       correo
                FROM aocr_contacto_ecuador
                WHERE codigo_solicitud = @codigo
                LIMIT 1;
            ";

            using (var cn = ConexionDAO.CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo", codigoSolicitud);
                cn.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return null;

                    return new ContactoEcuadorPG
                    {
                        CodigoSolicitud = dr.GetInt32(dr.GetOrdinal("codigo_solicitud")),
                        NombreRepresentante = dr["nombre_representante"] as string,
                        RucRepresentante = dr["ruc_representante"] as string,
                        Direccion = dr["direccion"] as string,
                        Telefono = dr["telefono"] as string,
                        Correo = dr["correo"] as string
                    };
                }
            }
        }

        public void GuardarOActualizar(ContactoEcuadorPG c)
        {
            const string sql = @"
                INSERT INTO aocr_contacto_ecuador
                    (codigo_solicitud, nombre_representante, ruc_representante, direccion, telefono, correo)
                VALUES
                    (@codigo, @nombre, @ruc, @direccion, @telefono, @correo)
                ON CONFLICT (codigo_solicitud)
                DO UPDATE SET
                    nombre_representante = EXCLUDED.nombre_representante,
                    ruc_representante = EXCLUDED.ruc_representante,
                    direccion = EXCLUDED.direccion,
                    telefono = EXCLUDED.telefono,
                    correo = EXCLUDED.correo;
            ";

            using (var cn = ConexionDAO.CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo", c.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@nombre", (object)(c.NombreRepresentante ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruc", (object)(c.RucRepresentante ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@direccion", (object)(c.Direccion ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@telefono", (object)(c.Telefono ?? "") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@correo", (object)(c.Correo ?? "") ?? DBNull.Value);

                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
