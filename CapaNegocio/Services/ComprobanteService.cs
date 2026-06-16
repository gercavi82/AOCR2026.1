using System;
using System.IO;
using System.Web;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaNegocio.Helpers;

namespace CapaNegocio.Services
{
    public class ComprobanteService
    {
        public const string MensajeAprobacionSinComprobante =
            "No se puede aprobar el pago porque el RT aún no ha cargado el comprobante de depósito o transferencia.";

        private const string MensajeBase = MensajeAprobacionSinComprobante;

        public bool ExisteComprobanteValido(int ordenId)
        {
            return ExisteComprobanteValido(ordenId, out _);
        }

        public bool ExisteComprobanteValido(int ordenId, out string mensaje)
        {
            mensaje = MensajeBase;

            if (ordenId <= 0)
            {
                return false;
            }

            try
            {
                var dao = new OrdenRecaudacionDAO();

                var rutaFactura = dao.ObtenerRutaFacturaPago(ordenId);
                if (!string.IsNullOrWhiteSpace(rutaFactura))
                {
                    if (ArchivoExiste(rutaFactura, out var detalleFactura))
                    {
                        return true;
                    }

                    var detalle = string.IsNullOrWhiteSpace(detalleFactura) ? "Archivo no existe" : detalleFactura;
                    CapaNegocio.LogBL.RegistrarAdvertencia(
                        $"Factura registrada sin archivo. OrdenId={ordenId} Ruta={rutaFactura}. Detalle={detalle}",
                        "ComprobanteService");
                    return false;
                }

                var pago = dao.ObtenerUltimoPagoPorOrden(ordenId);
                if (pago != null)
                {
                    var estado = (pago.Estado ?? string.Empty).Trim().ToUpperInvariant();
                    if (string.Equals(estado, EstadoPago.Anulado, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(estado, EstadoPago.Rechazado, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(pago.RutaComprobante))
                    {
                        if (ArchivoExiste(pago.RutaComprobante, out var detallePago))
                        {
                            return true;
                        }

                        var detalle = string.IsNullOrWhiteSpace(detallePago) ? "Archivo no existe" : detallePago;
                        CapaNegocio.LogBL.RegistrarAdvertencia(
                            $"Comprobante registrado sin archivo. OrdenId={ordenId} Ruta={pago.RutaComprobante}. Detalle={detalle}",
                            "ComprobanteService");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError(
                    $"Error validando comprobante. OrdenId={ordenId}",
                    ex.ToString(),
                    "ComprobanteService");
                return false;
            }
        }

        private static bool ArchivoExiste(string ruta, out string detalle)
        {
            detalle = null;
            if (string.IsNullOrWhiteSpace(ruta))
            {
                detalle = "Ruta vacia.";
                return false;
            }

            try
            {
                var rutaNormalizada = FileStorageHelper.NormalizeStoredPath(ruta);
                string path = rutaNormalizada;
                if (rutaNormalizada.StartsWith("~"))
                {
                    var ctx = HttpContext.Current;
                    if (ctx?.Server != null)
                    {
                        path = ctx.Server.MapPath(rutaNormalizada);
                    }

                    if (!File.Exists(path))
                    {
                        var baseVirtual = FileStorageHelper.NormalizeStoredPath(FileStorageHelper.BasePathStorage).TrimEnd('/');
                        if (!string.IsNullOrWhiteSpace(baseVirtual) &&
                            rutaNormalizada.StartsWith(baseVirtual + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            var relativePath = rutaNormalizada.Substring(baseVirtual.Length).TrimStart('/', '\\');
                            var basePath = FileStorageHelper.GetPhysicalBasePath(FileStorageHelper.BasePathStorage);
                            path = Path.Combine(basePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        }
                    }
                }
                else if (!Path.IsPathRooted(rutaNormalizada))
                {
                    var basePath = FileStorageHelper.GetPhysicalBasePath(FileStorageHelper.BasePathStorage);
                    path = Path.Combine(basePath, rutaNormalizada.TrimStart('/', '\\'));
                }

                if (File.Exists(path))
                {
                    return true;
                }

                detalle = $"Archivo no existe: {path}";
                return false;
            }
            catch (Exception ex)
            {
                detalle = "Error validando archivo: " + ex.Message;
                return false;
            }
        }
    }
}
