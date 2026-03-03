using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Infrastructure;
using CapaDatos.Models;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    public class FacturacionAS400Service
    {
        private readonly ILoggingService _logger;
        private readonly SyncLogService _syncLog;
        private readonly IdempotencyService _idempotency;
        private readonly AuditTrailService _audit;

        public FacturacionAS400Service()
        {
            _logger = LoggingServiceFactory.Create();
            _syncLog = new SyncLogService();
            _idempotency = new IdempotencyService();
            _audit = new AuditTrailService();
        }

        public bool TestDb2Connection(out string mensaje)
        {
            mensaje = null;
            try
            {
                var dao = new FacturacionAS400DAO();
                return dao.TestConnection(out mensaje);
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
                return false;
            }
        }

        public bool TryReintentarFr3(int ordenId, string usuario, out string mensaje)
        {
            mensaje = null;

            if (!IsEnabled())
            {
                mensaje = "Facturación AS400 deshabilitada por configuración.";
                return false;
            }

            var ordenDao = new OrdenRecaudacionDAO();
            var factura = ordenDao.ObtenerFacturaPagoPorOrden(ordenId);
            if (factura == null)
            {
                mensaje = "No existe factura registrada para reintentar FR3.";
                return false;
            }

            if (string.Equals(factura.Fr3Estado, "FR3_GENERADO", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(factura.Fr3Numero))
            {
                mensaje = "FR3 ya generado: " + factura.Fr3Numero;
                return true;
            }

            return TryRegistrarFactura(
                ordenId,
                factura.PagoId,
                factura.NumeroFactura,
                factura.AutorizacionFactura,
                factura.FechaEmision,
                factura.Subtotal,
                factura.Iva,
                factura.Total,
                factura.Observaciones,
                usuario,
                out mensaje);
        }

        public bool TryRegistrarFactura(
            int ordenId,
            int? pagoId,
            string numeroFactura,
            string autorizacionFactura,
            DateTime fechaEmision,
            decimal subtotal,
            decimal iva,
            decimal total,
            string observaciones,
            string usuario,
            out string mensaje)
        {
            mensaje = null;

            if (!IsEnabled())
            {
                return true;
            }

            // --- Idempotency check ---
            var claveIdempotencia = IdempotencyService.GenerarClaveFr3(ordenId, numeroFactura);
            string resultadoExistente;
            if (_idempotency.ExisteOperacion(claveIdempotencia, out resultadoExistente))
            {
                mensaje = resultadoExistente ?? "FR3 ya procesado (idempotencia).";
                _logger.LogInfo("FR3 idempotente detectado para orden " + ordenId);
                return true;
            }

            if (!_idempotency.TryAcquire(claveIdempotencia, "FR3_GENERAR"))
            {
                mensaje = "Operación FR3 ya en proceso para esta orden (concurrencia).";
                return false;
            }

            // --- Sync log start ---
            var syncLogId = _syncLog.IniciarOperacion(
                "FR3_GENERAR",
                ordenId,
                pagoId,
                claveIdempotencia,
                usuario,
                null,
                string.Format("orden:{0}|pago:{1}|total:{2}", ordenId, pagoId, total));

            try
            {
                var ordenDao = new OrdenRecaudacionDAO();
                var orden = ordenDao.ObtenerOrdenPorId(ordenId);
                if (orden == null)
                {
                    mensaje = "Orden no encontrada para facturación AS400.";
                    _idempotency.MarcarError(claveIdempotencia, mensaje);
                    _syncLog.FallarOperacion(syncLogId, mensaje, "ORDEN_NOT_FOUND", false);
                    return false;
                }

                var detalles = ordenDao.ObtenerDetallesPorOrdenId(ordenId) ?? new List<CapaDatos.Entidades.DetalleOrden>();
                Pago pago = null;

                if (pagoId.HasValue && pagoId.Value > 0)
                {
                    pago = ordenDao.ObtenerPagoPorId(pagoId.Value);
                    if (pago == null)
                    {
                        mensaje = "No se encontró el pago especificado para generar FR3.";
                        _idempotency.MarcarError(claveIdempotencia, mensaje);
                        _syncLog.FallarOperacion(syncLogId, mensaje, "PAGO_NOT_FOUND", false);
                        return false;
                    }
                }
                else
                {
                    pago = ordenDao.ObtenerUltimoPagoPorOrden(ordenId);
                }

                var record = MapearFactura(
                    ordenId,
                    pago != null ? (int?)pago.Id : pagoId,
                    numeroFactura,
                    autorizacionFactura,
                    fechaEmision,
                    subtotal,
                    iva,
                    total,
                    observaciones,
                    usuario,
                    orden,
                    detalles,
                    pago);

                var dao = new FacturacionAS400DAO();
                FacturacionAS400Result resultadoFr3;
                string errorAs400;
                var ok = dao.RegistrarFactura(record, out resultadoFr3, out errorAs400);

                if (ok)
                {
                    string errorRegistro;
                    var estadoFr3 = "FR3_GENERADO";
                    var detalle = resultadoFr3 != null && resultadoFr3.EsDuplicado
                        ? "FR3 existente reutilizado por idempotencia."
                        : null;

                    ordenDao.RegistrarResultadoFr3(
                        ordenId,
                        pago != null ? (int?)pago.Id : pagoId,
                        resultadoFr3,
                        estadoFr3,
                        detalle,
                        usuario,
                        out errorRegistro);

                    if (!string.IsNullOrWhiteSpace(errorRegistro))
                    {
                        _logger.LogWarning("FR3 generado, pero no se pudo persistir trazabilidad en PG: " + errorRegistro);
                    }

                    var fr3Numero = resultadoFr3 != null ? resultadoFr3.NumeroFr3 : null;
                    mensaje = !string.IsNullOrWhiteSpace(fr3Numero)
                        ? "FR3 generado: " + fr3Numero
                        : "FR3 generado correctamente.";

                    // --- Enterprise: sync complete, idempotency mark, audit ---
                    _syncLog.CompletarOperacion(syncLogId, fr3Numero, fr3Numero);
                    _idempotency.MarcarCompletada(claveIdempotencia, mensaje);
                    _audit.RegistrarFr3Generado(ordenId, fr3Numero, null, usuario);

                    return true;
                }

                // --- FR3 failed ---
                string errorPersistencia;
                ordenDao.RegistrarResultadoFr3(
                    ordenId,
                    pago != null ? (int?)pago.Id : pagoId,
                    null,
                    "FR3_ERROR",
                    errorAs400,
                    usuario,
                    out errorPersistencia);

                if (!string.IsNullOrWhiteSpace(errorPersistencia))
                {
                    _logger.LogWarning("FR3 error registrado sin persistencia completa en PG: " + errorPersistencia);
                }

                mensaje = errorAs400 ?? "Error no determinado al generar FR3.";

                // --- Enterprise: sync fail, idempotency release, audit error ---
                _syncLog.FallarOperacion(syncLogId, mensaje, "FR3_AS400_ERROR", true);
                _idempotency.Liberar(claveIdempotencia);
                _audit.RegistrarFr3Error(ordenId, mensaje, null, usuario);

                return false;
            }
            catch (CircuitBreakerOpenException cbEx)
            {
                mensaje = "Sistema AS400 temporalmente no disponible (circuit breaker abierto). Reintente en unos minutos.";
                _syncLog.FallarOperacion(syncLogId, mensaje, "CIRCUIT_BREAKER_OPEN", true);
                _idempotency.Liberar(claveIdempotencia);
                _audit.RegistrarFr3Error(ordenId, mensaje, null, usuario);
                _logger.LogWarning("CircuitBreaker abierto para FR3 orden " + ordenId + ": " + cbEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
                _syncLog.FallarOperacion(syncLogId, mensaje, "FR3_SERVICE_ERROR", true);
                _idempotency.Liberar(claveIdempotencia);
                _audit.RegistrarFr3Error(ordenId, mensaje, null, usuario);
                _logger.LogError(ex, new LogContext
                {
                    ErrorCode = "FR3_SERVICE_ERROR",
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "OrdenId", ordenId },
                        { "PagoId", pagoId.HasValue ? (object)pagoId.Value : null }
                    }
                });
                return false;
            }
        }

        private static FacturaAs400Record MapearFactura(
            int ordenId,
            int? pagoId,
            string numeroFactura,
            string autorizacionFactura,
            DateTime fechaEmision,
            decimal subtotal,
            decimal iva,
            decimal total,
            string observaciones,
            string usuario,
            CapaDatos.Entidades.OrdenRecaudacion orden,
            List<CapaDatos.Entidades.DetalleOrden> detalles,
            Pago pago)
        {
            var aeropuerto = ResolverCodigoAeropuerto(orden?.LugarEmision);
            var tipoOperacion = GetSetting("AS400:Facturacion:TipoOperacion", "AO").ToUpperInvariant();
            var formaPago = ResolverFormaPago(pago);
            var ruta = ConstruirRutaFr3(orden, detalles);
            var bancoDefault = GetSetting("AS400:Facturacion:BancoDefault", string.Empty);
            var tipoCobro = GetSetting("AS400:Facturacion:TipoCobro", "01");
            var oidFormularioStr = GetSetting("AS400:Facturacion:OidFormularioDefault", "0");
            var oidFormularioNacStr = GetSetting("AS400:Facturacion:OidFormularioNacional", oidFormularioStr);
            var oidFormularioIntStr = GetSetting("AS400:Facturacion:OidFormularioInternacional", oidFormularioStr);
            var codigoContableDefault = GetSetting("AS400:Facturacion:CodigoContableDefault", "623.01.11.02");
            var codigoItemDefault = GetSetting("AS400:Facturacion:CodigoItemDefault", "FITEM");

            var cantidadTotal = (detalles ?? new List<CapaDatos.Entidades.DetalleOrden>())
                .Where(d => d != null)
                .Sum(d => d.Cantidad > 0 ? d.Cantidad : 1);
            var numAterriza = cantidadTotal > 0
                ? cantidadTotal
                : GetSettingInt("AS400:Facturacion:NumAterrizaDefault", 1);

            decimal oidFormulario = 0m;
            decimal.TryParse(oidFormularioStr, NumberStyles.Any, CultureInfo.InvariantCulture, out oidFormulario);
            decimal oidFormularioNac = oidFormulario;
            decimal oidFormularioInt = oidFormulario;
            decimal.TryParse(oidFormularioNacStr, NumberStyles.Any, CultureInfo.InvariantCulture, out oidFormularioNac);
            decimal.TryParse(oidFormularioIntStr, NumberStyles.Any, CultureInfo.InvariantCulture, out oidFormularioInt);

            var nacInter = ResolverNacInter(orden, detalles);
            var descripcionCuenta = nacInter == "N"
                ? GetSetting("AS400:Facturacion:DescripcionCuentaNacional", "VUELOS CHARTER O ESPECIALES NACIONAL")
                : GetSetting("AS400:Facturacion:DescripcionCuentaInternacional", "VUELOS CHARTER O ESPECIALES INTERNACIONAL");
            var fechaControl = fechaEmision.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var deposito = !string.IsNullOrWhiteSpace(pago?.NumeroComprobante) ? pago.NumeroComprobante.Trim() : numeroFactura;
            var banco = !string.IsNullOrWhiteSpace(pago?.BancoOrigen) ? pago.BancoOrigen.Trim() : bancoDefault;
            var observacionFr3 = ConstruirObservacionFr3(orden, pago, numeroFactura, observaciones);

            var record = new FacturaAs400Record
            {
                OrdenId = ordenId,
                PagoId = pagoId,
                NumeroFactura = numeroFactura,
                AutorizacionFactura = autorizacionFactura,
                FechaEmision = fechaEmision,
                Subtotal = subtotal,
                Iva = iva,
                Total = total,
                Observaciones = observacionFr3,
                Ruc = orden?.RucCedula,
                Correo = orden?.Correo,
                Compania = orden?.Compania,
                Telefono = orden?.Telefono,
                Aeropuerto = aeropuerto,
                Anio = fechaEmision.ToString("yyyy", CultureInfo.InvariantCulture),
                FechaControl = fechaControl,
                TipoOperacion = tipoOperacion,
                Ruta = ruta,
                NumAterrizaPais = numAterriza,
                FormaPago = formaPago,
                CodigoBanco = banco,
                Deposito = deposito,
                UsuarioRegistro = string.IsNullOrWhiteSpace(usuario) ? "AOCR" : usuario
            };

            record.Autorizacion = NormalizarAutorizacion(autorizacionFactura);
            record.GranTotalLetras = ConstruirGranTotalLetrasFallback(total);
            record.NacInter = nacInter;
            record.NombreAeropuerto = ResolverNombreAeropuerto(aeropuerto, orden?.LugarEmision);
            record.EmailUsuarioDGAC = string.IsNullOrWhiteSpace(usuario) ? null : (usuario.Trim() + "@aviacioncivil.gob.ec");
            record.Callsign = GetSetting("AS400:Facturacion:CallsignDefault", string.Empty);
            record.Matricula = GetSetting("AS400:Facturacion:MatriculaDefault", string.Empty);
            record.Modelo = GetSetting("AS400:Facturacion:ModeloDefault", string.Empty);
            record.PesoMatricula = GetSettingDecimal("AS400:Facturacion:PesoMatriculaDefault", 0m);
            record.CodigoOACICia = GetSetting("AS400:Facturacion:CodigoOACICiaDefault", string.Empty);
            record.FechaRecepcion = fechaControl;
            record.Origen = GetSetting("AS400:Facturacion:OrigenDefault", string.Empty);
            record.Destino = GetSetting("AS400:Facturacion:DestinoDefault", string.Empty);
            record.Retorno = GetSetting("AS400:Facturacion:RetornoDefault", string.Empty);
            record.OidCiaAviacion = GetSettingDecimal("AS400:Facturacion:OidCiaAviacionDefault", 0m);
            record.OidUbicacion = GetSettingDecimal("AS400:Facturacion:OidUbicacionDefault", 0m);
            record.OidUbicacionCliente = GetSettingDecimal("AS400:Facturacion:OidUbicacionClienteDefault", 0m);
            record.IdAeropuerto = GetSettingDecimal("AS400:Facturacion:IdAeropuertoDefault", 0m);
            record.Procesado = "E";

            if (detalles != null && detalles.Count > 0)
            {
                foreach (var det in detalles)
                {
                    var cantidad = det.Cantidad > 0 ? det.Cantidad : 1;
                    var valor = det.ValorUnitario > 0 ? det.ValorUnitario : (cantidad > 0 ? det.Subtotal / cantidad : 0m);
                    var totalLinea = det.TotalLinea > 0 ? det.TotalLinea : (valor * cantidad);
                    var descripcionDetalle = ConstruirDescripcionDetalleFr3(det, orden, pago, numeroFactura);

                    record.Detalles.Add(new FacturaAs400Detalle
                    {
                        CodigoContable = ResolverCodigoContable(det != null ? det.ConceptoCodigo : null, codigoContableDefault),
                        Descripcion = descripcionDetalle,
                        Cantidad = cantidad,
                        Valor = valor,
                        Total = totalLinea,
                        TipoCobro = tipoCobro,
                        OidFormulario = (nacInter == "N" ? oidFormularioNac : oidFormularioInt) > 0m
                            ? (decimal?)(nacInter == "N" ? oidFormularioNac : oidFormularioInt)
                            : null,
                        HacerDescuento = "N",
                        CobrarImpuesto = "N",
                        IngresarCantidad = "S",
                        DescripcionCuenta = descripcionCuenta,
                        Codigo = codigoItemDefault
                    });
                }
            }
            else
            {
                record.Detalles.Add(new FacturaAs400Detalle
                {
                    CodigoContable = string.Empty,
                    Descripcion = record.Observaciones,
                    Cantidad = 1,
                    Valor = total,
                    Total = total,
                    TipoCobro = tipoCobro,
                    OidFormulario = (nacInter == "N" ? oidFormularioNac : oidFormularioInt) > 0m
                        ? (decimal?)(nacInter == "N" ? oidFormularioNac : oidFormularioInt)
                        : null,
                    HacerDescuento = "N",
                    CobrarImpuesto = "N",
                    IngresarCantidad = "S",
                    DescripcionCuenta = descripcionCuenta,
                    Codigo = codigoItemDefault
                });
            }

            return record;
        }

        private static string ConstruirObservacionFr3(
            CapaDatos.Entidades.OrdenRecaudacion orden,
            Pago pago,
            string numeroFactura,
            string observaciones)
        {
            var codigoSolicitud = 0;
            if (orden != null && orden.CodigoSolicitud.HasValue)
            {
                codigoSolicitud = orden.CodigoSolicitud.Value;
            }
            if (codigoSolicitud <= 0 && orden != null)
            {
                codigoSolicitud = orden.Id;
            }

            var correlacion = string.Format(
                "SOL:{0}|ORD:{1}",
                codigoSolicitud > 0 ? codigoSolicitud.ToString(CultureInfo.InvariantCulture) : "N/A",
                orden != null ? (orden.NumeroOrden ?? orden.Id.ToString(CultureInfo.InvariantCulture)) : "N/A");

            if (!string.IsNullOrWhiteSpace(observaciones))
            {
                return Truncar(observaciones.Trim() + " | " + correlacion, 250);
            }

            var cp = !string.IsNullOrWhiteSpace(pago != null ? pago.NumeroComprobante : null)
                ? pago.NumeroComprobante.Trim()
                : (numeroFactura ?? string.Empty);

            return Truncar(
                string.Format("AOCR | {0} | C/PAGO:{1} | FACT:{2}", correlacion, cp, numeroFactura ?? string.Empty),
                250);
        }

        private static string ConstruirDescripcionDetalleFr3(
            CapaDatos.Entidades.DetalleOrden det,
            CapaDatos.Entidades.OrdenRecaudacion orden,
            Pago pago,
            string numeroFactura)
        {
            var concepto = !string.IsNullOrWhiteSpace(det != null ? det.Descripcion : null)
                ? det.Descripcion.Trim()
                : (!string.IsNullOrWhiteSpace(det != null ? det.ConceptoNombre : null) ? det.ConceptoNombre.Trim() : "SERVICIO AOCR");
            var numeroOrden = orden != null ? (orden.NumeroOrden ?? orden.Id.ToString()) : "N/A";
            var pagoRef = !string.IsNullOrWhiteSpace(pago != null ? pago.NumeroComprobante : null) ? pago.NumeroComprobante.Trim() : string.Empty;
            return string.Format("{0} | ORDEN {1} | C/P {2} | FACT {3}", concepto, numeroOrden, pagoRef, numeroFactura ?? string.Empty);
        }

        private static string ResolverCodigoContable(string conceptoCodigo, string codigoContableDefault)
        {
            var fallback = string.IsNullOrWhiteSpace(codigoContableDefault) ? "623.01.11.02" : codigoContableDefault.Trim();
            if (string.IsNullOrWhiteSpace(conceptoCodigo))
            {
                return fallback;
            }

            var codigo = conceptoCodigo.Trim();
            var looksAccountingCode = codigo.Any(char.IsDigit) && codigo.Contains(".");
            return looksAccountingCode ? codigo : fallback;
        }

        private static string ConstruirRutaFr3(CapaDatos.Entidades.OrdenRecaudacion orden, List<CapaDatos.Entidades.DetalleOrden> detalles)
        {
            var rutaConfig = GetSetting("AS400:Facturacion:RutaDefault", string.Empty);
            if (!string.IsNullOrWhiteSpace(rutaConfig))
            {
                return rutaConfig.Trim();
            }

            var primerCodigo = detalles != null
                ? detalles.FirstOrDefault(d => d != null && !string.IsNullOrWhiteSpace(d.ConceptoCodigo))?.ConceptoCodigo
                : null;
            if (!string.IsNullOrWhiteSpace(primerCodigo))
            {
                return Truncar(string.Format("{0}/{1}", primerCodigo, orden != null ? orden.NumeroOrden : null), 50);
            }

            return Truncar("AOCR/" + (orden != null ? orden.NumeroOrden : string.Empty), 50);
        }

        private static string ResolverCodigoAeropuerto(string lugarEmision)
        {
            var porConfig = GetSetting("AS400:Facturacion:DefaultAeropuerto", "SEQU");
            if (string.IsNullOrWhiteSpace(lugarEmision))
            {
                return porConfig.Trim().ToUpperInvariant();
            }

            var lugar = lugarEmision.Trim().ToUpperInvariant();
            if (lugar.Contains("QUITO")) return "SEQU";
            if (lugar.Contains("GUAYAQUIL")) return "SEGU";
            if (lugar.Contains("CUENCA")) return "SECU";

            return porConfig.Trim().ToUpperInvariant();
        }

        private static string ResolverNombreAeropuerto(string codigoAeropuerto, string lugarEmision)
        {
            if (string.Equals(codigoAeropuerto, "SEQU", StringComparison.OrdinalIgnoreCase))
            {
                return "DGAC-MATRIZ QUITO";
            }

            var cfg = GetSetting("AS400:Facturacion:NombreAeropuertoDefault", string.Empty);
            if (!string.IsNullOrWhiteSpace(cfg))
            {
                return cfg.Trim();
            }

            return string.IsNullOrWhiteSpace(lugarEmision) ? "DGAC" : ("DGAC-" + lugarEmision.Trim().ToUpperInvariant());
        }

        private static string ResolverFormaPago(Pago pago)
        {
            var defaultCode = GetSetting("AS400:Facturacion:FormaPago", "02");
            var metodo = (pago != null ? pago.MetodoPago : string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(metodo))
            {
                return defaultCode;
            }

            if (metodo.Contains("TRANSFER") || metodo.Contains("DEPOS"))
            {
                return "02";
            }

            if (metodo.Contains("EFECT"))
            {
                return GetSetting("AS400:Facturacion:FormaPagoEfectivo", defaultCode);
            }

            return defaultCode;
        }

        private static string ResolverNacInter(CapaDatos.Entidades.OrdenRecaudacion orden, List<CapaDatos.Entidades.DetalleOrden> detalles)
        {
            var cfg = GetSetting("AS400:Facturacion:NacInterDefault", "N").Trim().ToUpperInvariant();
            if (cfg == "N" || cfg == "I")
            {
                return cfg;
            }

            var texto = string.Join(" ", (detalles ?? new List<CapaDatos.Entidades.DetalleOrden>())
                .Where(d => d != null)
                .Select(d => string.Format("{0} {1} {2}", d.ConceptoCodigo, d.ConceptoNombre, d.Descripcion)))
                .ToUpperInvariant();

            if (texto.Contains("INT") || texto.Contains("INTERNAC"))
            {
                return "I";
            }

            return "N";
        }

        private static string NormalizarAutorizacion(string autorizacion)
        {
            var valor = (autorizacion ?? string.Empty).Trim();
            if (valor.Length <= 12)
            {
                return valor;
            }

            try
            {
                return valor.Substring(6, valor.Length - 12);
            }
            catch
            {
                return valor;
            }
        }

        private static string ConstruirGranTotalLetrasFallback(decimal total)
        {
            return string.Format(CultureInfo.InvariantCulture, "TOTAL {0:0.00}", total);
        }

        private static int GetSettingInt(string key, int fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            int parsed;
            return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static decimal GetSettingDecimal(string key, decimal fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            decimal parsed;
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static string Truncar(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var clean = value.Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }

        public static bool IsEnabled()
        {
            var flag = GetSetting("AS400:Facturacion:Enabled", "false");
            return flag.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSetting(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
