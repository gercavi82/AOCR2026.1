using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Dapper;
using CapaDatos.Entidades;
using CapaDatos.Constants;
using CapaDatos.Interfaces;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaModelo.DTOs;
using DetalleOrdenEnt = CapaDatos.Entidades.DetalleOrden;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// Data Access Object para OrdenRecaudacion
    /// </summary>
    public class OrdenRecaudacionDAO : IOrdenRecaudacionRepository, IOrdenRecaudacionDAO
    {
        private readonly string _connectionString;
        private readonly ILoggerService _logger;

        public OrdenRecaudacionDAO()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString
                ?? System.Configuration.ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                ?? "";
            _logger = new NLogLoggerService("OrdenRecaudacionDAO");
        }

        public OrdenRecaudacionDAO(string connectionString)
        {
            _connectionString = connectionString;
            _logger = new NLogLoggerService("OrdenRecaudacionDAO");
        }

        #region M�todos de Lectura

        /// <summary>
        /// Obtiene todas las �rdenes de recaudaci�n
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
                _logger.LogError(ex, "Error en ObtenerTodas");
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
                _logger.LogError(ex, "Error en ObtenerPorId");
                throw;
            }

            return orden;
        }

        /// <summary>
        /// Obtiene �rdenes por estado
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
                _logger.LogError(ex, "Error en ObtenerPorEstado");
                throw;
            }

            return ordenes;
        }

        /// <summary>
        /// Obtiene �rdenes por usuario
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
                                WHERE o.codigo_usuario::text = @codigoUsuario 
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
                _logger.LogError(ex, "Error en ObtenerPorUsuario");
                throw;
            }

            return ordenes;
        }

        /// <summary>
        /// Obtiene los detalles de una orden
        /// </summary>
        public List<DetalleOrdenEnt> ObtenerDetallesPorOrdenId(int ordenId)
        {
            var detalles = new List<DetalleOrdenEnt>();

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
                _logger.LogError(ex, "Error en ObtenerDetallesPorOrdenId");
                throw;
            }

            return detalles;
        }

        #endregion

        #region M�todos de Escritura

        /// <summary>
        /// Inserta una nueva orden de recaudaci�n
        /// </summary>
        public int Insertar(OrdenRecaudacion orden)
        {
            System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.Insertar: Insertando orden con numero = {orden.NumeroOrden}");
            
            string sql = @"
                INSERT INTO aocr_or_orden (
                    codigo_usuario,
                    codigo_solicitud,
                    numero_orden,
                    fecha_creacion,
                    estado,
                    compania,
                    ruc_cedula,
                    total
                ) VALUES (
                    @CodigoUsuario,
                    @CodigoSolicitud,
                    @NumeroOrden,
                    @FechaCreacion,
                    @Estado,
                    @Compania,
                    @RucCedula,
                    @Total
                ) RETURNING id";
            
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Usar los valores directamente ya que son int?
                        var parametros = new
                        {
                            CodigoUsuario = orden.CodigoUsuario,
                            CodigoSolicitud = orden.CodigoSolicitud,
                            orden.NumeroOrden,
                            orden.FechaCreacion,
                            orden.Estado,
                            orden.Compania,
                            orden.RucCedula,
                            orden.Total
                        };
                        
                        int ordenId = conn.ExecuteScalar<int>(sql, parametros, trans);
                        
                        // Insertar detalles
                        if (orden.Detalles != null && orden.Detalles.Any())
                        {
                            string sqlDetalle = @"
                                INSERT INTO aocr_or_orden_detalle (
                                    orden_id,
                                    concepto_id,
                                    concepto_nombre,
                                    cantidad,
                                    valor_unitario,
                                    total_linea
                                ) VALUES (
                                    @OrdenId,
                                    @ConceptoId,
                                    @ConceptoNombre,
                                    @Cantidad,
                                    @ValorUnitario,
                                    @TotalLinea
                                )";
                            
                            foreach (var detalle in orden.Detalles)
                            {
                                conn.Execute(sqlDetalle, new
                                {
                                    OrdenId = ordenId,
                                    detalle.ConceptoId,
                                    detalle.ConceptoNombre,
                                    detalle.Cantidad,
                                    detalle.ValorUnitario,
                                    detalle.TotalLinea
                                }, trans);
                            }
                        }
                        
                        trans.Commit();
                        return ordenId;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Inserta un detalle de orden
        /// </summary>
        private void InsertarDetalle(DetalleOrdenEnt detalle, NpgsqlConnection conn)
        {
            // Si falta ConceptoCodigo o ConceptoNombre, obtenerlos desde la BD
            if (detalle.ConceptoId.HasValue && (string.IsNullOrEmpty(detalle.ConceptoCodigo) || string.IsNullOrEmpty(detalle.ConceptoNombre)))
            {
                var sqlConcepto = "SELECT codigo, nombre FROM aocr_or_concepto WHERE id = @conceptoId";
                using (var cmdConcepto = new NpgsqlCommand(sqlConcepto, conn))
                {
                    cmdConcepto.Parameters.AddWithValue("@conceptoId", detalle.ConceptoId.Value);
                    using (var reader = cmdConcepto.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            detalle.ConceptoCodigo = reader["codigo"]?.ToString();
                            detalle.ConceptoNombre = reader["nombre"]?.ToString();
                        }
                    }
                }
            }

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
                cmd.Parameters.AddWithValue("@porcentajeAdmin", detalle.PorcentajeAdmin); // NOT NULL en DB
                cmd.Parameters.AddWithValue("@subtotal", detalle.Subtotal);
                cmd.Parameters.AddWithValue("@admin", detalle.Admin);
                cmd.Parameters.AddWithValue("@totalLinea", detalle.TotalLinea);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Actualiza una orden de recaudaci�n
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
                _logger.LogError(ex, "Error en Actualizar");
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
                _logger.LogError(ex, "Error en CambiarEstado");
                throw;
            }
        }

        /// <summary>
        /// Anula una orden
        /// </summary>
        public bool Anular(int id, string motivo = null)
        {
            return CambiarEstado(id, EstadoOrden.Anulada, motivo);
        }

        #endregion

        #region Estad�sticas

        /// <summary>
        /// Obtiene estad�sticas de las �rdenes
        /// </summary>
        public Dictionary<string, object> ObtenerEstadisticas()
        {
            var estadisticas = new Dictionary<string, object>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Total de �rdenes
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
                _logger.LogError(ex, "Error en ObtenerEstadisticas");
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
                    var sql = "SELECT COUNT(*) FROM aocr_or_orden WHERE codigo_usuario::text = @codigoUsuario AND estado = 'BORRADOR'";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario ?? "");
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en TieneOrdenBorrador");
                return false;
            }
        }

        /// <summary>
        /// Prueba la conexi�n a la base de datos
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

        #region M�todos Privados de Mapeo

        /// <summary>
        /// Helper method to safely get int32 value from reader, handling both integer and string types
        /// </summary>
        private int GetSafeInt32(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return 0;

            var fieldType = reader.GetFieldType(ordinal);
            if (fieldType == typeof(int))
                return reader.GetInt32(ordinal);
            else if (fieldType == typeof(string))
                return ParseIntOrDefault(reader.GetString(ordinal));
            else
                return Convert.ToInt32(reader.GetValue(ordinal));
        }

        /// <summary>
        /// Helper method to safely get nullable int value from reader, handling both integer and string types
        /// </summary>
        private int? GetSafeNullableInt(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            var fieldType = reader.GetFieldType(ordinal);
            if (fieldType == typeof(int))
                return reader.GetInt32(ordinal);
            else if (fieldType == typeof(string))
                return ParseIntOrDefault(reader.GetString(ordinal));
            else
                return Convert.ToInt32(reader.GetValue(ordinal));
        }

        /// <summary>
        /// Mapea un IDataReader a una entidad OrdenRecaudacion
        /// </summary>
        private OrdenRecaudacion MapearOrden(IDataReader reader)
        {
            var orden = new OrdenRecaudacion
            {
                Id = GetSafeInt32(reader, "id"),
                CodigoUsuario = GetSafeNullableInt(reader, "codigo_usuario"),
                CodigoSolicitud = GetSafeNullableInt(reader, "codigo_solicitud"),
                NumeroOrden = reader.IsDBNull(reader.GetOrdinal("numero_orden")) ? null : reader.GetString(reader.GetOrdinal("numero_orden")),
                FechaCreacion = reader.IsDBNull(reader.GetOrdinal("fecha_creacion")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("fecha_creacion")),
                Estado = reader.IsDBNull(reader.GetOrdinal("estado")) ? EstadoOrden.Borrador : reader.GetString(reader.GetOrdinal("estado")),
                Observacion = reader.IsDBNull(reader.GetOrdinal("observacion")) ? null : reader.GetString(reader.GetOrdinal("observacion")),
                Subtotal = reader.IsDBNull(reader.GetOrdinal("subtotal")) ? 0m : reader.GetDecimal(reader.GetOrdinal("subtotal")),
                Admin = reader.IsDBNull(reader.GetOrdinal("admin")) ? 0m : reader.GetDecimal(reader.GetOrdinal("admin")),
                Total = reader.IsDBNull(reader.GetOrdinal("total")) ? 0m : reader.GetDecimal(reader.GetOrdinal("total")),
                LugarEmision = reader.IsDBNull(reader.GetOrdinal("lugar_emision")) ? null : reader.GetString(reader.GetOrdinal("lugar_emision")),
                Compania = reader.IsDBNull(reader.GetOrdinal("compania")) ? null : reader.GetString(reader.GetOrdinal("compania")),
                RucCedula = reader.IsDBNull(reader.GetOrdinal("ruc_cedula")) ? null : reader.GetString(reader.GetOrdinal("ruc_cedula")),
                Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString(reader.GetOrdinal("correo")),
                Telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString(reader.GetOrdinal("telefono")),
                ConceptoId = GetSafeNullableInt(reader, "concepto_id")
            };

            // Intentar obtener el nombre del concepto si est� en el resultado
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
                // La columna concepto_nombre no est� en el resultado, ignorar
            }

            return orden;
        }

        /// <summary>
        /// Mapea un IDataReader a una entidad DetalleOrdenEnt
        /// </summary>
        private DetalleOrdenEnt MapearDetalle(IDataReader reader)
        {
            return new DetalleOrdenEnt
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                OrdenId = reader.GetInt32(reader.GetOrdinal("orden_id")),
                ConceptoId = reader.IsDBNull(reader.GetOrdinal("concepto_id")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("concepto_id")),
                ConceptoCodigo = reader.IsDBNull(reader.GetOrdinal("concepto_codigo")) ? null : reader.GetString(reader.GetOrdinal("concepto_codigo")),
                ConceptoNombre = reader.IsDBNull(reader.GetOrdinal("concepto_nombre")) ? null : reader.GetString(reader.GetOrdinal("concepto_nombre")),
                Descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion")) ? null : reader.GetString(reader.GetOrdinal("descripcion")),
                Cantidad = reader.GetInt32(reader.GetOrdinal("cantidad")),
                ValorUnitario = reader.GetDecimal(reader.GetOrdinal("valor_unitario")),
                PorcentajeAdmin = reader.IsDBNull(reader.GetOrdinal("porcentaje_admin")) ? 0m : reader.GetDecimal(reader.GetOrdinal("porcentaje_admin")),
                Subtotal = reader.GetDecimal(reader.GetOrdinal("subtotal")),
                Admin = reader.IsDBNull(reader.GetOrdinal("admin")) ? 0m : reader.GetDecimal(reader.GetOrdinal("admin")),
                TotalLinea = reader.GetDecimal(reader.GetOrdinal("total_linea"))
            };
        }

        #endregion

        #region Compatibilidad / Repositorio / Helpers

        // =============================
        // Helpers internos
        // =============================

        private List<OrdenRecaudacion> ObtenerOrdenesInterno(string codigoUsuario, string estado)
        {
            var ordenes = new List<OrdenRecaudacion>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var sql = @"SELECT o.*, c.nombre as concepto_nombre
                                FROM aocr_or_orden o
                                LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id";

                    var filtros = new List<string>();
                    if (!string.IsNullOrWhiteSpace(codigoUsuario))
                    {
                        filtros.Add("o.codigo_usuario::text = @codigoUsuario");
                    }
                    if (!string.IsNullOrWhiteSpace(estado))
                    {
                        filtros.Add("o.estado = @estado");
                    }

                    if (filtros.Count > 0)
                    {
                        sql += " WHERE " + string.Join(" AND ", filtros);
                    }

                    sql += " ORDER BY o.fecha_creacion DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(codigoUsuario))
                        {
                            cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario);
                        }
                        if (!string.IsNullOrWhiteSpace(estado))
                        {
                            cmd.Parameters.AddWithValue("@estado", estado);
                        }

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
                _logger.LogError(ex, "Error en ObtenerOrdenesInterno");
                throw;
            }

            return ordenes;
        }

        private int ParseIntOrDefault(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            int result;
            return int.TryParse(value, out result) ? result : 0;
        }

        private OrdenRecaudacionModel MapearOrdenModel(OrdenRecaudacion orden)
        {
            if (orden == null)
            {
                return null;
            }

            var model = new OrdenRecaudacionModel
            {
                Id = orden.Id,
                NumeroOrden = orden.NumeroOrden,
                Estado = orden.Estado,
                Total = orden.Total ?? 0m,
                Subtotal = orden.Subtotal ?? 0m,
                Iva = orden.Iva ?? 0m,
                FechaCreacion = orden.FechaCreacion,
                NombreContribuyente = orden.NombreContribuyente,
                CodigoUsuario = orden.CodigoUsuario ?? 0,
                CodigoSolicitud = orden.CodigoSolicitud?.ToString() ?? "0",
                LugarEmision = orden.LugarEmision,
                Compania = orden.Compania,
                RucCedula = orden.RucCedula,
                Correo = orden.Correo,
                Telefono = orden.Telefono,
                Observacion = orden.Observacion,
                Admin = orden.Admin ?? 0m
            };

            if (orden.Detalles != null && orden.Detalles.Count > 0)
            {
                foreach (var d in orden.Detalles)
                {
                    model.Detalles.Add(MapearDetalleModel(d));
                }
            }

            return model;
        }

        private OrdenDetalleModel MapearDetalleModel(DetalleOrdenEnt detalle)
        {
            if (detalle == null)
            {
                return null;
            }

            return new OrdenDetalleModel
            {
                Id = detalle.Id,
                OrdenId = detalle.OrdenId,
                ConceptoId = detalle.ConceptoId ?? 0,
                ConceptoCodigo = detalle.ConceptoCodigo,
                ConceptoNombre = detalle.ConceptoNombre,
                Descripcion = detalle.Descripcion,
                Cantidad = detalle.Cantidad,
                ValorUnitario = detalle.ValorUnitario,
                PorcentajeAdmin = detalle.PorcentajeAdmin,
                Subtotal = detalle.Subtotal,
                Admin = detalle.Admin,
                TotalLinea = detalle.TotalLinea
            };
        }

        private OrdenRecaudacion MapearOrdenEntidad(OrdenRecaudacionModel orden)
        {
            if (orden == null)
            {
                return null;
            }

            var entidad = new OrdenRecaudacion
            {
                Id = orden.Id,
                NumeroOrden = orden.NumeroOrden,
                Estado = orden.Estado,
                Total = orden.Total,
                Subtotal = orden.Subtotal,
                Admin = orden.Admin,
                FechaCreacion = orden.FechaCreacion == default(DateTime) ? DateTime.Now : orden.FechaCreacion,
                CodigoUsuario = orden.CodigoUsuario == 0 ? (int?)null : orden.CodigoUsuario,
                CodigoSolicitud = int.TryParse(orden.CodigoSolicitud, out int cs) ? (cs == 0 ? (int?)null : cs) : (int?)null,
                LugarEmision = orden.LugarEmision,
                Compania = orden.Compania,
                RucCedula = orden.RucCedula,
                Correo = orden.Correo,
                Telefono = orden.Telefono,
                Observacion = orden.Observacion
            };

            if (orden.Detalles != null && orden.Detalles.Count > 0)
            {
                entidad.Detalles = new List<DetalleOrdenEnt>();
                foreach (var d in orden.Detalles)
                {
                    entidad.Detalles.Add(new DetalleOrdenEnt
                    {
                        Id = d.Id,
                        OrdenId = d.OrdenId,
                        ConceptoId = d.ConceptoId,
                        ConceptoCodigo = d.ConceptoCodigo,
                        ConceptoNombre = d.ConceptoNombre,
                        Descripcion = d.Descripcion,
                        Cantidad = (int)d.Cantidad,
                        ValorUnitario = d.ValorUnitario,
                        PorcentajeAdmin = d.PorcentajeAdmin,
                        Subtotal = d.Subtotal,
                        Admin = d.Admin,
                        TotalLinea = d.TotalLinea
                    });
                }
            }

            return entidad;
        }

        private int ObtenerCodigoSolicitudDesdeOrden(int ordenId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "SELECT codigo_solicitud FROM aocr_or_orden WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", ordenId);
                        var result = cmd.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                        {
                            return 0;
                        }
                        return ParseIntOrDefault(result.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerCodigoSolicitudDesdeOrden");
                return 0;
            }
        }

        private PagoModel MapearPagoModel(IDataReader reader)
        {
            return new PagoModel
            {
                CodigoPago = reader["codigo_pago"] != DBNull.Value ? Convert.ToInt32(reader["codigo_pago"]) : 0,
                CodigoSolicitud = reader["codigo_solicitud"] != DBNull.Value ? Convert.ToInt32(reader["codigo_solicitud"]) : 0,
                NumeroFactura = reader["numero_factura"] != DBNull.Value ? reader["numero_factura"].ToString() : null,
                Monto = reader["monto"] != DBNull.Value ? Convert.ToDecimal(reader["monto"]) : 0m,
                Moneda = reader["moneda"] != DBNull.Value ? reader["moneda"].ToString() : null,
                Concepto = reader["concepto"] != DBNull.Value ? reader["concepto"].ToString() : null,
                MetodoPago = reader["metodo_pago"] != DBNull.Value ? reader["metodo_pago"].ToString() : null,
                Banco = reader["banco"] != DBNull.Value ? reader["banco"].ToString() : null,
                Estado = reader["estado"] != DBNull.Value ? reader["estado"].ToString() : null,
                FechaPago = reader["fecha_pago"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["fecha_pago"]) : null,
                FechaValidacion = reader["fecha_validacion"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["fecha_validacion"]) : null,
                ValidadoPor = reader["validado_por"] != DBNull.Value ? reader["validado_por"].ToString() : null,
                Observaciones = reader["observaciones"] != DBNull.Value ? reader["observaciones"].ToString() : null,
                ComprobanteRuta = reader["comprobante_ruta"] != DBNull.Value ? reader["comprobante_ruta"].ToString() : null
            };
        }

        private Pago MapearPagoEntidad(IDataReader reader)
        {
            return new Pago
            {
                Id = reader["codigo_pago"] != DBNull.Value ? Convert.ToInt32(reader["codigo_pago"]) : 0,
                CodigoSolicitud = reader["codigo_solicitud"] != DBNull.Value ? Convert.ToInt32(reader["codigo_solicitud"]) : 0,
                NumeroComprobante = reader["numero_factura"] != DBNull.Value ? reader["numero_factura"].ToString() : null,
                MontoPagado = reader["monto"] != DBNull.Value ? Convert.ToDecimal(reader["monto"]) : 0m,
                MetodoPago = reader["metodo_pago"] != DBNull.Value ? reader["metodo_pago"].ToString() : null,
                Estado = reader["estado"] != DBNull.Value ? reader["estado"].ToString() : null,
                FechaPago = reader["fecha_pago"] != DBNull.Value ? Convert.ToDateTime(reader["fecha_pago"]) : DateTime.MinValue,
                Observaciones = reader["observaciones"] != DBNull.Value ? reader["observaciones"].ToString() : null,
                RutaComprobante = reader["comprobante_ruta"] != DBNull.Value ? reader["comprobante_ruta"].ToString() : null,
                FechaValidacion = reader["fecha_validacion"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["fecha_validacion"]) : null,
                UsuarioValidacion = reader["validado_por"] != DBNull.Value ? reader["validado_por"].ToString() : null
            };
        }

        // =============================
        // Metodos publicos adicionales (compatibilidad)
        // =============================

        public bool Ping() => ProbarConexion();

        public List<OrdenRecaudacion> ListarPorUsuario(int codigoUsuario, string estado)
        {
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim();
            return ObtenerOrdenesInterno(codigoUsuario.ToString(), estadoFiltro);
        }

        public List<OrdenRecaudacion> ObtenerTodasLasOrdenes(string estado)
        {
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim();
            return ObtenerOrdenesInterno(null, estadoFiltro);
        }

        public OrdenRecaudacion ObtenerOrdenPorId(int id) => ObtenerPorId(id);

        public OrdenRecaudacionModel ObtenerOrdenPorIdModel(int id)
        {
            var orden = ObtenerPorId(id);
            return MapearOrdenModel(orden);
        }

        public List<OrdenRecaudacionModel> ListarPorUsuarioModel(int codigoUsuario, string estado)
        {
            var ordenes = ListarPorUsuario(codigoUsuario, estado);
            return ordenes.Select(MapearOrdenModel).ToList();
        }


        public DataTable ObtenerOrdenesPorUsuario(int codigoUsuario)
        {
            return ((IOrdenRecaudacionDAO)this).ObtenerOrdenesPorUsuario(codigoUsuario);
        }

        public bool ActualizarOrden(OrdenRecaudacion orden) => Actualizar(orden);

        public bool ActualizarOrden(OrdenRecaudacionModel orden)
        {
            var entidad = MapearOrdenEntidad(orden);
            return Actualizar(entidad);
        }

        public bool CambiarEstadoOrden(int id, string nuevoEstado) => CambiarEstado(id, nuevoEstado);

        public bool CambiarEstadoOrden(int id, string nuevoEstado, out string err)
        {
            try
            {
                err = null;
                return CambiarEstado(id, nuevoEstado);
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
        }

        public List<PagoModel> ObtenerPagosPorOrden(int ordenId)
        {
            var pagos = new List<PagoModel>();
            var codigoSolicitud = ObtenerCodigoSolicitudDesdeOrden(ordenId);
            if (codigoSolicitud <= 0)
            {
                codigoSolicitud = ordenId;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT * FROM aocr_tbpago
                                WHERE codigo_solicitud = @codigoSolicitud
                                ORDER BY fecha_pago DESC, codigo_pago DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                pagos.Add(MapearPagoModel(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerPagosPorOrden");
            }

            return pagos;
        }

        public Pago ObtenerUltimoPagoPorOrden(int ordenId)
        {
            var codigoSolicitud = ObtenerCodigoSolicitudDesdeOrden(ordenId);
            if (codigoSolicitud <= 0)
            {
                codigoSolicitud = ordenId;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT * FROM aocr_tbpago
                                WHERE codigo_solicitud = @codigoSolicitud
                                ORDER BY fecha_pago DESC, codigo_pago DESC
                                LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapearPagoEntidad(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerUltimoPagoPorOrden");
            }

            return null;
        }

        public bool ActualizarUltimoPagoEstado(int ordenId, string nuevoEstado, string usuario, string observacion = null)
        {
            var codigoSolicitud = ObtenerCodigoSolicitudDesdeOrden(ordenId);
            if (codigoSolicitud <= 0)
            {
                codigoSolicitud = ordenId;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var sql = @"
                        UPDATE aocr_tbpago
                        SET estado = @estado,
                            fecha_validacion = @fecha_validacion,
                            validado_por = @validado_por,
                            observaciones = COALESCE(@observaciones, observaciones)
                        WHERE codigo_pago = (
                            SELECT codigo_pago FROM aocr_tbpago
                            WHERE codigo_solicitud = @codigoSolicitud
                            ORDER BY fecha_pago DESC, codigo_pago DESC
                            LIMIT 1
                        )";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@estado", nuevoEstado ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@fecha_validacion", DateTime.Now);
                        cmd.Parameters.AddWithValue("@validado_por", (object)usuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@observaciones", (object)observacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ActualizarUltimoPagoEstado");
                return false;
            }
        }

        public int ObtenerCodigoSolicitudPorNumero(string numeroSolicitud)
        {
            if (string.IsNullOrWhiteSpace(numeroSolicitud))
            {
                return 0;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "SELECT codigo_solicitud FROM aocr_tbsolicitud WHERE numero_solicitud = @numero";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@numero", numeroSolicitud.Trim());
                        var result = cmd.ExecuteScalar();
                        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerCodigoSolicitudPorNumero");
                return 0;
            }
        }

        public int ObtenerCodigoSolicitudPorRuc(string ruc)
        {
            if (string.IsNullOrWhiteSpace(ruc))
            {
                return 0;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "SELECT codigo_solicitud FROM aocr_tbsolicitud WHERE ruc = @ruc";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ruc", ruc.Trim());
                        var result = cmd.ExecuteScalar();
                        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerCodigoSolicitudPorRuc");
                return 0;
            }
        }

        public bool ExisteSolicitud(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0)
            {
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "SELECT COUNT(*) FROM aocr_tbsolicitud WHERE codigo_solicitud = @codigo";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigo", codigoSolicitud);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExisteSolicitud");
                return false;
            }
        }

        public bool ActualizarCodigoSolicitudOrden(int ordenId, int codigoSolicitud)
        {
            if (ordenId <= 0 || codigoSolicitud <= 0)
            {
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "UPDATE aocr_or_orden SET codigo_solicitud = @codigoSolicitud WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud.ToString());
                        cmd.Parameters.AddWithValue("@id", ordenId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ActualizarCodigoSolicitudOrden");
                return false;
            }
        }

        public bool RegistrarPago(int codigoSolicitud, PagoModel pago, out string err)
        {
            err = null;
            if (codigoSolicitud <= 0 || pago == null)
            {
                err = "Solicitud/pago invalido.";
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"
                        INSERT INTO aocr_tbpago
                        (codigo_solicitud, numero_factura, monto, moneda, concepto, metodo_pago, banco, estado, fecha_pago, observaciones, comprobante_ruta)
                        VALUES
                        (@codigoSolicitud, @numeroFactura, @monto, @moneda, @concepto, @metodoPago, @banco, @estado, @fechaPago, @observaciones, @comprobanteRuta)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                        cmd.Parameters.AddWithValue("@numeroFactura", (object)pago.NumeroFactura ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@monto", pago.Monto);
                        cmd.Parameters.AddWithValue("@moneda", (object)pago.Moneda ?? "USD");
                        cmd.Parameters.AddWithValue("@concepto", (object)pago.Concepto ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@metodoPago", (object)pago.MetodoPago ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@banco", (object)pago.Banco ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@estado", (object)pago.Estado ?? EstadoPago.Pendiente);
                        cmd.Parameters.AddWithValue("@fechaPago", (object)pago.FechaPago ?? DateTime.Now);
                        cmd.Parameters.AddWithValue("@observaciones", (object)pago.Observaciones ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@comprobanteRuta", (object)pago.ComprobanteRuta ?? DBNull.Value);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                err = ex.Message;
                _logger.LogError(ex, "Error en RegistrarPago");
                return false;
            }
        }

        public bool RegistrarPago(int codigoSolicitud, CapaModelo.PagoModel pago, out string err)
        {
            if (pago == null)
            {
                err = "Solicitud/pago invalido.";
                return false;
            }

            var pagoModel = new PagoModel
            {
                CodigoPago = pago.CodigoPago,
                CodigoSolicitud = pago.CodigoSolicitud,
                NumeroFactura = pago.NumeroFactura,
                Monto = pago.Monto,
                Moneda = pago.Moneda,
                Concepto = pago.Concepto,
                MetodoPago = pago.MetodoPago,
                Estado = pago.Estado,
                FechaPago = pago.FechaPago,
                FechaValidacion = pago.FechaValidacion,
                ValidadoPor = pago.ValidadoPor,
                Observaciones = pago.Observaciones,
                ComprobanteRuta = pago.ComprobanteRuta
            };

            return RegistrarPago(codigoSolicitud, pagoModel, out err);
        }

        // =============================
        // Estadisticas por usuario
        // =============================

        public Dictionary<string, object> ObtenerEstadisticas(int codigoUsuario)
        {
            var estadisticas = new Dictionary<string, object>();
            var codigoUsuarioStr = codigoUsuario > 0 ? codigoUsuario.ToString() : null;

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    var filtroUsuario = !string.IsNullOrWhiteSpace(codigoUsuarioStr);

                    // Total de órdenes
                    var sqlTotal = filtroUsuario 
                        ? "SELECT COUNT(*) FROM aocr_or_orden WHERE codigo_usuario::text = @codigoUsuario"
                        : "SELECT COUNT(*) FROM aocr_or_orden";
                    using (var cmd = new NpgsqlCommand(sqlTotal, conn))
                    {
                        if (filtroUsuario) cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuarioStr);
                        estadisticas["total"] = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Órdenes pagadas
                    var sqlPagadas = filtroUsuario
                        ? "SELECT COUNT(*) FROM aocr_or_orden WHERE estado IN ('COMPLETADA', 'FACTURADA') AND codigo_usuario::text = @codigoUsuario"
                        : "SELECT COUNT(*) FROM aocr_or_orden WHERE estado IN ('COMPLETADA', 'FACTURADA')";
                    using (var cmd = new NpgsqlCommand(sqlPagadas, conn))
                    {
                        if (filtroUsuario) cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuarioStr);
                        estadisticas["pagada"] = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Monto total
                    var sqlMontoTotal = filtroUsuario
                        ? "SELECT COALESCE(SUM(total), 0) FROM aocr_or_orden WHERE codigo_usuario::text = @codigoUsuario"
                        : "SELECT COALESCE(SUM(total), 0) FROM aocr_or_orden";
                    using (var cmd = new NpgsqlCommand(sqlMontoTotal, conn))
                    {
                        if (filtroUsuario) cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuarioStr);
                        estadisticas["monto_total"] = Convert.ToDecimal(cmd.ExecuteScalar());
                    }

                    // Monto recaudado
                    var sqlMontoRecaudado = filtroUsuario
                        ? "SELECT COALESCE(SUM(total), 0) FROM aocr_or_orden WHERE estado IN ('COMPLETADA', 'FACTURADA') AND codigo_usuario::text = @codigoUsuario"
                        : "SELECT COALESCE(SUM(total), 0) FROM aocr_or_orden WHERE estado IN ('COMPLETADA', 'FACTURADA')";
                    using (var cmd = new NpgsqlCommand(sqlMontoRecaudado, conn))
                    {
                        if (filtroUsuario) cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuarioStr);
                        estadisticas["monto_recaudado"] = Convert.ToDecimal(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerEstadisticas(codigoUsuario)");
                estadisticas["total"] = 0;
                estadisticas["pagada"] = 0;
                estadisticas["monto_total"] = 0m;
                estadisticas["monto_recaudado"] = 0m;
            }

            return estadisticas;
        }

        // =============================
        // Validaciones flujo
        // =============================

        public bool ExisteORGeneradaOPagada(int codigoUsuario)
        {
            if (codigoUsuario <= 0)
            {
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT COUNT(*) FROM aocr_or_orden
                                WHERE codigo_usuario::text = @codigoUsuario
                                AND estado NOT IN ('BORRADOR', 'ANULADA')";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario.ToString());
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExisteORGeneradaOPagada");
                return false;
            }
        }

        public bool ExisteORMinima(int codigoUsuario)
        {
            if (codigoUsuario <= 0)
            {
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT COUNT(*) FROM aocr_or_orden
                                WHERE codigo_usuario::text = @codigoUsuario
                                AND estado = 'BORRADOR'";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario.ToString());
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExisteORMinima");
                return false;
            }
        }

        // =============================
        // IOrdenRecaudacionRepository (async)
        // =============================

        public Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
        {
            return Task.FromResult(ObtenerPorId(id));
        }

        public Task<IEnumerable<OrdenRecaudacion>> ObtenerTodosAsync()
        {
            return Task.FromResult<IEnumerable<OrdenRecaudacion>>(ObtenerTodas());
        }

        public Task<IEnumerable<OrdenRecaudacion>> ObtenerPorEstadoAsync(string estado)
        {
            return Task.FromResult<IEnumerable<OrdenRecaudacion>>(ObtenerPorEstado(estado));
        }

        public Task<int> ObtenerConsecutivoDiarioAsync(DateTime fecha)
        {
            var count = 0;

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    // Simplificar para debug - solo contar órdenes del día
                    var sql = "SELECT COUNT(*) FROM aocr_or_orden WHERE fecha_creacion::date = @fecha";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@fecha", fecha.Date);
                        count = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    
                    // Debug log
                    System.Diagnostics.Debug.WriteLine($"ObtenerConsecutivoDiarioAsync: Fecha={fecha:yyyy-MM-dd}, Count={count}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerConsecutivoDiarioAsync");
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerConsecutivoDiarioAsync: {ex.Message}");
                // En caso de error, usar timestamp como fallback
                count = DateTime.Now.Millisecond % 100;
            }

            return Task.FromResult(count);
        }

        public Task<int> CrearAsync(OrdenRecaudacion orden)
        {
            return Task.FromResult(Insertar(orden));
        }

        public Task CrearDetalleAsync(DetalleOrdenEnt detalle)
        {
            if (detalle == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    InsertarDetalle(detalle, conn);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CrearDetalleAsync");
                throw;
            }

            return Task.CompletedTask;
        }

        public Task<bool> ActualizarAsync(OrdenRecaudacion orden)
        {
            return Task.FromResult(Actualizar(orden));
        }

        public Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado, string usuario)
        {
            return Task.FromResult(CambiarEstado(id, nuevoEstado));
        }

        public Task<bool> EliminarAsync(int id, string usuario)
        {
            return Task.FromResult(CambiarEstado(id, EstadoOrden.Anulada));
        }

        // =============================
        // IOrdenRecaudacionDAO (explicita)
        // =============================

        List<OrdenRecaudacionModel> IOrdenRecaudacionDAO.ListarPorUsuario(int codigoUsuario, string estado)
        {
            return ListarPorUsuarioModel(codigoUsuario, estado);
        }

        List<OrdenRecaudacionModel> IOrdenRecaudacionDAO.ObtenerOrdenes(int? codigoUsuario, string estado)
        {
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim();
            var codigo = codigoUsuario.HasValue ? codigoUsuario.Value.ToString() : null;
            var ordenes = ObtenerOrdenesInterno(codigo, estadoFiltro);
            return ordenes.Select(MapearOrdenModel).ToList();
        }

        OrdenRecaudacionModel IOrdenRecaudacionDAO.ObtenerOrdenPorId(int id)
        {
            return ObtenerOrdenPorIdModel(id);
        }

        int IOrdenRecaudacionDAO.CrearOrden(OrdenRecaudacionModel orden)
        {
            var entidad = MapearOrdenEntidad(orden);
            return Insertar(entidad);
        }

        bool IOrdenRecaudacionDAO.ActualizarOrden(OrdenRecaudacionModel orden)
        {
            var entidad = MapearOrdenEntidad(orden);
            return Actualizar(entidad);
        }

        bool IOrdenRecaudacionDAO.CambiarEstadoOrden(int id, string nuevoEstado)
        {
            return CambiarEstadoOrden(id, nuevoEstado);
        }

        List<OrdenRecaudacionModel> IOrdenRecaudacionDAO.BuscarOrdenes(string criterio, int? codigoUsuario)
        {
            var ordenes = new List<OrdenRecaudacion>();
            var codigo = codigoUsuario.HasValue ? codigoUsuario.Value.ToString() : null;

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT o.*, c.nombre as concepto_nombre
                                FROM aocr_or_orden o
                                LEFT JOIN aocr_or_concepto c ON o.concepto_id = c.id
                                WHERE (o.numero_orden ILIKE @criterio OR o.ruc_cedula ILIKE @criterio)";

                    if (!string.IsNullOrWhiteSpace(codigo))
                    {
                        sql += " AND o.codigo_usuario::text = @codigoUsuario";
                    }

                    sql += " ORDER BY o.fecha_creacion DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@criterio", "%" + (criterio ?? "").Trim() + "%");
                        if (!string.IsNullOrWhiteSpace(codigo))
                        {
                            cmd.Parameters.AddWithValue("@codigoUsuario", codigo);
                        }

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
                _logger.LogError(ex, "Error en BuscarOrdenes");
            }

            return ordenes.Select(MapearOrdenModel).ToList();
        }

        Dictionary<string, object> IOrdenRecaudacionDAO.ObtenerEstadisticas(int codigoUsuario)
        {
            return ObtenerEstadisticas(codigoUsuario);
        }

        bool IOrdenRecaudacionDAO.RegistrarPago(int idOrden, PagoModel pago)
        {
            string err;
            return RegistrarPago(idOrden, pago, out err);
        }

        DataTable IOrdenRecaudacionDAO.ObtenerOrdenesPorUsuario(int codigoUsuario)
        {
            var dt = new DataTable();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "SELECT * FROM aocr_or_orden WHERE codigo_usuario::text = @codigoUsuario ORDER BY fecha_creacion DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario.ToString());
                        using (var adapter = new NpgsqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerOrdenesPorUsuario");
            }

            return dt;
        }

        OrdenRecaudacionPdfDto IOrdenRecaudacionDAO.ObtenerDatosParaPdf(int ordenId, int usuarioId)
        {
            var dto = new OrdenRecaudacionPdfDto();

            try
            {
                var orden = ObtenerPorId(ordenId);
                if (orden == null)
                {
                    return null;
                }

                // Validar usuario si aplica
                if (usuarioId > 0)
                {
                    var codigoUsuario = orden.CodigoUsuario ?? 0;
                    if (codigoUsuario > 0 && codigoUsuario != usuarioId)
                    {
                        return null;
                    }
                }

                dto.OrdenId = orden.Id;
                dto.NumeroOrden = orden.NumeroOrden;
                dto.FechaEmision = orden.FechaCreacion;
                dto.LugarEmision = orden.LugarEmision;
                dto.NombreCompania = orden.Compania;
                dto.Ruc = orden.RucCedula;
                dto.Email = orden.Correo;
                dto.Telefono = orden.Telefono;
                dto.Referencia = orden.NumeroOrden;
                dto.Observacion = orden.Observacion;

                if (orden.Detalles != null && orden.Detalles.Count > 0)
                {
                    foreach (var d in orden.Detalles)
                    {
                        dto.Detalles.Add(new OrdenRecaudacionPdfDetalleDto
                        {
                            CodigoConcepto = d.ConceptoCodigo,
                            NombreConcepto = d.ConceptoNombre,
                            Cantidad = d.Cantidad,
                            ValorUnitario = d.ValorUnitario,
                            PorcentajeAdmin = d.PorcentajeAdmin,
                            SubtotalLinea = d.Subtotal,
                            AdminLinea = d.Admin,
                            ValorTotal = d.TotalLinea
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerDatosParaPdf");
                return null;
            }

            return dto;
        }

        OrdenRecaudacionModel IOrdenRecaudacionDAO.ObtenerPorId(int id)
        {
            return ObtenerOrdenPorIdModel(id);
        }

        int IOrdenRecaudacionDAO.Insertar(OrdenRecaudacionModel orden)
        {
            var entidad = MapearOrdenEntidad(orden);
            return Insertar(entidad);
        }

        bool IOrdenRecaudacionDAO.Actualizar(OrdenRecaudacionModel orden)
        {
            var entidad = MapearOrdenEntidad(orden);
            return Actualizar(entidad);
        }

        bool IOrdenRecaudacionDAO.CambiarEstado(int id, string estado)
        {
            return CambiarEstado(id, estado);
        }

        #endregion
    }
}



