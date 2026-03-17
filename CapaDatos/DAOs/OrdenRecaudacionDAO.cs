using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Dapper;
using CapaDatos.Entidades;
using CapaDatos.Constants;
using CapaDatos.Interfaces;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaDatos.Services;
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
        private static bool _fr3ColumnsNoDisponibles;
        private static bool _facturaPagoTableNoDisponible;
        private static readonly object _schemaWarningLock = new object();

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

        #region Metodos de Lectura

        /// <summary>
        /// Obtiene todas las Ordenes de recaudaciÃ³n
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

                    AplicarTotalesNormalizadosPorDetalle(conn, ordenes);
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
            System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.ObtenerPorId: Obteniendo orden con id = {id}");
            
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
                                System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.ObtenerPorId: Orden encontrada, numero_orden = {orden.NumeroOrden}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.ObtenerPorId: No se encontrÃ³ orden con id = {id}");
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
        /// Obtiene Ordenes por estado
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

                    AplicarTotalesNormalizadosPorDetalle(conn, ordenes);
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
        /// Obtiene Ordenes por usuario
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

                    AplicarTotalesNormalizadosPorDetalle(conn, ordenes);
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

        #region Metodos de Escritura

        /// <summary>
        /// Inserta una nueva orden de recaudaciÃ³n
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
                    observacion,
                    subtotal,
                    admin,
                    compania,
                    ruc_cedula,
                    lugar_emision,
                    correo,
                    telefono,
                    total
                ) VALUES (
                    @CodigoUsuario,
                    @CodigoSolicitud,
                    @NumeroOrden,
                    @FechaCreacion,
                    @Estado,
                    @Observacion,
                    @Subtotal,
                    @Admin,
                    @Compania,
                    @RucCedula,
                    @LugarEmision,
                    @Correo,
                    @Telefono,
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
                            orden.Observacion,
                            orden.Subtotal,
                            orden.Admin,
                            orden.Compania,
                            orden.RucCedula,
                            orden.LugarEmision,
                            orden.Correo,
                            orden.Telefono,
                            orden.Total
                        };
                        
                        System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.Insertar: Ejecutando INSERT con NumeroOrden = {parametros.NumeroOrden}");
                        int ordenId = conn.ExecuteScalar<int>(sql, parametros, trans);
                        System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.Insertar: INSERT exitoso, ordenId = {ordenId}");
                        
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
        /// Actualiza una orden de recaudaciÃ³n
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
                                concepto_id = COALESCE(@conceptoId, concepto_id)
                                WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (!orden.ConceptoId.HasValue)
                        {
                            _logger.LogWarning("Actualizar orden id={0} sin concepto_id en payload; se conserva el valor actual.", orden.Id);
                        }

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

                        var rows = cmd.ExecuteNonQuery();
                        _logger.LogInfo("Actualizar orden id={0} filas_afectadas={1}", orden.Id, rows);
                        return rows > 0;
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

        #region EstadÃ­sticas

        /// <summary>
        /// Obtiene estadÃ­sticas de las Ordenes
        /// </summary>
        public Dictionary<string, object> ObtenerEstadisticas()
        {
            var estadisticas = new Dictionary<string, object>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Total de Ordenes
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
        /// Prueba la conexiÃ³n a la base de datos
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

        #region MÃ©todos Privados de Mapeo

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
            var numeroOrden = reader.IsDBNull(reader.GetOrdinal("numero_orden")) ? null : reader.GetString(reader.GetOrdinal("numero_orden"));
            System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.MapearOrden: Mapeando orden con numero_orden = {numeroOrden}");
            
            var orden = new OrdenRecaudacion
            {
                Id = GetSafeInt32(reader, "id"),
                CodigoUsuario = GetSafeNullableInt(reader, "codigo_usuario"),
                CodigoSolicitud = GetSafeNullableInt(reader, "codigo_solicitud"),
                NumeroOrden = numeroOrden,
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

            // Intentar obtener el nombre del concepto si estÃ¡ en el resultado
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
                // La columna concepto_nombre no estÃ¡ en el resultado, ignorar
            }

            return orden;
        }

        /// <summary>
        /// Mapea un IDataReader a una entidad DetalleOrdenEnt
        /// </summary>
        private DetalleOrdenEnt MapearDetalle(IDataReader reader)
        {
            var detalle = new DetalleOrdenEnt
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

            return NormalizarMontosDetalle(detalle);
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

                    AplicarTotalesNormalizadosPorDetalle(conn, ordenes);
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

        private sealed class TotalesOrdenNormalizados
        {
            public decimal Subtotal { get; set; }
            public decimal Admin { get; set; }
            public decimal Total { get; set; }
        }

        private void AplicarTotalesNormalizadosPorDetalle(NpgsqlConnection conn, IList<OrdenRecaudacion> ordenes)
        {
            if (conn == null || ordenes == null || ordenes.Count == 0)
            {
                return;
            }

            var ordenIds = ordenes
                .Where(o => o != null && o.Id > 0)
                .Select(o => o.Id)
                .Distinct()
                .ToArray();

            if (ordenIds.Length == 0)
            {
                return;
            }

            const string sql = @"
                WITH detalle_base AS (
                    SELECT
                        d.orden_id,
                        COALESCE(d.subtotal, 0) AS subtotal,
                        COALESCE(d.admin, 0) AS admin_actual,
                        COALESCE(d.total_linea, 0) AS total_actual,
                        CASE
                            WHEN COALESCE(d.porcentaje_admin, 0) > 100
                                 AND COALESCE(d.porcentaje_admin, 0) <= 10000
                                THEN COALESCE(d.porcentaje_admin, 0) / 100.0
                            ELSE COALESCE(d.porcentaje_admin, 0)
                        END AS porcentaje_admin_norm
                    FROM aocr_or_orden_detalle d
                    WHERE d.orden_id = ANY(@ordenIds)
                ),
                detalle_resuelto AS (
                    SELECT
                        orden_id,
                        subtotal,
                        CASE
                            WHEN ABS(admin_actual - ROUND(subtotal * (porcentaje_admin_norm / 100.0), 2)) > 0.01
                                THEN ROUND(subtotal * (porcentaje_admin_norm / 100.0), 2)
                            ELSE admin_actual
                        END AS admin_resuelto,
                        total_actual
                    FROM detalle_base
                )
                SELECT
                    orden_id,
                    ROUND(SUM(subtotal), 2) AS subtotal,
                    ROUND(SUM(admin_resuelto), 2) AS admin,
                    ROUND(SUM(
                        CASE
                            WHEN ABS(total_actual - ROUND(subtotal + admin_resuelto, 2)) > 0.01
                                THEN ROUND(subtotal + admin_resuelto, 2)
                            ELSE total_actual
                        END
                    ), 2) AS total
                FROM detalle_resuelto
                GROUP BY orden_id";

            var totalesPorOrden = new Dictionary<int, TotalesOrdenNormalizados>();
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ordenIds", ordenIds);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var ordenId = reader.GetInt32(reader.GetOrdinal("orden_id"));
                        var subtotal = reader.IsDBNull(reader.GetOrdinal("subtotal")) ? 0m : reader.GetDecimal(reader.GetOrdinal("subtotal"));
                        var admin = reader.IsDBNull(reader.GetOrdinal("admin")) ? 0m : reader.GetDecimal(reader.GetOrdinal("admin"));
                        var total = reader.IsDBNull(reader.GetOrdinal("total")) ? 0m : reader.GetDecimal(reader.GetOrdinal("total"));

                        totalesPorOrden[ordenId] = new TotalesOrdenNormalizados
                        {
                            Subtotal = subtotal,
                            Admin = admin,
                            Total = total
                        };
                    }
                }
            }

            if (totalesPorOrden.Count == 0)
            {
                return;
            }

            foreach (var orden in ordenes)
            {
                if (orden == null)
                {
                    continue;
                }

                TotalesOrdenNormalizados totalNormalizado;
                if (!totalesPorOrden.TryGetValue(orden.Id, out totalNormalizado))
                {
                    continue;
                }

                orden.Subtotal = totalNormalizado.Subtotal;
                orden.Admin = totalNormalizado.Admin;
                orden.Total = totalNormalizado.Total;
            }
        }

        private decimal NormalizarPorcentajeAdmin(decimal porcentaje)
        {
            // Algunos datos quedaron persistidos como 800 en lugar de 8.
            if (porcentaje > 100m && porcentaje <= 10000m)
            {
                return porcentaje / 100m;
            }

            return porcentaje;
        }

        private DetalleOrdenEnt NormalizarMontosDetalle(DetalleOrdenEnt detalle)
        {
            if (detalle == null) return null;

            detalle.PorcentajeAdmin = NormalizarPorcentajeAdmin(detalle.PorcentajeAdmin);

            if (detalle.Subtotal < 0m || detalle.PorcentajeAdmin < 0m)
            {
                return detalle;
            }

            var adminCalculado = Math.Round(
                detalle.Subtotal * (detalle.PorcentajeAdmin / 100m),
                2,
                MidpointRounding.AwayFromZero);

            // Si el admin guardado no cuadra con el porcentaje (caso 640 vs 6.40), corregir para visualizaciÃ³n.
            if (Math.Abs(detalle.Admin - adminCalculado) > 0.01m)
            {
                detalle.Admin = adminCalculado;
            }

            var totalCalculado = Math.Round(
                detalle.Subtotal + detalle.Admin,
                2,
                MidpointRounding.AwayFromZero);

            if (Math.Abs(detalle.TotalLinea - totalCalculado) > 0.01m)
            {
                detalle.TotalLinea = totalCalculado;
            }

            return detalle;
        }

        private OrdenRecaudacionModel MapearOrdenModel(OrdenRecaudacion orden)
        {
            if (orden == null)
            {
                System.Diagnostics.Debug.WriteLine("OrdenRecaudacionDAO.MapearOrdenModel: orden es null");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"OrdenRecaudacionDAO.MapearOrdenModel: Mapeando orden id={orden.Id}, numero_orden={orden.NumeroOrden}");

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

                // Mostrar totales consistentes con el detalle (corrige histÃ³ricos con porcentaje mal guardado).
                model.Subtotal = model.Detalles.Sum(d => d.Subtotal);
                model.Admin = model.Detalles.Sum(d => d.Admin);
                model.Total = model.Subtotal + model.Admin;
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

        private int ObtenerCodigoSolicitudPagoDesdeOrden(int ordenId)
        {
            if (ordenId <= 0)
            {
                return 0;
            }

            var codigoSolicitud = ObtenerCodigoSolicitudDesdeOrden(ordenId);
            return codigoSolicitud > 0 ? codigoSolicitud : ordenId;
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
                Banco = GetSafeBanco(reader),  // MÃ©todo seguro para obtener banco
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
                BancoOrigen = GetSafeBanco(reader),
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

        public List<OrdenRecaudacion> ListarFiltrado(int? codigoUsuario, string estado, DateTime? fechaDesde, DateTime? fechaHasta, string numeroOrden)
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
                                WHERE 1=1";

                    var filtros = new List<string>();
                    if (codigoUsuario.HasValue)
                        filtros.Add("o.codigo_usuario = @codigoUsuario");
                    if (!string.IsNullOrWhiteSpace(estado))
                        filtros.Add("UPPER(o.estado) = UPPER(@estado)");
                    if (fechaDesde.HasValue)
                        filtros.Add("o.fecha_creacion >= @fechaDesde");
                    if (fechaHasta.HasValue)
                        filtros.Add("o.fecha_creacion <= @fechaHasta");
                    if (!string.IsNullOrWhiteSpace(numeroOrden))
                        filtros.Add("o.numero_orden ILIKE @numeroOrden");

                    if (filtros.Count > 0)
                        sql += " AND " + string.Join(" AND ", filtros);

                    sql += " ORDER BY o.fecha_creacion DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (codigoUsuario.HasValue)
                            cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuario.Value);
                        if (!string.IsNullOrWhiteSpace(estado))
                            cmd.Parameters.AddWithValue("@estado", estado.Trim());
                        if (fechaDesde.HasValue)
                            cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde.Value);
                        if (fechaHasta.HasValue)
                            cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta.Value);
                        if (!string.IsNullOrWhiteSpace(numeroOrden))
                            cmd.Parameters.AddWithValue("@numeroOrden", "%" + numeroOrden.Trim() + "%");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ordenes.Add(MapearOrden(reader));
                            }
                        }
                    }

                    AplicarTotalesNormalizadosPorDetalle(conn, ordenes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ListarFiltrado");
                throw;
            }

            return ordenes;
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
            if (ordenId <= 0)
            {
                return pagos;
            }

            var codigoSolicitud = ObtenerCodigoSolicitudPagoDesdeOrden(ordenId);

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT * FROM aocr_tbpago
                                WHERE codigo_solicitud = @ordenId
                                   OR codigo_solicitud = @codigoSolicitud
                                ORDER BY
                                    CASE
                                        WHEN codigo_solicitud = @codigoSolicitud THEN 0
                                        WHEN codigo_solicitud = @ordenId THEN 1
                                        ELSE 2
                                    END,
                                    fecha_pago DESC,
                                    codigo_pago DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ordenId", ordenId);
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
            if (ordenId <= 0)
            {
                return null;
            }

            var codigoSolicitud = ObtenerCodigoSolicitudPagoDesdeOrden(ordenId);

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = @"SELECT * FROM aocr_tbpago
                                WHERE codigo_solicitud = @ordenId
                                   OR codigo_solicitud = @codigoSolicitud
                                ORDER BY
                                    CASE
                                        WHEN codigo_solicitud = @codigoSolicitud THEN 0
                                        WHEN codigo_solicitud = @ordenId THEN 1
                                        ELSE 2
                                    END,
                                    fecha_pago DESC,
                                    codigo_pago DESC
                                LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ordenId", ordenId);
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

        public Pago ObtenerPagoPorId(int pagoId)
        {
            if (pagoId <= 0)
            {
                return null;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = @"SELECT * FROM aocr_tbpago WHERE codigo_pago = @codigoPago LIMIT 1";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoPago", pagoId);
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
                _logger.LogError(ex, "Error en ObtenerPagoPorId");
            }

            return null;
        }

        public bool ActualizarPagoEstadoPorId(int ordenId, int pagoId, string nuevoEstado, string usuario, string observacion = null)
        {
            if (ordenId <= 0 || pagoId <= 0 || string.IsNullOrWhiteSpace(nuevoEstado))
            {
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    const string sql = @"
                        UPDATE aocr_tbpago p
                        SET estado = @estado,
                            fecha_validacion = @fecha_validacion,
                            validado_por = @validado_por,
                            observaciones = COALESCE(@observaciones, p.observaciones)
                        WHERE p.codigo_pago = @pago_id
                          AND EXISTS (
                              SELECT 1
                              FROM aocr_or_orden o
                              WHERE o.id = @orden_id
                                AND (p.codigo_solicitud = @orden_id OR p.codigo_solicitud = COALESCE(o.codigo_solicitud::int, @orden_id))
                          )";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                        cmd.Parameters.AddWithValue("@fecha_validacion", DateTime.Now);
                        cmd.Parameters.AddWithValue("@validado_por", (object)usuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@observaciones", (object)observacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@pago_id", pagoId);
                        cmd.Parameters.AddWithValue("@orden_id", ordenId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ActualizarPagoEstadoPorId");
                return false;
            }
        }

        public FacturaPagoRegistroModel ObtenerFacturaPagoPorOrden(int ordenId)
        {
            if (ordenId <= 0)
            {
                return null;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    if (_facturaPagoTableNoDisponible || _fr3ColumnsNoDisponibles)
                    {
                        RefreshFacturaPagoSchemaFlags(conn);
                    }
                    var includeFr3 = !_fr3ColumnsNoDisponibles;

                    try
                    {
                        return EjecutarConsultaFacturaPagoPorOrden(conn, ordenId, includeFr3);
                    }
                    catch (PostgresException ex) when (
                        includeFr3 && string.Equals(ex.SqlState, "42703", StringComparison.OrdinalIgnoreCase))
                    {
                        LogWarningOnce(ref _fr3ColumnsNoDisponibles,
                            "Columnas FR3 aun no desplegadas en aocr_tb_factura_pago. Se aplica fallback sin FR3.");
                        return EjecutarConsultaFacturaPagoPorOrden(conn, ordenId, includeFr3: false);
                    }
                }
            }
            catch (PostgresException ex) when (string.Equals(ex.SqlState, "42P01", StringComparison.OrdinalIgnoreCase))
            {
                LogWarningOnce(ref _facturaPagoTableNoDisponible,
                    "Tabla aocr_tb_factura_pago no disponible para ObtenerFacturaPagoPorOrden.");
                return null;
            }
            catch (PostgresException ex) when (string.Equals(ex.SqlState, "42703", StringComparison.OrdinalIgnoreCase))
            {
                LogWarningOnce(ref _fr3ColumnsNoDisponibles,
                    "Columnas FR3 aun no desplegadas en aocr_tb_factura_pago.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerFacturaPagoPorOrden");
                return null;
            }
        }

        private void RefreshFacturaPagoSchemaFlags(NpgsqlConnection conn)
        {
            if (conn == null)
            {
                return;
            }

            try
            {
                var tableExists = false;
                const string sqlTable = @"
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'aocr_tb_factura_pago'
                    LIMIT 1";

                using (var cmd = new NpgsqlCommand(sqlTable, conn))
                {
                    var exists = cmd.ExecuteScalar();
                    tableExists = exists != null && exists != DBNull.Value;
                }

                if (!tableExists)
                {
                    return;
                }

                var fr3ColumnsCount = 0;
                const string sqlCols = @"
                    SELECT COUNT(*)
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'aocr_tb_factura_pago'
                      AND column_name IN (
                        'fr3_estado',
                        'fr3_numero',
                        'fr3_secuencial',
                        'fr3_aeropuerto',
                        'fr3_anio',
                        'fr3_error'
                      )";

                using (var cmdCols = new NpgsqlCommand(sqlCols, conn))
                {
                    var countValue = cmdCols.ExecuteScalar();
                    fr3ColumnsCount = countValue != null && countValue != DBNull.Value
                        ? Convert.ToInt32(countValue)
                        : 0;
                }

                lock (_schemaWarningLock)
                {
                    _facturaPagoTableNoDisponible = false;
                    _fr3ColumnsNoDisponibles = fr3ColumnsCount < 6;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo refrescar el esquema de aocr_tb_factura_pago: " + ex.Message);
            }
        }

        private FacturaPagoRegistroModel EjecutarConsultaFacturaPagoPorOrden(NpgsqlConnection conn, int ordenId, bool includeFr3)
        {
            var sql = includeFr3
                ? @"
                        SELECT
                            orden_id,
                            pago_id,
                            numero_factura,
                            autorizacion_factura,
                            fecha_emision,
                            subtotal,
                            iva,
                            total,
                            observaciones,
                            file_name,
                            content_type,
                            file_size,
                            file_path,
                            fr3_estado,
                            fr3_numero,
                            fr3_secuencial,
                            fr3_aeropuerto,
                            fr3_anio,
                            fr3_error
                        FROM aocr_tb_factura_pago
                        WHERE orden_id = @ordenId
                        ORDER BY creado_en DESC
                        LIMIT 1"
                : @"
                        SELECT
                            orden_id,
                            pago_id,
                            numero_factura,
                            autorizacion_factura,
                            fecha_emision,
                            subtotal,
                            iva,
                            total,
                            observaciones,
                            file_name,
                            content_type,
                            file_size,
                            file_path
                        FROM aocr_tb_factura_pago
                        WHERE orden_id = @ordenId
                        ORDER BY creado_en DESC
                        LIMIT 1";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ordenId", ordenId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    var model = new FacturaPagoRegistroModel
                    {
                        OrdenId = reader["orden_id"] != DBNull.Value ? Convert.ToInt32(reader["orden_id"]) : 0,
                        PagoId = reader["pago_id"] != DBNull.Value ? (int?)Convert.ToInt32(reader["pago_id"]) : null,
                        NumeroFactura = reader["numero_factura"] != DBNull.Value ? reader["numero_factura"].ToString() : null,
                        AutorizacionFactura = reader["autorizacion_factura"] != DBNull.Value ? reader["autorizacion_factura"].ToString() : null,
                        FechaEmision = reader["fecha_emision"] != DBNull.Value ? Convert.ToDateTime(reader["fecha_emision"]) : DateTime.Now,
                        Subtotal = reader["subtotal"] != DBNull.Value ? Convert.ToDecimal(reader["subtotal"]) : 0m,
                        Iva = reader["iva"] != DBNull.Value ? Convert.ToDecimal(reader["iva"]) : 0m,
                        Total = reader["total"] != DBNull.Value ? Convert.ToDecimal(reader["total"]) : 0m,
                        Observaciones = reader["observaciones"] != DBNull.Value ? reader["observaciones"].ToString() : null,
                        FileName = reader["file_name"] != DBNull.Value ? reader["file_name"].ToString() : null,
                        ContentType = reader["content_type"] != DBNull.Value ? reader["content_type"].ToString() : null,
                        FileSize = reader["file_size"] != DBNull.Value ? Convert.ToInt64(reader["file_size"]) : 0L,
                        FilePath = reader["file_path"] != DBNull.Value ? reader["file_path"].ToString() : null
                    };

                    if (includeFr3)
                    {
                        model.Fr3Estado = reader["fr3_estado"] != DBNull.Value ? reader["fr3_estado"].ToString() : null;
                        model.Fr3Numero = reader["fr3_numero"] != DBNull.Value ? reader["fr3_numero"].ToString() : null;
                        model.Fr3Secuencial = reader["fr3_secuencial"] != DBNull.Value ? (decimal?)Convert.ToDecimal(reader["fr3_secuencial"]) : null;
                        model.Fr3Aeropuerto = reader["fr3_aeropuerto"] != DBNull.Value ? reader["fr3_aeropuerto"].ToString() : null;
                        model.Fr3Anio = reader["fr3_anio"] != DBNull.Value ? reader["fr3_anio"].ToString() : null;
                        model.Fr3Error = reader["fr3_error"] != DBNull.Value ? reader["fr3_error"].ToString() : null;
                    }

                    return model;
                }
            }
        }

        private void LogWarningOnce(ref bool marker, string message)
        {
            if (marker)
            {
                return;
            }

            lock (_schemaWarningLock)
            {
                if (marker)
                {
                    return;
                }

                _logger.LogWarning(message);
                marker = true;
            }
        }

        public string ObtenerRutaFacturaPago(int ordenId)
        {
            if (ordenId <= 0)
            {
                return null;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = @"SELECT file_path
                                         FROM aocr_tb_factura_pago
                                         WHERE orden_id = @ordenId
                                         ORDER BY creado_en DESC
                                         LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ordenId", ordenId);
                        var result = cmd.ExecuteScalar();
                        return result == null || result == DBNull.Value ? null : result.ToString();
                    }
                }
            }
            catch (PostgresException ex) when (string.Equals(ex.SqlState, "42P01", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Tabla aocr_tb_factura_pago no existe al validar comprobante. Detalle={0}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerRutaFacturaPago");
                return null;
            }
        }

        public bool ActualizarUltimoPagoEstado(int ordenId, string nuevoEstado, string usuario, string observacion = null)
        {
            if (ordenId <= 0)
            {
                return false;
            }

            var codigoSolicitud = ObtenerCodigoSolicitudPagoDesdeOrden(ordenId);

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
                            WHERE codigo_solicitud = @ordenId
                               OR codigo_solicitud = @codigoSolicitud
                            ORDER BY fecha_pago DESC, codigo_pago DESC
                            LIMIT 1
                        )";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@estado", nuevoEstado ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@fecha_validacion", DateTime.Now);
                        cmd.Parameters.AddWithValue("@validado_por", (object)usuario ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@observaciones", (object)observacion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ordenId", ordenId);
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
                    
                    // Verificar si la columna banco existe
                    var tieneBanco = VerificarColumnaBanco(conn);
                    
                    var sql = tieneBanco ? @"
                        INSERT INTO aocr_tbpago
                        (codigo_solicitud, numero_factura, monto, moneda, concepto, metodo_pago, banco, estado, fecha_pago, observaciones, comprobante_ruta)
                        VALUES
                        (@codigoSolicitud, @numeroFactura, @monto, @moneda, @concepto, @metodoPago, @banco, @estado, @fechaPago, @observaciones, @comprobanteRuta)" : @"
                        INSERT INTO aocr_tbpago
                        (codigo_solicitud, numero_factura, monto, moneda, concepto, metodo_pago, estado, fecha_pago, observaciones, comprobante_ruta)
                        VALUES
                        (@codigoSolicitud, @numeroFactura, @monto, @moneda, @concepto, @metodoPago, @estado, @fechaPago, @observaciones, @comprobanteRuta)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                        cmd.Parameters.AddWithValue("@numeroFactura", (object)pago.NumeroFactura ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@monto", pago.Monto);
                        cmd.Parameters.AddWithValue("@moneda", (object)pago.Moneda ?? "USD");
                        cmd.Parameters.AddWithValue("@concepto", (object)pago.Concepto ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@metodoPago", (object)pago.MetodoPago ?? DBNull.Value);
                        
                        // Solo agregar parÃ¡metro banco si la columna existe
                        if (tieneBanco)
                        {
                            cmd.Parameters.AddWithValue("@banco", (object)pago.Banco ?? DBNull.Value);
                        }
                        
                        cmd.Parameters.AddWithValue("@estado", (object)pago.Estado ?? CapaDatos.Constants.EstadoPago.Pendiente);
                        cmd.Parameters.AddWithValue("@fechaPago", (object)pago.FechaPago ?? DateTime.Now);
                        cmd.Parameters.AddWithValue("@observaciones", (object)pago.Observaciones ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@comprobanteRuta", (object)pago.ComprobanteRuta ?? DBNull.Value);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                var pgEx = ex as PostgresException;
                if (pgEx != null &&
                    pgEx.SqlState == "23505" &&
                    string.Equals(pgEx.ConstraintName, "aocr_tbpago_numero_factura_key", StringComparison.OrdinalIgnoreCase))
                {
                    err = "El numero de comprobante ya existe. Verifique el numero de factura/comprobante e intente nuevamente.";
                    _logger.LogWarning("RegistrarPago duplicado: codigoSolicitud={0}, numeroFactura={1}", codigoSolicitud, pago?.NumeroFactura);
                    return false;
                }

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

        /// <summary>
        /// Inserta el pago y actualiza el estado de la orden en una transacciÃ³n para mantener consistencia.
        /// </summary>
        public bool RegistrarPagoYActualizarEstadoTransaccional(int ordenId, int codigoSolicitud, PagoModel pago, string nuevoEstadoOrden, out string err)
        {
            err = null;
            if (ordenId <= 0 || codigoSolicitud <= 0 || pago == null)
            {
                err = "Parametros invÃ¡lidos.";
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        // Insert pago (usar la misma lÃ³gica que RegistrarPago)
                        var tieneBanco = VerificarColumnaBanco(conn);

                        var sqlInsert = tieneBanco ? @"
                            INSERT INTO aocr_tbpago
                            (codigo_solicitud, numero_factura, monto, moneda, concepto, metodo_pago, banco, estado, fecha_pago, observaciones, comprobante_ruta)
                            VALUES
                            (@codigoSolicitud, @numeroFactura, @monto, @moneda, @concepto, @metodoPago, @banco, @estado, @fechaPago, @observaciones, @comprobanteRuta)" : @"
                            INSERT INTO aocr_tbpago
                            (codigo_solicitud, numero_factura, monto, moneda, concepto, metodo_pago, estado, fecha_pago, observaciones, comprobante_ruta)
                            VALUES
                            (@codigoSolicitud, @numeroFactura, @monto, @moneda, @concepto, @metodoPago, @estado, @fechaPago, @observaciones, @comprobanteRuta)";

                        using (var cmd = new NpgsqlCommand(sqlInsert, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                            cmd.Parameters.AddWithValue("@numeroFactura", (object)pago.NumeroFactura ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@monto", pago.Monto);
                            cmd.Parameters.AddWithValue("@moneda", (object)pago.Moneda ?? "USD");
                            cmd.Parameters.AddWithValue("@concepto", (object)pago.Concepto ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@metodoPago", (object)pago.MetodoPago ?? DBNull.Value);
                            if (tieneBanco) cmd.Parameters.AddWithValue("@banco", (object)pago.Banco ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@estado", (object)pago.Estado ?? CapaDatos.Constants.EstadoPago.Pendiente);
                            cmd.Parameters.AddWithValue("@fechaPago", (object)pago.FechaPago ?? DateTime.Now);
                            cmd.Parameters.AddWithValue("@observaciones", (object)pago.Observaciones ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@comprobanteRuta", (object)pago.ComprobanteRuta ?? DBNull.Value);

                            var inserted = cmd.ExecuteNonQuery();
                            if (inserted <= 0)
                            {
                                tx.Rollback();
                                err = "Fallo al insertar registro de pago.";
                                return false;
                            }
                        }

                        // Actualizar estado de la orden
                        var sqlUpdate = "UPDATE aocr_or_orden SET estado = @estado WHERE id = @id";
                        using (var upd = new NpgsqlCommand(sqlUpdate, conn, tx))
                        {
                            upd.Parameters.AddWithValue("@estado", (object)nuevoEstadoOrden ?? DBNull.Value);
                            upd.Parameters.AddWithValue("@id", ordenId);
                            var updated = upd.ExecuteNonQuery();
                            if (updated <= 0)
                            {
                                tx.Rollback();
                                err = "Fallo al actualizar el estado de la orden.";
                                return false;
                            }
                        }

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                var pgEx = ex as PostgresException;
                if (pgEx != null &&
                    pgEx.SqlState == "23505" &&
                    string.Equals(pgEx.ConstraintName, "aocr_tbpago_numero_factura_key", StringComparison.OrdinalIgnoreCase))
                {
                    err = "El numero de comprobante ya existe. Verifique el numero de factura/comprobante e intente nuevamente.";
                    _logger.LogWarning("RegistrarPagoYActualizarEstadoTransaccional duplicado: ordenId={0}, codigoSolicitud={1}, numeroFactura={2}", ordenId, codigoSolicitud, pago?.NumeroFactura);
                    return false;
                }

                err = ex.Message;
                _logger.LogError(ex, "Error en RegistrarPagoYActualizarEstadoTransaccional");
                return false;
            }
        }

        // =============================
        // Estadisticas por usuario
        // =============================

        /// <summary>
        /// Actualiza el estado del ultimo pago (o pago especificado) y el estado de la orden en una transacciÃ³n.
        /// </summary>
        public bool ActualizarPagoYEstadoTransaccional(int ordenId, int? pagoId, string estadoPago, string usuario, string observaciones, string nuevoEstadoOrden, out string err)
        {
            err = null;
            if (ordenId <= 0 || string.IsNullOrWhiteSpace(estadoPago) || string.IsNullOrWhiteSpace(nuevoEstadoOrden))
            {
                err = "Parametros invalidos.";
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        // Determinar id de pago si no fue proporcionado: obtener ultimo pago por orden
                        
                        int targetPagoId = pagoId ?? 0;
                        int codigoSolicitud = ordenId;
                        string numeroOrden = null;
                        decimal totalOrden = 0m;
                        if (targetPagoId == 0)
                        {
                            var sqlCodigoSolicitud = "SELECT codigo_solicitud, numero_orden, total FROM aocr_or_orden WHERE id = @ordenId";
                            using (var cmdCodigo = new NpgsqlCommand(sqlCodigoSolicitud, conn, tx))
                            {
                                cmdCodigo.Parameters.AddWithValue("@ordenId", ordenId);
                                using (var readerCodigo = cmdCodigo.ExecuteReader())
                                {
                                    if (readerCodigo.Read())
                                    {
                                        if (readerCodigo["codigo_solicitud"] != DBNull.Value)
                                        {
                                            var parsedCodigo = ParseIntOrDefault(readerCodigo["codigo_solicitud"].ToString());
                                            if (parsedCodigo > 0)
                                            {
                                                codigoSolicitud = parsedCodigo;
                                            }
                                        }

                                        if (readerCodigo["numero_orden"] != DBNull.Value)
                                        {
                                            numeroOrden = readerCodigo["numero_orden"].ToString();
                                        }

                                        if (readerCodigo["total"] != DBNull.Value)
                                        {
                                            totalOrden = Convert.ToDecimal(readerCodigo["total"]);
                                        }
                                    }
                                }
                            }

                            var sqlGet = @"SELECT codigo_pago
                                           FROM aocr_tbpago
                                           WHERE codigo_solicitud = @ordenId
                                              OR codigo_solicitud = @codigoSolicitud
                                           ORDER BY
                                               CASE
                                                   WHEN codigo_solicitud = @codigoSolicitud THEN 0
                                                   WHEN codigo_solicitud = @ordenId THEN 1
                                                   ELSE 2
                                               END,
                                               fecha_pago DESC,
                                               codigo_pago DESC
                                           LIMIT 1";
                            using (var cmdGet = new NpgsqlCommand(sqlGet, conn, tx))
                            {
                                cmdGet.Parameters.AddWithValue("@ordenId", ordenId);
                                cmdGet.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                                var obj = cmdGet.ExecuteScalar();
                                if (obj != null && obj != DBNull.Value) targetPagoId = Convert.ToInt32(obj);
                            }
                        }

                        var estadoPagoNormalizado = (estadoPago ?? string.Empty).Trim().ToUpperInvariant();
                        var requierePago = estadoPagoNormalizado != "ANULADO" && estadoPagoNormalizado != "RECHAZADO";
                        if (targetPagoId == 0 && requierePago)
                        {
                            try
                            {
                                var numeroFacturaAuto = string.IsNullOrWhiteSpace(numeroOrden)
                                    ? ("ORDEN-" + ordenId.ToString())
                                    : numeroOrden.Trim();
                                var montoAuto = totalOrden > 0m ? totalOrden : 0.01m;

                                targetPagoId = CrearPagoValidadoDesdeOrden(
                                    conn,
                                    tx,
                                    codigoSolicitud,
                                    numeroFacturaAuto,
                                    montoAuto,
                                    usuario,
                                    observaciones,
                                    null);

                                _logger.LogInfo(
                                    "ActualizarPagoYEstadoTransaccional: pago auto-creado para ordenId={0}, codigoSolicitud={1}, pagoId={2}",
                                    ordenId,
                                    codigoSolicitud,
                                    targetPagoId);
                            }
                            catch (Exception exAutoPago)
                            {
                                tx.Rollback();
                                err = "No se encontró pago para validar y no se pudo generar uno automáticamente. " + exAutoPago.Message;
                                return false;
                            }
                        }

                        if (targetPagoId > 0)
                        {
                            var sqlUpdatePago = "UPDATE aocr_tbpago SET estado = @estado, fecha_validacion = @fecha, validado_por = @usuario, observaciones = @obs WHERE codigo_pago = @pagoId";
                            using (var cmdUpd = new NpgsqlCommand(sqlUpdatePago, conn, tx))
                            {
                                cmdUpd.Parameters.AddWithValue("@estado", estadoPago);
                                cmdUpd.Parameters.AddWithValue("@fecha", DateTime.Now);
                                cmdUpd.Parameters.AddWithValue("@usuario", usuario ?? (object)DBNull.Value);
                                cmdUpd.Parameters.AddWithValue("@obs", (object)observaciones ?? DBNull.Value);
                                cmdUpd.Parameters.AddWithValue("@pagoId", targetPagoId);
                                var rows = cmdUpd.ExecuteNonQuery();
                                if (rows <= 0)
                                {
                                    tx.Rollback();
                                    err = "Fallo al actualizar pago";
                                    return false;
                                }
                            }
                        }

                        var sqlUpdateOrden = "UPDATE aocr_or_orden SET estado = @estado WHERE id = @id";
                        using (var cmdOrd = new NpgsqlCommand(sqlUpdateOrden, conn, tx))
                        {
                            cmdOrd.Parameters.AddWithValue("@estado", nuevoEstadoOrden);
                            cmdOrd.Parameters.AddWithValue("@id", ordenId);
                            var rowsOrd = cmdOrd.ExecuteNonQuery();
                            if (rowsOrd <= 0)
                            {
                                tx.Rollback();
                                err = "Fallo al actualizar orden";
                                return false;
                            }
                        }

                        if (string.Equals((estadoPago ?? string.Empty).Trim(), EstadoPago.Validado, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals((nuevoEstadoOrden ?? string.Empty).Trim(), EstadoOrden.Facturada, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals((nuevoEstadoOrden ?? string.Empty).Trim(), EstadoOrden.Completada, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals((nuevoEstadoOrden ?? string.Empty).Trim(), EstadoOrden.Pagada, StringComparison.OrdinalIgnoreCase))
                        {
                            string detalleCambioSolicitud;
                            if (!ActualizarSolicitudPendienteAsignacionRt(conn, tx, codigoSolicitud, usuario, out detalleCambioSolicitud))
                            {
                                _logger.LogWarning(
                                    "ActualizarPagoYEstadoTransaccional: transicion solicitud omitida (no critica). OrdenId={0}, CodigoSolicitud={1}, Detalle={2}",
                                    ordenId, codigoSolicitud, detalleCambioSolicitud ?? string.Empty);
                            }
                        }

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                err = ex.Message;
                _logger.LogError(ex, "Error en ActualizarPagoYEstadoTransaccional");
                return false;
            }
        }

        public bool AprobarPagoConFacturaTransaccional(
            int ordenId,
            int? pagoId,
            string usuarioAprobador,
            string numeroFactura,
            string autorizacionFactura,
            DateTime fechaEmision,
            decimal subtotal,
            decimal iva,
            decimal total,
            string observaciones,
            string fileName,
            string contentType,
            long fileSize,
            string filePath,
            out string err,
            out bool idempotente,
            out string advertencia)
        {
            err = null;
            advertencia = null;
            idempotente = false;

            if (ordenId <= 0)
            {
                err = "Orden inválida.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(numeroFactura))
            {
                err = "El número de factura es obligatorio.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(fileName))
            {
                err = "Debe adjuntar una factura válida.";
                return false;
            }

            if (total <= 0m)
            {
                err = "El total de la factura debe ser mayor a cero.";
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        EnsureFacturacionSchema(conn, tx);

                        string numeroOrden = null;
                        string estadoOrden = null;
                        string correoDestino = null;
                        string nombreDestino = null;
                        string observacionActual = null;
                        int codigoSolicitud = 0;
                        decimal totalOrden = total;

                        const string sqlOrden = @"
                            SELECT id, numero_orden, estado, codigo_solicitud, correo, compania, observacion, total
                            FROM aocr_or_orden
                            WHERE id = @orden_id
                            FOR UPDATE";

                        using (var cmdOrden = new NpgsqlCommand(sqlOrden, conn, tx))
                        {
                            cmdOrden.Parameters.AddWithValue("@orden_id", ordenId);
                            using (var reader = cmdOrden.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tx.Rollback();
                                    err = "No se encontró la orden especificada.";
                                    return false;
                                }

                                numeroOrden = reader["numero_orden"] != DBNull.Value ? reader["numero_orden"].ToString() : null;
                                estadoOrden = reader["estado"] != DBNull.Value ? reader["estado"].ToString() : null;
                                correoDestino = reader["correo"] != DBNull.Value ? reader["correo"].ToString() : null;
                                nombreDestino = reader["compania"] != DBNull.Value ? reader["compania"].ToString() : null;
                                observacionActual = reader["observacion"] != DBNull.Value ? reader["observacion"].ToString() : null;
                                if (reader["codigo_solicitud"] != DBNull.Value)
                                {
                                    codigoSolicitud = ParseIntOrDefault(reader["codigo_solicitud"].ToString());
                                }
                                if (reader["total"] != DBNull.Value)
                                {
                                    totalOrden = Convert.ToDecimal(reader["total"]);
                                }
                            }
                        }

                        if (codigoSolicitud <= 0)
                        {
                            codigoSolicitud = ordenId;
                        }

                        var estadoNormalizado = (estadoOrden ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
                        if (EstadoOrden.EsPagado(estadoNormalizado))
                        {
                            idempotente = true;
                            tx.Commit();
                            return true;
                        }

                        var esPendiente = !string.IsNullOrWhiteSpace(estadoNormalizado) && estadoNormalizado.StartsWith("PENDIENTE");
                        if (estadoNormalizado != "PROCESADA" && !esPendiente)
                        {
                            tx.Rollback();
                            err = "La orden no está en un estado válido para aprobar el pago.";
                            return false;
                        }

                        int pagoObjetivoId = pagoId.HasValue ? pagoId.Value : 0;
                        if (pagoObjetivoId > 0)
                        {
                            const string sqlPagoById = @"SELECT codigo_pago FROM aocr_tbpago WHERE codigo_pago = @pago_id FOR UPDATE";
                            using (var cmdPago = new NpgsqlCommand(sqlPagoById, conn, tx))
                            {
                                cmdPago.Parameters.AddWithValue("@pago_id", pagoObjetivoId);
                                var existePago = cmdPago.ExecuteScalar();
                                if (existePago == null || existePago == DBNull.Value)
                                {
                                    tx.Rollback();
                                    err = "No se encontró el pago especificado.";
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            const string sqlPago = @"
                                SELECT codigo_pago
                                FROM aocr_tbpago
                                WHERE codigo_solicitud = @orden_id
                                   OR codigo_solicitud = @codigo_solicitud
                                ORDER BY fecha_pago DESC, codigo_pago DESC
                                LIMIT 1
                                FOR UPDATE";
                            using (var cmdPago = new NpgsqlCommand(sqlPago, conn, tx))
                            {
                                cmdPago.Parameters.AddWithValue("@orden_id", ordenId);
                                cmdPago.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                                var valorPago = cmdPago.ExecuteScalar();
                                if (valorPago == null || valorPago == DBNull.Value)
                                {
                                    pagoObjetivoId = CrearPagoValidadoDesdeOrden(
                                        conn,
                                        tx,
                                        codigoSolicitud,
                                        numeroFactura,
                                        totalOrden > 0m ? totalOrden : total,
                                        usuarioAprobador,
                                        observaciones,
                                        filePath);
                                }
                                else
                                {
                                    pagoObjetivoId = Convert.ToInt32(valorPago);
                                }
                            }
                        }

                        const string sqlFacturaExistente = @"
                            SELECT id
                            FROM aocr_tb_factura_pago
                            WHERE orden_id = @orden_id OR pago_id = @pago_id
                            LIMIT 1
                            FOR UPDATE";
                        using (var cmdFacturaExistente = new NpgsqlCommand(sqlFacturaExistente, conn, tx))
                        {
                            cmdFacturaExistente.Parameters.AddWithValue("@orden_id", ordenId);
                            cmdFacturaExistente.Parameters.AddWithValue("@pago_id", pagoObjetivoId);
                            var facturaExistente = cmdFacturaExistente.ExecuteScalar();
                            if (facturaExistente != null && facturaExistente != DBNull.Value)
                            {
                                idempotente = true;
                                tx.Commit();
                                return true;
                            }
                        }

                        const string sqlInsertFactura = @"
                            INSERT INTO aocr_tb_factura_pago
                            (
                                orden_id,
                                pago_id,
                                numero_factura,
                                autorizacion_factura,
                                fecha_emision,
                                subtotal,
                                iva,
                                total,
                                observaciones,
                                file_name,
                                content_type,
                                file_size,
                                file_path,
                                creado_por,
                                creado_en
                            )
                            VALUES
                            (
                                @orden_id,
                                @pago_id,
                                @numero_factura,
                                @autorizacion_factura,
                                @fecha_emision,
                                @subtotal,
                                @iva,
                                @total,
                                @observaciones,
                                @file_name,
                                @content_type,
                                @file_size,
                                @file_path,
                                @creado_por,
                                NOW()
                            )";

                        using (var cmdInsertFactura = new NpgsqlCommand(sqlInsertFactura, conn, tx))
                        {
                            cmdInsertFactura.Parameters.AddWithValue("@orden_id", ordenId);
                            cmdInsertFactura.Parameters.AddWithValue("@pago_id", pagoObjetivoId);
                            cmdInsertFactura.Parameters.AddWithValue("@numero_factura", numeroFactura.Trim());
                            cmdInsertFactura.Parameters.AddWithValue("@autorizacion_factura", (object)(autorizacionFactura ?? string.Empty));
                            cmdInsertFactura.Parameters.AddWithValue("@fecha_emision", fechaEmision);
                            cmdInsertFactura.Parameters.AddWithValue("@subtotal", subtotal);
                            cmdInsertFactura.Parameters.AddWithValue("@iva", iva);
                            cmdInsertFactura.Parameters.AddWithValue("@total", total);
                            cmdInsertFactura.Parameters.AddWithValue("@observaciones", (object)(observaciones ?? string.Empty));
                            cmdInsertFactura.Parameters.AddWithValue("@file_name", fileName);
                            cmdInsertFactura.Parameters.AddWithValue("@content_type", contentType ?? "application/octet-stream");
                            cmdInsertFactura.Parameters.AddWithValue("@file_size", fileSize);
                            cmdInsertFactura.Parameters.AddWithValue("@file_path", filePath);
                            cmdInsertFactura.Parameters.AddWithValue("@creado_por", (object)(usuarioAprobador ?? "FINANCIERO"));

                            var rowsFactura = cmdInsertFactura.ExecuteNonQuery();
                            if (rowsFactura <= 0)
                            {
                                tx.Rollback();
                                err = "No se pudo registrar la factura.";
                                return false;
                            }
                        }

                        const string sqlUpdatePago = @"
                            UPDATE aocr_tbpago
                            SET
                                estado = @estado,
                                fecha_validacion = @fecha_validacion,
                                validado_por = @validado_por,
                                observaciones = CASE
                                    WHEN @observaciones IS NULL OR TRIM(@observaciones) = '' THEN observaciones
                                    ELSE @observaciones
                                END
                            WHERE codigo_pago = @pago_id";
                        using (var cmdUpdatePago = new NpgsqlCommand(sqlUpdatePago, conn, tx))
                        {
                            cmdUpdatePago.Parameters.AddWithValue("@estado", EstadoPago.Validado);
                            cmdUpdatePago.Parameters.AddWithValue("@fecha_validacion", DateTime.Now);
                            cmdUpdatePago.Parameters.AddWithValue("@validado_por", (object)(usuarioAprobador ?? "FINANCIERO"));
                            cmdUpdatePago.Parameters.AddWithValue("@observaciones", (object)(observaciones ?? string.Empty));
                            cmdUpdatePago.Parameters.AddWithValue("@pago_id", pagoObjetivoId);

                            var rowsPago = cmdUpdatePago.ExecuteNonQuery();
                            if (rowsPago <= 0)
                            {
                                tx.Rollback();
                                err = "No se pudo actualizar el estado del pago.";
                                return false;
                            }
                        }

                        var notaAprobacion = string.Format(
                            "Pago aprobado con factura {0} ({1:dd/MM/yyyy}).",
                            numeroFactura.Trim(),
                            fechaEmision);
                        var observacionFinal = string.IsNullOrWhiteSpace(observacionActual)
                            ? notaAprobacion
                            : observacionActual + " | " + notaAprobacion;

                        const string sqlUpdateOrden = @"
                            UPDATE aocr_or_orden
                            SET
                                estado = @estado,
                                observacion = @observacion
                            WHERE id = @orden_id";
                        using (var cmdUpdateOrden = new NpgsqlCommand(sqlUpdateOrden, conn, tx))
                        {
                            cmdUpdateOrden.Parameters.AddWithValue("@estado", EstadoOrden.Facturada);
                            cmdUpdateOrden.Parameters.AddWithValue("@observacion", observacionFinal);
                            cmdUpdateOrden.Parameters.AddWithValue("@orden_id", ordenId);

                            var rowsOrden = cmdUpdateOrden.ExecuteNonQuery();
                            if (rowsOrden <= 0)
                            {
                                tx.Rollback();
                                err = "No se pudo actualizar el estado de la orden.";
                                return false;
                            }
                        }

                        string detalleCambioSolicitudPago;
                        if (!ActualizarSolicitudPendienteAsignacionRt(conn, tx, codigoSolicitud, usuarioAprobador, out detalleCambioSolicitudPago))
                        {
                            _logger.LogWarning(
                                "AprobarPagoConFacturaTransaccional: transicion solicitud omitida (no critica). OrdenId={0}, CodigoSolicitud={1}, Detalle={2}",
                                ordenId, codigoSolicitud, detalleCambioSolicitudPago ?? string.Empty);
                        }

                        var eventKey = string.Format("ORDEN_{0}_FACTURA_REGISTRADA", ordenId);
                        var asuntoCorreo = string.Format("Factura registrada - Orden {0}", numeroOrden ?? ordenId.ToString());
                        var cuerpoCorreo = ConstruirCorreoFacturaRegistrada(
                            nombreDestino,
                            numeroOrden,
                            numeroFactura,
                            fechaEmision,
                            total,
                            observaciones);

                        var queueService = new EmailQueueService(_connectionString);
                        bool duplicateEvent;

                        if (string.IsNullOrWhiteSpace(correoDestino))
                        {
                            advertencia = "La orden no tiene correo del solicitante. Se registró el pago, pero el correo quedó en ERROR.";
                            var queueItemError = new EmailQueueItem
                            {
                                Para = "no-email@invalid.local",
                                Asunto = asuntoCorreo,
                                Cuerpo = cuerpoCorreo,
                                Estado = "ERROR",
                                OrdenId = codigoSolicitud,
                                EventKey = eventKey
                            };

                            queueService.EncolarConAdjuntosEnTransaccion(
                                conn,
                                tx,
                                queueItemError,
                                null,
                                out duplicateEvent);
                        }
                        else
                        {
                            var queueItem = new EmailQueueItem
                            {
                                Para = correoDestino.Trim(),
                                ParaNombre = string.IsNullOrWhiteSpace(nombreDestino) ? "Solicitante" : nombreDestino.Trim(),
                                Asunto = asuntoCorreo,
                                Cuerpo = cuerpoCorreo,
                                Estado = "PENDIENTE",
                                OrdenId = codigoSolicitud,
                                EventKey = eventKey
                            };

                            var adjunto = new EmailAttachmentItem
                            {
                                FileName = fileName,
                                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                                FilePath = filePath,
                                FileSize = fileSize
                            };

                            queueService.EncolarConAdjuntosEnTransaccion(
                                conn,
                                tx,
                                queueItem,
                                new[] { adjunto },
                                out duplicateEvent);
                        }

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (PostgresException pgEx)
            {
                if (pgEx.SqlState == "23505" &&
                    (string.Equals(pgEx.ConstraintName, "uq_aocr_tb_factura_pago_orden", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(pgEx.ConstraintName, "uq_aocr_tb_factura_pago_pago", StringComparison.OrdinalIgnoreCase)))
                {
                    idempotente = true;
                    return true;
                }

                err = pgEx.MessageText ?? pgEx.Message;
                _logger.LogError(pgEx, "Error en AprobarPagoConFacturaTransaccional");
                return false;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                _logger.LogError(ex, "Error en AprobarPagoConFacturaTransaccional");
                return false;
            }
        }

        public bool RegistrarResultadoFr3(
            int ordenId,
            int? pagoId,
            FacturacionAS400Result resultadoFr3,
            string estadoFr3,
            string detalleError,
            string usuario,
            out string err)
        {
            err = null;

            if (ordenId <= 0)
            {
                err = "Orden inválida para registrar resultado FR3.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(estadoFr3))
            {
                err = "Estado FR3 no especificado.";
                return false;
            }

            var estadoNormalizado = estadoFr3.Trim().ToUpperInvariant();
            var fr3Numero = resultadoFr3 != null ? resultadoFr3.NumeroFr3 : null;
            var fr3Secuencial = resultadoFr3 != null ? resultadoFr3.Secuencial : 0m;
            var fr3Aeropuerto = resultadoFr3 != null ? resultadoFr3.Aeropuerto : null;
            var fr3Anio = resultadoFr3 != null ? resultadoFr3.Anio : null;
            var numeroOrden = string.Empty;
            var correoOrden = string.Empty;
            var nombreOrden = string.Empty;
            var codigoSolicitud = ordenId;

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        EnsureFacturacionSchema(conn, tx);

                        const string sqlUpdateFactura = @"
                            UPDATE aocr_tb_factura_pago
                            SET
                                pago_id = COALESCE(pago_id, @pago_id),
                                fr3_estado = @fr3_estado,
                                fr3_numero = CASE
                                    WHEN @fr3_numero IS NULL OR TRIM(@fr3_numero) = '' THEN fr3_numero
                                    ELSE @fr3_numero
                                END,
                                fr3_secuencial = CASE
                                    WHEN @fr3_secuencial > 0 THEN @fr3_secuencial
                                    ELSE fr3_secuencial
                                END,
                                fr3_aeropuerto = CASE
                                    WHEN @fr3_aeropuerto IS NULL OR TRIM(@fr3_aeropuerto) = '' THEN fr3_aeropuerto
                                    ELSE @fr3_aeropuerto
                                END,
                                fr3_anio = CASE
                                    WHEN @fr3_anio IS NULL OR TRIM(@fr3_anio) = '' THEN fr3_anio
                                    ELSE @fr3_anio
                                END,
                                fr3_error = @fr3_error,
                                fr3_generado_en = CASE
                                    WHEN @fr3_estado = 'FR3_GENERADO' THEN NOW()
                                    ELSE fr3_generado_en
                                END,
                                fr3_reintentos = COALESCE(fr3_reintentos, 0) + @retry_increment,
                                updated_at = NOW()
                            WHERE id = (
                                SELECT id
                                FROM aocr_tb_factura_pago
                                WHERE orden_id = @orden_id
                                ORDER BY
                                    CASE
                                        WHEN @pago_id IS NOT NULL AND pago_id = @pago_id THEN 0
                                        WHEN pago_id IS NULL THEN 1
                                        ELSE 2
                                    END,
                                    creado_en DESC,
                                    id DESC
                                LIMIT 1
                            )";

                        using (var cmdFactura = new NpgsqlCommand(sqlUpdateFactura, conn, tx))
                        {
                            cmdFactura.Parameters.AddWithValue("@fr3_estado", estadoNormalizado);
                            cmdFactura.Parameters.Add(new NpgsqlParameter("@fr3_numero", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object)fr3Numero ?? DBNull.Value });
                            cmdFactura.Parameters.AddWithValue("@fr3_secuencial", fr3Secuencial);
                            cmdFactura.Parameters.Add(new NpgsqlParameter("@fr3_aeropuerto", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object)fr3Aeropuerto ?? DBNull.Value });
                            cmdFactura.Parameters.Add(new NpgsqlParameter("@fr3_anio", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object)fr3Anio ?? DBNull.Value });
                            cmdFactura.Parameters.Add(new NpgsqlParameter("@fr3_error", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object)detalleError ?? DBNull.Value });
                            cmdFactura.Parameters.AddWithValue("@retry_increment", estadoNormalizado == "FR3_ERROR" ? 1 : 0);
                            cmdFactura.Parameters.AddWithValue("@orden_id", ordenId);
                            cmdFactura.Parameters.Add(new NpgsqlParameter("@pago_id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = pagoId.HasValue ? (object)pagoId.Value : DBNull.Value });

                            var rows = cmdFactura.ExecuteNonQuery();
                            if (rows <= 0)
                            {
                                string placeholderError;
                                if (TryCrearRegistroFacturaPlaceholderParaFr3(
                                    conn,
                                    tx,
                                    ordenId,
                                    pagoId,
                                    resultadoFr3,
                                    usuario,
                                    out placeholderError))
                                {
                                    rows = cmdFactura.ExecuteNonQuery();
                                }

                                if (rows <= 0)
                                {
                                    tx.Rollback();
                                    err = "No existe registro de factura asociado para actualizar estado FR3."
                                        + (string.IsNullOrWhiteSpace(placeholderError)
                                            ? string.Empty
                                            : (" " + placeholderError));
                                    return false;
                                }
                            }
                        }

                        const string sqlOrdenMeta = @"
                            SELECT
                                COALESCE(codigo_solicitud::int, @orden_id) AS codigo_solicitud,
                                numero_orden,
                                correo,
                                compania
                            FROM aocr_or_orden
                            WHERE id = @orden_id
                            LIMIT 1";
                        using (var cmdOrdenMeta = new NpgsqlCommand(sqlOrdenMeta, conn, tx))
                        {
                            cmdOrdenMeta.Parameters.AddWithValue("@orden_id", ordenId);
                            using (var reader = cmdOrdenMeta.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    codigoSolicitud = reader["codigo_solicitud"] != DBNull.Value
                                        ? Convert.ToInt32(reader["codigo_solicitud"])
                                        : ordenId;
                                    numeroOrden = reader["numero_orden"] != DBNull.Value
                                        ? reader["numero_orden"].ToString()
                                        : string.Empty;
                                    correoOrden = reader["correo"] != DBNull.Value
                                        ? reader["correo"].ToString()
                                        : string.Empty;
                                    nombreOrden = reader["compania"] != DBNull.Value
                                        ? reader["compania"].ToString()
                                        : string.Empty;
                                }
                            }
                        }

                        const string sqlUpdateOrden = @"
                            UPDATE aocr_or_orden
                            SET
                                estado = CASE
                                    WHEN @fr3_estado = 'FR3_GENERADO'
                                         AND UPPER(COALESCE(estado, '')) = 'FACTURADA'
                                    THEN 'COMPLETADA'
                                    ELSE estado
                                END,
                                observacion = CASE
                                    WHEN @nota IS NULL OR TRIM(@nota) = '' THEN observacion
                                    WHEN observacion IS NULL OR TRIM(observacion) = '' THEN @nota
                                    ELSE observacion || ' | ' || @nota
                                END
                            WHERE id = @orden_id";
                        using (var cmdOrden = new NpgsqlCommand(sqlUpdateOrden, conn, tx))
                        {
                            var nota = estadoNormalizado == "FR3_GENERADO"
                                ? string.Format("FR3 generado: {0}", fr3Numero ?? "N/D")
                                : string.Format("FR3 con error: {0}", detalleError ?? "Sin detalle");

                            cmdOrden.Parameters.AddWithValue("@nota", nota);
                            cmdOrden.Parameters.AddWithValue("@fr3_estado", estadoNormalizado);
                            cmdOrden.Parameters.AddWithValue("@orden_id", ordenId);
                            cmdOrden.ExecuteNonQuery();
                        }

                        var idempotencyKey = string.Format(
                            "FR3:{0}:{1}:{2}",
                            ordenId,
                            pagoId.HasValue ? pagoId.Value.ToString() : "0",
                            estadoNormalizado);

                        var payload = string.Format(
                            "{{\"ordenId\":{0},\"pagoId\":{1},\"estado\":\"{2}\",\"fr3Numero\":\"{3}\",\"fr3Aeropuerto\":\"{4}\",\"fr3Anio\":\"{5}\"}}",
                            ordenId,
                            pagoId.HasValue ? pagoId.Value.ToString() : "null",
                            estadoNormalizado,
                            (fr3Numero ?? string.Empty).Replace("\"", "'"),
                            (fr3Aeropuerto ?? string.Empty).Replace("\"", "'"),
                            (fr3Anio ?? string.Empty).Replace("\"", "'"));

                        const string sqlSyncLog = @"
                            INSERT INTO aocr_tb_sync_log
                            (
                                idempotency_key,
                                orden_id,
                                pago_id,
                                modulo,
                                operacion,
                                estado,
                                mensaje,
                                fr3_numero,
                                payload,
                                intentos,
                                usuario,
                                created_at
                            )
                            VALUES
                            (
                                @idempotency_key,
                                @orden_id,
                                @pago_id,
                                'FR3',
                                'DB2_SYNC',
                                @estado,
                                @mensaje,
                                @fr3_numero,
                                @payload::jsonb,
                                @intentos,
                                @usuario,
                                NOW()
                            )
                            ON CONFLICT (idempotency_key)
                            DO UPDATE SET
                                estado = EXCLUDED.estado,
                                mensaje = EXCLUDED.mensaje,
                                fr3_numero = EXCLUDED.fr3_numero,
                                payload = EXCLUDED.payload,
                                intentos = aocr_tb_sync_log.intentos + 1,
                                usuario = EXCLUDED.usuario,
                                updated_at = NOW()";

                        using (var cmdSync = new NpgsqlCommand(sqlSyncLog, conn, tx))
                        {
                            cmdSync.Parameters.AddWithValue("@idempotency_key", idempotencyKey);
                            cmdSync.Parameters.AddWithValue("@orden_id", ordenId);
                            cmdSync.Parameters.Add(new NpgsqlParameter("@pago_id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = pagoId.HasValue ? (object)pagoId.Value : DBNull.Value });
                            cmdSync.Parameters.AddWithValue("@estado", estadoNormalizado);
                            cmdSync.Parameters.AddWithValue("@mensaje", (object)detalleError ?? (estadoNormalizado == "FR3_GENERADO" ? "OK" : "SIN_DETALLE"));
                            cmdSync.Parameters.Add(new NpgsqlParameter("@fr3_numero", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object)fr3Numero ?? DBNull.Value });
                            cmdSync.Parameters.AddWithValue("@payload", payload);
                            cmdSync.Parameters.AddWithValue("@intentos", estadoNormalizado == "FR3_ERROR" ? 1 : 0);
                            cmdSync.Parameters.AddWithValue("@usuario", (object)usuario ?? "SISTEMA");
                            cmdSync.ExecuteNonQuery();
                        }

                        TryEncolarNotificacionFr3(
                            conn,
                            tx,
                            ordenId,
                            codigoSolicitud,
                            numeroOrden,
                            correoOrden,
                            nombreOrden,
                            estadoNormalizado,
                            fr3Numero,
                            detalleError);

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                err = ex.Message;
                _logger.LogError(ex, "Error en RegistrarResultadoFr3");
                return false;
            }
        }

        public bool AsegurarFacturaPagoParaFr3(
            int ordenId,
            int? pagoId,
            string numeroFactura,
            string usuario,
            out string err)
        {
            err = null;

            if (ordenId <= 0)
            {
                err = "Orden invalida para preparar trazabilidad FR3.";
                return false;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        EnsureFacturacionSchema(conn, tx);

                        var preResultado = new FacturacionAS400Result
                        {
                            NumeroFactura = numeroFactura
                        };

                        string placeholderError;
                        var ensured = TryCrearRegistroFacturaPlaceholderParaFr3(
                            conn,
                            tx,
                            ordenId,
                            pagoId,
                            preResultado,
                            usuario,
                            out placeholderError);

                        if (!ensured)
                        {
                            tx.Rollback();
                            err = string.IsNullOrWhiteSpace(placeholderError)
                                ? "No se pudo asegurar el registro base de factura para FR3."
                                : placeholderError;
                            return false;
                        }

                        const string sqlTouch = @"
                            UPDATE aocr_tb_factura_pago
                            SET
                                pago_id = CASE
                                    WHEN @pago_id IS NULL THEN pago_id
                                    WHEN pago_id IS NULL THEN @pago_id
                                    ELSE pago_id
                                END,
                                numero_factura = CASE
                                    WHEN @numero_factura IS NULL OR TRIM(@numero_factura) = '' THEN numero_factura
                                    ELSE @numero_factura
                                END,
                                fr3_estado = CASE
                                    WHEN fr3_estado IS NULL OR TRIM(fr3_estado) = '' THEN 'PENDIENTE'
                                    ELSE fr3_estado
                                END,
                                updated_at = NOW()
                            WHERE orden_id = @orden_id";

                        using (var cmdTouch = new NpgsqlCommand(sqlTouch, conn, tx))
                        {
                            cmdTouch.Parameters.AddWithValue("@orden_id", ordenId);
                            cmdTouch.Parameters.Add(new NpgsqlParameter("@pago_id", NpgsqlDbType.Integer)
                            {
                                Value = pagoId.HasValue ? (object)pagoId.Value : DBNull.Value
                            });
                            cmdTouch.Parameters.Add(new NpgsqlParameter("@numero_factura", NpgsqlDbType.Text)
                            {
                                Value = string.IsNullOrWhiteSpace(numeroFactura) ? (object)DBNull.Value : numeroFactura.Trim()
                            });

                            var rows = cmdTouch.ExecuteNonQuery();
                            if (rows <= 0)
                            {
                                tx.Rollback();
                                err = "No se encontro registro de factura para preparar trazabilidad FR3.";
                                return false;
                            }
                        }

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                err = ex.Message;
                _logger.LogError(ex, "Error en AsegurarFacturaPagoParaFr3");
                return false;
            }
        }

        private bool TryCrearRegistroFacturaPlaceholderParaFr3(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            int ordenId,
            int? pagoId,
            FacturacionAS400Result resultadoFr3,
            string usuario,
            out string err)
        {
            err = null;

            try
            {
                const string sqlExiste = @"
                    SELECT id
                    FROM aocr_tb_factura_pago
                    WHERE orden_id = @orden_id
                    LIMIT 1
                    FOR UPDATE";

                using (var cmdExiste = new NpgsqlCommand(sqlExiste, conn, tx))
                {
                    cmdExiste.Parameters.AddWithValue("@orden_id", ordenId);

                    var existe = cmdExiste.ExecuteScalar();
                    if (existe != null && existe != DBNull.Value)
                    {
                        return true;
                    }
                }

                var subtotalExpr = ExisteColumnaEnTabla(conn, tx, "aocr_or_orden", "subtotal")
                    ? "COALESCE(subtotal, 0)"
                    : "0::numeric";
                var ivaExpr = ExisteColumnaEnTabla(conn, tx, "aocr_or_orden", "iva")
                    ? "COALESCE(iva, 0)"
                    : "0::numeric";
                var totalExpr = ExisteColumnaEnTabla(conn, tx, "aocr_or_orden", "total")
                    ? "COALESCE(total, 0)"
                    : "0::numeric";

                var sqlOrden = string.Format(
                    @"SELECT numero_orden,
                             {0} AS subtotal,
                             {1} AS iva,
                             {2} AS total
                      FROM aocr_or_orden
                      WHERE id = @orden_id
                      LIMIT 1",
                    subtotalExpr,
                    ivaExpr,
                    totalExpr);

                string numeroOrden = null;
                decimal subtotal = 0m;
                decimal iva = 0m;
                decimal total = 0m;

                using (var cmdOrden = new NpgsqlCommand(sqlOrden, conn, tx))
                {
                    cmdOrden.Parameters.AddWithValue("@orden_id", ordenId);
                    using (var reader = cmdOrden.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            err = "No se encontró la orden para crear trazabilidad FR3 en aocr_tb_factura_pago.";
                            return false;
                        }

                        numeroOrden = reader["numero_orden"] != DBNull.Value
                            ? reader["numero_orden"].ToString()
                            : null;
                        subtotal = reader["subtotal"] != DBNull.Value
                            ? Convert.ToDecimal(reader["subtotal"])
                            : 0m;
                        iva = reader["iva"] != DBNull.Value
                            ? Convert.ToDecimal(reader["iva"])
                            : 0m;
                        total = reader["total"] != DBNull.Value
                            ? Convert.ToDecimal(reader["total"])
                            : 0m;
                    }
                }

                if (total <= 0m)
                {
                    total = subtotal + iva;
                }
                if (total <= 0m)
                {
                    total = 0.01m;
                }

                var numeroFactura = NormalizarNumeroFacturaPlaceholder(
                    resultadoFr3 != null ? resultadoFr3.NumeroFactura : null,
                    numeroOrden,
                    ordenId);

                int? pagoIdParaInsertar = pagoId;
                if (pagoId.HasValue && pagoId.Value > 0)
                {
                    const string sqlPagoEnUso = @"
                        SELECT orden_id
                        FROM aocr_tb_factura_pago
                        WHERE pago_id = @pago_id
                        LIMIT 1";

                    using (var cmdPagoEnUso = new NpgsqlCommand(sqlPagoEnUso, conn, tx))
                    {
                        cmdPagoEnUso.Parameters.AddWithValue("@pago_id", pagoId.Value);
                        var ordenPagoObj = cmdPagoEnUso.ExecuteScalar();
                        if (ordenPagoObj != null && ordenPagoObj != DBNull.Value)
                        {
                            var ordenPago = Convert.ToInt32(ordenPagoObj);
                            if (ordenPago != ordenId)
                            {
                                pagoIdParaInsertar = null;
                                _logger.LogWarning(
                                    "Pago ya vinculado a otra orden en aocr_tb_factura_pago. Se creara placeholder sin pago_id. ordenId={0}, pagoId={1}, ordenExistente={2}",
                                    ordenId,
                                    pagoId.Value,
                                    ordenPago);
                            }
                        }
                    }
                }

                const string sqlInsert = @"
                    INSERT INTO aocr_tb_factura_pago
                    (
                        orden_id,
                        pago_id,
                        numero_factura,
                        autorizacion_factura,
                        fecha_emision,
                        subtotal,
                        iva,
                        total,
                        observaciones,
                        file_name,
                        content_type,
                        file_size,
                        file_path,
                        creado_por,
                        creado_en,
                        fr3_estado,
                        updated_at
                    )
                    VALUES
                    (
                        @orden_id,
                        @pago_id,
                        @numero_factura,
                        NULL,
                        @fecha_emision,
                        @subtotal,
                        @iva,
                        @total,
                        @observaciones,
                        @file_name,
                        @content_type,
                        @file_size,
                        @file_path,
                        @creado_por,
                        NOW(),
                        @fr3_estado,
                        NOW()
                    )";

                using (var cmdInsert = new NpgsqlCommand(sqlInsert, conn, tx))
                {
                    cmdInsert.Parameters.AddWithValue("@orden_id", ordenId);
                    cmdInsert.Parameters.Add(new NpgsqlParameter("@pago_id", NpgsqlDbType.Integer)
                    {
                        Value = pagoIdParaInsertar.HasValue ? (object)pagoIdParaInsertar.Value : DBNull.Value
                    });
                    cmdInsert.Parameters.AddWithValue("@numero_factura", numeroFactura);
                    cmdInsert.Parameters.AddWithValue("@fecha_emision", DateTime.Today);
                    cmdInsert.Parameters.AddWithValue("@subtotal", subtotal);
                    cmdInsert.Parameters.AddWithValue("@iva", iva);
                    cmdInsert.Parameters.AddWithValue("@total", total);
                    cmdInsert.Parameters.AddWithValue("@observaciones", "Registro automático para trazabilidad FR3.");
                    cmdInsert.Parameters.AddWithValue("@file_name", "FR3_AUTOGENERADO.txt");
                    cmdInsert.Parameters.AddWithValue("@content_type", "text/plain");
                    cmdInsert.Parameters.AddWithValue("@file_size", 0L);
                    cmdInsert.Parameters.AddWithValue("@file_path", "AUTO://FR3");
                    cmdInsert.Parameters.AddWithValue("@creado_por", (object)usuario ?? "SISTEMA");
                    cmdInsert.Parameters.AddWithValue("@fr3_estado", "PENDIENTE");
                    cmdInsert.ExecuteNonQuery();
                }

                _logger.LogInfo(
                    "Se creó registro placeholder en aocr_tb_factura_pago para trazabilidad FR3. ordenId={0}, pagoId={1}, numeroFactura={2}",
                    ordenId,
                    pagoId.HasValue ? pagoId.Value.ToString() : "null",
                    numeroFactura);

                return true;
            }
            catch (PostgresException pgEx)
            {
                if (pgEx.SqlState == "23505")
                {
                    if (ExisteRegistroFacturaPagoPorOrden(conn, tx, ordenId))
                    {
                        return true;
                    }

                    err = "Conflicto de unicidad al crear placeholder FR3 y no existe registro asociado a la orden.";
                    _logger.LogWarning("No se pudo crear placeholder de factura para FR3. Detalle={0}", pgEx.Message);
                    return false;
                }

                err = pgEx.MessageText ?? pgEx.Message;
                _logger.LogWarning("No se pudo crear placeholder de factura para FR3. Detalle={0}", pgEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                _logger.LogWarning("No se pudo crear placeholder de factura para FR3. Detalle={0}", ex.Message);
                return false;
            }
        }

        private static bool ExisteRegistroFacturaPagoPorOrden(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            int ordenId)
        {
            const string sql = @"
                SELECT 1
                FROM aocr_tb_factura_pago
                WHERE orden_id = @orden_id
                LIMIT 1";

            using (var cmd = new NpgsqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@orden_id", ordenId);
                var value = cmd.ExecuteScalar();
                return value != null && value != DBNull.Value;
            }
        }

        private static bool ExisteColumnaEnTabla(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            string tableName,
            string columnName)
        {
            const string sql = @"
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @table_name
                  AND column_name = @column_name
                LIMIT 1";

            using (var cmd = new NpgsqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@table_name", tableName);
                cmd.Parameters.AddWithValue("@column_name", columnName);
                var value = cmd.ExecuteScalar();
                return value != null && value != DBNull.Value;
            }
        }

        private static string NormalizarNumeroFacturaPlaceholder(string numeroFactura, string numeroOrden, int ordenId)
        {
            var candidato = string.IsNullOrWhiteSpace(numeroFactura)
                ? (string.IsNullOrWhiteSpace(numeroOrden) ? string.Empty : numeroOrden.Trim())
                : numeroFactura.Trim();

            if (string.IsNullOrWhiteSpace(candidato))
            {
                candidato = "FR3-" + ordenId;
            }

            return candidato.Length <= 80
                ? candidato
                : candidato.Substring(0, 80);
        }

        private void TryEncolarNotificacionFr3(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            int ordenId,
            int codigoSolicitud,
            string numeroOrden,
            string correoSolicitante,
            string nombreSolicitante,
            string estadoFr3,
            string fr3Numero,
            string detalleError)
        {
            try
            {
                var destinatarios = new List<string>();

                if (!string.IsNullOrWhiteSpace(correoSolicitante))
                {
                    destinatarios.Add(correoSolicitante.Trim());
                }

                var adminEmailsRaw = System.Configuration.ConfigurationManager.AppSettings["AdminEmails"];
                if (!string.IsNullOrWhiteSpace(adminEmailsRaw))
                {
                    foreach (var email in adminEmailsRaw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var clean = email.Trim();
                        if (!string.IsNullOrWhiteSpace(clean))
                        {
                            destinatarios.Add(clean);
                        }
                    }
                }

                destinatarios = destinatarios
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (destinatarios.Count == 0)
                {
                    return;
                }

                var ordenLabel = !string.IsNullOrWhiteSpace(numeroOrden)
                    ? numeroOrden.Trim()
                    : ordenId.ToString();
                var estado = string.IsNullOrWhiteSpace(estadoFr3) ? "FR3_ERROR" : estadoFr3.Trim().ToUpperInvariant();
                var asunto = string.Equals(estado, "FR3_GENERADO", StringComparison.OrdinalIgnoreCase)
                    ? string.Format("FR3 generado - Orden {0}", ordenLabel)
                    : string.Format("FR3 con error - Orden {0}", ordenLabel);
                var cuerpo = ConstruirCuerpoNotificacionFr3(ordenLabel, estado, fr3Numero, detalleError);

                var queueService = new EmailQueueService(_connectionString);
                foreach (var destinatario in destinatarios)
                {
                    bool duplicateEvent;
                    var eventKey = string.Format(
                        "ORDEN_{0}_FR3_{1}_{2}",
                        ordenId,
                        estado,
                        NormalizarFragmentoEventKey(destinatario));

                    var item = new EmailQueueItem
                    {
                        Para = destinatario,
                        ParaNombre = string.IsNullOrWhiteSpace(nombreSolicitante) ? "Usuario AOCR" : nombreSolicitante.Trim(),
                        Asunto = asunto,
                        Cuerpo = cuerpo,
                        Estado = "PENDIENTE",
                        OrdenId = codigoSolicitud > 0 ? (int?)codigoSolicitud : ordenId,
                        EventKey = eventKey
                    };

                    queueService.EncolarConAdjuntosEnTransaccion(conn, tx, item, null, out duplicateEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo encolar notificacion FR3: " + ex.Message);
            }
        }

        private static string ConstruirCuerpoNotificacionFr3(
            string numeroOrden,
            string estadoFr3,
            string fr3Numero,
            string detalleError)
        {
            var fr3 = string.IsNullOrWhiteSpace(fr3Numero) ? "N/D" : fr3Numero.Trim();
            var detalle = string.IsNullOrWhiteSpace(detalleError) ? "Sin detalle adicional." : detalleError.Trim();

            if (string.Equals(estadoFr3, "FR3_GENERADO", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(
                    "<p>Estimado usuario,</p><p>La orden <strong>{0}</strong> completó la generación de FR3.</p><p><strong>FR3:</strong> {1}</p><p>Sistema AOCR</p>",
                    numeroOrden,
                    fr3);
            }

            return string.Format(
                "<p>Estimado usuario,</p><p>La orden <strong>{0}</strong> presentó un error en la generación FR3.</p><p><strong>Detalle:</strong> {1}</p><p>Sistema AOCR</p>",
                numeroOrden,
                detalle);
        }

        private static string NormalizarFragmentoEventKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "sin_destino";
            }

            var chars = value
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            var normalized = new string(chars);
            while (normalized.Contains("__"))
            {
                normalized = normalized.Replace("__", "_");
            }

            return normalized.Length <= 60
                ? normalized
                : normalized.Substring(0, 60);
        }

        private static void EnsureFacturacionSchema(NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS public.aocr_tb_factura_pago (
                    id SERIAL PRIMARY KEY,
                    orden_id INTEGER NOT NULL,
                    pago_id INTEGER,
                    numero_factura VARCHAR(80) NOT NULL,
                    autorizacion_factura VARCHAR(80),
                    fecha_emision DATE NOT NULL,
                    subtotal NUMERIC(18,2) NOT NULL,
                    iva NUMERIC(18,2) NOT NULL,
                    total NUMERIC(18,2) NOT NULL,
                    observaciones TEXT,
                    file_name VARCHAR(255) NOT NULL,
                    content_type VARCHAR(120) NOT NULL,
                    file_size BIGINT NOT NULL,
                    file_path TEXT NOT NULL,
                    creado_por VARCHAR(120),
                    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),
                    fr3_estado VARCHAR(30),
                    fr3_numero VARCHAR(80),
                    fr3_secuencial NUMERIC(18,0),
                    fr3_aeropuerto VARCHAR(10),
                    fr3_anio VARCHAR(4),
                    fr3_error TEXT,
                    fr3_generado_en TIMESTAMP,
                    fr3_reintentos INTEGER NOT NULL DEFAULT 0,
                    updated_at TIMESTAMP
                );

                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS orden_id INTEGER;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS pago_id INTEGER;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS numero_factura VARCHAR(80);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS autorizacion_factura VARCHAR(80);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fecha_emision DATE;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS subtotal NUMERIC(18,2);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS iva NUMERIC(18,2);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS total NUMERIC(18,2);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS observaciones TEXT;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS file_name VARCHAR(255);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS content_type VARCHAR(120);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS file_size BIGINT;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS file_path TEXT;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS creado_por VARCHAR(120);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS creado_en TIMESTAMP;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'uq_aocr_tb_factura_pago_orden'
                    ) THEN
                        ALTER TABLE public.aocr_tb_factura_pago
                        ADD CONSTRAINT uq_aocr_tb_factura_pago_orden UNIQUE (orden_id);
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'uq_aocr_tb_factura_pago_pago'
                    ) THEN
                        ALTER TABLE public.aocr_tb_factura_pago
                        ADD CONSTRAINT uq_aocr_tb_factura_pago_pago UNIQUE (pago_id);
                    END IF;
                END
                $$;

                CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_orden_id
                    ON public.aocr_tb_factura_pago(orden_id);

                CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_fecha_emision
                    ON public.aocr_tb_factura_pago(fecha_emision);

                CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_fr3_estado
                    ON public.aocr_tb_factura_pago(fr3_estado);

                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_estado VARCHAR(30);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_numero VARCHAR(80);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_secuencial NUMERIC(18,0);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_aeropuerto VARCHAR(10);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_anio VARCHAR(4);
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_error TEXT;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_generado_en TIMESTAMP;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_reintentos INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;

                CREATE TABLE IF NOT EXISTS public.aocr_tb_sync_log (
                    id BIGSERIAL PRIMARY KEY,
                    idempotency_key VARCHAR(200) NOT NULL,
                    orden_id INTEGER NOT NULL,
                    pago_id INTEGER,
                    modulo VARCHAR(50) NOT NULL,
                    operacion VARCHAR(100) NOT NULL,
                    estado VARCHAR(30) NOT NULL,
                    mensaje TEXT,
                    fr3_numero VARCHAR(80),
                    payload JSONB,
                    intentos INTEGER NOT NULL DEFAULT 0,
                    usuario VARCHAR(120),
                    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                    updated_at TIMESTAMP
                );

                CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tb_sync_log_idempotency
                    ON public.aocr_tb_sync_log(idempotency_key);

                CREATE INDEX IF NOT EXISTS idx_aocr_tb_sync_log_orden_estado
                    ON public.aocr_tb_sync_log(orden_id, estado, created_at DESC);

                ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS event_key VARCHAR(200);
                ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS error_message TEXT;
                ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS intentos INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;

                CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
                    ON public.email_queue(event_key)
                    WHERE event_key IS NOT NULL;

                CREATE TABLE IF NOT EXISTS public.email_attachment (
                    id SERIAL PRIMARY KEY,
                    email_queue_id INTEGER NOT NULL,
                    file_name VARCHAR(255) NOT NULL,
                    content_type VARCHAR(120) NOT NULL,
                    file_path TEXT NOT NULL,
                    file_size BIGINT,
                    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                    CONSTRAINT fk_email_attachment_queue
                        FOREIGN KEY (email_queue_id) REFERENCES public.email_queue(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_email_attachment_queue_id
                    ON public.email_attachment(email_queue_id);";

            using (var cmd = new NpgsqlCommand(sql, conn, tx))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static int CrearPagoValidadoDesdeOrden(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            int codigoSolicitud,
            string numeroFactura,
            decimal monto,
            string usuarioAprobador,
            string observaciones,
            string comprobanteRuta)
        {
            const string sqlInsertPago = @"
                INSERT INTO aocr_tbpago
                (
                    codigo_solicitud,
                    numero_factura,
                    monto,
                    moneda,
                    concepto,
                    metodo_pago,
                    estado,
                    fecha_pago,
                    fecha_validacion,
                    validado_por,
                    observaciones,
                    comprobante_ruta
                )
                VALUES
                (
                    @codigo_solicitud,
                    @numero_factura,
                    @monto,
                    'USD',
                    'Aprobacion financiera con registro de factura',
                    'NO_ESPECIFICADO',
                    @estado,
                    NOW(),
                    NOW(),
                    @validado_por,
                    @observaciones,
                    @comprobante_ruta
                )
                RETURNING codigo_pago";

            var montoPago = monto > 0m ? monto : 0.01m;
            var observacionPago = string.IsNullOrWhiteSpace(observaciones)
                ? "Pago registrado automaticamente al aprobar factura."
                : observaciones;

            using (var cmd = new NpgsqlCommand(sqlInsertPago, conn, tx))
            {
                cmd.Parameters.AddWithValue("@codigo_solicitud", codigoSolicitud);
                cmd.Parameters.AddWithValue("@numero_factura", (object)(numeroFactura ?? string.Empty));
                cmd.Parameters.AddWithValue("@monto", montoPago);
                cmd.Parameters.AddWithValue("@estado", EstadoPago.Validado);
                cmd.Parameters.AddWithValue("@validado_por", (object)(usuarioAprobador ?? "FINANCIERO"));
                cmd.Parameters.AddWithValue("@observaciones", (object)observacionPago);
                cmd.Parameters.AddWithValue("@comprobante_ruta", (object)(comprobanteRuta ?? string.Empty));

                var valor = cmd.ExecuteScalar();
                if (valor == null || valor == DBNull.Value)
                {
                    throw new InvalidOperationException("No se pudo crear un registro de pago para la orden.");
                }

                return Convert.ToInt32(valor);
            }
        }

        private string ConstruirCorreoFacturaRegistrada(
            string nombreDestino,
            string numeroOrden,
            string numeroFactura,
            DateTime fechaEmision,
            decimal total,
            string observaciones)
        {
            var nombre = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreDestino) ? "Solicitante" : nombreDestino.Trim());
            var orden = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(numeroOrden) ? "N/A" : numeroOrden.Trim());
            var factura = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(numeroFactura) ? "N/A" : numeroFactura.Trim());
            var fecha = WebUtility.HtmlEncode(fechaEmision.ToString("dd/MM/yyyy"));
            var totalFmt = WebUtility.HtmlEncode(total.ToString("N2"));
            var obs = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(observaciones) ? "Sin observaciones." : observaciones.Trim());

            return string.Format(@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; margin:0; padding:20px; background:#f5f7fa;'>
  <div style='max-width:680px; margin:0 auto; background:#fff; border:1px solid #d7dde6; border-radius:8px; padding:24px;'>
    <h2 style='margin-top:0; color:#1f3a5f;'>Confirmación de factura registrada</h2>
    <p>Estimado/a <strong>{0}</strong>,</p>
    <p>Se confirma el registro de su factura para la orden <strong>{1}</strong>.</p>
    <table style='width:100%; border-collapse:collapse; margin:16px 0;'>
      <tr><td style='padding:6px 0;'><strong>Número de factura:</strong></td><td style='padding:6px 0;'>{2}</td></tr>
      <tr><td style='padding:6px 0;'><strong>Fecha de emisión:</strong></td><td style='padding:6px 0;'>{3}</td></tr>
      <tr><td style='padding:6px 0;'><strong>Total:</strong></td><td style='padding:6px 0;'>${4}</td></tr>
      <tr><td style='padding:6px 0; vertical-align:top;'><strong>Observaciones:</strong></td><td style='padding:6px 0;'>{5}</td></tr>
    </table>
    <p>Factura adjunta a este correo.</p>
    <hr style='margin:18px 0; border:none; border-top:1px solid #e6eaf0;' />
    <p style='font-size:12px; color:#6b7280; margin:0;'>Mensaje automático del sistema AOCR.</p>
  </div>
</body>
</html>",
                nombre,
                orden,
                factura,
                fecha,
                totalFmt,
                obs);
        }

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

                    // Total de Ã³rdenes
                    var sqlTotal = filtroUsuario 
                        ? "SELECT COUNT(*) FROM aocr_or_orden WHERE codigo_usuario::text = @codigoUsuario"
                        : "SELECT COUNT(*) FROM aocr_or_orden";
                    using (var cmd = new NpgsqlCommand(sqlTotal, conn))
                    {
                        if (filtroUsuario) cmd.Parameters.AddWithValue("@codigoUsuario", codigoUsuarioStr);
                        estadisticas["total"] = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Ã“rdenes pagadas
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
                    // Contar Ã³rdenes del dÃ­a actual que tienen nÃºmero de orden generado
                    var sql = @"SELECT COUNT(*) FROM aocr_or_orden 
                               WHERE fecha_creacion::date = @fecha 
                               AND numero_orden IS NOT NULL 
                               AND numero_orden != ''";

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
                // En caso de error, usar un nÃºmero basado en timestamp
                count = (int)((DateTime.Now.Ticks / TimeSpan.TicksPerSecond) % 1000);
            }

            return Task.FromResult(count);
        }

        /// <summary>
        /// Obtiene el valor de la columna banco de forma segura, manejando si no existe
        /// </summary>
        private string GetSafeBanco(IDataReader reader)
        {
            try
            {
                var bancoOrdinal = reader.GetOrdinal("banco");
                return reader.IsDBNull(bancoOrdinal) ? null : reader.GetString(bancoOrdinal);
            }
            catch (IndexOutOfRangeException)
            {
                // La columna banco no existe en la tabla, intentar inferir desde mÃ©todo de pago
                System.Diagnostics.Debug.WriteLine("MapearPagoModel: Columna 'banco' no existe, intentando inferir desde mÃ©todo de pago");
                
                try
                {
                    var metodoPago = reader["metodo_pago"] != DBNull.Value ? reader["metodo_pago"].ToString() : null;
                    return InferirBancoDesdeMetodoPago(metodoPago);
                }
                catch
                {
                    return "NO_ESPECIFICADO";
                }
            }
        }
        
        /// <summary>
        /// Intenta inferir el banco basÃ¡ndose en el mÃ©todo de pago
        /// </summary>
        private string InferirBancoDesdeMetodoPago(string metodoPago)
        {
            if (string.IsNullOrWhiteSpace(metodoPago))
                return "NO_ESPECIFICADO";
                
            var metodo = metodoPago.ToUpperInvariant();
            
            // Mapeo comÃºn de mÃ©todos de pago a bancos
            if (metodo.Contains("PICHINCHA"))
                return "BANCO PICHINCHA";
            else if (metodo.Contains("GUAYAQUIL"))
                return "BANCO GUAYAQUIL";
            else if (metodo.Contains("PACIFICO"))
                return "BANCO DEL PACIFICO";
            else if (metodo.Contains("PRODUBANCO"))
                return "BANCO PRODUBANCO";
            else if (metodo.Contains("BOLIVARIANO"))
                return "BANCO BOLIVARIANO";
            else if (metodo.Contains("INTERNACIONAL"))
                return "BANCO INTERNACIONAL";
            else if (metodo.Contains("TRANSFERENCIA") || metodo.Contains("DEPOSITO"))
                return "TRANSFERENCIA_BANCARIA";
            else if (metodo.Contains("EFECTIVO"))
                return "PAGO_EFECTIVO";
            else if (metodo.Contains("CHEQUE"))
                return "PAGO_CHEQUE";
            else
                return "METODO: " + metodoPago;
        }

        /// <summary>
        /// Verifica si la columna banco existe en la tabla aocr_tbpago
        /// </summary>
        private bool VerificarColumnaBanco(NpgsqlConnection conn)
        {
            try
            {
                var sql = @"SELECT COUNT(*) FROM information_schema.columns 
                            WHERE table_name = 'aocr_tbpago' AND column_name = 'banco'";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    var count = Convert.ToInt32(cmd.ExecuteScalar());
                    var existe = count > 0;
                    System.Diagnostics.Debug.WriteLine($"VerificarColumnaBanco: La columna 'banco' existe = {existe}");
                    return existe;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando columna banco: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// MÃ©todo temporal para agregar la columna banco a la tabla aocr_tbpago
        /// Este mÃ©todo debe ser ejecutado una sola vez por un administrador
        /// </summary>
        public bool AgregarColumnaBancoTemporal()
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    
                    // Verificar si la columna ya existe
                    if (VerificarColumnaBanco(conn))
                    {
                        System.Diagnostics.Debug.WriteLine("La columna banco ya existe");
                        return true;
                    }
                    
                    // Agregar la columna
                    var sqlAgregar = "ALTER TABLE aocr_tbpago ADD COLUMN banco VARCHAR(255);";
                    using (var cmd = new NpgsqlCommand(sqlAgregar, conn))
                    {
                        cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("Columna banco agregada exitosamente");
                    }
                    
                    // Actualizar registros existentes
                    var sqlActualizar = "UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL;";
                    using (var cmd = new NpgsqlCommand(sqlActualizar, conn))
                    {
                        var filasActualizadas = cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"Actualizadas {filasActualizadas} filas con valor por defecto");
                    }
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error agregando columna banco");
                System.Diagnostics.Debug.WriteLine($"Error agregando columna banco: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si ya existe un nÃºmero de orden en la base de datos
        /// </summary>
        public bool ExisteNumeroOrden(string numeroOrden)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "SELECT COUNT(*) FROM aocr_or_orden WHERE numero_orden = @numeroOrden";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden ?? "");
                        var count = Convert.ToInt32(cmd.ExecuteScalar());
                        
                        System.Diagnostics.Debug.WriteLine($"ExisteNumeroOrden: numero={numeroOrden}, existe={count > 0}");
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExisteNumeroOrden");
                System.Diagnostics.Debug.WriteLine($"Error en ExisteNumeroOrden: {ex.Message}");
                return false; // En caso de error, asumir que no existe
            }
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

                    AplicarTotalesNormalizadosPorDetalle(conn, ordenes);
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

        private static bool ActualizarSolicitudPendienteAsignacionRt(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            int codigoSolicitud,
            string usuario,
            out string detalle)
        {
            detalle = string.Empty;

            if (codigoSolicitud <= 0)
            {
                detalle = "Código de solicitud inválido.";
                return false;
            }

            string estadoActual;
            const string sqlEstado = @"
                SELECT estado
                FROM aocr_tbsolicitud
                WHERE codigo_solicitud = @codigoSolicitud
                  AND deleted_at IS NULL
                FOR UPDATE;";

            using (var cmdEstado = new NpgsqlCommand(sqlEstado, conn, tx))
            {
                cmdEstado.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                var value = cmdEstado.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    detalle = "La solicitud asociada no existe o no está disponible.";
                    return false;
                }

                estadoActual = value.ToString();
            }

            var estadoNormalizado = EstadoSolicitud.Normalizar(estadoActual);
            if (!string.Equals(estadoNormalizado, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(estadoNormalizado, EstadoSolicitud.DocumentacionCompleta, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(estadoNormalizado, EstadoSolicitud.PendienteAsignacionRT, StringComparison.OrdinalIgnoreCase))
            {
                detalle = "Estado actual de solicitud no permite transición automática: " + (estadoActual ?? "");
                return false;
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.PendienteAsignacionRT, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            const string sqlUpdateSolicitud = @"
                UPDATE aocr_tbsolicitud
                SET estado = @estado,
                    updated_at = NOW(),
                    updated_by = @updatedBy
                WHERE codigo_solicitud = @codigoSolicitud
                  AND deleted_at IS NULL;";

            using (var cmdUpdate = new NpgsqlCommand(sqlUpdateSolicitud, conn, tx))
            {
                cmdUpdate.Parameters.AddWithValue("@estado", EstadoSolicitud.PendienteAsignacionRT);
                cmdUpdate.Parameters.AddWithValue("@updatedBy", (object)(usuario ?? "FINANCIERO") ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("@codigoSolicitud", codigoSolicitud);
                var rows = cmdUpdate.ExecuteNonQuery();
                if (rows <= 0)
                {
                    detalle = "No se pudo actualizar el estado de la solicitud en base de datos.";
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region MÃ©todos Async Adicionales para Controller

        /// <summary>
        /// VersiÃ³n async de ListarPorUsuarioModel
        /// </summary>
        public Task<List<OrdenRecaudacionModel>> ListarPorUsuarioModelAsync(int codigoUsuario, string estado)
        {
            return Task.Run(() => ListarPorUsuarioModel(codigoUsuario, estado));
        }

        /// <summary>
        /// VersiÃ³n async de ObtenerOrdenPorIdModel
        /// </summary>
        public Task<OrdenRecaudacionModel> ObtenerOrdenPorIdModelAsync(int id)
        {
            return Task.Run(() => ObtenerOrdenPorIdModel(id));
        }

        /// <summary>
        /// VersiÃ³n async de ObtenerPagosPorOrden
        /// </summary>
        public Task<List<PagoModel>> ObtenerPagosPorOrdenAsync(int ordenId)
        {
            return Task.Run(() => ObtenerPagosPorOrden(ordenId));
        }

        /// <summary>
        /// VersiÃ³n async de CambiarEstadoOrden
        /// </summary>
        public Task<bool> CambiarEstadoOrdenAsync(int id, string nuevoEstado)
        {
            return Task.Run(() => CambiarEstadoOrden(id, nuevoEstado));
        }

        /// <summary>
        /// VersiÃ³n async de ActualizarOrden con OrdenRecaudacionModel
        /// </summary>
        public Task<bool> ActualizarOrdenModelAsync(OrdenRecaudacionModel orden)
        {
            return Task.Run(() => ActualizarOrden(orden));
        }

        /// <summary>
        /// VersiÃ³n async de Insertar
        /// </summary>
        public Task<int> InsertarAsync(OrdenRecaudacion orden)
        {
            return Task.Run(() => Insertar(orden));
        }

        #endregion
    }
}





