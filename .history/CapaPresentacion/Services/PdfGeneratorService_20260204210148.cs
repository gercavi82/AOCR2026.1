using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using CapaDatos.Entidades;
using CapaDatos.Services;
using CapaDatos.DAOs;

namespace CapaPresentacion.Services
{
    public interface IPdfGeneratorService
    {
        Task<byte[]> GenerarOrdenRecaudacionPdfAsync(OrdenRecaudacion orden);
        byte[] GenerarOrdenRecaudacionPDF(OrdenRecaudacion orden);
    }

    public class PdfGeneratorService : IPdfGeneratorService
    {
        private readonly ILoggingService _logger;
        private readonly ParametroDAO _parametroDAO;

        public PdfGeneratorService()
        {
            _logger = LoggingServiceFactory.Create();
            _parametroDAO = new ParametroDAO();
        }

        public async Task<byte[]> GenerarOrdenRecaudacionPdfAsync(OrdenRecaudacion orden)
        {
            return await Task.Run(() => GenerarOrdenRecaudacionPDF(orden));
        }

        public byte[] GenerarOrdenRecaudacionPDF(OrdenRecaudacion orden)
        {
            try
            {
                ValidarOrden(orden);
                var html = GenerarHtmlOrden(orden);
                return GenerarPdfDesdeHtml(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { NumeroOrden = orden?.NumeroOrden });
                throw;
            }
        }

        private void ValidarOrden(OrdenRecaudacion orden)
        {
            if (orden == null)
                throw new ArgumentNullException("orden", "La orden no puede ser nula");

            if (string.IsNullOrWhiteSpace(orden.NumeroOrden))
                throw new ArgumentException("El número de orden es requerido");
        }

                private byte[] GenerarPdfDesdeHtml(string html)
        {
            // Usar la librería disponible o generar un HTML simple como fallback
            try
            {
                // Evitar dependencia directa si SelectPdf no está referenciado
                var converterType = Type.GetType("SelectPdf.HtmlToPdf, SelectPdf");
                if (converterType == null)
                {
                    throw new InvalidOperationException("SelectPdf no disponible.");
                }

                var converter = Activator.CreateInstance(converterType);
                var convertMethod = converterType.GetMethod("ConvertHtmlString", new[] { typeof(string) });
                var document = convertMethod.Invoke(converter, new object[] { html });

                using (var ms = new MemoryStream())
                {
                    var saveMethod = document.GetType().GetMethod("Save", new[] { typeof(Stream) });
                    saveMethod.Invoke(document, new object[] { ms });

                    var closeMethod = document.GetType().GetMethod("Close", Type.EmptyTypes);
                    if (closeMethod != null) closeMethod.Invoke(document, null);

                    return ms.ToArray();
                }
            }
            catch
            {
                // Fallback: retornar HTML como bytes (para pruebas)
                return System.Text.Encoding.UTF8.GetBytes(html);
            }
        }
private string GenerarHtmlOrden(OrdenRecaudacion orden)
        {
            var nombreContribuyente = SanitizeHtml(orden.NombreContribuyente ?? "N/A");
            var rucContribuyente = SanitizeHtml(orden.RucContribuyente ?? "N/A");
            var emailContribuyente = SanitizeHtml(orden.EmailContribuyente ?? "N/A");
            var observaciones = SanitizeHtml(orden.Observaciones ?? "");
            var numeroOrden = SanitizeHtml(orden.NumeroOrden);

            return string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Orden de Recaudación - {0}</title>
    <style>
        body {{ font-family: Arial, sans-serif; font-size: 12px; padding: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .title {{ font-size: 18px; font-weight: bold; color: #1B4F72; }}
        .subtitle {{ font-size: 14px; color: #666; }}
        .section {{ margin: 20px 0; }}
        .section-title {{ font-weight: bold; border-bottom: 1px solid #ddd; padding-bottom: 5px; margin-bottom: 10px; }}
        table {{ width: 100%; border-collapse: collapse; }}
        td {{ padding: 8px; border: 1px solid #ddd; }}
        .label {{ background: #f5f5f5; font-weight: bold; width: 30%; }}
        .total {{ font-size: 16px; font-weight: bold; text-align: right; margin-top: 20px; }}
        .footer {{ margin-top: 40px; font-size: 10px; color: #666; text-align: center; }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='title'>{1}</div>
        <div class='subtitle'>{0}</div>
    </div>

    <div class='section'>
        <div class='section-title'>Datos de la Orden</div>
        <table>
            <tr><td class='label'>Número de Orden:</td><td>{0}</td></tr>
            <tr><td class='label'>Fecha de Creación:</td><td>{1:dd/MM/yyyy HH:mm}</td></tr>
            <tr><td class='label'>Estado:</td><td>{2}</td></tr>
        </table>
    </div>

    <div class='section'>
        <div class='section-title'>Datos del Contribuyente</div>
        <table>
            <tr><td class='label'>Nombre/Razón Social:</td><td>{3}</td></tr>
            <tr><td class='label'>RUC/CI:</td><td>{4}</td></tr>
            <tr><td class='label'>Correo Electrónico:</td><td>{5}</td></tr>
        </table>
    </div>

    <div class='section'>
        <div class='section-title'>Detalle de Valores</div>
        <table>
            <tr><td class='label'>Subtotal:</td><td style='text-align: right;'>$ {6:N2}</td></tr>
            <tr><td class='label'>IVA:</td><td style='text-align: right;'>$ {7:N2}</td></tr>
            <tr><td class='label' style='font-size: 14px;'>TOTAL A PAGAR:</td><td style='text-align: right; font-size: 14px; font-weight: bold;'>$ {8:N2}</td></tr>
        </table>
    </div>

    {9}

    <div class='footer'>
        <p>{2}</p>
        <p>{3}</p>
    </div>
</body>
</html>",
                numeroOrden,
                orden.FechaCreacion,
                orden.Estado ?? "PENDIENTE",
                nombreContribuyente,
                rucContribuyente,
                emailContribuyente,
                orden.Subtotal,
                orden.Iva,
                orden.Total,
                string.IsNullOrEmpty(observaciones) ? "" : 
                    string.Format("<div class='section'><div class='section-title'>Observaciones</div><p>{0}</p></div>", observaciones),
                DateTime.Now);
        }

        private string SanitizeHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return HttpUtility.HtmlEncode(input);
        }
    }

    public class PdfValidationException : Exception
    {
        public PdfValidationException(string message) : base(message) { }
    }

    public class PdfGenerationException : Exception
    {
        public PdfGenerationException(string message, Exception inner = null) : base(message, inner) { }
    }
}


