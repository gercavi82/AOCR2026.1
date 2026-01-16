using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class PagoDAO
    {
        private string ConnectionString =>
            ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;

        // =========================================================
        // INSERTAR
        // =========================================================
        public int Insertar(Pago p, string usuario)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                // Ajusta nombres de columna si tu tabla difiere.
                // Aquí asumimos tabla: aocr_tbpago
                string sql = @"
                    INSERT INTO aocr_tbpago
                    (codigo_solicitud, numero_factura, monto, moneda, concepto, metodo_pago, estado,
                     fecha_pago, fecha_validacion, validado_por, observaciones, comprobante_ruta,
                     created_at, created_by)
                    VALUES
                    (@codigo_solicitud, @numero_factura, @monto, @moneda, @concepto, @metodo_pago, @estado,
                     @fecha_pago, @fecha_validacion, @validado_por, @observaciones, @comprobante_ruta,
                     NOW(), @created_by)
                    RETURNING codigo_pago;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", p.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@numero_factura", (object)p.NumeroFactura ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@monto", (object)p.Monto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@moneda", (object)p.Moneda ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@concepto", (object)p.Concepto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@metodo_pago", (object)p.MetodoPago ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)p.Estado ?? "REGISTRADO");

                    cmd.Parameters.AddWithValue("@fecha_pago", (object)p.FechaPago ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_validacion", (object)p.FechaValidacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@validado_por", (object)p.ValidadoPor ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@observaciones", (object)p.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@comprobante_ruta", (object)p.ComprobanteRuta ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@created_by", (object)usuario ?? "sistema");

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }
        // =========================================================
        // INSERTAR (OVERLOAD SIN USUARIO) - para PagoBL
        // =========================================================
        public bool Insertar(Pago p)
        {
            // tu BL espera bool, así que devolvemos true/false
            var id = Insertar(p, "sistema");
            return id > 0;
        }

        // =========================================================
        // OBTENER TODOS
        // =========================================================
        public List<Pago> ObtenerTodos()
        {
            var lista = new List<Pago>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    SELECT codigo_pago, codigo_solicitud, numero_factura, monto, moneda, concepto,
                           metodo_pago, estado, fecha_pago, fecha_validacion, validado_por,
                           observaciones, comprobante_ruta
                    FROM aocr_tbpago
                    ORDER BY codigo_pago DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(Mapear(rd));
                }
            }

            return lista;
        }

        // =========================================================
        // OBTENER POR SOLICITUD
        // =========================================================
        public List<Pago> ObtenerPorSolicitud(int solicitudId)
        {
            var lista = new List<Pago>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    SELECT codigo_pago, codigo_solicitud, numero_factura, monto, moneda, concepto,
                           metodo_pago, estado, fecha_pago, fecha_validacion, validado_por,
                           observaciones, comprobante_ruta
                    FROM aocr_tbpago
                    WHERE codigo_solicitud = @id
                    ORDER BY codigo_pago DESC;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", solicitudId);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            lista.Add(Mapear(rd));
                    }
                }
            }

            return lista;
        }

        // =========================================================
        // OBTENER ÚLTIMO POR SOLICITUD
        // =========================================================
        public Pago ObtenerUltimoPorSolicitud(int solicitudId)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    SELECT codigo_pago, codigo_solicitud, numero_factura, monto, moneda, concepto,
                           metodo_pago, estado, fecha_pago, fecha_validacion, validado_por,
                           observaciones, comprobante_ruta
                    FROM aocr_tbpago
                    WHERE codigo_solicitud = @id
                    ORDER BY codigo_pago DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", solicitudId);

                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Mapear(rd) : null;
                    }
                }
            }
        }

        // =========================================================
        // OBTENER POR ID
        // =========================================================
        public Pago ObtenerPorId(int id)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    SELECT codigo_pago, codigo_solicitud, numero_factura, monto, moneda, concepto,
                           metodo_pago, estado, fecha_pago, fecha_validacion, validado_por,
                           observaciones, comprobante_ruta
                    FROM aocr_tbpago
                    WHERE codigo_pago = @id;";

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

        // =========================================================
        // ACTUALIZAR
        // =========================================================
        public bool Actualizar(Pago p) => Actualizar(p, null);

        public bool Actualizar(Pago p, string usuario)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (p.CodigoPago <= 0) throw new Exception("Código de pago inválido.");

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"
                    UPDATE aocr_tbpago
                    SET codigo_solicitud   = @codigo_solicitud,
                        numero_factura     = @numero_factura,
                        monto              = @monto,
                        moneda             = @moneda,
                        concepto           = @concepto,
                        metodo_pago        = @metodo_pago,
                        estado             = @estado,
                        fecha_pago         = @fecha_pago,
                        fecha_validacion   = @fecha_validacion,
                        validado_por       = @validado_por,
                        observaciones      = @observaciones,
                        comprobante_ruta   = @comprobante_ruta,
                        updated_at         = NOW(),
                        updated_by         = @updated_by
                    WHERE codigo_pago = @codigo_pago;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_pago", p.CodigoPago);
                    cmd.Parameters.AddWithValue("@codigo_solicitud", p.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@numero_factura", (object)p.NumeroFactura ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@monto", (object)p.Monto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@moneda", (object)p.Moneda ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@concepto", (object)p.Concepto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@metodo_pago", (object)p.MetodoPago ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)p.Estado ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@fecha_pago", (object)p.FechaPago ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_validacion", (object)p.FechaValidacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@validado_por", (object)p.ValidadoPor ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@observaciones", (object)p.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@comprobante_ruta", (object)p.ComprobanteRuta ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@updated_by", (object)(usuario ?? "sistema"));

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // =========================================================
        // EXISTE POR NUMERO TRANSACCION
        // (Compatibilidad con PagoBL: lo interpretamos como NumeroFactura)
        // =========================================================
        public bool ExistePorNumeroTransaccion(string numeroTransaccion)
        {
            if (string.IsNullOrWhiteSpace(numeroTransaccion)) return false;

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                string sql = @"SELECT COUNT(1)
                               FROM aocr_tbpago
                               WHERE numero_factura = @n;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@n", numeroTransaccion.Trim());
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        // =========================================================
        // OBTENER PAGOS VALIDADOS HOY
        // =========================================================
        public List<Pago> ObtenerPagosValidadosHoy()
        {
            var lista = new List<Pago>();

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                // PostgreSQL: CURRENT_DATE
                string sql = @"
                    SELECT codigo_pago, codigo_solicitud, numero_factura, monto, moneda, concepto,
                           metodo_pago, estado, fecha_pago, fecha_validacion, validado_por,
                           observaciones, comprobante_ruta
                    FROM aocr_tbpago
                    WHERE fecha_validacion::date = CURRENT_DATE
                    ORDER BY fecha_validacion DESC NULLS LAST;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(Mapear(rd));
                }
            }

            return lista;
        }

        // =========================================================
        // OBTENER MONTO RECAUDADO DEL MES (VALIDADOS)
        // =========================================================
        public decimal ObtenerMontoRecaudadoMes(int year, int month)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();

                // rango [inicio, fin)
                var inicio = new DateTime(year, month, 1);
                var fin = inicio.AddMonths(1);

                string sql = @"
                    SELECT COALESCE(SUM(monto), 0)
                    FROM aocr_tbpago
                    WHERE fecha_validacion >= @ini
                      AND fecha_validacion <  @fin
                      AND (estado = 'VALIDADO' OR estado = 'APROBADO' OR estado = 'VALIDADO_TESORERIA');";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@ini", inicio);
                    cmd.Parameters.AddWithValue("@fin", fin);

                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
        }

        // =========================================================
        // MAPEO
        // =========================================================
        private Pago Mapear(IDataRecord rd)
        {
            return new Pago
            {
                CodigoPago = rd["codigo_pago"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_pago"]),
                CodigoSolicitud = rd["codigo_solicitud"] == DBNull.Value ? 0 : Convert.ToInt32(rd["codigo_solicitud"]),

                NumeroFactura = rd["numero_factura"] == DBNull.Value ? null : rd["numero_factura"].ToString(),
                Monto = rd["monto"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rd["monto"]),
                Moneda = rd["moneda"] == DBNull.Value ? null : rd["moneda"].ToString(),

                Concepto = rd["concepto"] == DBNull.Value ? null : rd["concepto"].ToString(),
                MetodoPago = rd["metodo_pago"] == DBNull.Value ? null : rd["metodo_pago"].ToString(),
                Estado = rd["estado"] == DBNull.Value ? null : rd["estado"].ToString(),

                FechaPago = rd["fecha_pago"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_pago"]),
                FechaValidacion = rd["fecha_validacion"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["fecha_validacion"]),
                ValidadoPor = rd["validado_por"] == DBNull.Value ? null : rd["validado_por"].ToString(),

                Observaciones = rd["observaciones"] == DBNull.Value ? null : rd["observaciones"].ToString(),
                ComprobanteRuta = rd["comprobante_ruta"] == DBNull.Value ? null : rd["comprobante_ruta"].ToString()
            };
        }
    }
}
