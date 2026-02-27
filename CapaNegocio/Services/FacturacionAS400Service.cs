using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Models;

namespace CapaNegocio.Services
{
    public class FacturacionAS400Service
    {
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

            try
            {
                var ordenDao = new OrdenRecaudacionDAO();
                var orden = ordenDao.ObtenerOrdenPorId(ordenId);
                if (orden == null)
                {
                    mensaje = "Orden no encontrada para facturación AS400.";
                    return false;
                }

                var detalles = ordenDao.ObtenerDetallesPorOrdenId(ordenId);
                var pago = ordenDao.ObtenerUltimoPagoPorOrden(ordenId);

                var record = MapearFactura(
                    ordenId,
                    pagoId,
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
                return dao.RegistrarFactura(record, out mensaje);
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
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
                        CodigoContable = ResolverCodigoContable(det?.ConceptoCodigo, codigoContableDefault),
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
            if (!string.IsNullOrWhiteSpace(observaciones))
            {
                return observaciones.Trim();
            }

            var numeroOrden = orden?.NumeroOrden ?? "N/A";
            var cp = !string.IsNullOrWhiteSpace(pago?.NumeroComprobante) ? pago.NumeroComprobante.Trim() : (numeroFactura ?? string.Empty);
            return string.Format("AOCR, C/PAGO: {0}, ORDEN: {1}, FACT: {2}", cp, numeroOrden, numeroFactura ?? string.Empty);
        }

        private static string ConstruirDescripcionDetalleFr3(
            CapaDatos.Entidades.DetalleOrden det,
            CapaDatos.Entidades.OrdenRecaudacion orden,
            Pago pago,
            string numeroFactura)
        {
            var concepto = !string.IsNullOrWhiteSpace(det?.Descripcion)
                ? det.Descripcion.Trim()
                : (!string.IsNullOrWhiteSpace(det?.ConceptoNombre) ? det.ConceptoNombre.Trim() : "SERVICIO AOCR");
            var numeroOrden = orden?.NumeroOrden ?? (orden?.Id.ToString() ?? "N/A");
            var pagoRef = !string.IsNullOrWhiteSpace(pago?.NumeroComprobante) ? pago.NumeroComprobante.Trim() : string.Empty;
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

            // En AOCR "concepto_codigo" suele ser funcional (ej. REN_AOCR). Solo usamos el valor
            // cuando parece un código contable para no enviar basura a AS400.
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

            var primerCodigo = detalles?.FirstOrDefault(d => d != null && !string.IsNullOrWhiteSpace(d.ConceptoCodigo))?.ConceptoCodigo;
            if (!string.IsNullOrWhiteSpace(primerCodigo))
            {
                return Truncar($"{primerCodigo}/{orden?.NumeroOrden}", 50);
            }

            return Truncar($"AOCR/{orden?.NumeroOrden}", 50);
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
            var metodo = (pago?.MetodoPago ?? string.Empty).Trim().ToUpperInvariant();
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
                .Select(d => $"{d.ConceptoCodigo} {d.ConceptoNombre} {d.Descripcion}"))
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

            // Emula la idea del legacy: remover prefijo/sufijo cuando viene con adornos.
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
