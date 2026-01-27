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

        // Mapear desde DataReader (tolerante a nombres legacy vs snake_case)
        private Pago MapPago(IDataRecord r)
        {
            return new Pago
            {
                CodigoPago = GetInt(r, "codigopago", "codigo_pago", "id"),
                CodigoSolicitud = GetInt(r, "codigosolicitud", "codigo_solicitud"),
                NumeroComprobante = GetString(r, "numerocomprobante", "numero_factura"),
                Monto = GetDecimal(r, "monto"),
                Moneda = GetString(r, "moneda"),
                Concepto = GetString(r, "concepto"),
                FormaPago = GetString(r, "formapago", "metodo_pago"),
                Banco = GetString(r, "banco"),
                NumeroTransaccion = GetString(r, "numerotransaccion"),
                Estado = GetString(r, "estado"),
                FechaPago = GetDateTime(r, "fechapago", "fecha_pago"),
                FechaValidacion = GetDateTime(r, "fechavalidacion", "fecha_validacion"),
                FechaRechazo = GetDateTime(r, "fecharechazo", "fecha_rechazo"),
                FechaAnulacion = GetDateTime(r, "fechaanulacion", "fecha_anulacion"),
                Observaciones = GetString(r, "observaciones"),
                UsuarioRegistro = GetString(r, "usuarioregistro", "usuario_registro"),
                UsuarioValidacion = GetString(r, "usuariovalidacion", "validado_por", "usuario_validacion"),
                UsuarioRechazo = GetString(r, "usuariorechazo", "usuario_rechazo"),
                UsuarioAnulacion = GetString(r, "usuarioanulacion", "usuario_anulacion"),
                ComprobanteRuta = GetString(r, "comprobante_ruta")
            };
        }

        private static bool HasColumn(IDataRecord r, string name)
        {
            for (var i = 0; i < r.FieldCount; i++)
            {
                if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string GetString(IDataRecord r, params string[] names)
        {
            foreach (var name in names)
            {
                if (HasColumn(r, name) && r[name] != DBNull.Value)
                    return r[name].ToString();
            }

            return null;
        }

        private static int GetInt(IDataRecord r, params string[] names)
        {
            foreach (var name in names)
            {
                if (HasColumn(r, name) && r[name] != DBNull.Value)
                    return Convert.ToInt32(r[name]);
            }

            return 0;
        }

        private static decimal? GetDecimal(IDataRecord r, params string[] names)
        {
            foreach (var name in names)
            {
                if (HasColumn(r, name) && r[name] != DBNull.Value)
                    return Convert.ToDecimal(r[name]);
            }

            return null;
        }

        private static DateTime? GetDateTime(IDataRecord r, params string[] names)
        {
            foreach (var name in names)
            {
                if (HasColumn(r, name) && r[name] != DBNull.Value)
                    return Convert.ToDateTime(r[name]);
            }

            return null;
        }

        // Crear nuevo pago
        public int Crear(Pago pago)
        {
            const string sql = @"
                INSERT INTO aocr_tbpago 
                (codigo_solicitud, numero_factura, monto, moneda, concepto, metodo_pago,
                 estado, fecha_pago, fecha_validacion, validado_por, observaciones, comprobante_ruta)
                VALUES 
                (@codSolicitud, @numComp, @monto, @moneda, @concepto, @forma,
                 @estado, @fecha, @fechaVal, @validadoPor, @obs, @comprobanteRuta)
                RETURNING codigopago;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codSolicitud", pago.CodigoSolicitud);
                cmd.Parameters.AddWithValue("@numComp", (object)pago.NumeroComprobante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", pago.Monto);
                cmd.Parameters.AddWithValue("@moneda", (object)pago.Moneda ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@concepto", (object)pago.Concepto ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@forma", (object)pago.FormaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", (object)pago.Estado ?? "REGISTRADO");
                cmd.Parameters.AddWithValue("@fecha", (object)pago.FechaPago ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@fechaVal", (object)pago.FechaValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@validadoPor", (object)pago.UsuarioValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object)pago.Observaciones ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@comprobanteRuta", (object)pago.ComprobanteRuta ?? DBNull.Value);

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
                    numero_factura = @numComp,
                    monto = @monto,
                    moneda = @moneda,
                    concepto = @concepto,
                    metodo_pago = @forma,
                    estado = @estado,
                    fecha_pago = @fecha,
                    fecha_validacion = @fechaVal,
                    fecha_rechazo = @fechaRech,
                    fecha_anulacion = @fechaAnul,
                    observaciones = @obs,
                    validado_por = @usuarioVal,
                    usuario_rechazo = @usuarioRech,
                    usuario_anulacion = @usuarioAnul,
                    comprobante_ruta = @comprobanteRuta
                WHERE codigopago = @id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", pago.CodigoPago);
                cmd.Parameters.AddWithValue("@numComp", (object)pago.NumeroComprobante ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", pago.Monto);
                cmd.Parameters.AddWithValue("@moneda", (object)pago.Moneda ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@concepto", (object)pago.Concepto ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@forma", (object)pago.FormaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", pago.Estado);
                cmd.Parameters.AddWithValue("@fecha", (object)pago.FechaPago ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fechaVal", (object)pago.FechaValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fechaRech", (object)pago.FechaRechazo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fechaAnul", (object)pago.FechaAnulacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object)pago.Observaciones ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuarioVal", (object)pago.UsuarioValidacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuarioRech", (object)pago.UsuarioRechazo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuarioAnul", (object)pago.UsuarioAnulacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@comprobanteRuta", (object)pago.ComprobanteRuta ?? DBNull.Value);

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
                WHERE codigo_solicitud = @idSolicitud
                ORDER BY fecha_pago DESC
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
                WHERE codigo_solicitud = @idSolicitud
                ORDER BY fecha_pago DESC;";

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
                WHERE fecha_pago BETWEEN @fechaInicio AND @fechaFin
                ORDER BY fecha_pago DESC;";

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
                ORDER BY fecha_pago DESC;";

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
                WHERE codigo_solicitud = @idSolicitud;";

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
                ORDER BY fecha_pago DESC;";

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
                WHERE fecha_validacion::date = CURRENT_DATE
                ORDER BY fecha_validacion DESC;";

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
                WHERE fecha_validacion >= @inicio
                  AND fecha_validacion < @fin
                  AND (estado = 'VALIDADO' OR estado = 'APROBADO' OR fecha_validacion IS NOT NULL);";

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
