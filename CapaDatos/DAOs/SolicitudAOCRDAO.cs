using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class SolicitudAOCRDAO
    {
        public string GetConnectionString() => ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        // ===========================
        // Listados
        // ===========================

        public List<SolicitudAOCR> ListarActivas() => ObtenerPorFiltro("deleted_at IS NULL");

        public List<SolicitudAOCR> ObtenerTodos() => ObtenerPorFiltro("1=1");

        public List<SolicitudAOCR> ObtenerPorUsuario(int codigoUsuario) =>
            ObtenerPorFiltro("codigo_usuario = @u", cmd => cmd.Parameters.AddWithValue("@u", codigoUsuario));

        public List<SolicitudAOCR> ObtenerPorEstado(string estado) =>
            ObtenerPorFiltro("estado = @e", cmd => cmd.Parameters.AddWithValue("@e", estado));

        public List<SolicitudAOCR> ObtenerPendientesRevision() =>
            ObtenerPorEstado("ENVIADO_A_INSPECTOR");

        public List<SolicitudAOCR> ObtenerParaValidacionJefatura() =>
            ObtenerPorFiltro("estado = @e AND deleted_at IS NULL", cmd => cmd.Parameters.AddWithValue("@e", "ENVIADO_A_JEFATURA"));

        // ===========================
        // Consultas individuales
        // ===========================

        public SolicitudAOCR ObtenerPorId(int id)
        {
            using (var cn = new NpgsqlConnection(GetConnectionString()))
            {
                cn.Open();
                string sql = @"SELECT codigo_solicitud, nombre_operador, ruc, matricula, estado, fecha_solicitud, 
                                      codigo_usuario, banco, num_comp, observaciones_director, updated_at, updated_by 
                               FROM aocr_tbsolicitud 
                               WHERE codigo_solicitud = @id AND deleted_at IS NULL";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                        return rd.Read() ? Mapear(rd) : null;
                }
            }
        }

        // ===========================
        // Inserción
        // ===========================

        public int InsertarConReturn(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(GetConnectionString()))
            {
                cn.Open();
                string sql = @"INSERT INTO aocr_tbsolicitud 
                               (nombre_operador, ruc, matricula, fecha_solicitud, estado, codigo_usuario, banco, num_comp) 
                               VALUES (@n, @r, @m, @f, @e, @u, @b, @nc) 
                               RETURNING codigo_solicitud";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@n", s.NombreOperador ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@r", s.Ruc ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@m", s.Matricula ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@f", s.FechaSolicitud);
                    cmd.Parameters.AddWithValue("@e", s.Estado ?? "PENDIENTE");
                    cmd.Parameters.AddWithValue("@u", s.CodigoUsuario);
                    cmd.Parameters.AddWithValue("@b", s.Banco ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@nc", s.NumComp ?? (object)DBNull.Value);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ===========================
        // Actualización general
        // ===========================

        public bool ActualizarGeneral(SolicitudAOCR s)
        {
            using (var cn = new NpgsqlConnection(GetConnectionString()))
            {
                cn.Open();
                string sql = @"UPDATE aocr_tbsolicitud 
                               SET nombre_operador = @n, ruc = @r, matricula = @m, 
                                   updated_at = NOW(), updated_by = @u 
                               WHERE codigo_solicitud = @id";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@n", s.NombreOperador ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@r", s.Ruc ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@m", s.Matricula ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@u", s.UpdatedBy ?? "0");
                    cmd.Parameters.AddWithValue("@id", s.CodigoSolicitud);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ===========================
        // Cambio de estado
        // ===========================

        public bool CambiarEstado(int id, string estado, int usuario, string obs = "")
        {
            using (var cn = new NpgsqlConnection(GetConnectionString()))
            {
                cn.Open();
                string sql = @"UPDATE aocr_tbsolicitud 
                               SET estado = @e, observaciones_director = @o, updated_at = NOW(), updated_by = @u 
                               WHERE codigo_solicitud = @id";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@e", estado ?? "");
                    cmd.Parameters.AddWithValue("@o", obs ?? "");
                    cmd.Parameters.AddWithValue("@u", usuario.ToString());
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ===========================
        // Asignación de inspectores
        // ===========================

        public bool AsignarInspectores(int idSolicitud, int principal, int? apoyo, DateTime fecha, string observaciones, out string mensaje)
        {
            mensaje = "";

            try
            {
                using (var cn = new NpgsqlConnection(GetConnectionString()))
                {
                    cn.Open();

                    const string sql = @"
                        INSERT INTO aocr_tbinspeccion 
                        (codigo_solicitud, inspector_principal, inspector_apoyo, fecha_inspeccion, observaciones, created_at)
                        VALUES 
                        (@id, @pri, @apo, @fecha, @obs, NOW())";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@id", idSolicitud);
                        cmd.Parameters.AddWithValue("@pri", principal);
                        cmd.Parameters.AddWithValue("@apo", (object)apoyo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fecha", fecha);
                        cmd.Parameters.AddWithValue("@obs", observaciones ?? "");

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            mensaje = "Inspectores asignados correctamente.";
                            return true;
                        }
                        else
                        {
                            mensaje = "No se pudo asignar la inspección.";
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje = "Error al asignar inspectores: " + ex.Message;
                return false;
            }
        }

        // ===========================
        // Filtro genérico
        // ===========================

        private List<SolicitudAOCR> ObtenerPorFiltro(string where, Action<NpgsqlCommand> parametros = null)
        {
            var lista = new List<SolicitudAOCR>();
            using (var cn = new NpgsqlConnection(GetConnectionString()))
            {
                cn.Open();
                string sql = $"SELECT * FROM aocr_tbsolicitud WHERE {where} ORDER BY fecha_solicitud DESC";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    parametros?.Invoke(cmd);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            lista.Add(Mapear(rd));
                    }
                }
            }
            return lista;
        }

        private SolicitudAOCR Mapear(IDataRecord rd)
        {
            return new SolicitudAOCR
            {
                CodigoSolicitud = Convert.ToInt32(rd["codigo_solicitud"]),
                NombreOperador = rd["nombre_operador"]?.ToString(),
                Ruc = rd["ruc"]?.ToString(),
                Matricula = rd["matricula"]?.ToString(),
                Estado = rd["estado"]?.ToString(),
                FechaSolicitud = Convert.ToDateTime(rd["fecha_solicitud"]),
                CodigoUsuario = Convert.ToInt32(rd["codigo_usuario"]),
                Banco = rd["banco"]?.ToString(),
                NumComp = rd["num_comp"]?.ToString(),
                ObservacionesDirector = rd["observaciones_director"]?.ToString()
            };
        }
        public bool AsignarInspeccion(int codigoSolicitud, int codigoInspector, DateTime fecha, TimeSpan hora, string lugar, string comentarios, int usuario, out string mensaje)
        {
            mensaje = "";

            try
            {
                using (var cn = new NpgsqlConnection(GetConnectionString()))
                {
                    cn.Open();

                    const string sql = @"
                INSERT INTO aocr_tbinspeccion 
                (codigo_solicitud, codigo_inspector, fecha_programada, hora_programada, lugar, comentarios, created_at, created_by, estado, completada, aprobada)
                VALUES 
                (@sol, @insp, @fecha, @hora, @lugar, @comentarios, NOW(), @usuario, 'PENDIENTE', FALSE, FALSE);";

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@sol", codigoSolicitud);
                        cmd.Parameters.AddWithValue("@insp", codigoInspector);
                        cmd.Parameters.AddWithValue("@fecha", fecha.Date);
                        cmd.Parameters.AddWithValue("@hora", hora);
                        cmd.Parameters.AddWithValue("@lugar", lugar ?? "");
                        cmd.Parameters.AddWithValue("@comentarios", comentarios ?? "");
                        cmd.Parameters.AddWithValue("@usuario", usuario);

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            mensaje = "Inspección programada correctamente.";
                            return true;
                        }
                        else
                        {
                            mensaje = "No se pudo registrar la inspección.";
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje = "Error al asignar inspección: " + ex.Message;
                return false;
            }
        }

    }
}
