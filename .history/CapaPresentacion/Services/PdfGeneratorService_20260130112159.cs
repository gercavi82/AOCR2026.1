using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using CapaDatos.Entidades;
using CapaDatos.Infrastructure;
using CapaNegocio.DTOs;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;
using SelectPdf;

namespace CapaPresentacion.Services
{
    /// <summary>
    /// Servicio de generación de PDF resiliente
    /// </summary>
    public class PdfGeneratorService : IPdfGeneratorService
    {
        private readonly ILoggingService _logger;
        private readonly IPdfRegistroService _registroService;

        // Configuración
        private const int TimeoutSeconds = 60;
        private const int MaxRetries = 2;

        public PdfGeneratorService(IPdfRegistroService registroService = null)
        {
            _logger = LoggingServiceFactory.Create();
            _registroService = registroService;
        }

        public async Task<byte[]> GenerarOrdenRecaudacionPdfAsync(OrdenRecaudacion orden)
        {
            var registro = new PdfGeneracionRegistro
            {
                TipoDocumento = "ORDEN_RECAUDACION",
                EntidadId = orden.Id,
                NumeroReferencia = orden.NumeroOrden,
                FechaInicio = DateTime.Now
            };

            try
            {
                // Validar datos de entrada
                ValidarOrden(orden);

                _logger.LogInfo(string.Format("Generando PDF para orden {0}", orden.NumeroOrden),
                    new LogContext { NumeroOrden = orden.NumeroOrden });

                // Generar HTML
                var html = GenerarHtmlOrden(orden);

                // Convertir a PDF con reintentos
                var pdfBytes = await GenerarPdfConReintentosAsync(html, registro);

                registro.Exitoso = true;
                registro.TamanoBytes = pdfBytes.Length;
                registro.FechaFin = DateTime.Now;

                await RegistrarGeneracionAsync(registro);

                _logger.LogInfo(string.Format("PDF generado exitosamente: {0} bytes", pdfBytes.Length),
                    new LogContext { NumeroOrden = orden.NumeroOrden });

                return pdfBytes;
            }
            catch (PdfValidationException ex)
            {
                registro.Exitoso = false;
                registro.Error = ex.Message;
                registro.FechaFin = DateTime.Now;
                await RegistrarGeneracionAsync(registro);

                _logger.LogWarning("Validación fallida: " + ex.Message,
                    new LogContext { NumeroOrden = orden.NumeroOrden });
                throw;
            }
            catch (Exception ex)
            {
                registro.Exitoso = false;
                registro.Error = ex.Message;
                registro.FechaFin = DateTime.Now;
                await RegistrarGeneracionAsync(registro);

                _logger.LogError(ex, new LogContext { NumeroOrden = orden.NumeroOrden });
                throw new PdfGenerationException("Error al generar PDF de orden", ex);
            }
        }

        public async Task<byte[]> GenerarFacturaPdfAsync(OrdenRecaudacion orden)
        {
            var registro = new PdfGeneracionRegistro
            {
                TipoDocumento = "FACTURA",
                EntidadId = orden.Id,
                NumeroReferencia = orden.NumeroOrden,
                FechaInicio = DateTime.Now
            };

            try
            {
                ValidarOrden(orden);

                var html = GenerarHtmlFactura(orden);
                var pdfBytes = await GenerarPdfConReintentosAsync(html, registro);

                registro.Exitoso = true;
                registro.TamanoBytes = pdfBytes.Length;
                registro.FechaFin = DateTime.Now;
                await RegistrarGeneracionAsync(registro);

                return pdfBytes;
            }
            catch (Exception ex)
            {
                registro.Exitoso = false;
                registro.Error = ex.Message;
                registro.FechaFin = DateTime.Now;
                await RegistrarGeneracionAsync(registro);
                throw new PdfGenerationException("Error al generar PDF de factura", ex);
            }
        }

        public async Task<byte[]> GenerarComprobantePagoPdfAsync(Pago pago)
        {
            var registro = new PdfGeneracionRegistro
            {
                TipoDocumento = "COMPROBANTE_PAGO",
                EntidadId = pago.Id,
                NumeroReferencia = pago.NumeroComprobante,
                FechaInicio = DateTime.Now
            };

            try
            {
                ValidarPago(pago);

                var html = GenerarHtmlComprobante(pago);
                var pdfBytes = await GenerarPdfConReintentosAsync(html, registro);

                registro.Exitoso = true;
                registro.TamanoBytes = pdfBytes.Length;
                registro.FechaFin = DateTime.Now;
                await RegistrarGeneracionAsync(registro);

                return pdfBytes;
            }
            catch (Exception ex)
            {
                registro.Exitoso = false;
                registro.Error = ex.Message;
                registro.FechaFin = DateTime.Now;
                await RegistrarGeneracionAsync(registro);
                throw new PdfGenerationException("Error al generar PDF de comprobante", ex);
            }
        }

