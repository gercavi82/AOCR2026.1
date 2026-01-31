using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CapaDatos.Entidades;
using Npgsql;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Data Access Object para OrdenRecaudacion
    /// </summary>
    public class OrdenRecaudacionDAO
    {
        private readonly string _connectionString;

        public OrdenRecaudacionDAO()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString
                ?? System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                ?? "";
        }

        public OrdenRecaudacionDAO(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Métodos de Lectura

        /// <summary>
        /// Obtiene todas las órdenes de recaudación
        /// </summary>
        public List<OrdenRecaudacion> ObtenerTodas()
        {
            var ordenes = new List<OrdenRecaudacion>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT o.*, c.nombre as concepto_nombre 
                                FROM aocr_or_orden o 
                                LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id 
                                ORDER BY o.fecha_creacion DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ordenes.Add(MapearOrden(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ObtenerTodas: " + ex.Message);
                throw;
            }

            return ordenes;
        }

        /// <summary>
        /// Obtiene una orden por su ID
        /// </summary>
        public OrdenRecaudacion ObtenerPorId(int id)
        {
            OrdenRecaudacion orden = null;

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT o.*, c.nombre as concepto_nombre 
                                FROM aocr_or_orden o 
                                LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id 
                                WHERE o.id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                orden = MapearOrden(reader);
                            }
                        }
                    }

                    // Cargar detalles si existe la orden
                    if (orden != null)
                    {
                        orden.Detalles = ObtenerDetallesPorOrdenId(orden.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ObtenerPorId: " + ex.Message);
                throw;
            }

            return orden;
        }

        /// <summary>
        /// Obtiene órdenes por estado
        /// </summary>
        public List<OrdenRecaudacion> ObtenerPorEstado(string estado)
        {
            var ordenes = new List<OrdenRecaudacion>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT o.*, c.nombre as concepto_nombre 
                                FROM aocr_or_orden o 
                                LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id 
                                WHERE o.estado = @estado 
                                ORDER BY o.fecha_creacion DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@estado", estado ?? "");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ordenes.Add(MapearOrden(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ObtenerPorEstado: " + ex.Message);
                throw;
            }

            return ordenes;
        }

        /// <summary>
        /// Obtiene órdenes por usuario
        /// </summary>
        public List<OrdenRecaudacion> ObtenerPorUsuario(string codigoUsuario)
        {
            var ordenes = new List<OrdenRecaudacion>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT o.*, c.nombre as concepto_nombre 
                                FROM aocr_or_orden o 
                                LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id 
                                WHERE o.codigo_usuario = @codigoUsuario 
                                ORDER BY o.fecha_creacion DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario ?? "");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ordenes.Add(MapearOrden(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ObtenerPorUsuario: " + ex.Message);
                throw;
            }

            return ordenes;
        }

        /// <summary>
        /// Obtiene los detalles de una orden
        /// </summary>
        public List<DetalleOrden> ObtenerDetallesPorOrdenId(int ordenId)
        {
            var detalles = new List<DetalleOrden>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT * FROM aocr_or_orden_detalle WHERE orden_id = @ordenId ORDER BY id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ordenId", ordenId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                detalles.Add(MapearDetalle(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ObtenerDetallesPorOrdenId: " + ex.Message);
                throw;
            }

            return detalles;
        }

        #endregion

        #region Métodos de Escritura

        /// <summary>
        /// Inserta una nueva orden de recaudación
        /// </summary>
        public int Insertar(OrdenRecaudacion orden)
        {
            int nuevoId = 0;

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var sql = @"INSERT INTO aocr_or_orden 
                                (codigo_usuario, codigo_solicitud, numero_orden, fecha_creacion, estado, 
                                 observacion, subtotal, admin, total, lugar_emision, compania, 
                                 ruc_cedula, correo, telefono, concepto_id)
                                VALUES 
                                (@codigoUsuario, @codigoSolicitud, @numeroOrden, @fechaCreacion, @estado,
                                 @observacion, @subtotal, @admin, @total, @lugarEmision, @compania,
                                 @rucCedula, @correo, @telefono, @conceptoId)
                                RETURNING id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoUsuario", (object)orden.CodigoUsuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@codigoSolicitud", (object)orden.CodigoSolicitud ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@numeroOrden", (object)orden.NumeroOrden ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fechaCreacion", orden.FechaCreacion);
                        cmd.Parameters.AddWithValue("@estado", (object)orden.Estado ?? "BORRADOR");
                        cmd.Parameters.AddWithValue("@observacion", (object)orden.Observacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@subtotal", (object)orden.Subtotal ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@admin", (object)orden.Admin ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@total", (object)orden.Total ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@lugarEmision", (object)orden.LugarEmision ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@compania", (object)orden.Compania ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@rucCedula", (object)orden.RucCedula ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@correo", (object)orden.Correo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@telefono", (object)orden.Telefono ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@conceptoId", (object)orden.ConceptoId ?? DBNull.Value);

                        var result = cmd.ExecuteScalar();
                        nuevoId = Convert.ToInt32(result);
                    }

                    // Insertar detalles si existen
                    if (orden.Detalles != null && orden.Detalles.Count > 0)
                    {
                        foreach (var detalle in orden.Detalles)
                        {
                            detalle.OrdenId = nuevoId;
                            InsertarDetalle(detalle, conn);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en Insertar: " + ex.Message);
                throw;
            }

            return nuevoId;
        }

        /// <summary>
        /// Inserta un detalle de orden
        /// </summary>
        private void InsertarDetalle(DetalleOrden detalle, NpgsqlConnection conn)
        {
            var sql = @"INSERT INTO aocr_or_orden_detalle 
                        (orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion, 
                         cantidad, valor_unitario, porcentaje_admin, subtotal, admin, total_linea)
                        VALUES 
                        (@ordenId, @conceptoId, @conceptoCodigo, @conceptoNombre, @descripcion,
                         @cantidad, @valorUnitario, @porcentajeAdmin, @subtotal, @admin, @totalLinea)";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ordenId", detalle.OrdenId);
                cmd.Parameters.AddWithValue("@conceptoId", (object)detalle.ConceptoId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@conceptoCodigo", (object)detalle.ConceptoCodigo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@conceptoNombre", (object)detalle.ConceptoNombre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@descripcion", (object)detalle.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cantidad", detalle.Cantidad);
                cmd.Parameters.AddWithValue("@valorUnitario", detalle.ValorUnitario);
                cmd.Parameters.AddWithValue("@porcentajeAdmin", (object)detalle.PorcentajeAdmin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@subtotal", detalle.Subtotal);
                cmd.Parameters.AddWithValue("@admin", (object)detalle.Admin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@totalLinea", detalle.TotalLinea);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Actualiza una orden de recaudación
        /// </summary>
        public bool Actualizar(OrdenRecaudacion orden)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var sql = @"UPDATE aocr_or_orden SET
                                codigo_usuario = @codigoUsuario,
                                codigo_solicitud = @codigoSolicitud,
                                numero_orden = @numeroOrden,
                                estado = @estado,
                                observacion = @observacion,
                                subtotal = @subtotal,
                                admin = @admin,
                                total = @total,
                                lugar_emision = @lugarEmision,
                                compania = @compania,
                                ruc_cedula = @rucCedula,
                                correo = @correo,
                                telefono = @telefono,
                                concepto_id = @conceptoId
                                WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", orden.Id);
                        cmd.Parameters.AddWithValue("@codigoUsuario", (object)orden.CodigoUsuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@codigoSolicitud", (object)orden.CodigoSolicitud ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@numeroOrden", (object)orden.NumeroOrden ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@estado", (object)orden.Estado ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@observacion", (object)orden.Observacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@subtotal", (object)orden.Subtotal ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@admin", (object)orden.Admin ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@total", (object)orden.Total ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@lugarEmision", (object)orden.LugarEmision ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@compania", (object)orden.Compania ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@rucCedula", (object)orden.RucCedula ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@correo", (object)orden.Correo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@telefono", (object)orden.Telefono ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@conceptoId", (object)orden.ConceptoId ?? DBNull.Value);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en Actualizar: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Cambia el estado de una orden
        /// </summary>
        public bool CambiarEstado(int id, string nuevoEstado, string observacion = null)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var sql = "UPDATE aocr_or_orden SET estado = @estado";
                    if (!string.IsNullOrEmpty(observacion))
                    {
                        sql += ", observacion = @observacion";
                    }
                    sql += " WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                        if (!string.IsNullOrEmpty(observacion))
                        {
                            cmd.Parameters.AddWithValue("@observacion", observacion);
                        }

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en CambiarEstado: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Anula una orden
        /// </summary>
        public bool Anular(int id, string motivo = null)
        {
            return CambiarEstado(id, "ANULADA", motivo);
        }

        #endregion

        #region Estadísticas

        /// <summary>
        /// Obtiene estadísticas de las órdenes
        /// </summary>
        public Dictionary<string, object> ObtenerEstadisticas()
        {
            var estadisticas = new Dictionary<string, object>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Total de órdenes
                    var sqlTotal = "SELECT COUNT(*) FROM aocr_or_orden";
                    using (var cmd = new NpgsqlCommand(sqlTotal, conn))
                    {
                        estadisticas["Total"] = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Pagadas (COMPLETADA o FACTURADA)
                    var sqlPagadas = "SELECT COUNT(*) FROM aocr_or_orden WHERE estado IN ('COMPLETADA', 'FACTURADA')";
                    using (var cmd = new NpgsqlCommand(sqlPagadas, conn))
                    {
                        estadisticas["Pagadas"] = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Saldo pendiente
                    var sqlSaldoPendiente = "SELECT COALESCE(SUM(total), 0) FROM aocr_or_orden WHERE estado NOT IN ('COMPLETADA', 'FACTURADA', 'ANULADA')";
                    using (var cmd = new NpgsqlCommand(sqlSaldoPendiente, conn))
                    {
                        estadisticas["SaldoPendiente"] = Convert.ToDecimal(cmd.ExecuteScalar());
                    }

                    // Monto pagado
                    var sqlMontoPagado = "SELECT COALESCE(SUM(total), 0) FROM aocr_or_orden WHERE estado IN ('COMPLETADA', 'FACTURADA')";
                    using (var cmd = new NpgsqlCommand(sqlMontoPagado, conn))
                    {
                        estadisticas["MontoPagado"] = Convert.ToDecimal(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ObtenerEstadisticas: " + ex.Message);
                estadisticas["Total"] = 0;
                estadisticas["Pagadas"] = 0;
                estadisticas["SaldoPendiente"] = 0m;
                estadisticas["MontoPagado"] = 0m;
            }

            return estadisticas;
        }

        /// <summary>
        /// Verifica si existe una orden en borrador para un usuario
        /// </summary>
        public bool TieneOrdenBorrador(string codigoUsuario)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "SELECT COUNT(*) FROM aocr_or_orden WHERE codigo_usuario = @codigoUsuario AND estado = 'BORRADOR'";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario ?? "");
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en TieneOrdenBorrador: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Prueba la conexión a la base de datos
        /// </summary>
        public bool ProbarConexion()
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Métodos Privados de Mapeo

        /// <summary>
        /// Mapea un IDataReader a una entidad OrdenRecaudacion
        /// </summary>
        private OrdenRecaudacion MapearOrden(IDataReader reader)
        {
            var orden = new OrdenRecaudacion
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                CodigoUsuario = reader.IsDBNull(reader.GetOrdinal("codigo_usuario")) ? null : reader.GetString(reader.GetOrdinal("codigo_usuario")),
                CodigoSolicitud = reader.IsDBNull(reader.GetOrdinal("codigo_solicitud")) ? null : reader.GetString(reader.GetOrdinal("codigo_solicitud")),
                NumeroOrden = reader.IsDBNull(reader.GetOrdinal("numero_orden")) ? null : reader.GetString(reader.GetOrdinal("numero_orden")),
                FechaCreacion = reader.IsDBNull(reader.GetOrdinal("fecha_creacion")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("fecha_creacion")),
                Estado = reader.IsDBNull(reader.GetOrdinal("estado")) ? "BORRADOR" : reader.GetString(reader.GetOrdinal("estado")),
                Observacion = reader.IsDBNull(reader.GetOrdinal("observacion")) ? null : reader.GetString(reader.GetOrdinal("observacion")),
                Subtotal = reader.IsDBNull(reader.GetOrdinal("subtotal")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("subtotal")),
                Admin = reader.IsDBNull(reader.GetOrdinal("admin")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("admin")),
                Total = reader.IsDBNull(reader.GetOrdinal("total")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("total")),
                LugarEmision = reader.IsDBNull(reader.GetOrdinal("lugar_emision")) ? null : reader.GetString(reader.GetOrdinal("lugar_emision")),
                Compania = reader.IsDBNull(reader.GetOrdinal("compania")) ? null : reader.GetString(reader.GetOrdinal("compania")),
                RucCedula = reader.IsDBNull(reader.GetOrdinal("ruc_cedula")) ? null : reader.GetString(reader.GetOrdinal("ruc_cedula")),
                Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString(reader.GetOrdinal("correo")),
                Telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString(reader.GetOrdinal("telefono")),
                ConceptoId = reader.IsDBNull(reader.GetOrdinal("concepto_id")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("concepto_id"))
            };

            // Intentar obtener el nombre del concepto si está en el resultado
            try
            {
                var conceptoNombreOrdinal = reader.GetOrdinal("concepto_nombre");
                if (!reader.IsDBNull(conceptoNombreOrdinal))
                {
                    orden.ConceptoNombre = reader.GetString(conceptoNombreOrdinal);
                }
            }
            catch
            {
                // La columna concepto_nombre no está en el resultado, ignorar
            }

            return orden;
        }

        /// <summary>
        /// Mapea un IDataReader a una entidad DetalleOrden
        /// </summary>
        private DetalleOrden MapearDetalle(IDataReader reader)
        {
            return new DetalleOrden
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                OrdenId = reader.GetInt32(reader.GetOrdinal("orden_id")),
                ConceptoId = reader.IsDBNull(reader.GetOrdinal("concepto_id")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("concepto_id")),
                ConceptoCodigo = reader.IsDBNull(reader.GetOrdinal("concepto_codigo")) ? null : reader.GetString(reader.GetOrdinal("concepto_codigo")),
                ConceptoNombre = reader.IsDBNull(reader.GetOrdinal("concepto_nombre")) ? null : reader.GetString(reader.GetOrdinal("concepto_nombre")),
                Descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion")) ? null : reader.GetString(reader.GetOrdinal("descripcion")),
                Cantidad = reader.GetInt32(reader.GetOrdinal("cantidad")),
                ValorUnitario = reader.GetDecimal(reader.GetOrdinal("valor_unitario")),
                PorcentajeAdmin = reader.IsDBNull(reader.GetOrdinal("porcentaje_admin")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("porcentaje_admin")),
                Subtotal = reader.GetDecimal(reader.GetOrdinal("subtotal")),
                Admin = reader.IsDBNull(reader.GetOrdinal("admin")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("admin")),
                TotalLinea = reader.GetDecimal(reader.GetOrdinal("total_linea"))
            };
        }

        #endregion
    }
}