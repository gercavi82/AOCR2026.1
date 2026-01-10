using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class PagoDAO
    {
        // ==========================================
        // Conexión reutilizando tu ConexionDAO
        // ==========================================
        private NpgsqlConnection CrearConexion()
        {
            return ConexionDAO.CrearConexion();
        }

        // ==========================================
        // Helper: verificar si existe una columna
        // ==========================================
        private static bool TieneColumna(IDataRecord r, string nombre)
        {
            for (int i = 0; i < r.FieldCount; i++)
            {
                if (string.Equals(r.GetName(i), nombre, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ==========================================
        // Mapeo a modelo Pago
        // ==========================================
        private static Pago Map(IDataRecord r)
        {
            var p = new Pago();

            if (TieneColumna(r, "codigopago") && r["codigopago"] != DBNull.Value)
                p.CodigoPago = Convert.ToInt32(r["codigopago"]);

            if (TieneColumna(r, "codigosolicitud") && r["codigosolicitud"] != DBNull.Value)
                p.CodigoSolicitud = Convert.ToInt32(r["codigosolicitud"]);

            if (TieneColumna(r, "monto") && r["monto"] != DBNull.Value)
                p.Monto = Convert.ToDecimal(r["monto"]);

            if (TieneColumna(r, "metodopago") && r["metodopago"] != DBNull.Value)
                p.MetodoPago = r["metodopago"].ToString();

            if (TieneColumna(r, "fechapago") && r["fechapago"] != DBNull.Value)
                p.FechaPago = (DateTime?)Convert.ToDateTime(r["fechapago"]);

            if (TieneColumna(r, "numerotransaccion") && r["numerotransaccion"] != DBNull.Value)
                p.NumeroTransaccion = r["numerotransaccion"].ToString();

            if (TieneColumna(r, "rutacomprobante") && r["rutacomprobante"] != DBNull.Value)
                p.RutaComprobante = r["rutacomprobante"].ToString();

            if (TieneColumna(r, "estado") && r["estado"] != DBNull.Value)
                p.Estado = r["estado"].ToString();

            if (TieneColumna(r, "fechavalidacion") && r["fechavalidacion"] != DBNull.Value)
                p.FechaValidacion = (DateTime?)Convert.ToDateTime(r["fechavalidacion"]);

            if (TieneColumna(r, "usuariovalidacion") && r["usuariovalidacion"] != DBNull.Value)
                p.UsuarioValidacion = Convert.ToInt32(r["usuariovalidacion"]);

            if (TieneColumna(r, "observacionesvalidacion") && r["observacionesvalidacion"] != DBNull.Value)
                p.ObservacionesValidacion = r["observacionesvalidacion"].ToString();

            return p;
        }

        // ==========================================
        // Obtener todos los pagos (pendientes o no)
        // ==========================================
        public List<Pago> ObtenerTodos()
        {
            var lista = new List<Pago>();

            const string sql = @"
                SELECT codigopago, codigosolicitud, monto, metodopago,
                       fechapago, numerotransaccion, rutacomprobante,
                       estado, fechavalidacion, usuariovalidacion, observacionesvalidacion
                FROM aocr_tbpago
                WHERE deletedat IS NULL
                ORDER BY fechapago DESC, codigopago DESC;
            ";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        lista.Add(Map(dr));
                }
            }

            return lista;
        }

        // ==========================================
        // Obtener pago por ID
        // ==========================================
        public Pago ObtenerPorId(int codigoPago)
        {
            const string sql = @"
                SELECT codigopago, codigosolicitud, monto, metodopago,
                       fechapago, numerotransaccion, rutacomprobante,
                       estado, fechavalidacion, usuariovalidacion, observacionesvalidacion
                FROM aocr_tbpago
                WHERE codigopago = @id;
            ";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoPago);
                cn.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                        return Map(dr);
                }
            }
            return null;
        }

        // ==========================================
        // Obtener pagos por solicitud
        // ==========================================
        public List<Pago> ObtenerPorSolicitud(int codigoSolicitud)
        {
            var lista = new List<Pago>();

            const string sql = @"
                SELECT codigopago, codigosolicitud, monto, metodopago,
                       fechapago, numerotransaccion, rutacomprobante,
                       estado, fechavalidacion, usuariovalidacion, observacionesvalidacion
                FROM aocr_tbpago
                WHERE codigosolicitud = @sol
                  AND deletedat IS NULL
                ORDER BY fechapago DESC, codigopago DESC;
            ";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@sol", codigoSolicitud);
                cn.Open();

                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        lista.Add(Map(dr));
                }
            }
            return lista;
        }

        // ==========================================
        // Insertar nuevo pago
        // ==========================================
        public bool Insertar(Pago pago)
        {
            const string sql = @"
                INSERT INTO aocr_tbpago
                    (codigosolicitud, monto, metodopago, fechapago,
                     numerotransaccion, rutacomprobante, estado)
                VALUES
                    (@sol, @monto, @met, @fecha, @num, @ruta, @estado);
            ";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@sol", pago.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@monto", pago.Monto);
                cmd.Parameters.AddWithValue("@met", (object)pago.MetodoPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha", (object)pago.FechaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@num", (object)pago.NumeroTransaccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta", (object)pago.RutaComprobante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", (object)pago.Estado ?? DBNull.Value);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ==========================================
        // Actualizar un pago
        // ==========================================
        public bool Actualizar(Pago pago)
        {
            const string sql = @"
                UPDATE aocr_tbpago
                   SET codigosolicitud = @sol,
                       monto = @monto,
                       metodopago = @met,
                       fechapago = @fecha,
                       numerotransaccion = @num,
                       rutacomprobante = @ruta,
                       estado = @estado,
                       fechavalidacion = @fechaval,
                       usuariovalidacion = @usrval,
                       observacionesvalidacion = @obs
                 WHERE codigopago = @id;
            ";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@sol", pago.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@monto", pago.Monto);
                cmd.Parameters.AddWithValue("@met", (object)pago.MetodoPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha", (object)pago.FechaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@num", (object)pago.NumeroTransaccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ruta", (object)pago.RutaComprobante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", (object)pago.Estado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fechaval", (object)pago.FechaValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usrval", (object)pago.UsuarioValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object)pago.ObservacionesValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", pago.CodigoPago);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ==========================================
        // Existe por número de transacción
        // ==========================================
        public bool ExistePorNumeroTransaccion(string numeroTransaccion)
        {
            const string sql = @"
                SELECT COUNT(*)
                  FROM aocr_tbpago
                 WHERE numerotransaccion = @num;
            ";
            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@num", (object)numeroTransaccion ?? DBNull.Value);
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        // ==========================================
        // Obtener pagos validados hoy
        // ==========================================
        public List<Pago> ObtenerPagosValidadosHoy()
        {
            var lista = new List<Pago>();
            const string sql = @"
                SELECT codigopago, codigosolicitud, monto, metodopago,
                       fechapago, numerotransaccion, rutacomprobante,
                       estado, fechavalidacion, usuariovalidacion, observacionesvalidacion
                FROM aocr_tbpago
               WHERE estado = 'APROBADO'
                 AND DATE(fechapago) = CURRENT_DATE
            ORDER BY fechapago DESC, codigopago DESC;
            ";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        lista.Add(Map(dr));
                }
            }
            return lista;
        }

        // ==========================================
        // Obtener monto recaudado en mes/año
        // ==========================================
        public decimal ObtenerMontoRecaudadoMes(int anio, int mes)
        {
            const string sql = @"
                SELECT COALESCE(SUM(monto), 0)
                  FROM aocr_tbpago
                 WHERE EXTRACT(YEAR FROM fechapago) = @anio
                   AND EXTRACT(MONTH FROM fechapago) = @mes
                   AND estado = 'APROBADO';
            ";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@anio", anio);
                cmd.Parameters.AddWithValue("@mes", mes);
                cn.Open();

                var valor = cmd.ExecuteScalar();
                return valor != null && valor != DBNull.Value
                    ? Convert.ToDecimal(valor)
                    : 0m;
            }
        }
    }
}