        #region Validaciones

        private void ValidarOrden(OrdenRecaudacion orden)
        {
            if (orden == null)
            {
                throw new PdfValidationException("La orden no puede ser nula");
            }

            if (string.IsNullOrWhiteSpace(orden.NumeroOrden))
            {
                throw new PdfValidationException("El número de orden es requerido");
            }

            if (orden.Total < 0)
            {
                throw new PdfValidationException("El total no puede ser negativo");
            }

            // Sanitizar datos para prevenir XSS en HTML
            orden.NumeroOrden = SanitizeHtml(orden.NumeroOrden);
            orden.NombreContribuyente = SanitizeHtml(orden.NombreContribuyente);
            orden.Observaciones = SanitizeHtml(orden.Observaciones);
        }

        private void ValidarPago(Pago pago)
        {
            if (pago == null)
            {
                throw new PdfValidationException("El pago no puede ser nulo");
            }

            if (string.IsNullOrWhiteSpace(pago.NumeroComprobante))
            {
                throw new PdfValidationException("El número de comprobante es requerido");
            }
        }

        private string SanitizeHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return HttpUtility.HtmlEncode(input);
        }

        #endregion

        #region Generación con Reintentos

        private async Task<byte[]> GenerarPdfConReintentosAsync(string html, PdfGeneracionRegistro registro)
        {
            Exception lastException = null;

            for (int intento = 1; intento <= MaxRetries; intento++)
            {
                try
                {
                    registro.Intentos = intento;
                    return GenerarPdfDesdeHtml(html);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(string.Format("Intento {0} fallido: {1}", intento, ex.Message));

                    if (intento < MaxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(intento * 2)); // Backoff
                    }
                }
            }

