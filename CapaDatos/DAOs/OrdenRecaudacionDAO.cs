using System;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo.DTOs;

namespace CapaDatos.DAOs
{
    public class OrdenRecaudacionDAO : IOrdenRecaudacionDAO
    {
        private readonly string _connectionString;

        public OrdenRecaudacionDAO()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en Web.config/app.config.");
        }

        public bool ExisteORMinima(int usuarioId)
        {
            const string sql = @"
                SELECT EXISTS(
                    SELECT 1
                    FROM public.aocr_or_orden
                    WHERE codigo_usuario = @u
                      AND estado = 'BORRADOR'
                );";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", usuarioId);
                    conn.Open();
                    return Convert.ToBoolean(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return false;
            }
        }

        public bool ExisteORGeneradaOPagada(int usuarioId)
        {
            const string sql = @"
                SELECT EXISTS(
                    SELECT 1
                    FROM public.aocr_or_orden
                    WHERE codigo_usuario = @u
                      AND estado IN ('GENERADA', 'PAGADA', 'COMPLETADA', 'FACTURADA')
                );";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", usuarioId);
                    conn.Open();
                    return Convert.ToBoolean(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return false;
            }
        }

        public bool ConceptoExiste(string conceptoCodigo)
        {
            // En tu tabla orden tienes concepto_id (int). Si tú manejas "codigo", cambia la consulta.
            // Aquí asumo que tienes tabla aocr_or_concepto con campo "id" y "activo".
            const string sql = @"
                SELECT EXISTS(
                    SELECT 1
                    FROM public.aocr_or_concepto
                    WHERE codigo = @codigo
                      AND activo = true
                );";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@codigo", conceptoCodigo);
                    conn.Open();
                    return Convert.ToBoolean(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return false;
            }
        }

        public decimal ObtenerValorConceptoPorId(int conceptoId)
        {
            const string sql = @"
                SELECT valor_base
                FROM public.aocr_or_concepto
                WHERE id = @id AND activo = true;";

            using (var conn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", conceptoId);
                conn.Open();
                var r = cmd.ExecuteScalar();
                return (r == null || r == DBNull.Value) ? 0m : Convert.ToDecimal(r);
            }
        }

        public DataTable ObtenerConceptosActivos()
        {
            var dt = new DataTable();
            const string sql = @"
                SELECT id, codigo, nombre, valor_base, descripcion
                FROM public.aocr_or_concepto
                WHERE activo = true
                ORDER BY nombre;";

            using (var conn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, conn))
            using (var da = new NpgsqlDataAdapter(cmd))
            {
                conn.Open();
                da.Fill(dt);
            }
            return dt;
        }

        public DataTable ObtenerOrdenesPorUsuario(int usuarioId)
        {
            var dt = new DataTable();
            const string sql = @"
                SELECT id, numero_orden, estado, fecha_creacion, total
                FROM public.aocr_or_orden
                WHERE codigo_usuario = @u
                ORDER BY fecha_creacion DESC;";

            using (var conn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, conn))
            using (var da = new NpgsqlDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@u", usuarioId);
                conn.Open();
                da.Fill(dt);
            }
            return dt;
        }

        public int InsertarOrdenAOCR(int idUsuario, string codigoSolicitud, int conceptoId, int estaciones, int dias, string obs)
        {
            if (idUsuario <= 0) throw new Exception("Usuario no válido.");
            if (string.IsNullOrWhiteSpace(codigoSolicitud)) throw new Exception("Solicitud no válida (código requerido).");
            if (conceptoId <= 0) throw new Exception("Concepto no válido.");
            if (estaciones < 0 || estaciones > 50) throw new Exception("Estaciones fuera de rango.");
            if (dias < 0 || dias > 30) throw new Exception("Días fuera de rango.");

            if (ExisteORMinima(idUsuario))
                throw new Exception("Ya existe una orden en BORRADOR para este usuario.");

            decimal valorBase = ObtenerValorConceptoPorId(conceptoId);
            if (valorBase <= 0) throw new Exception("El concepto no existe o no está activo.");

            decimal inspecciones = estaciones * 500m;
            decimal viaticos = dias * 80m;
            decimal admin = viaticos * 0.08m;
            decimal subtotal = valorBase + inspecciones + viaticos;
            decimal total = subtotal + admin;

            // Numeración robusta (puedes reemplazar por secuencia real)
            string numeroOrden = $"OR-{DateTime.Now:yyyyMMddHHmmss}-{idUsuario}";

            const string sql = @"
                INSERT INTO public.aocr_or_orden
                (codigo_usuario, codigo_solicitud, numero_orden, estado, observacion, subtotal, admin, total, lugar_emision, concepto_id)
                VALUES
                (@u, @sol, @num, 'BORRADOR', @obs, @subtotal, @admin, @total, @lugar, @concepto)
                RETURNING id;";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", idUsuario);
                    cmd.Parameters.AddWithValue("@sol", codigoSolicitud.Trim());
                    cmd.Parameters.AddWithValue("@num", numeroOrden);
                    cmd.Parameters.AddWithValue("@obs", string.IsNullOrWhiteSpace(obs) ? (object)DBNull.Value : obs.Trim());
                    cmd.Parameters.AddWithValue("@subtotal", subtotal);
                    cmd.Parameters.AddWithValue("@admin", admin);
                    cmd.Parameters.AddWithValue("@total", total);
                    cmd.Parameters.AddWithValue("@lugar", "Quito");
                    cmd.Parameters.AddWithValue("@concepto", conceptoId);

                    conn.Open();
                    var r = cmd.ExecuteScalar();
                    return (r == null || r == DBNull.Value) ? 0 : Convert.ToInt32(r);
                }
            }
            catch (PostgresException pgEx)
            {
                // Producción: log real
                string msg = $"Error de base de datos: {pgEx.MessageText}";
                if (pgEx.SqlState == "23505") msg = "Ya existe una orden con ese número.";
                throw new Exception(msg, pgEx);
            }
        }

        public OrdenRecaudacionPdfDto ObtenerDatosParaPdf(int ordenId, int usuarioId)
        {
            if (ordenId <= 0) throw new Exception("Orden no válida.");
            if (usuarioId <= 0) throw new Exception("Usuario no válido.");

            const string sql = @"
                SELECT
                    o.id,
                    o.numero_orden,
                    o.fecha_creacion,
                    o.lugar_emision,
                    o.compania,
                    o.ruc_cedula,
                    o.correo,
                    o.telefono,
                    o.subtotal,
                    o.admin,
                    o.total,
                    o.observacion,
                    o.estaciones,
                    o.dias,
                    c.nombre as concepto_nombre,
                    c.valor_base
                FROM public.aocr_or_orden o
                LEFT JOIN public.aocr_or_concepto c ON o.concepto_id = c.id
                WHERE o.id = @id
                  AND o.codigo_usuario = @u;";

            using (var conn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", ordenId);
                cmd.Parameters.AddWithValue("@u", usuarioId);
                conn.Open();

                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                        throw new Exception("Orden no encontrada o no pertenece al usuario.");

                    var dto = new OrdenRecaudacionPdfDto
                    {
                        OrdenId = Convert.ToInt32(rd["id"]),
                        NumeroOrden = rd["numero_orden"]?.ToString(),
                        FechaEmision = Convert.ToDateTime(rd["fecha_creacion"]),
                        LugarEmision = rd["lugar_emision"]?.ToString() ?? "Quito",

                        NombreCompania = rd["compania"]?.ToString() ?? "No registrado",
                        Ruc = rd["ruc_cedula"]?.ToString() ?? "No registrado",
                        Email = rd["correo"]?.ToString() ?? "No registrado",
                        Telefono = rd["telefono"]?.ToString() ?? "No registrado",

                        ConceptoPrincipal = rd["concepto_nombre"]?.ToString() ?? "Servicio DGAC",
                        ValorBase = (rd["valor_base"] == DBNull.Value) ? 0m : Convert.ToDecimal(rd["valor_base"]),

                        Estaciones = (rd["estaciones"] == DBNull.Value) ? 0 : Convert.ToInt32(rd["estaciones"]),
                        Dias = (rd["dias"] == DBNull.Value) ? 0 : Convert.ToInt32(rd["dias"]),

                        Observacion = rd["observacion"]?.ToString(),
                        Referencia = rd["observacion"]?.ToString(),

                        // Puedes obtenerlo de tabla de usuario si la tienes
                        NombreRepresentante = "Representante"
                    };

                    dto.CalcularTotales(); // recalcula consistente
                    dto.NombreInspector = "DGAC";
                    dto.CargoInspector = "Autoridad competente";

                    return dto;
                }
            }
        }

        public byte[] GenerarPDFOrden(int ordenId, int usuarioId)
        {
            // Se genera en CapaPresentacion por arquitectura,
            // pero lo dejo aquí para cumplir interfaz (llamará al service desde Presentación).
            // En la práctica, este método puede no usarse desde CapaDatos.
            throw new NotImplementedException("Generación PDF debe realizarse en CapaPresentacion/Services.");
        }
    }
}
