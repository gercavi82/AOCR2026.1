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
        private NpgsqlConnection CrearConexion()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en el config.");

            return new NpgsqlConnection(cs);
        }

        // Mapear desde DataReader
        private Pago MapPago(IDataRecord r)
        {
            return new Pago
            {
                CodigoPago = r["codigopago"] != DBNull.Value ? Convert.ToInt32(r["codigopago"]) : 0,
                CodigoSolicitud = r["codigosolicitud"] != DBNull.Value ? Convert.ToInt32(r["codigosolicitud"]) : 0,
                NumeroComprobante = r["numerocomprobante"]?.ToString(),
                Monto = r["monto"] != DBNull.Value ? Convert.ToDecimal(r["monto"]) : 0m,
                FormaPago = r["formapago"]?.ToString(),
                Banco = r["banco"]?.ToString(),
                NumeroTransaccion = r["numerotransaccion"]?.ToString(),
                Estado = r["estado"]?.ToString(),
                FechaPago = r["fechapago"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["fechapago"]) : null,
                FechaValidacion = r["fechavalidacion"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["fechavalidacion"]) : null,
                FechaRechazo = r["fecharechazo"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["fecharechazo"]) : null,
                FechaAnulacion = r["fechaanulacion"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["fechaanulacion"]) : null,
                Observaciones = r["observaciones"]?.ToString(),
                UsuarioRegistro = r["usuarioregistro"]?.ToString(),
                UsuarioValidacion = r["usuariovalidacion"]?.ToString(),
                UsuarioRechazo = r["usuariorechazo"]?.ToString(),
                UsuarioAnulacion = r["usuarioanulacion"]?.ToString()
            };
        }

        // Crear nuevo pago
        public int Crear(Pago pago)
        {
            const string sql = @"
                INSERT INTO aocr_tbpago 
                (codigosolicitud, numerocomprobante, monto, formapago, banco, 
                 numerotransaccion, estado, fechapago, observaciones, usuarioregistro)
                VALUES 
                (@codSolicitud, @numComp, @monto, @forma, @banco, 
                 @numTrans, @estado, @fecha, @obs, @usuario)
                RETURNING codigopago;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codSolicitud", pago.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@numComp", (object)pago.NumeroComprobante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", pago.Monto);
                cmd.Parameters.AddWithValue("@forma", (object)pago.FormaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@banco", (object)pago.Banco ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@numTrans", (object)pago.NumeroTransaccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", (object)pago.Estado ?? "REGISTRADO");
                cmd.Parameters.AddWithValue("@fecha", (object)pago.FechaPago ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@obs", (object)pago.Observaciones ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuario", (object)pago.UsuarioRegistro ?? DBNull.Value);

                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // Actualizar pago
        public bool Actualizar(Pago pago)
        {
            const string sql = @"
                UPDATE aocr_tbpago
                SET
                    numerocomprobante = @numComp,
                    monto = @monto,
                    formapago = @forma,
                    banco = @banco,
                    numerotransaccion = @numTrans,
                    estado = @estado,
                    fechapago = @fecha,
                    fechavalidacion = @fechaVal,
                    fecharechazo = @fechaRech,
                    fechaanulacion = @fechaAnul,
                    observaciones = @obs,
                    usuarioregistro = @usuarioReg,
                    usuariovalidacion = @usuarioVal,
                    usuariorechazo = @usuarioRech,
                    usuarioanulacion = @usuarioAnul
                WHERE codigopago = @id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", pago.CodigoPago);
                cmd.Parameters.AddWithValue("@numComp", (object)pago.NumeroComprobante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", pago.Monto);
                cmd.Parameters.AddWithValue("@forma", (object)pago.FormaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@banco", (object)pago.Banco ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@numTrans", (object)pago.NumeroTransaccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", pago.Estado);
                cmd.Parameters.AddWithValue("@fecha", (object)pago.FechaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fechaVal", (object)pago.FechaValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fechaRech", (object)pago.FechaRechazo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fechaAnul", (object)pago.FechaAnulacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object)pago.Observaciones ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuarioReg", (object)pago.UsuarioRegistro ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuarioVal", (object)pago.UsuarioValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuarioRech", (object)pago.UsuarioRechazo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuarioAnul", (object)pago.UsuarioAnulacion ?? DBNull.Value);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // Obtener pago por ID
        public Pago ObtenerPorId(int codigoPago)
        {
            const string sql = @"
                SELECT * FROM aocr_tbpago
                WHERE codigopago = @id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoPago);
                cn.Open();

                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                        return MapPago(rd);
                }
            }

            return null;
        }

        // Obtener pago por solicitud (último pago)
        public Pago ObtenerPorSolicitud(int codigoSolicitud)
        {
            const string sql = @"
                SELECT * FROM aocr_tbpago
                WHERE codigosolicitud = @idSolicitud
                ORDER BY fechapago DESC
                LIMIT 1;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@idSolicitud", codigoSolicitud);
                cn.Open();

                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                        return MapPago(rd);
                }
            }

            return null;
        }

        // Obtener todos los pagos de una solicitud
        public List<Pago> ObtenerPorSolicitudCompleto(int codigoSolicitud)
        {
            var lista = new List<Pago>();

            const string sql = @"
                SELECT * FROM aocr_tbpago
                WHERE codigosolicitud = @idSolicitud
                ORDER BY fechapago DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@idSolicitud", codigoSolicitud);
                cn.Open();

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapPago(rd));
                }
            }

            return lista;
        }

        // Obtener pagos por rango de fechas
        public List<Pago> ObtenerPorRangoFechas(DateTime fechaInicio, DateTime fechaFin)
        {
            var lista = new List<Pago>();

            const string sql = @"
                SELECT * FROM aocr_tbpago
                WHERE fechapago BETWEEN @fechaInicio AND @fechaFin
                ORDER BY fechapago DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@fechaInicio", fechaInicio);
                cmd.Parameters.AddWithValue("@fechaFin", fechaFin);
                cn.Open();

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapPago(rd));
                }
            }

            return lista;
        }

        // Obtener pagos por estado
        public List<Pago> ObtenerPorEstado(string estado)
        {
            var lista = new List<Pago>();

            const string sql = @"
                SELECT * FROM aocr_tbpago
                WHERE estado = @estado
                ORDER BY fechapago DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@estado", estado);
                cn.Open();

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapPago(rd));
                }
            }

            return lista;
        }

        // Verificar si existe pago para una solicitud
        public bool ExistePagoParaSolicitud(int codigoSolicitud)
        {
            const string sql = @"
                SELECT COUNT(*) FROM aocr_tbpago
                WHERE codigosolicitud = @idSolicitud;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@idSolicitud", codigoSolicitud);
                cn.Open();

                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
        }
        // ==========================
        // ✅ NUEVOS MÉTODOS (para compatibilidad con BL)
        // ==========================

        /// <summary>
        /// Obtiene todos los pagos (útil para reportes internos).
        /// En producción, considera paginar o filtrar por rango/estado.
        /// </summary>
        public List<Pago> ObtenerTodos()
        {
            var lista = new List<Pago>();

            const string sql = @"
                SELECT * FROM aocr_tbpago
                ORDER BY fechapago DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapPago(rd));
                }
            }

            return lista;
        }

        /// <summary>
        /// Verifica si ya existe un pago por número de transacción.
        /// </summary>
        public bool ExistePorNumeroTransaccion(string numeroTransaccion)
        {
            if (string.IsNullOrWhiteSpace(numeroTransaccion)) return false;

            const string sql = @"
                SELECT COUNT(*)
                FROM aocr_tbpago
                WHERE numerotransaccion = @num;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@num", numeroTransaccion.Trim());
                cn.Open();
                var obj = cmd.ExecuteScalar();
                return Convert.ToInt32(obj) > 0;
            }
        }

        /// <summary>
        /// Pagos validados hoy (fechaValidacion = hoy).
        /// Ajusta según tu zona horaria/criterio.
        /// </summary>
        public List<Pago> ObtenerPagosValidadosHoy()
        {
            var lista = new List<Pago>();

            const string sql = @"
                SELECT *
                FROM aocr_tbpago
                WHERE fechavalidacion::date = CURRENT_DATE
                ORDER BY fechavalidacion DESC;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapPago(rd));
                }
            }

            return lista;
        }

        /// <summary>
        /// Total recaudado en un mes (SUM monto) para pagos validados.
        /// Si tu lógica usa otro estado (VALIDADO), ajusta el WHERE.
        /// </summary>
        public decimal ObtenerMontoRecaudadoMes(int year, int month)
        {
            // Rango [inicio, fin)
            var inicio = new DateTime(year, month, 1);
            var fin = inicio.AddMonths(1);

            const string sql = @"
                SELECT COALESCE(SUM(monto), 0)
                FROM aocr_tbpago
                WHERE fechavalidacion >= @inicio
                  AND fechavalidacion < @fin
                  AND (estado = 'VALIDADO' OR estado = 'APROBADO' OR fechavalidacion IS NOT NULL);";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@inicio", inicio);
                cmd.Parameters.AddWithValue("@fin", fin);

                cn.Open();
                var obj = cmd.ExecuteScalar();
                return (obj == null || obj == DBNull.Value) ? 0m : Convert.ToDecimal(obj);
            }
        }
        // ==========================
        // ✅ COMPATIBILIDAD con código antiguo (Controllers/BL)
        // ==========================

        /// <summary>
        /// Alias de "ObtenerPorSolicitud" (ya te devuelve el último por fecha).
        /// </summary>
        public Pago ObtenerUltimoPorSolicitud(int codigoSolicitud)
        {
            return ObtenerPorSolicitud(codigoSolicitud);
        }

        /// <summary>
        /// Alias de "Crear" pero con auditoría y compatibilidad con nombres antiguos del modelo
        /// (MetodoPago / NumeroFactura).
        /// </summary>
        public int Insertar(Pago pago, string usuarioRegistro)
        {
            if (pago == null) throw new ArgumentNullException(nameof(pago));
            if (pago.CodigoSolicitud <= 0) throw new Exception("Código de solicitud inválido.");

            // Auditoría
            pago.UsuarioRegistro = string.IsNullOrWhiteSpace(usuarioRegistro) ? "SISTEMA" : usuarioRegistro;

            // Compatibilidad: si tu Controller usa MetodoPago / NumeroFactura,
            // los mapeamos a FormaPago / NumeroComprobante (que tu DAO guarda en BD).
            if (string.IsNullOrWhiteSpace(pago.FormaPago))
            {
                var pi = pago.GetType().GetProperty("MetodoPago");
                if (pi != null)
                {
                    var v = pi.GetValue(pago, null);
                    if (v != null) pago.FormaPago = v.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(pago.NumeroComprobante))
            {
                var pi = pago.GetType().GetProperty("NumeroFactura");
                if (pi != null)
                {
                    var v = pi.GetValue(pago, null);
                    if (v != null) pago.NumeroComprobante = v.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(pago.Estado))
                pago.Estado = "REGISTRADO";

            if (!pago.FechaPago.HasValue)
                pago.FechaPago = DateTime.Now;

            return Crear(pago);
        }


    }
}