            throw new PdfGenerationException(
                string.Format("Error después de {0} intentos", MaxRetries), lastException);
        }

        private byte[] GenerarPdfDesdeHtml(string html)
        {
            var converter = new HtmlToPdf();

            // Configuración
            converter.Options.PdfPageSize = PdfPageSize.A4;
            converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
            converter.Options.MarginTop = 20;
            converter.Options.MarginBottom = 20;
            converter.Options.MarginLeft = 15;
            converter.Options.MarginRight = 15;

            // Timeouts
            converter.Options.MaxPageLoadTime = TimeoutSeconds;

            // Generar
            var document = converter.ConvertHtmlString(html);

            using (var ms = new MemoryStream())
            {
                document.Save(ms);
                document.Close();
                return ms.ToArray();
            }
        }

        #endregion

        #region Generación HTML

        private string GenerarHtmlOrden(OrdenRecaudacion orden)
        {
            return string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; font-size: 12px; }}
        .header {{ text-align: center; margin-bottom: 20px; }}
        .title {{ font-size: 18px; font-weight: bold; color: #1B4F72; }}
        .info-table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        .info-table td {{ padding: 8px; border: 1px solid #ddd; }}
        .info-table th {{ background: #1B4F72; color: white; padding: 10px; text-align: left; }}
        .total {{ font-size: 16px; font-weight: bold; text-align: right; margin-top: 20px; }}
        .footer {{ margin-top: 30px; font-size: 10px; color: #666; text-align: center; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='title'>ORDEN DE RECAUDACIÓN</div>
        <div>Sistema AOCR</div>
    </div>
    
    <table class='info-table'>
        <tr><th colspan='2'>Datos de la Orden</th></tr>
        <tr><td><strong>Número:</strong></td><td>{0}</td></tr>
        <tr><td><strong>Fecha:</strong></td><td>{1:dd/MM/yyyy HH:mm}</td></tr>
        <tr><td><strong>Estado:</strong></td><td>{2}</td></tr>
    </table>
    
    <table class='info-table'>
        <tr><th colspan='2'>Datos del Contribuyente</th></tr>
        <tr><td><strong>Nombre:</strong></td><td>{3}</td></tr>
        <tr><td><strong>RUC/CI:</strong></td><td>{4}</td></tr>
        <tr><td><strong>Email:</strong></td><td>{5}</td></tr>
    </table>
    
    <table class='info-table'>
        <tr><th colspan='2'>Detalle de Valores</th></tr>
        <tr><td><strong>Subtotal:</strong></td><td style='text-align: right;'>${6:N2}</td></tr>
        <tr><td><strong>IVA:</strong></td><td style='text-align: right;'>${7:N2}</td></tr>
        <tr><td><strong>TOTAL:</strong></td><td style='text-align: right; font-weight: bold; font-size: 14px;'>${8:N2}</td></tr>
    </table>
    
    {9}
    
    <div class='footer'>
        Documento generado el {10:dd/MM/yyyy HH:mm:ss}<br>
        Este documento es válido sin firma ni sello.
    </div>
</body>
</html>",
                orden.NumeroOrden,
                orden.FechaCreacion,
                orden.Estado,
                orden.NombreContribuyente ?? "N/A",
                orden.RucContribuyente ?? "N/A",
                orden.EmailContribuyente ?? "N/A",
                orden.Subtotal,
                orden.Iva,
                orden.Total,
                !string.IsNullOrEmpty(orden.Observaciones)
                    ? string.Format("<p><strong>Observaciones:</strong> {0}</p>", orden.Observaciones)
                    : "",
                DateTime.Now);
        }

        private string GenerarHtmlFactura(OrdenRecaudacion orden)
        {
            // Similar a orden pero con formato de factura
            return GenerarHtmlOrden(orden).Replace("ORDEN DE RECAUDACIÓN", "FACTURA");
        }

        private string GenerarHtmlComprobante(Pago pago)
        {
            return string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; font-size: 12px; }}
        .header {{ text-align: center; margin-bottom: 20px; }}
        .title {{ font-size: 18px; font-weight: bold; color: #28a745; }}
        .info-table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        .info-table td {{ padding: 8px; border: 1px solid #ddd; }}
        .info-table th {{ background: #28a745; color: white; padding: 10px; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='title'>COMPROBANTE DE PAGO</div>
    </div>
    <table class='info-table'>
        <tr><td><strong>Número:</strong></td><td>{0}</td></tr>
        <tr><td><strong>Fecha de Pago:</strong></td><td>{1:dd/MM/yyyy}</td></tr>
        <tr><td><strong>Monto:</strong></td><td>${2:N2}</td></tr>
        <tr><td><strong>Método:</strong></td><td>{3}</td></tr>
        <tr><td><strong>Estado:</strong></td><td>{4}</td></tr>
    </table>
</body>
</html>",
                pago.NumeroComprobante,
                pago.FechaPago,
                pago.MontoPagado,
                pago.MetodoPago ?? "N/A",
                pago.Estado);
        }

        #endregion

        #region Registro

        private async Task RegistrarGeneracionAsync(PdfGeneracionRegistro registro)
        {
            if (_registroService != null)
            {
                try
                {
                    await _registroService.RegistrarAsync(registro);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error al registrar generación PDF: " + ex.Message);
                }
            }
        }

        #endregion
    }

    #region Excepciones

    public class PdfValidationException : Exception
    {
        public PdfValidationException(string message) : base(message) { }
    }

    public class PdfGenerationException : Exception
    {
        public PdfGenerationException(string message, Exception inner = null) : base(message, inner) { }
    }

    #endregion

    #region Registro de Generación

    public class PdfGeneracionRegistro
    {
        public int Id { get; set; }
        public string TipoDocumento { get; set; }
        public int EntidadId { get; set; }
        public string NumeroReferencia { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool Exitoso { get; set; }
        public string Error { get; set; }
        public int Intentos { get; set; }
        public long TamanoBytes { get; set; }
    }

    public interface IPdfRegistroService
    {
        Task RegistrarAsync(PdfGeneracionRegistro registro);
    }

    public class PdfRegistroService : BaseDAO, IPdfRegistroService
    {
        public PdfRegistroService(string connectionString) : base(connectionString) { }

        public async Task RegistrarAsync(PdfGeneracionRegistro registro)
        {
            const string sql = @"
                INSERT INTO pdf_generaciones (
                    tipo_documento, entidad_id, numero_referencia,
                    fecha_inicio, fecha_fin, exitoso, error, intentos, tamano_bytes
                ) VALUES (
                    @tipo, @entidad_id, @numero, @inicio, @fin, @exitoso, @error, @intentos, @tamano
                )";

            ExecuteWithConnection(conn =>
            {
                ExecuteNonQuery(conn, sql, cmd =>
                {
                    AddParameter(cmd, "@tipo", registro.TipoDocumento);
                    AddParameter(cmd, "@entidad_id", registro.EntidadId);
                    AddParameter(cmd, "@numero", registro.NumeroReferencia ?? (object)DBNull.Value);
                    AddParameter(cmd, "@inicio", registro.FechaInicio);
                    AddParameter(cmd, "@fin", registro.FechaFin ?? (object)DBNull.Value);
                    AddParameter(cmd, "@exitoso", registro.Exitoso);
                    AddParameter(cmd, "@error", registro.Error ?? (object)DBNull.Value);
                    AddParameter(cmd, "@intentos", registro.Intentos);
                    AddParameter(cmd, "@tamano", registro.TamanoBytes);
                });
            });
        }
    }

    #endregion
}
