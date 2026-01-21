using System;
using System.Data;
using Npgsql;
using System.Configuration;

namespace CapaDatos.DAOs
{
    // INTERFAZ
    public interface IOrdenRecaudacionDAO
    {
        bool ExisteORMinima(int usuarioId);
        bool ExisteORGeneradaOPagada(int usuarioId);
        bool ConceptoExiste(string conceptoCodigo);
        int InsertarOrdenAOCR(int idUsuario, int idSolicitud, string concepto, int estaciones, int dias, string obs);
        decimal ObtenerValorConcepto(string codigoConcepto);
        DataTable ObtenerConceptosActivos();
        DataTable ObtenerOrdenesPorUsuario(int usuarioId);
    }

    // IMPLEMENTACIÓN
    public class OrdenRecaudacionDAO : IOrdenRecaudacionDAO
    {
        private readonly string _connectionString;

        public OrdenRecaudacionDAO()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
        }

        public bool ExisteORMinima(int usuarioId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = @"
                            SELECT EXISTS(
                                SELECT 1
                                FROM public.aocr_or_orden
                                WHERE id_usuario = @id_usuario
                                  AND estado = 'BORRADOR'
                            )";

                        cmd.Parameters.AddWithValue("@id_usuario", usuarioId);
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception) { return false; }
        }

        public bool ExisteORGeneradaOPagada(int usuarioId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = @"
                            SELECT EXISTS(
                                SELECT 1
                                FROM public.aocr_or_orden
                                WHERE id_usuario = @id_usuario
                                  AND estado IN ('GENERADA', 'PAGADA')
                            )";

                        cmd.Parameters.AddWithValue("@id_usuario", usuarioId);
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception) { return false; }
        }

        public bool ConceptoExiste(string conceptoCodigo)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = @"
                            SELECT EXISTS(
                                SELECT 1
                                FROM public.aocr_or_concepto
                                WHERE codigo = @codigo
                                  AND activo = true
                            )";

                        cmd.Parameters.AddWithValue("@codigo", conceptoCodigo);
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception) { return false; }
        }

        public int InsertarOrdenAOCR(int idUsuario, int idSolicitud, string concepto,
                                     int estaciones, int dias, string obs)
        {
            try
            {
                if (idUsuario <= 0) throw new Exception("Usuario no válido");
                if (!ConceptoExiste(concepto)) throw new Exception($"Concepto '{concepto}' no encontrado");
                if (ExisteORMinima(idUsuario)) throw new Exception("Ya tiene una orden en estado BORRADOR");

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT public.aocr_or_crear_orden(@p_id_usuario, @p_codigo_solicitud, " +
                                         "@p_concepto_principal_codigo, @p_estaciones, @p_dias, " +
                                         "@p_referencia_oficio, @p_observacion)";

                        cmd.Parameters.AddWithValue("@p_id_usuario", idUsuario);
                        cmd.Parameters.AddWithValue("@p_codigo_solicitud", idSolicitud);
                        cmd.Parameters.AddWithValue("@p_concepto_principal_codigo", concepto);
                        cmd.Parameters.AddWithValue("@p_estaciones", estaciones);
                        cmd.Parameters.AddWithValue("@p_dias", dias);
                        cmd.Parameters.AddWithValue("@p_referencia_oficio", DBNull.Value);
                        cmd.Parameters.AddWithValue("@p_observacion",
                            string.IsNullOrEmpty(obs) ? (object)DBNull.Value : obs);

                        var result = cmd.ExecuteScalar();
                        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (PostgresException pgEx)
            {
                string mensaje;
                switch (pgEx.SqlState)
                {
                    case "P0001": mensaje = pgEx.MessageText; break;
                    case "23503": mensaje = "Error: El usuario o concepto no existe"; break;
                    case "23505": mensaje = "Error: Ya existe una orden con esos datos"; break;
                    case "23502": mensaje = "Error: Faltan datos requeridos"; break;
                    default: mensaje = $"Error de base de datos: {pgEx.MessageText}"; break;
                }
                throw new Exception(mensaje);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al crear orden: {ex.Message}");
            }
        }

        public decimal ObtenerValorConcepto(string codigoConcepto)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = @"
                            SELECT valor_base 
                            FROM public.aocr_or_concepto 
                            WHERE codigo = @codigo 
                              AND activo = true";

                        cmd.Parameters.AddWithValue("@codigo", codigoConcepto);
                        var result = cmd.ExecuteScalar();
                        return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }
                }
            }
            catch (Exception) { return 0; }
        }

        public DataTable ObtenerConceptosActivos()
        {
            var dt = new DataTable();
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = @"
                            SELECT codigo, nombre, valor_base, descripcion
                            FROM public.aocr_or_concepto
                            WHERE activo = true
                            ORDER BY orden";

                        using (var da = new NpgsqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                }
            }
            catch { }
            return dt;
        }

        public DataTable ObtenerOrdenesPorUsuario(int usuarioId)
        {
            var dt = new DataTable();
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = @"
                            SELECT id, numero_orden, estado, fecha_creacion, total
                            FROM public.aocr_or_orden
                            WHERE id_usuario = @id_usuario
                            ORDER BY fecha_creacion DESC";

                        cmd.Parameters.AddWithValue("@id_usuario", usuarioId);
                        using (var da = new NpgsqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }
                    }
                }
            }
            catch { }
            return dt;
        }
    }
}
