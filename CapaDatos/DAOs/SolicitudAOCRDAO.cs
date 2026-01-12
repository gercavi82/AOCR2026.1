using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class SolicitudAOCRDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        // ============================
        // LISTADOS
        // ============================
        public List<SolicitudAOCR> ListarActivas()
        {
            return ObtenerPorFiltro("deleted_at IS NULL");
        }

        public List<SolicitudAOCR> ObtenerTodos()
        {
            return ObtenerPorFiltro("1=1");
        }

        public List<SolicitudAOCR> ObtenerPorUsuario(int codigoUsuario)
        {
            return ObtenerPorFiltro(
                "codigo_usuario = @u",
                cmd => cmd.Parameters.AddWithValue("@u", codigoUsuario)
            );
        }

        public List<SolicitudAOCR> ObtenerPorEstado(string estado)
        {
            return ObtenerPorFiltro(
                "estado = @e",
                cmd => cmd.Parameters.AddWithValue("@e", estado)
            );
        }

        public List<SolicitudAOCR> ObtenerPendientesRevision()
        {
            return ObtenerPorEstado("ENVIADO_A_INSPECTOR");
        }

        public List<SolicitudAOCR> ObtenerParaValidacionJefatura()
        {
            return ObtenerPorFiltro(
                "estado = @e AND deleted_at IS NULL",
                cmd => cmd.Parameters.AddWithValue("@e", "ENVIADO_A_JEFATURA")
            );
        }

        // ============================
        // OBTENER INDIVIDUAL
        // ============================
        public SolicitudAOCR ObtenerPorId(int id)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"SELECT * FROM aocr_tbsolicitud
                               WHERE codigo_solicitud = @id AND deleted_at IS NULL";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Mapear(rd) : null;
                    }
                }
            }
        }

        public SolicitudAOCR ObtenerPorCodigo(string codigo)
        {
            int id;
            if (!int.TryParse(codigo, out id)) return null;
            return ObtenerPorId(id);
        }

        // ============================
        // INSERTAR
        // ============================
        public int InsertarConReturn(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    INSERT INTO aocr_tbsolicitud
                    (nombre_operador, fecha_solicitud, estado, codigo_usuario)
                    VALUES (@n, @f, @e, @u)
                    RETURNING codigo_solicitud";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@n", s.NombreOperador);
                    cmd.Parameters.AddWithValue("@f", s.FechaSolicitud);
                    cmd.Parameters.AddWithValue("@e", s.Estado);
                    cmd.Parameters.AddWithValue("@u", s.CodigoUsuario);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ============================
        // ACTUALIZAR
        // ============================
        public bool ActualizarGeneral(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    UPDATE aocr_tbsolicitud
                    SET nombre_operador = @n,
                        updated_at = NOW(),
                        updated_by = @u
                    WHERE codigo_solicitud = @id";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@n", s.NombreOperador);
                    cmd.Parameters.AddWithValue("@u", s.UpdatedBy ?? "0");
                    cmd.Parameters.AddWithValue("@id", s.CodigoSolicitud);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool Actualizar(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    UPDATE aocr_tbsolicitud
                    SET estado = @e,
                        observaciones = @o,
                        updated_at = NOW(),
                        updated_by = @u
                    WHERE codigo_solicitud = @id";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@e", s.Estado);
                    cmd.Parameters.AddWithValue("@o", (object)s.ObservacionesInspector ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@u", s.UsuarioRevisor ?? "SISTEMA");
                    cmd.Parameters.AddWithValue("@id", s.CodigoSolicitud);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool ActualizarTecnico(int solicitud, int tecnico, int usuario)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    UPDATE aocr_tbsolicitud
                    SET codigo_tecnico = @t,
                        updated_at = NOW(),
                        updated_by = @u
                    WHERE codigo_solicitud = @id";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@t", tecnico);
                    cmd.Parameters.AddWithValue("@u", usuario.ToString());
                    cmd.Parameters.AddWithValue("@id", solicitud);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool CambiarEstado(int id, string estado, int usuario, string obs = "")
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    UPDATE aocr_tbsolicitud
                    SET estado = @e,
                        observaciones = @o,
                        updated_at = NOW(),
                        updated_by = @u
                    WHERE codigo_solicitud = @id";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@e", estado);
                    cmd.Parameters.AddWithValue("@o", obs);
                    cmd.Parameters.AddWithValue("@u", usuario.ToString());
                    cmd.Parameters.AddWithValue("@id", id);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool Eliminar(int id)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"DELETE FROM aocr_tbsolicitud WHERE codigo_solicitud = @id";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ============================
        // MÉTODOS INTERNOS
        // ============================
        private List<SolicitudAOCR> ObtenerPorFiltro(
            string where,
            Action<NpgsqlCommand> parametros = null)
        {
            var lista = new List<SolicitudAOCR>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = $@"
                    SELECT * FROM aocr_tbsolicitud
                    WHERE {where}
                    ORDER BY fecha_solicitud DESC";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    parametros?.Invoke(cmd);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lista.Add(Mapear(rd));
                        }
                    }
                }
            }

            return lista;
        }

        private SolicitudAOCR Mapear(NpgsqlDataReader rd)
        {
            return new SolicitudAOCR
            {
                CodigoSolicitud = Convert.ToInt32(rd["codigo_solicitud"]),
                NombreOperador = rd["nombre_operador"].ToString(),
                Estado = rd["estado"].ToString(),
                FechaSolicitud = Convert.ToDateTime(rd["fecha_solicitud"]),
                CodigoUsuario = Convert.ToInt32(rd["codigo_usuario"])
            };
        }

    }
}