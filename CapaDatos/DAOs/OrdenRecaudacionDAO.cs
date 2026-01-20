using System;
using System.Data;
using Npgsql;
using System.Configuration;

namespace CapaDatos.DAOs
{
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

    public class OrdenRecaudacionDAO : IOrdenRecaudacionDAO
    {
        private readonly string _connectionString;

        public OrdenRecaudacionDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (settings == null)
                throw new Exception("Error: La cadena de conexión 'AOCRConnection' no está definida en el Web.config.");

            _connectionString = settings.ConnectionString;
        }

        public bool ExisteORMinima(int usuarioId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT EXISTS(
                            SELECT 1 FROM public.aocr_or_orden 
                            WHERE id_usuario = @id_usuario AND estado = 'BORRADOR'
                        )", conn))
                    {
                        cmd.Parameters.AddWithValue("@id_usuario", usuarioId);
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return false; }
        }

        public bool ExisteORGeneradaOPagada(int usuarioId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT EXISTS(
                            SELECT 1 FROM public.aocr_or_orden 
                            WHERE id_usuario = @id_usuario AND estado IN ('GENERADA', 'PAGADA')
                        )", conn))
                    {
                        cmd.Parameters.AddWithValue("@id_usuario", usuarioId);
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return false; }
        }

        public bool ConceptoExiste(string conceptoCodigo)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT EXISTS(
                            SELECT 1 FROM public.aocr_or_concepto 
                            WHERE codigo = @codigo AND activo = true
                        )", conn))
                    {
                        cmd.Parameters.AddWithValue("@codigo", conceptoCodigo);
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return false; }
        }

        public int InsertarOrdenAOCR(int idUsuario, int idSolicitud, string concepto, int estaciones, int dias, string obs)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT public.aocr_or_crear_orden(
                            @p_id_usuario, @p_codigo_solicitud, @p_concepto_principal_codigo, 
                            @p_estaciones, @p_dias, @p_referencia_oficio, @p_observacion)", conn))
                    {
                        cmd.Parameters.AddWithValue("@p_id_usuario", idUsuario);
                        cmd.Parameters.AddWithValue("@p_codigo_solicitud", idSolicitud);
                        cmd.Parameters.AddWithValue("@p_concepto_principal_codigo", concepto);
                        cmd.Parameters.AddWithValue("@p_estaciones", estaciones);
                        cmd.Parameters.AddWithValue("@p_dias", dias);
                        cmd.Parameters.AddWithValue("@p_referencia_oficio", DBNull.Value);
                        cmd.Parameters.AddWithValue("@p_observacion", string.IsNullOrEmpty(obs) ? (object)DBNull.Value : obs);

                        var result = cmd.ExecuteScalar();
                        return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (PostgresException pgEx)
            {
                // Switch compatible con C# 5
                string mensajePersonalizado;
                switch (pgEx.SqlState)
                {
                    case "P0001": mensajePersonalizado = pgEx.MessageText; break;
                    case "23503": mensajePersonalizado = "Error: El usuario o concepto no existe."; break;
                    case "23505": mensajePersonalizado = "Error: Ya existe una orden registrada."; break;
                    default: mensajePersonalizado = "Error de base de datos: " + pgEx.MessageText; break;
                }
                throw new Exception(mensajePersonalizado);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al crear orden: " + ex.Message);
            }
        }

        public decimal ObtenerValorConcepto(string codigoConcepto)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT valor_base FROM public.aocr_or_concepto 
                        WHERE codigo = @codigo AND activo = true", conn))
                    {
                        cmd.Parameters.AddWithValue("@codigo", codigoConcepto);
                        var result = cmd.ExecuteScalar();
                        return (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                    }
                }
            }
            catch { return 0; }
        }

        public DataTable ObtenerConceptosActivos()
        {
            DataTable dt = new DataTable();
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT codigo, nombre, valor_base, descripcion FROM public.aocr_or_concepto
                        WHERE activo = true ORDER BY orden", conn))
                    using (var da = new NpgsqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public DataTable ObtenerOrdenesPorUsuario(int usuarioId)
        {
            DataTable dt = new DataTable();
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT id, numero_orden, estado, fecha_creacion, total FROM public.aocr_or_orden
                        WHERE id_usuario = @id_usuario ORDER BY fecha_creacion DESC", conn))
                    {
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