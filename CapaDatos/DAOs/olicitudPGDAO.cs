using System;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class SolicitudPGDAO
    {
        public SolicitudAOCR ObtenerSolicitudPorCodigo(int codigoSolicitud)
        {
            NpgsqlConnection con = null;
            try
            {
                con = ConexionDAO.ObtenerConexion();

                string sql = @"
                    SELECT 
                        codigo_solicitud,
                        numero_solicitud,
                        nombre_operador,
                        ruc,
                        razon_social,
                        email,
                        telefono,
                        direccion,
                        representante_legal,
                        cedula_representante,
                        tipo_operacion,
                        descripcion_operacion,
                        observaciones
                    FROM public.aocr_tbsolicitud
                    WHERE codigo_solicitud = @codigo_solicitud
                    LIMIT 1;
                ";

                using (var cmd = new NpgsqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;

                        var s = new SolicitudAOCR();
                        s.CodigoSolicitud = dr["codigo_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(dr["codigo_solicitud"]);
                        s.NumeroSolicitud = dr["numero_solicitud"]?.ToString();
                        s.NombreOperador = dr["nombre_operador"]?.ToString();
                        s.Ruc = dr["ruc"]?.ToString();
                        s.RazonSocial = dr["razon_social"]?.ToString();
                        s.Email = dr["email"]?.ToString();
                        s.Telefono = dr["telefono"]?.ToString();
                        s.Direccion = dr["direccion"]?.ToString();
                        s.RepresentanteLegal = dr["representante_legal"]?.ToString();
                        s.CedulaRepresentante = dr["cedula_representante"]?.ToString();
                        s.TipoOperacion = dr["tipo_operacion"]?.ToString();
                        s.DescripcionOperacion = dr["descripcion_operacion"]?.ToString();
                        s.Observaciones = dr["observaciones"]?.ToString();

                        return s;
                    }
                }
            }
            finally
            {
                ConexionDAO.CerrarConexion(con);
            }
        }
    }
}
