using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using CapaDatos.Entidades;
using CapaDatos.Infrastructure;
using CapaDatos.Interfaces;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO para Órdenes de Recaudación con SQL parametrizado y transacciones seguras
    /// </summary>
    public class OrdenRecaudacionDAO : BaseDAO, IOrdenRecaudacionRepository
    {
        #region Constructor

        public OrdenRecaudacionDAO(string connectionString) : base(connectionString)
        {
        }

        #endregion

        #region Consultas

        public async Task<OrdenRecaudacion> ObtenerPorIdAsync(int id)
        {
            const string sql = @"
                SELECT o.*, 
                       c.nombre AS contribuyente_nombre,
                       c.ruc_cedula AS contribuyente_ruc,
                       c.correo AS contribuyente_correo,
                       con.nombre AS concepto_nombre
                FROM ordenes_recaudacion o
                LEFT JOIN contribuyentes c ON o.contribuyente_id = c.id
                LEFT JOIN conceptos con ON o.concepto_id = con.id
                WHERE o.id = @id AND o.activo = true";

            return ExecuteWithConnection(conn =>
            {
                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapearOrden(reader);
                        }
                        return null;
                    }
                }
            });
        }

        public async Task<IEnumerable<OrdenRecaudacion>> ObtenerTodosAsync()
        {
            const string sql = @"
                SELECT o.*, 
                       c.nombre AS contribuyente_nombre,
                       c.ruc_cedula AS contribuyente_ruc,
                       c.correo AS contribuyente_correo,
                       con.nombre AS concepto_nombre
                FROM ordenes_recaudacion o
                LEFT JOIN contribuyentes c ON o.contribuyente_id = c.id
                LEFT JOIN conceptos con ON o.concepto_id = con.id
                WHERE o.activo = true
                ORDER BY o.fecha_creacion DESC";

            return ExecuteWithConnection(conn =>
            {
                var lista = new List<OrdenRecaudacion>();

                using (var cmd = CreateCommand(conn, sql))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(MapearOrden(reader));
                    }
                }

                return lista;
            });
        }

        public async Task<IEnumerable<OrdenRecaudacion>> ObtenerPorEstadoAsync(string estado)
        {
            const string sql = @"
                SELECT o.*, 
                       c.nombre AS contribuyente_nombre,
                       c.ruc_cedula AS contribuyente_ruc,
                       c.correo AS contribuyente_correo,
                       con.nombre AS concepto_nombre
                FROM ordenes_recaudacion o
                LEFT JOIN contribuyentes c ON o.contribuyente_id = c.id
                LEFT JOIN conceptos con ON o.concepto_id = con.id
                WHERE o.estado = @estado AND o.activo = true
                ORDER BY o.fecha_creacion DESC";

            return ExecuteWithConnection(conn =>
            {
                var lista = new List<OrdenRecaudacion>();

                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@estado", estado, NpgsqlDbType.Varchar);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapearOrden(reader));
                        }
                    }
                }

                return lista;
            });
        }

        public async Task<int> ObtenerConsecutivoDiarioAsync(DateTime fecha)
        {
            const string sql = @"
                SELECT COALESCE(MAX(CAST(SUBSTRING(numero_orden FROM '\d{4}$') AS INTEGER)), 0)
                FROM ordenes_recaudacion
                WHERE fecha_creacion::date = @fecha::date";

            return ExecuteWithConnection(conn =>
            {
                return ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@fecha", fecha.Date, NpgsqlDbType.Date);
                });
            });
        }

        #endregion

        #region Crear con Transacción

        /// <summary>
        /// Crea una orden completa con detalles en una transacción
        /// </summary>
        public async Task<int> CrearAsync(OrdenRecaudacion orden)
        {
            return ExecuteInTransaction<int>((conn, trans) =>
            {
                // 1. Insertar orden principal
                var ordenId = InsertarOrden(conn, trans, orden);

                // 2. Insertar detalles si existen
                if (orden.Detalles != null)
                {
                    foreach (var detalle in orden.Detalles)
                    {
                        detalle.OrdenId = ordenId;
                        InsertarDetalle(conn, trans, detalle);
                    }
                }

                // 3. Registrar estado inicial en historial
                InsertarHistorialEstado(conn, trans, ordenId, "CREADA", orden.Estado, orden.UsuarioCreacion);

                return ordenId;
            });
        }

        private int InsertarOrden(NpgsqlConnection conn, NpgsqlTransaction trans, OrdenRecaudacion orden)
        {
            const string sql = @"
                INSERT INTO ordenes_recaudacion (
                    numero_orden, solicitud_id, concepto_id, contribuyente_id,
                    subtotal, iva, total, observaciones, estado,
                    fecha_creacion, usuario_creacion, activo
                ) VALUES (
                    @numero_orden, @solicitud_id, @concepto_id, @contribuyente_id,
                    @subtotal, @iva, @total, @observaciones, @estado,
                    @fecha_creacion, @usuario_creacion, @activo
                ) RETURNING id";

            using (var cmd = CreateCommand(conn, sql, trans))
            {
                AddParameter(cmd, "@numero_orden", orden.NumeroOrden, NpgsqlDbType.Varchar);
                AddParameter(cmd, "@solicitud_id", orden.SolicitudId > 0 ? (object)orden.SolicitudId : DBNull.Value, NpgsqlDbType.Integer);
                AddParameter(cmd, "@concepto_id", orden.ConceptoId, NpgsqlDbType.Integer);
                AddParameter(cmd, "@contribuyente_id", orden.ContribuyenteId, NpgsqlDbType.Integer);
                AddParameter(cmd, "@subtotal", orden.Subtotal, NpgsqlDbType.Numeric);
                AddParameter(cmd, "@iva", orden.Iva, NpgsqlDbType.Numeric);
                AddParameter(cmd, "@total", orden.Total, NpgsqlDbType.Numeric);
                AddParameter(cmd, "@observaciones", orden.Observaciones ?? (object)DBNull.Value, NpgsqlDbType.Text);
                AddParameter(cmd, "@estado", orden.Estado ?? "PENDIENTE", NpgsqlDbType.Varchar);
                AddParameter(cmd, "@fecha_creacion", orden.FechaCreacion, NpgsqlDbType.Timestamp);
                AddParameter(cmd, "@usuario_creacion", orden.UsuarioCreacion, NpgsqlDbType.Varchar);
                AddParameter(cmd, "@activo", true, NpgsqlDbType.Boolean);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public async Task CrearDetalleAsync(DetalleOrden detalle)
        {
            ExecuteWithConnection(conn =>
            {
                InsertarDetalle(conn, null, detalle);
            });
        }

        private void InsertarDetalle(NpgsqlConnection conn, NpgsqlTransaction trans, DetalleOrden detalle)
        {
            const string sql = @"
                INSERT INTO detalles_orden (
                    orden_id, concepto_id, descripcion, cantidad, precio_unitario, subtotal
                ) VALUES (
                    @orden_id, @concepto_id, @descripcion, @cantidad, @precio_unitario, @subtotal
                )";

            using (var cmd = CreateCommand(conn, sql, trans))
            {
                AddParameter(cmd, "@orden_id", detalle.OrdenId, NpgsqlDbType.Integer);
                AddParameter(cmd, "@concepto_id", detalle.ConceptoId, NpgsqlDbType.Integer);
                AddParameter(cmd, "@descripcion", detalle.Descripcion ?? (object)DBNull.Value, NpgsqlDbType.Text);
                AddParameter(cmd, "@cantidad", detalle.Cantidad, NpgsqlDbType.Integer);
                AddParameter(cmd, "@precio_unitario", detalle.PrecioUnitario, NpgsqlDbType.Numeric);
                AddParameter(cmd, "@subtotal", detalle.Subtotal, NpgsqlDbType.Numeric);

                cmd.ExecuteNonQuery();
            }
        }

        private void InsertarHistorialEstado(NpgsqlConnection conn, NpgsqlTransaction trans, 
            int ordenId, string estadoAnterior, string estadoNuevo, string usuario)
        {
            const string sql = @"
                INSERT INTO historial_estados_orden (
                    orden_id, estado_anterior, estado_nuevo, fecha_cambio, usuario_cambio
                ) VALUES (
                    @orden_id, @estado_anterior, @estado_nuevo, @fecha_cambio, @usuario_cambio
                )";

            using (var cmd = CreateCommand(conn, sql, trans))
            {
                AddParameter(cmd, "@orden_id", ordenId, NpgsqlDbType.Integer);
                AddParameter(cmd, "@estado_anterior", estadoAnterior ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                AddParameter(cmd, "@estado_nuevo", estadoNuevo, NpgsqlDbType.Varchar);
                AddParameter(cmd, "@fecha_cambio", DateTime.Now, NpgsqlDbType.Timestamp);
                AddParameter(cmd, "@usuario_cambio", usuario, NpgsqlDbType.Varchar);

                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Actualizar

        public async Task<bool> ActualizarAsync(OrdenRecaudacion orden)
        {
            const string sql = @"
                UPDATE ordenes_recaudacion SET
                    concepto_id = @concepto_id,
                    subtotal = @subtotal,
                    iva = @iva,
                    total = @total,
                    observaciones = @observaciones,
                    fecha_modificacion = @fecha_modificacion,
                    usuario_modificacion = @usuario_modificacion
                WHERE id = @id AND activo = true";

            return ExecuteWithConnection(conn =>
            {
                var rows = ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", orden.Id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@concepto_id", orden.ConceptoId, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@subtotal", orden.Subtotal, NpgsqlDbType.Numeric);
                    AddParameter(cmd, "@iva", orden.Iva, NpgsqlDbType.Numeric);
                    AddParameter(cmd, "@total", orden.Total, NpgsqlDbType.Numeric);
                    AddParameter(cmd, "@observaciones", orden.Observaciones ?? (object)DBNull.Value, NpgsqlDbType.Text);
                    AddParameter(cmd, "@fecha_modificacion", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@usuario_modificacion", orden.UsuarioModificacion, NpgsqlDbType.Varchar);
                });

                return rows > 0;
            });
        }

        public async Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado, string usuario)
        {
            return ExecuteInTransaction<bool>((conn, trans) =>
            {
                // 1. Obtener estado actual
                string estadoActual = null;
                const string sqlSelect = "SELECT estado FROM ordenes_recaudacion WHERE id = @id";

                using (var cmd = CreateCommand(conn, sqlSelect, trans))
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    estadoActual = cmd.ExecuteScalar() as string;
                }

                if (estadoActual == null)
                {
                    return false;
                }

                // 2. Actualizar estado
                const string sqlUpdate = @"
                    UPDATE ordenes_recaudacion SET
                        estado = @estado,
                        fecha_modificacion = @fecha_modificacion,
                        usuario_modificacion = @usuario_modificacion
                    WHERE id = @id";

                using (var cmd = CreateCommand(conn, sqlUpdate, trans))
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@estado", nuevoEstado, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@fecha_modificacion", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@usuario_modificacion", usuario, NpgsqlDbType.Varchar);

                    cmd.ExecuteNonQuery();
                }

                // 3. Registrar en historial
                InsertarHistorialEstado(conn, trans, id, estadoActual, nuevoEstado, usuario);

                return true;
            });
        }

        #endregion

        #region Eliminar

        public async Task<bool> EliminarAsync(int id, string usuario)
        {
            // Eliminación lógica
            const string sql = @"
                UPDATE ordenes_recaudacion SET
                    activo = false,
                    fecha_modificacion = @fecha_modificacion,
                    usuario_modificacion = @usuario_modificacion
                WHERE id = @id";

            return ExecuteWithConnection(conn =>
            {
                var rows = ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@fecha_modificacion", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@usuario_modificacion", usuario, NpgsqlDbType.Varchar);
                });

                return rows > 0;
            });
        }

        #endregion

        #region Mapeo

        private OrdenRecaudacion MapearOrden(IDataReader reader)
        {
            return new OrdenRecaudacion
            {
                Id = GetInt(reader, "id"),
                NumeroOrden = GetString(reader, "numero_orden"),
                SolicitudId = GetInt(reader, "solicitud_id"),
                ConceptoId = GetInt(reader, "concepto_id"),
                ContribuyenteId = GetInt(reader, "contribuyente_id"),
                Subtotal = GetDecimal(reader, "subtotal"),
                Iva = GetDecimal(reader, "iva"),
                Total = GetDecimal(reader, "total"),
                Observaciones = GetString(reader, "observaciones"),
                Estado = GetString(reader, "estado"),
                FechaCreacion = GetDateTime(reader, "fecha_creacion"),
                UsuarioCreacion = GetString(reader, "usuario_creacion"),
                FechaModificacion = GetNullableDateTime(reader, "fecha_modificacion"),
                UsuarioModificacion = GetString(reader, "usuario_modificacion"),
                Activo = GetBool(reader, "activo", true),
                // Datos de join
                NombreContribuyente = GetString(reader, "contribuyente_nombre"),
                RucContribuyente = GetString(reader, "contribuyente_ruc"),
                EmailContribuyente = GetString(reader, "contribuyente_correo"),
                ConceptoNombre = GetString(reader, "concepto_nombre")
            };
        }

        #endregion
    }
}
