using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using CapaDatos.Entidades;
using CapaDatos.Infrastructure;
using CapaDatos.Interfaces;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO para Pagos con SQL parametrizado
    /// </summary>
    public class PagoDAO : BaseDAO, IPagoRepository
    {
        public PagoDAO(string connectionString) : base(connectionString)
        {
        }

        public async Task<int> CrearAsync(Pago pago)
        {
            const string sql = @"
                INSERT INTO pagos (
                    orden_id, numero_comprobante, monto_pagado, fecha_pago,
                    metodo_pago, banco_origen, observaciones, ruta_comprobante,
                    estado, fecha_registro, usuario_registro
                ) VALUES (
                    @orden_id, @numero_comprobante, @monto_pagado, @fecha_pago,
                    @metodo_pago, @banco_origen, @observaciones, @ruta_comprobante,
                    @estado, @fecha_registro, @usuario_registro
                ) RETURNING id";

            return ExecuteWithConnection(conn =>
            {
                return ExecuteScalar<int>(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@orden_id", pago.OrdenId, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@numero_comprobante", pago.NumeroComprobante, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@monto_pagado", pago.MontoPagado, NpgsqlDbType.Numeric);
                    AddParameter(cmd, "@fecha_pago", pago.FechaPago, NpgsqlDbType.Date);
                    AddParameter(cmd, "@metodo_pago", pago.MetodoPago ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@banco_origen", pago.BancoOrigen ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@observaciones", pago.Observaciones ?? (object)DBNull.Value, NpgsqlDbType.Text);
                    AddParameter(cmd, "@ruta_comprobante", pago.RutaComprobante ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@estado", pago.Estado ?? "PENDIENTE_VALIDACION", NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@fecha_registro", pago.FechaRegistro, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@usuario_registro", pago.UsuarioRegistro, NpgsqlDbType.Varchar);
                });
            });
        }

        public async Task<Pago> ObtenerPorIdAsync(int id)
        {
            const string sql = @"
                SELECT * FROM pagos WHERE id = @id";

            return ExecuteWithConnection(conn =>
            {
                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapearPago(reader);
                        }
                        return null;
                    }
                }
            });
        }

        public async Task<Pago> ObtenerPorOrdenIdAsync(int ordenId)
        {
            const string sql = @"
                SELECT * FROM pagos 
                WHERE orden_id = @orden_id 
                ORDER BY fecha_registro DESC 
                LIMIT 1";

            return ExecuteWithConnection(conn =>
            {
                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@orden_id", ordenId, NpgsqlDbType.Integer);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapearPago(reader);
                        }
                        return null;
                    }
                }
            });
        }

        public async Task<IEnumerable<Pago>> ObtenerPorEstadoAsync(string estado)
        {
            const string sql = @"
                SELECT p.*, o.numero_orden
                FROM pagos p
                INNER JOIN ordenes_recaudacion o ON p.orden_id = o.id
                WHERE p.estado = @estado
                ORDER BY p.fecha_registro ASC";

            return ExecuteWithConnection(conn =>
            {
                var lista = new List<Pago>();

                using (var cmd = CreateCommand(conn, sql))
                {
                    AddParameter(cmd, "@estado", estado, NpgsqlDbType.Varchar);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapearPago(reader));
                        }
                    }
                }

                return lista;
            });
        }

        public async Task<bool> ActualizarAsync(Pago pago)
        {
            const string sql = @"
                UPDATE pagos SET
                    estado = @estado,
                    observaciones = @observaciones,
                    fecha_validacion = @fecha_validacion,
                    usuario_validacion = @usuario_validacion
                WHERE id = @id";

            return ExecuteWithConnection(conn =>
            {
                var rows = ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", pago.Id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@estado", pago.Estado, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@observaciones", pago.Observaciones ?? (object)DBNull.Value, NpgsqlDbType.Text);
                    AddParameter(cmd, "@fecha_validacion", pago.FechaValidacion ?? (object)DBNull.Value, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@usuario_validacion", pago.UsuarioValidacion ?? (object)DBNull.Value, NpgsqlDbType.Varchar);
                });

                return rows > 0;
            });
        }

        public async Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado, string usuario)
        {
            const string sql = @"
                UPDATE pagos SET
                    estado = @estado,
                    fecha_validacion = @fecha_validacion,
                    usuario_validacion = @usuario_validacion
                WHERE id = @id";

            return ExecuteWithConnection(conn =>
            {
                var rows = ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@id", id, NpgsqlDbType.Integer);
                    AddParameter(cmd, "@estado", nuevoEstado, NpgsqlDbType.Varchar);
                    AddParameter(cmd, "@fecha_validacion", DateTime.Now, NpgsqlDbType.Timestamp);
                    AddParameter(cmd, "@usuario_validacion", usuario, NpgsqlDbType.Varchar);
                });

                return rows > 0;
            });
        }

        private Pago MapearPago(IDataReader reader)
        {
            return new Pago
            {
                Id = GetInt(reader, "id"),
                OrdenId = GetInt(reader, "orden_id"),
                NumeroComprobante = GetString(reader, "numero_comprobante"),
                MontoPagado = GetDecimal(reader, "monto_pagado"),
                FechaPago = GetDateTime(reader, "fecha_pago"),
                MetodoPago = GetString(reader, "metodo_pago"),
                BancoOrigen = GetString(reader, "banco_origen"),
                Observaciones = GetString(reader, "observaciones"),
                RutaComprobante = GetString(reader, "ruta_comprobante"),
                Estado = GetString(reader, "estado"),
                FechaRegistro = GetDateTime(reader, "fecha_registro"),
                UsuarioRegistro = GetString(reader, "usuario_registro"),
                FechaValidacion = GetNullableDateTime(reader, "fecha_validacion"),
                UsuarioValidacion = GetString(reader, "usuario_validacion")
            };
        }
    }
}
