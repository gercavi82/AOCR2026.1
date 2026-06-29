using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Linq;
using System.Text;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaDatos.DAOs
{
    public class FacturacionAS400Result
    {
        public bool EsDuplicado { get; set; }
        public decimal Secuencial { get; set; }
        public string Aeropuerto { get; set; }
        public string Anio { get; set; }
        public string NumeroFactura { get; set; }
        public string NumeroFr3 { get; set; }
    }

    public class FacturacionAS400DAO : AS400BaseDAO
    {
        private readonly string _schema;
        private readonly string _tablaCabecera;
        private readonly string _tablaDetalle;
        private readonly string _tablaSecuencial;
        private readonly ILoggingService _logger;
        private readonly Dictionary<string, Dictionary<string, int>> _textColumnLengthCache;
        private readonly Dictionary<string, Dictionary<string, int>> _numericColumnLengthCache;
        private readonly object _textColumnLengthCacheLock;
        private readonly object _numericColumnLengthCacheLock;

        public FacturacionAS400DAO(ISecureConfigurationService configService) : base(configService)
        {
            var creds = configService.GetAS400Credentials();
            _schema = (creds.Library ?? creds.Database ?? "DGACDAT").Trim().ToUpperInvariant();
            _tablaCabecera = GetSetting("AS400:Facturacion:OPCAR5Table", "OPCAR5").Trim().ToUpperInvariant();
            _tablaDetalle = GetSetting("AS400:Facturacion:OPCAR6Table", "OPCAR6").Trim().ToUpperInvariant();
            _tablaSecuencial = GetSetting("AS400:Facturacion:OPSARCTable", "OPSARC").Trim().ToUpperInvariant();
            _logger = LoggingServiceFactory.Create();
            _textColumnLengthCache = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            _numericColumnLengthCache = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            _textColumnLengthCacheLock = new object();
            _numericColumnLengthCacheLock = new object();
        }

        [Obsolete("Use el constructor con ISecureConfigurationService")]
        public FacturacionAS400DAO() : this(new SecureConfigurationService())
        {
        }

        public bool TestConnection(out string message)
        {
            return TryTestConnection(out message);
        }

        public bool RegistrarFactura(FacturaAs400Record record, out string error)
        {
            FacturacionAS400Result ignored;
            return RegistrarFactura(record, out ignored, out error);
        }

        public bool RegistrarFactura(FacturaAs400Record record, out FacturacionAS400Result result, out string error)
        {
            error = null;
            result = null;

            if (record == null)
            {
                error = "Registro de factura vacio.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.Aeropuerto))
            {
                error = "Aeropuerto requerido para facturacion AS400.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.NumeroFactura))
            {
                error = "Numero de factura requerido para FR3.";
                return false;
            }

            // ✅ Sanitizar número de factura (eliminar caracteres especiales problemáticos)
            record.NumeroFactura = SanitizarNumeroFactura(record.NumeroFactura);

            if (string.IsNullOrWhiteSpace(record.NumeroFactura))
            {
                error = "Numero de factura invalido después de sanitización.";
                _logger.LogWarning("Factura rechazada: contiene solo caracteres especiales inválidos.");
                return false;
            }

            var aeropuerto = SafeString(record.Aeropuerto).ToUpperInvariant();
            var anio = ResolveAnio(record);
            FacturacionAS400Result localResult = null;

            try
            {
                ExecuteWithConnection(conn =>
                {
                    try
                    {
                        using (var tx = conn.BeginTransaction(IsolationLevel.Serializable))
                        {
                            localResult = RegistrarFacturaCore(conn, tx, record, aeropuerto, anio);
                            tx.Commit();
                        }
                    }
                    catch (OdbcException txEx) when (IsSql7008(txEx))
                    {
                        _logger.LogWarning(
                            "FR3 detectó SQL7008 en modo transaccional; se reintenta sin transacción para compatibilidad.");

                        // Reintento sin transacción para ambientes DB2 que no tienen journaling/commitment control habilitado.
                        localResult = RegistrarFacturaCore(conn, null, record, aeropuerto, anio);
                    }
                });

                result = localResult;
                return true;
            }
            catch (Exception ex)
            {
                var detailMessage = ex.Message;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    if (!string.IsNullOrWhiteSpace(inner.Message))
                    {
                        detailMessage = inner.Message;
                    }
                    inner = inner.InnerException;
                }

                error = detailMessage;

                try
                {
                    var observacionesLen = record.Observaciones == null ? 0 : record.Observaciones.Length;
                    var autorizacionLen = record.AutorizacionFactura == null ? 0 : record.AutorizacionFactura.Length;
                    var numeroFactura = record.NumeroFactura ?? string.Empty;
                    var deposito = record.Deposito ?? string.Empty;

                    var diag = string.Format(
                        "FR3 detalle error AS400: OrdenId={0}, Factura={1}, Aeropuerto={2}, Anio={3}, Subtotal={4}, Iva={5}, Total={6}, ObsLen={7}, AutLen={8}, Deposito={9}",
                        record.OrdenId,
                        numeroFactura,
                        aeropuerto,
                        anio,
                        record.Subtotal.ToString(CultureInfo.InvariantCulture),
                        record.Iva.ToString(CultureInfo.InvariantCulture),
                        record.Total.ToString(CultureInfo.InvariantCulture),
                        observacionesLen,
                        autorizacionLen,
                        deposito);

                    _logger.LogWarning(diag);
                }
                catch
                {
                    // Ignorar errores de logging detallado
                }
                
                // ✅ Logging mejorado con más contexto
                _logger.LogError(ex, new LogContext
                {
                    ErrorCode = "FR3_DB2_ERROR",
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "OrdenId", record.OrdenId },
                        { "FacturaOriginal", record.NumeroFactura ?? string.Empty },
                        { "Aeropuerto", aeropuerto },
                        { "Anio", anio },
                        { "TipoError", ex.GetType().Name },
                        { "SqlState", (ex as OdbcException)?.Errors.Count > 0 ? ((OdbcException)ex).Errors[0].SQLState : "N/A" },
                        { "Detalle", detailMessage },
                        { "UserMessage", "Error al registrar factura en AS400. Verifique los datos de entrada." }
                    }
                });
                
                return false;
            }
        }

        private FacturacionAS400Result RegistrarFacturaCore(
            OdbcConnection conn,
            OdbcTransaction tx,
            FacturaAs400Record record,
            string aeropuerto,
            string anio)
        {
            _logger.LogInfo(string.Format(
                "FR3 DB2 inicio: OrdenId={0}, Factura={1}, Aeropuerto={2}, Anio={3}",
                record.OrdenId,
                record.NumeroFactura,
                aeropuerto,
                anio));

            var colsCabecera = GetColumnas(conn, _schema, _tablaCabecera, tx);
            if (colsCabecera.Count == 0)
            {
                throw new InvalidOperationException(
                    string.Format("No se encontraron columnas en {0}.{1}.", _schema, _tablaCabecera));
            }

            var colsDetalle = GetColumnas(conn, _schema, _tablaDetalle, tx);

            decimal secuencialExistente;
            if (TryObtenerFacturaExistente(conn, tx, colsCabecera, record, aeropuerto, anio, out secuencialExistente))
            {
                _logger.LogWarning(string.Format(
                    "FR3 duplicado detectado: OrdenId={0}, Factura={1}, Sec={2}",
                    record.OrdenId,
                    record.NumeroFactura,
                    secuencialExistente));

                return BuildResult(true, secuencialExistente, aeropuerto, anio, record.NumeroFactura);
            }

            TryBloquearTablaCabecera(conn, tx);
            var secuencial = ObtenerSecuencialSeguro(conn, tx, colsCabecera, aeropuerto, anio);
            secuencial = AsegurarSecuencialNoDuplicado(conn, tx, colsCabecera, aeropuerto, anio, secuencial);

            var valoresCabecera = ConstruirValoresCabecera(record, secuencial);
            InsertarRegistro(conn, tx, _schema, _tablaCabecera, valoresCabecera, colsCabecera);
            _logger.LogInfo(string.Format(
                "FR3 cabecera insertada: Factura={0}, Sec={1}",
                record.NumeroFactura,
                secuencial.ToString(CultureInfo.InvariantCulture)));

            if (colsDetalle.Count > 0 && record.Detalles != null && record.Detalles.Count > 0)
            {
                var secDetalle = 1;
                foreach (var det in record.Detalles.Where(d => d != null))
                {
                    var valoresDetalle = ConstruirValoresDetalle(record, det, secuencial, secDetalle);
                    InsertarRegistro(conn, tx, _schema, _tablaDetalle, valoresDetalle, colsDetalle);
                    secDetalle++;
                }
            }

            TryActualizarSecuencial(conn, tx, aeropuerto, anio, secuencial);
            _logger.LogInfo(string.Format(
                "FR3 secuencial actualizado: Aeropuerto={0}, Anio={1}, Sec={2}",
                aeropuerto,
                anio,
                secuencial.ToString(CultureInfo.InvariantCulture)));

            var result = BuildResult(false, secuencial, aeropuerto, anio, record.NumeroFactura);
            _logger.LogInfo(string.Format(
                "FR3 DB2 OK: OrdenId={0}, Factura={1}, FR3={2}",
                record.OrdenId,
                record.NumeroFactura,
                result.NumeroFr3));

            return result;
        }

        private static bool IsSql7008(OdbcException ex)
        {
            if (ex == null || string.IsNullOrWhiteSpace(ex.Message))
            {
                return false;
            }

            return ex.Message.IndexOf("SQL7008", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static FacturacionAS400Result BuildResult(
            bool esDuplicado,
            decimal secuencial,
            string aeropuerto,
            string anio,
            string numeroFactura)
        {
            var sec = secuencial > 0m
                ? Convert.ToInt64(Math.Truncate(secuencial)).ToString(CultureInfo.InvariantCulture)
                : "0";

            return new FacturacionAS400Result
            {
                EsDuplicado = esDuplicado,
                Secuencial = secuencial,
                Aeropuerto = aeropuerto,
                Anio = anio,
                NumeroFactura = numeroFactura,
                NumeroFr3 = string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", sec, aeropuerto, anio)
            };
        }

        private object NormalizarValorBusquedaNumeroFactura(
            OdbcConnection conn,
            OdbcTransaction tx,
            string numeroFactura)
        {
            var normalized = SafeString(numeroFactura);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            var numericLengths = GetNumericColumnLengths(conn, _schema, _tablaCabecera, tx);
            int maxDigits;
            if (numericLengths != null && numericLengths.TryGetValue("OPCNUM", out maxDigits))
            {
                return NormalizeNumericStringValue(_schema, _tablaCabecera, "OPCNUM", normalized, maxDigits, true);
            }

            return normalized;
        }

        private bool TryObtenerFacturaExistente(
            OdbcConnection conn,
            OdbcTransaction tx,
            HashSet<string> colsCabecera,
            FacturaAs400Record record,
            string aeropuerto,
            string anio,
            out decimal secuencial)
        {
            secuencial = 0m;

            if (record == null)
            {
                return false;
            }

            if (colsCabecera.Contains("OPCNUM") && !string.IsNullOrWhiteSpace(record.NumeroFactura))
            {
                var numeroFacturaLookup = NormalizarValorBusquedaNumeroFactura(conn, tx, record.NumeroFactura);
                var numeroFacturaLookupText = Convert.ToString(numeroFacturaLookup, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(numeroFacturaLookupText)
                    || numeroFacturaLookupText.Trim().TrimStart('0').Length == 0)
                {
                    _logger.LogWarning(string.Format(
                        "FR3 duplicado por OPCNUM omitido: OrdenId={0}, Factura={1}, ValorNormalizado={2}",
                        record.OrdenId,
                        record.NumeroFactura,
                        numeroFacturaLookupText ?? string.Empty));
                }
                else
                {
                    var filtros = new List<string> { "OPCNUM = ?" };
                    var parametros = new List<object> { numeroFacturaLookup };

                    if (colsCabecera.Contains("OPCAER") && !string.IsNullOrWhiteSpace(aeropuerto))
                    {
                        filtros.Add("OPCAER = ?");
                        parametros.Add(aeropuerto);
                    }
                    if (colsCabecera.Contains("OPCANO") && !string.IsNullOrWhiteSpace(anio))
                    {
                        filtros.Add("OPCANO = ?");
                        parametros.Add(anio);
                    }

                    var filtrosFuertes = AgregarFiltrosFuertesFacturaExistente(filtros, parametros, colsCabecera, record);
                    if (filtrosFuertes < 2)
                    {
                        _logger.LogWarning(string.Format(
                            "FR3 duplicado por OPCNUM omitido por filtros insuficientes: OrdenId={0}, Factura={1}, FiltrosFuertes={2}",
                            record.OrdenId,
                            record.NumeroFactura,
                            filtrosFuertes));
                    }
                    else
                    {
                        var sql = string.Format(
                            "SELECT {0} FROM {1}.{2} WHERE {3} FETCH FIRST 1 ROWS ONLY",
                            colsCabecera.Contains("OPCSEC") ? "OPCSEC" : "1",
                            _schema,
                            _tablaCabecera,
                            string.Join(" AND ", filtros));

                        using (var cmd = new OdbcCommand(sql, conn, tx))
                        {
                            foreach (var parametro in parametros)
                            {
                                AddParameter(cmd, parametro, GetOdbcType(parametro));
                            }

                            var valor = cmd.ExecuteScalar();
                            if (valor != null && valor != DBNull.Value)
                            {
                                secuencial = SafeDecimal(valor);
                                _logger.LogWarning(string.Format(
                                    "FR3 duplicado confirmado por OPCNUM con filtros fuertes: OrdenId={0}, Factura={1}, Sec={2}",
                                    record.OrdenId,
                                    record.NumeroFactura,
                                    secuencial));
                                return true;
                            }
                        }
                    }
                }
            }

            if (colsCabecera.Contains("OPCOBS"))
            {
                var tokenCorrelacion = ExtractCorrelationToken(record.Observaciones);
                if (!string.IsNullOrWhiteSpace(tokenCorrelacion))
                {
                    var filtros = new List<string> { "UPPER(OPCOBS) LIKE ?" };
                    var parametros = new List<object> { "%" + tokenCorrelacion.ToUpperInvariant() + "%" };

                    if (colsCabecera.Contains("OPCAER") && !string.IsNullOrWhiteSpace(aeropuerto))
                    {
                        filtros.Add("OPCAER = ?");
                        parametros.Add(aeropuerto);
                    }
                    if (colsCabecera.Contains("OPCANO") && !string.IsNullOrWhiteSpace(anio))
                    {
                        filtros.Add("OPCANO = ?");
                        parametros.Add(anio);
                    }

                    var filtrosFuertes = AgregarFiltrosFuertesFacturaExistente(filtros, parametros, colsCabecera, record);
                    if (filtrosFuertes < 2)
                    {
                        _logger.LogWarning(string.Format(
                            "FR3 duplicado por OPCOBS omitido por filtros insuficientes: OrdenId={0}, Token={1}, FiltrosFuertes={2}",
                            record.OrdenId,
                            tokenCorrelacion,
                            filtrosFuertes));
                    }
                    else
                    {
                        var sql = string.Format(
                            "SELECT {0} FROM {1}.{2} WHERE {3} FETCH FIRST 1 ROWS ONLY",
                            colsCabecera.Contains("OPCSEC") ? "OPCSEC" : "1",
                            _schema,
                            _tablaCabecera,
                            string.Join(" AND ", filtros));

                        using (var cmd = new OdbcCommand(sql, conn, tx))
                        {
                            foreach (var parametro in parametros)
                            {
                                AddParameter(cmd, parametro, GetOdbcType(parametro));
                            }

                            var valor = cmd.ExecuteScalar();
                            if (valor != null && valor != DBNull.Value)
                            {
                                secuencial = SafeDecimal(valor);
                                _logger.LogWarning(string.Format(
                                    "FR3 duplicado confirmado por OPCOBS con filtros fuertes: OrdenId={0}, Token={1}, Sec={2}",
                                    record.OrdenId,
                                    tokenCorrelacion,
                                    secuencial));
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static int AgregarFiltrosFuertesFacturaExistente(
            IList<string> filtros,
            IList<object> parametros,
            HashSet<string> colsCabecera,
            FacturaAs400Record record)
        {
            var filtrosFuertes = 0;

            if (colsCabecera.Contains("OPCRU1") && !string.IsNullOrWhiteSpace(record.Ruc))
            {
                filtros.Add("TRIM(OPCRU1) = ?");
                parametros.Add(record.Ruc.Trim());
                filtrosFuertes++;
            }

            if (colsCabecera.Contains("OPCTOT"))
            {
                filtros.Add("OPCTOT = ?");
                parametros.Add(record.Total);
                filtrosFuertes++;
            }
            else if (colsCabecera.Contains("OPCGRA"))
            {
                filtros.Add("OPCGRA = ?");
                parametros.Add(record.Total);
                filtrosFuertes++;
            }

            if (colsCabecera.Contains("OPCC08") && !string.IsNullOrWhiteSpace(record.CodigoOACICia))
            {
                filtros.Add("UPPER(TRIM(OPCC08)) = ?");
                parametros.Add(record.CodigoOACICia.Trim().ToUpperInvariant());
                filtrosFuertes++;
            }
            else if (colsCabecera.Contains("OPCNO4") && !string.IsNullOrWhiteSpace(record.Compania))
            {
                filtros.Add("UPPER(TRIM(OPCNO4)) = ?");
                parametros.Add(record.Compania.Trim().ToUpperInvariant());
                filtrosFuertes++;
            }
            else if (colsCabecera.Contains("OPCNO5") && !string.IsNullOrWhiteSpace(record.Compania))
            {
                filtros.Add("UPPER(TRIM(OPCNO5)) = ?");
                parametros.Add(record.Compania.Trim().ToUpperInvariant());
                filtrosFuertes++;
            }

            return filtrosFuertes;
        }

        private void TryBloquearTablaCabecera(OdbcConnection conn, OdbcTransaction tx)
        {
            try
            {
                var sql = string.Format(
                    "LOCK TABLE {0}.{1} IN EXCLUSIVE MODE",
                    _schema,
                    _tablaCabecera);

                using (var cmd = new OdbcCommand(sql, conn, tx))
                {
                    cmd.CommandTimeout = _commandTimeout;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FR3: lock exclusivo de cabecera no disponible (" + ex.Message + ").");
            }
        }

        private decimal ObtenerSecuencialSeguro(
            OdbcConnection conn,
            OdbcTransaction tx,
            HashSet<string> colsCabecera,
            string aeropuerto,
            string anio)
        {
            try
            {
                var colsSec = GetColumnas(conn, _schema, _tablaSecuencial, tx);
                var hasOpsSec = colsSec.Contains("OPSSEC");
                var hasOpsAer = colsSec.Contains("OPSAER");
                var hasOpsAno = colsSec.Contains("OPSANO");

                if (hasOpsSec && hasOpsAer)
                {
                    var where = hasOpsAno ? "OPSAER = ? AND OPSANO = ?" : "OPSAER = ?";
                    var sqlSelect = string.Format(
                        "SELECT OPSSEC FROM {0}.{1} WHERE {2}",
                        _schema,
                        _tablaSecuencial,
                        where);

                    using (var cmd = new OdbcCommand(sqlSelect, conn, tx))
                    {
                        AddParameter(cmd, aeropuerto, OdbcType.VarChar);
                        if (hasOpsAno)
                        {
                            AddParameter(cmd, anio, OdbcType.VarChar);
                        }

                        var valor = cmd.ExecuteScalar();
                        if (valor != null && valor != DBNull.Value)
                        {
                            return SafeDecimal(valor) + 1m;
                        }
                    }
                }
            }
            catch (Exception exSec)
            {
                _logger.LogWarning(string.Format(
                    "ObtenerSecuencialSeguro: No se pudo leer OPSARC ({0}). Usando MAX de cabecera.",
                    exSec.Message));
            }

            return ObtenerMaxSecuencial(conn, tx, colsCabecera, aeropuerto, anio) + 1m;
        }

        private decimal AsegurarSecuencialNoDuplicado(
            OdbcConnection conn,
            OdbcTransaction tx,
            HashSet<string> colsCabecera,
            string aeropuerto,
            string anio,
            decimal secuencialInicial)
        {
            var secuencial = secuencialInicial <= 0m ? 1m : secuencialInicial;

            for (var attempt = 0; attempt < 10; attempt++)
            {
                if (!ExisteSecuencialCabecera(conn, tx, colsCabecera, aeropuerto, anio, secuencial))
                {
                    return secuencial;
                }

                secuencial += 1m;
            }

            throw new InvalidOperationException("No fue posible reservar un secuencial FR3 unico.");
        }

        private bool ExisteSecuencialCabecera(
            OdbcConnection conn,
            OdbcTransaction tx,
            HashSet<string> colsCabecera,
            string aeropuerto,
            string anio,
            decimal secuencial)
        {
            if (!colsCabecera.Contains("OPCSEC"))
            {
                return false;
            }

            var filtros = new List<string> { "OPCSEC = ?" };
            var parametros = new List<object> { secuencial };

            if (colsCabecera.Contains("OPCAER") && !string.IsNullOrWhiteSpace(aeropuerto))
            {
                filtros.Add("OPCAER = ?");
                parametros.Add(aeropuerto);
            }

            if (colsCabecera.Contains("OPCANO") && !string.IsNullOrWhiteSpace(anio))
            {
                filtros.Add("OPCANO = ?");
                parametros.Add(anio);
            }

            var sql = string.Format(
                "SELECT 1 FROM {0}.{1} WHERE {2} FETCH FIRST 1 ROWS ONLY",
                _schema,
                _tablaCabecera,
                string.Join(" AND ", filtros));

            using (var cmd = new OdbcCommand(sql, conn, tx))
            {
                foreach (var parametro in parametros)
                {
                    AddParameter(cmd, parametro, GetOdbcType(parametro));
                }

                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }

        private decimal ObtenerMaxSecuencial(
            OdbcConnection conn,
            OdbcTransaction tx,
            HashSet<string> colsCabecera,
            string aeropuerto,
            string anio)
        {
            var filtros = new List<string>();
            var parametros = new List<object>();

            if (colsCabecera.Contains("OPCAER") && !string.IsNullOrWhiteSpace(aeropuerto))
            {
                filtros.Add("OPCAER = ?");
                parametros.Add(aeropuerto);
            }
            if (colsCabecera.Contains("OPCANO") && !string.IsNullOrWhiteSpace(anio))
            {
                filtros.Add("OPCANO = ?");
                parametros.Add(anio);
            }

            var where = filtros.Count > 0 ? " WHERE " + string.Join(" AND ", filtros) : string.Empty;
            var sql = string.Format(
                "SELECT COALESCE(MAX(OPCSEC), 0) FROM {0}.{1}{2}",
                _schema,
                _tablaCabecera,
                where);

            using (var cmd = new OdbcCommand(sql, conn, tx))
            {
                foreach (var parametro in parametros)
                {
                    AddParameter(cmd, parametro, GetOdbcType(parametro));
                }

                var result = cmd.ExecuteScalar();
                return SafeDecimal(result);
            }
        }

        private void TryActualizarSecuencial(
            OdbcConnection conn,
            OdbcTransaction tx,
            string aeropuerto,
            string anio,
            decimal secuencial)
        {
            try
            {
                var cols = GetColumnas(conn, _schema, _tablaSecuencial, tx);
                if (!cols.Contains("OPSSEC") || !cols.Contains("OPSAER"))
                {
                    return;
                }

                var hasAno = cols.Contains("OPSANO");
                var where = hasAno ? "OPSAER = ? AND OPSANO = ?" : "OPSAER = ?";
                var sqlUpdate = string.Format(
                    "UPDATE {0}.{1} SET OPSSEC = ? WHERE {2}",
                    _schema,
                    _tablaSecuencial,
                    where);

                using (var cmd = new OdbcCommand(sqlUpdate, conn, tx))
                {
                    AddParameter(cmd, secuencial, OdbcType.Numeric);
                    AddParameter(cmd, aeropuerto, OdbcType.VarChar);
                    if (hasAno)
                    {
                        AddParameter(cmd, anio, OdbcType.VarChar);
                    }

                    var rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                    {
                        return;
                    }
                }

                var insertValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OPSAER"] = aeropuerto,
                    ["OPSSEC"] = secuencial
                };

                if (hasAno)
                {
                    insertValues["OPSANO"] = anio;
                }
                if (cols.Contains("OPSUSU"))
                {
                    insertValues["OPSUSU"] = "AOCR";
                }
                if (cols.Contains("OPSDA4"))
                {
                    insertValues["OPSDA4"] = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                }
                if (cols.Contains("OPSH01"))
                {
                    insertValues["OPSH01"] = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
                }

                InsertarRegistro(conn, tx, _schema, _tablaSecuencial, insertValues, cols);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("FR3: no se pudo actualizar OPSARC (" + ex.Message + ").");
            }
        }

        private Dictionary<string, object> ConstruirValoresCabecera(FacturaAs400Record record, decimal secuencial)
        {
            var fechaControl = string.IsNullOrWhiteSpace(record.FechaControl)
                ? record.FechaEmision.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                : record.FechaControl.Trim();

            var anio = ResolveAnio(record);

            var observacion = string.IsNullOrWhiteSpace(record.Observaciones)
                ? string.Format("FACTURA {0}", record.NumeroFactura)
                : record.Observaciones;

            var fechaRecepcion = string.IsNullOrWhiteSpace(record.FechaRecepcion)
                ? fechaControl
                : record.FechaRecepcion.Trim();

            var ahora = DateTime.Now;
            var fechaCreacion = ahora.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var horaCreacion = ahora.ToString("HHmmss", CultureInfo.InvariantCulture);

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["OPCSEC"] = secuencial,
                ["OPCAER"] = SafeString(record.Aeropuerto),
                ["OPCANO"] = SafeString(anio),
                ["OPCFE4"] = SafeString(fechaControl),
                ["OPCTIP"] = SafeString(record.TipoOperacion),
                ["OPCRUT"] = SafeString(record.Ruta),
                ["OPCNRO"] = record.NumAterrizaPais,
                ["OPCSUB"] = record.Subtotal,
                ["OPCTOT"] = record.Total,
                ["OPCGRA"] = record.Total,
                ["OPCSON"] = SafeString(record.GranTotalLetras),
                ["OPCAUT"] = SafeString(string.IsNullOrWhiteSpace(record.Autorizacion) ? record.AutorizacionFactura : record.Autorizacion),
                ["OPCOBS"] = SafeString(observacion),
                ["OPCOID"] = record.OidCiaAviacion.HasValue ? (object)record.OidCiaAviacion.Value : 0m,
                ["OPCORI"] = SafeString(record.Origen),
                ["OPCDE7"] = SafeString(record.Destino),
                ["OPCRET"] = SafeString(record.Retorno),
                ["OPCCAL"] = SafeString(record.Callsign),
                ["OPCRU1"] = SafeString(record.Ruc),
                ["OPCEM1"] = SafeString(record.Correo),
                ["OPCNAC"] = SafeString(record.NacInter),
                ["OPCTE1"] = SafeString(record.Telefono),
                ["OPCNO4"] = SafeString(record.Compania),
                ["OPCNO5"] = SafeString(record.Compania),
                ["OPCDA4"] = fechaCreacion,
                ["OPCH01"] = horaCreacion,
                ["OPCOI1"] = record.IdAeropuerto.HasValue ? (object)record.IdAeropuerto.Value : 0m,
                ["OPCOI2"] = record.OidUbicacionCliente.HasValue ? (object)record.OidUbicacionCliente.Value : 0m,
                ["OPCOI3"] = record.OidUbicacion.HasValue ? (object)record.OidUbicacion.Value : 0m,
                ["OPCFOR"] = SafeString(record.FormaPago),
                ["OPCBAN"] = SafeString(record.CodigoBanco),
                ["OPCCHE"] = SafeString(record.Deposito),
                ["OPCNUM"] = string.Empty,
                ["OPCFE9"] = SafeString(fechaRecepcion),
                ["OPCVA6"] = record.Total,
                ["OPCEST"] = "S",
                ["OPCUS7"] = SafeString(record.UsuarioRegistro, "AOCR"),
                ["OPCMOD"] = SafeString(record.Modelo),
                ["OPCPES"] = record.PesoMatricula.HasValue ? (object)record.PesoMatricula.Value : 0m,
                ["OPCC08"] = SafeString(record.CodigoOACICia),
                ["OPCNO6"] = SafeString(record.NombreAeropuerto),
                ["OPCEM2"] = SafeString(record.EmailUsuarioDGAC),
                ["OPCMAT"] = SafeString(record.Matricula),
                ["OPCPRO"] = SafeString(record.Procesado, "E"),
                ["OPCDI2"] = 0m
            };
        }

        private Dictionary<string, object> ConstruirValoresDetalle(
            FacturaAs400Record record,
            FacturaAs400Detalle detalle,
            decimal secuencial,
            int secuencialDetalle)
        {
            var anio = ResolveAnio(record);

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["OPCSE2"] = secuencial,
                ["OPCAE1"] = SafeString(record.Aeropuerto),
                ["OPCAN1"] = SafeString(anio),
                ["OPCSE1"] = secuencialDetalle,
                ["OPCTI1"] = SafeString(detalle.TipoCobro, "01"),
                ["OPCOI4"] = detalle.OidFormulario.HasValue ? (object)detalle.OidFormulario.Value : 0m,
                ["OPCC05"] = SafeString(detalle.CodigoContable),
                ["OPCDE8"] = SafeString(detalle.Descripcion),
                ["OPCCAN"] = detalle.Cantidad,
                ["OPCVA1"] = detalle.Valor,
                ["OPCHAC"] = SafeString(detalle.HacerDescuento, "N"),
                ["OPCCOB"] = SafeString(detalle.CobrarImpuesto, "N"),
                ["OPCING"] = SafeString(detalle.IngresarCantidad, "S"),
                ["OPCD01"] = SafeString(detalle.DescripcionCuenta),
                ["OPCC06"] = SafeString(detalle.Codigo, "FITEM"),
                ["OPCTO1"] = detalle.Total
            };
        }

        private void InsertarRegistro(
            OdbcConnection conn,
            OdbcTransaction tx,
            string schema,
            string table,
            Dictionary<string, object> valores,
            HashSet<string> columnasDisponibles)
        {
            var columnas = valores.Keys
                .Where(c => columnasDisponibles.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (columnas.Count == 0)
            {
                throw new InvalidOperationException(string.Format("No hay columnas validas para insertar en {0}.{1}.", schema, table));
            }

            var colsInsert = string.Join(", ", columnas);
            var placeholders = string.Join(", ", columnas.Select(_ => "?"));
            var sqlInsert = string.Format(
                "INSERT INTO {0}.{1} ({2}) VALUES ({3})",
                schema,
                table,
                colsInsert,
                placeholders);

            var textLengths = GetTextColumnLengths(conn, schema, table, tx);
            var numericLengths = GetNumericColumnLengths(conn, schema, table, tx);

            using (var cmd = new OdbcCommand(sqlInsert, conn, tx))
            {
                foreach (var col in columnas)
                {
                    var value = NormalizeColumnValue(schema, table, col, valores[col], textLengths, numericLengths);
                    AddParameter(cmd, value, GetOdbcType(value));
                }
                cmd.ExecuteNonQuery();
            }
        }

        private object NormalizeColumnValue(
            string schema,
            string table,
            string column,
            object value,
            Dictionary<string, int> textLengths,
            Dictionary<string, int> numericLengths)
        {
            if (value == null || value == DBNull.Value)
            {
                return value;
            }

            var maxNumericLen = 0;
            var columnIsNumeric = numericLengths != null && numericLengths.TryGetValue(column, out maxNumericLen);
            if (!columnIsNumeric)
            {
                var knownLen = GetKnownNumericLength(table, column);
                if (knownLen > 0)
                {
                    maxNumericLen = knownLen;
                    columnIsNumeric = true;
                }
            }

            var asString = value as string;
            if (asString != null)
            {
                var normalizedOriginal = SafeString(asString);
                var normalized = SanitizeTextForAs400(normalizedOriginal);
                if (columnIsNumeric)
                {
                    return NormalizeNumericStringValue(schema, table, column, normalized, maxNumericLen, false);
                }

                var maxLen = 0;
                var hasConfiguredTextLength = textLengths != null && textLengths.TryGetValue(column, out maxLen);
                if (!hasConfiguredTextLength || maxLen <= 0)
                {
                    maxLen = GetKnownTextLength(table, column);
                }

                if (!string.Equals(normalizedOriginal, normalized, StringComparison.Ordinal))
                {
                    _logger.LogWarning(string.Format(
                        "FR3 sanitizacion texto en {0}.{1}.{2}: originalLen={3}, sanitizedLen={4}.",
                        schema,
                        table,
                        column,
                        normalizedOriginal.Length,
                        normalized.Length));
                }

                if (maxLen > 0)
                {
                    var truncatedValue = TruncateToByteLength(normalized, maxLen);
                    if (!string.Equals(normalized, truncatedValue, StringComparison.Ordinal))
                    {
                        _logger.LogWarning(string.Format(
                            "FR3 truncamiento preventivo en {0}.{1}.{2}: bytes={3}, max={4}.",
                            schema,
                            table,
                            column,
                            Encoding.ASCII.GetByteCount(normalized),
                            maxLen));
                    }

                            return truncatedValue;
                }

                return normalized;
            }

            if (!columnIsNumeric)
            {
                return value;
            }

            decimal numericValue;
            if (!TryConvertToDecimal(value, out numericValue))
            {
                return value;
            }

            if (maxNumericLen <= 0 || numericValue != decimal.Truncate(numericValue))
            {
                return numericValue;
            }

            var sign = numericValue < 0m ? -1m : 1m;
            var digits = decimal.Truncate(Math.Abs(numericValue)).ToString("0", CultureInfo.InvariantCulture);
            if (digits.Length <= maxNumericLen)
            {
                return numericValue;
            }

            var truncated = digits.Substring(digits.Length - maxNumericLen, maxNumericLen);
            _logger.LogWarning(string.Format(
                "FR3 truncamiento numerico preventivo en {0}.{1}.{2}: digits={3}, max={4}.",
                schema,
                table,
                column,
                digits.Length,
                maxNumericLen));

            decimal parsed;
            if (decimal.TryParse(truncated, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed * sign;
            }

            return numericValue;
        }

        private static int GetKnownNumericLength(string table, string column)
        {
            if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
            {
                return 0;
            }

            var tableName = table.Trim().ToUpperInvariant();
            var columnName = column.Trim().ToUpperInvariant();

            if (tableName == "OPCAR5")
            {
                if (columnName == "OPCSEC") return 6;
                if (columnName == "OPCNRO") return 3;
                if (columnName == "OPCSUB" || columnName == "OPCTOT" || columnName == "OPCGRA" || columnName == "OPCVA6") return 9;
                if (columnName == "OPCOID" || columnName == "OPCOI1" || columnName == "OPCOI2" || columnName == "OPCOI3") return 10;
                if (columnName == "OPCNUM") return 10;
            }
            else if (tableName == "OPCAR6")
            {
                if (columnName == "OPCSE2") return 6;
                if (columnName == "OPCSE1" || columnName == "OPCOI4" || columnName == "OPCUBI") return 10;
                if (columnName == "OPCCAN" || columnName == "OPCPOR" || columnName == "OPCPO1") return 3;
                if (columnName == "OPCVA1" || columnName == "OPCTO1") return 9;
            }
            else if (tableName == "OPSARC")
            {
                if (columnName == "OPSSEC") return 6;
            }

            return 0;
        }

        private static int GetKnownTextLength(string table, string column)
        {
            if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
            {
                return 0;
            }

            var tableName = table.Trim().ToUpperInvariant();
            var columnName = column.Trim().ToUpperInvariant();

            if (tableName == "OPCAR5")
            {
                if (columnName == "OPCOBS") return 60;
                if (columnName == "OPCAUT") return 12;
                if (columnName == "OPCCHE") return 15;
            }

            if (tableName == "OPCAR6")
            {
                if (columnName == "OPCDE8") return 200;
                if (columnName == "OPCD01") return 60;
                if (columnName == "OPCC05") return 15;
                if (columnName == "OPCC06") return 10;
            }

            return 0;
        }

        private object NormalizeNumericStringValue(
            string schema,
            string table,
            string column,
            string value,
            int maxDigits,
            bool forLookup)
        {
            var raw = value ?? string.Empty;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return 0m;
            }

            if (maxDigits > 0 && digits.Length > maxDigits)
            {
                _logger.LogWarning(string.Format(
                    "FR3 truncamiento numerico preventivo en {0}.{1}.{2}: digits={3}, max={4}.",
                    schema,
                    table,
                    column,
                    digits.Length,
                    maxDigits));
                digits = digits.Substring(digits.Length - maxDigits, maxDigits);
            }

            decimal parsed;
            if (decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return forLookup ? (object)digits : 0m;
        }

        private static bool TryConvertToDecimal(object value, out decimal result)
        {
            try
            {
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0m;
                return false;
            }
        }

        private Dictionary<string, int> GetTextColumnLengths(
            OdbcConnection conn,
            string schema,
            string table,
            OdbcTransaction tx = null)
        {
            var key = string.Format("{0}.{1}", schema.ToUpperInvariant(), table.ToUpperInvariant());

            lock (_textColumnLengthCacheLock)
            {
                Dictionary<string, int> cached;
                if (_textColumnLengthCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
            }

            var loaded = LoadTextColumnLengths(conn, schema, table, tx);

            lock (_textColumnLengthCacheLock)
            {
                if (!_textColumnLengthCache.ContainsKey(key))
                {
                    _textColumnLengthCache[key] = loaded;
                }
                return _textColumnLengthCache[key];
            }
        }

        private Dictionary<string, int> LoadTextColumnLengths(
            OdbcConnection conn,
            string schema,
            string table,
            OdbcTransaction tx = null)
        {
            var lengths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var sql = @"
                    SELECT COLUMN_NAME, LENGTH, DATA_TYPE
                    FROM QSYS2.SYSCOLUMNS
                    WHERE TABLE_SCHEMA = ?
                      AND TABLE_NAME = ?";

                using (var cmd = new OdbcCommand(sql, conn))
                {
                    if (tx != null) cmd.Transaction = tx;
                    AddParameter(cmd, schema.ToUpperInvariant(), OdbcType.VarChar);
                    AddParameter(cmd, table.ToUpperInvariant(), OdbcType.VarChar);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(0))
                            {
                                continue;
                            }

                            var column = reader.GetString(0).Trim().ToUpperInvariant();
                            var dataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim().ToUpperInvariant();
                            if (!IsTextDataType(dataType))
                            {
                                continue;
                            }

                            int size = 0;
                            if (!reader.IsDBNull(1))
                            {
                                size = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                            }

                            if (size > 0)
                            {
                                lengths[column] = size;
                            }
                        }
                    }
                }

                if (lengths.Count > 0)
                {
                    return lengths;
                }

                var fallbackSql = @"
                    SELECT COLUMN_NAME, COLUMN_SIZE, TYPE_NAME
                    FROM SYSIBM.SQLCOLUMNS
                    WHERE TABLE_SCHEM = ?
                      AND TABLE_NAME = ?";

                using (var cmd2 = new OdbcCommand(fallbackSql, conn))
                {
                    if (tx != null) cmd2.Transaction = tx;
                    AddParameter(cmd2, schema.ToUpperInvariant(), OdbcType.VarChar);
                    AddParameter(cmd2, table.ToUpperInvariant(), OdbcType.VarChar);

                    using (var reader2 = cmd2.ExecuteReader())
                    {
                        while (reader2.Read())
                        {
                            if (reader2.IsDBNull(0))
                            {
                                continue;
                            }

                            var column = reader2.GetString(0).Trim().ToUpperInvariant();
                            var typeName = reader2.IsDBNull(2) ? string.Empty : reader2.GetString(2).Trim().ToUpperInvariant();
                            if (!IsTextDataType(typeName))
                            {
                                continue;
                            }

                            int size = 0;
                            if (!reader2.IsDBNull(1))
                            {
                                size = Convert.ToInt32(reader2.GetValue(1), CultureInfo.InvariantCulture);
                            }

                            if (size > 0)
                            {
                                lengths[column] = size;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(string.Format(
                    "No se pudo cargar longitudes de columnas para {0}.{1}: {2}",
                    schema,
                    table,
                    ex.Message));
            }

            return lengths;
        }

        private Dictionary<string, int> GetNumericColumnLengths(
            OdbcConnection conn,
            string schema,
            string table,
            OdbcTransaction tx = null)
        {
            var key = string.Format("{0}.{1}", schema.ToUpperInvariant(), table.ToUpperInvariant());

            lock (_numericColumnLengthCacheLock)
            {
                Dictionary<string, int> cached;
                if (_numericColumnLengthCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
            }

            var loaded = LoadNumericColumnLengths(conn, schema, table, tx);

            lock (_numericColumnLengthCacheLock)
            {
                if (!_numericColumnLengthCache.ContainsKey(key))
                {
                    _numericColumnLengthCache[key] = loaded;
                }
                return _numericColumnLengthCache[key];
            }
        }

        private Dictionary<string, int> LoadNumericColumnLengths(
            OdbcConnection conn,
            string schema,
            string table,
            OdbcTransaction tx = null)
        {
            var lengths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var sql = @"
                    SELECT COLUMN_NAME, LENGTH, DATA_TYPE
                    FROM QSYS2.SYSCOLUMNS
                    WHERE TABLE_SCHEMA = ?
                      AND TABLE_NAME = ?";

                using (var cmd = new OdbcCommand(sql, conn))
                {
                    if (tx != null) cmd.Transaction = tx;
                    AddParameter(cmd, schema.ToUpperInvariant(), OdbcType.VarChar);
                    AddParameter(cmd, table.ToUpperInvariant(), OdbcType.VarChar);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(0))
                            {
                                continue;
                            }

                            var column = reader.GetString(0).Trim().ToUpperInvariant();
                            var dataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim().ToUpperInvariant();
                            if (!IsNumericDataType(dataType))
                            {
                                continue;
                            }

                            int size = 0;
                            if (!reader.IsDBNull(1))
                            {
                                size = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                            }

                            if (size > 0)
                            {
                                lengths[column] = size;
                            }
                        }
                    }
                }

                if (lengths.Count > 0)
                {
                    return lengths;
                }

                var fallbackSql = @"
                    SELECT COLUMN_NAME, COLUMN_SIZE, TYPE_NAME
                    FROM SYSIBM.SQLCOLUMNS
                    WHERE TABLE_SCHEM = ?
                      AND TABLE_NAME = ?";

                using (var cmd2 = new OdbcCommand(fallbackSql, conn))
                {
                    if (tx != null) cmd2.Transaction = tx;
                    AddParameter(cmd2, schema.ToUpperInvariant(), OdbcType.VarChar);
                    AddParameter(cmd2, table.ToUpperInvariant(), OdbcType.VarChar);

                    using (var reader2 = cmd2.ExecuteReader())
                    {
                        while (reader2.Read())
                        {
                            if (reader2.IsDBNull(0))
                            {
                                continue;
                            }

                            var column = reader2.GetString(0).Trim().ToUpperInvariant();
                            var typeName = reader2.IsDBNull(2) ? string.Empty : reader2.GetString(2).Trim().ToUpperInvariant();
                            if (!IsNumericDataType(typeName))
                            {
                                continue;
                            }

                            int size = 0;
                            if (!reader2.IsDBNull(1))
                            {
                                size = Convert.ToInt32(reader2.GetValue(1), CultureInfo.InvariantCulture);
                            }

                            if (size > 0)
                            {
                                lengths[column] = size;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(string.Format(
                    "No se pudo cargar precision numerica para {0}.{1}: {2}",
                    schema,
                    table,
                    ex.Message));
            }

            return lengths;
        }

        private static bool IsTextDataType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            var normalized = typeName.Trim().ToUpperInvariant();
            return normalized.Contains("CHAR")
                   || normalized.Contains("GRAPHIC")
                   || normalized.Contains("VARCHAR")
                   || normalized.Contains("CLOB");
        }

        private static bool IsNumericDataType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            var normalized = typeName.Trim().ToUpperInvariant();
            return normalized.Contains("DEC")
                   || normalized.Contains("NUM")
                   || normalized.Contains("INT")
                   || normalized.Contains("REAL")
                   || normalized.Contains("FLOAT")
                   || normalized.Contains("DOUBLE")
                   || normalized.Contains("PACKED")
                   || normalized.Contains("ZONED");
        }

        private HashSet<string> GetColumnas(OdbcConnection conn, string schema, string table, OdbcTransaction tx = null)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Intento 1: QSYS2.SYSCOLUMNS
                var sql = @"
                    SELECT COLUMN_NAME
                    FROM QSYS2.SYSCOLUMNS
                    WHERE TABLE_SCHEMA = ?
                      AND TABLE_NAME = ?";
                using (var cmd = new OdbcCommand(sql, conn))
                {
                    if (tx != null) cmd.Transaction = tx;
                    AddParameter(cmd, schema.ToUpperInvariant(), OdbcType.VarChar);
                    AddParameter(cmd, table.ToUpperInvariant(), OdbcType.VarChar);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                columnas.Add(reader.GetString(0).Trim().ToUpperInvariant());
                            }
                        }
                    }
                }

                // Si QSYS2 no devolvió nada, intentar con SYSIBM.SQLCOLUMNS
                if (columnas.Count == 0)
                {
                    _logger.LogWarning(string.Format(
                        "GetColumnas: QSYS2.SYSCOLUMNS devolvió 0 columnas para {0}.{1}. Intentando SYSIBM.SQLCOLUMNS...",
                        schema, table));

                    var sql2 = @"
                        SELECT COLUMN_NAME
                        FROM SYSIBM.SQLCOLUMNS
                        WHERE TABLE_SCHEM = ?
                          AND TABLE_NAME = ?";
                    using (var cmd2 = new OdbcCommand(sql2, conn))
                    {
                        if (tx != null) cmd2.Transaction = tx;
                        AddParameter(cmd2, schema.ToUpperInvariant(), OdbcType.VarChar);
                        AddParameter(cmd2, table.ToUpperInvariant(), OdbcType.VarChar);

                        using (var reader2 = cmd2.ExecuteReader())
                        {
                            while (reader2.Read())
                            {
                                if (!reader2.IsDBNull(0))
                                {
                                    columnas.Add(reader2.GetString(0).Trim().ToUpperInvariant());
                                }
                            }
                        }
                    }
                }

                // Si aún no hay columnas, intentar con GetSchema de ODBC
                if (columnas.Count == 0)
                {
                    _logger.LogWarning(string.Format(
                        "GetColumnas: SYSIBM.SQLCOLUMNS también vacío para {0}.{1}. Intentando OdbcConnection.GetSchema...",
                        schema, table));

                    try
                    {
                        var schemaTable = conn.GetSchema("Columns", new string[] { null, schema.ToUpperInvariant(), table.ToUpperInvariant(), null });
                        foreach (System.Data.DataRow row in schemaTable.Rows)
                        {
                            var colName = row["COLUMN_NAME"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(colName))
                            {
                                columnas.Add(colName.Trim().ToUpperInvariant());
                            }
                        }
                    }
                    catch (Exception exGetSchema)
                    {
                        _logger.LogWarning("GetColumnas: GetSchema fallback también falló: " + exGetSchema.Message);
                    }
                }

                if (columnas.Count > 0)
                {
                    _logger.LogInfo(string.Format("GetColumnas: {0} columnas encontradas en {1}.{2}", columnas.Count, schema, table));
                }
                else
                {
                    _logger.LogWarning(string.Format("GetColumnas: No se encontraron columnas en {0}.{1} por ningún método.", schema, table));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(string.Format("GetColumnas: Error consultando {0}.{1}: {2}", schema, table, ex.Message));
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return columnas;
        }

        private static string ResolveAnio(FacturaAs400Record record)
        {
            if (record == null)
            {
                return DateTime.Now.ToString("yyyy", CultureInfo.InvariantCulture);
            }

            return string.IsNullOrWhiteSpace(record.Anio)
                ? record.FechaEmision.ToString("yyyy", CultureInfo.InvariantCulture)
                : record.Anio.Trim();
        }

        private static decimal SafeDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0m;
            }

            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        private static string ExtractCorrelationToken(string observaciones)
        {
            if (string.IsNullOrWhiteSpace(observaciones))
            {
                return null;
            }

            var raw = observaciones.Trim();
            var upper = raw.ToUpperInvariant();
            var idx = upper.IndexOf("ORD:");
            if (idx < 0)
            {
                return null;
            }

            var segment = raw.Substring(idx);
            var stop = segment.IndexOfAny(new[] { '|', ';', ',' });
            if (stop > 0)
            {
                segment = segment.Substring(0, stop);
            }

            return segment.Trim();
        }

        private static string SafeString(string value, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback ?? string.Empty;
            }
            return value.Trim();
        }

        private static string SanitizeTextForAs400(string value)
        {
            var input = SafeString(value);
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var normalized = input.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (character < 32)
                {
                    builder.Append(' ');
                    continue;
                }

                if (character <= 126)
                {
                    builder.Append(character);
                    continue;
                }

                builder.Append(' ');
            }

            return CollapseWhitespace(builder.ToString());
        }

        private static string TruncateToByteLength(string value, int maxBytes)
        {
            var input = value ?? string.Empty;
            if (maxBytes <= 0 || string.IsNullOrEmpty(input))
            {
                return input;
            }

            if (Encoding.ASCII.GetByteCount(input) <= maxBytes)
            {
                return input;
            }

            var builder = new StringBuilder(input.Length);
            var totalBytes = 0;

            foreach (var character in input)
            {
                var charAsString = character.ToString();
                var charBytes = Encoding.ASCII.GetByteCount(charAsString);
                if (totalBytes + charBytes > maxBytes)
                {
                    break;
                }

                builder.Append(character);
                totalBytes += charBytes;
            }

            return builder.ToString().TrimEnd();
        }

        private static string CollapseWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var previousWasWhitespace = false;

            foreach (var character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace)
                    {
                        builder.Append(' ');
                    }

                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(character);
                previousWasWhitespace = false;
            }

            return builder.ToString().Trim();
        }

        private static string GetSetting(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static OdbcType GetOdbcType(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return OdbcType.VarChar;
            }

            if (value is int || value is long || value is short)
            {
                return OdbcType.Int;
            }

            if (value is decimal || value is float || value is double)
            {
                return OdbcType.Numeric;
            }

            if (value is DateTime)
            {
                return OdbcType.DateTime;
            }

            return OdbcType.VarChar;
        }

        /// <summary>
        /// Sanitiza el número de factura eliminando caracteres especiales problemáticos para AS400/DB2
        /// </summary>
        /// <param name="numeroFactura">Número de factura original</param>
        /// <returns>Número de factura sanitizado</returns>
        private static string SanitizarNumeroFactura(string numeroFactura)
        {
            if (string.IsNullOrWhiteSpace(numeroFactura))
            {
                return string.Empty;
            }

            // Eliminar caracteres especiales problemáticos para SQL AS400/DB2
            // Permitidos: letras, números, guiones y guiones bajos
            var caracteresProblematicos = new[] { '|', ';', '\'', '"', '\\', '/', '<', '>', '&', '%', '*', '(', ')', '[', ']', '{', '}', '=', '+', '!', '?', ',', ':', '#' };
            
            var sanitizado = numeroFactura.Trim();
            
            foreach (var c in caracteresProblematicos)
            {
                sanitizado = sanitizado.Replace(c.ToString(), string.Empty);
            }

            // Limitar a 50 caracteres (longitud máxima típica en AS400)
            if (sanitizado.Length > 50)
            {
                sanitizado = sanitizado.Substring(0, 50);
            }

            return sanitizado.Trim();
        }
    }
}
