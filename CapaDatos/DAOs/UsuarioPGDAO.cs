using System;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class UsuarioPGDAO
    {
        public Usuario ObtenerPorId(int idUsuario)
        {
            using (var con = ConexionDAO.CrearConexion())
            {
                con.Open();

                // Según tu lista de columnas: usuario.idusuario, nombreusuario, apellidousuario, correo, numeroruc, cargo, codigorol, etc.
                string sql = @"
                    SELECT
                        idusuario,
                        codigousuario,
                        nombreusuario,
                        apellidousuario,
                        correo,
                        numeroruc,
                        cargo,
                        codigorol
                    FROM public.usuario
                    WHERE idusuario = @id
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@id", idUsuario);

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;

                        var u = new Usuario();

                        // Ajusta estos nombres de propiedades a TU clase Usuario real
                        SetIfExists(u, "IdUsuario", dr["idusuario"] == DBNull.Value ? 0 : Convert.ToInt32(dr["idusuario"]));
                        SetIfExists(u, "CodigoUsuario", dr["codigousuario"] == DBNull.Value ? null : dr["codigousuario"].ToString());
                        SetIfExists(u, "Nombre", dr["nombreusuario"] == DBNull.Value ? null : dr["nombreusuario"].ToString());
                        SetIfExists(u, "Apellido", dr["apellidousuario"] == DBNull.Value ? null : dr["apellidousuario"].ToString());
                        SetIfExists(u, "Email", dr["correo"] == DBNull.Value ? null : dr["correo"].ToString());
                        SetIfExists(u, "NumeroRuc", dr["numeroruc"] == DBNull.Value ? null : dr["numeroruc"].ToString());
                        SetIfExists(u, "Cargo", dr["cargo"] == DBNull.Value ? null : dr["cargo"].ToString());
                        SetIfExists(u, "CodigoRol", dr["codigorol"] == DBNull.Value ? 0 : Convert.ToInt32(dr["codigorol"]));

                        return u;
                    }
                }
            }
        }

        private static void SetIfExists(object obj, string prop, object value)
        {
            var pi = obj.GetType().GetProperty(prop);
            if (pi == null || !pi.CanWrite) return;
            pi.SetValue(obj, value, null);
        }
    }
}
