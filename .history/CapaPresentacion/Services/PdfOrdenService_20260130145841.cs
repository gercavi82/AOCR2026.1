using System;
using System.Threading.Tasks;
using System.Web;
using CapaDatos.Entidades;
using CapaDatos.Interfaces;
using CapaNegocio.DTOs;
using CapaNegocio.Services;

namespace CapaPresentacion.Services
{
    /// <summary>
    /// Servicio de generación de PDF para órdenes con validación y registro
    /// </summary>
    public class PdfOrdenService
    {
        private readonly PdfGeneratorService _pdfGenerator;
        private readonly IOrdenRecaudacionRepository _ordenRepository;
        private readonly ILoggingService _logger;

        public PdfOrdenService(
            PdfGeneratorService pdfGenerator,
            IOrdenRecaudacionRepository ordenRepository)
        {
            _pdfGenerator = pdfGenerator;
            _ordenRepository = ordenRepository;
            _logger = LoggingServiceFactory.Create();
        }

        /// <summary>
        /// Genera PDF de orden con validación completa
        /// </summary>
        public async Task<OperationResult<GenerarPdfResponse>> GenerarPdfOrdenAsync(int ordenId, string usuario)
        {
            var correlationId = HttpContext.Current?.Items["CorrelationId"] as string
                ?? Guid.NewGuid().ToString("N").Substring(0, 12);

            var context = new LogContext { CorrelationId = correlationId };

            try
            {
                // 1. Validar que la orden existe
                var orden = await _ordenRepository.ObtenerPorIdAsync(ordenId);
                if (orden == null)
                {
                    _logger.LogWarning(string.Format("Orden {0} no encontrada para PDF", ordenId), context);
                    return OperationResult<GenerarPdfResponse>.Fail("Orden no encontrada", "NOT_FOUND");
                }

                context.NumeroOrden = orden.NumeroOrden;
                HttpContext.Current.Items["NumeroOrden"] = orden.NumeroOrden;

                // 2. Validar permisos (opcional - según lógica de negocio)
                // ...

                _logger.LogInfo(string.Format("Iniciando generación PDF orden {0}", orden.NumeroOrden), context);

                // 3. Generar PDF (con reintentos internos)
                var pdfBytes = await _pdfGenerator.GenerarOrdenRecaudacionPdfAsync(orden);

                // 4. Preparar respuesta
                var response = new GenerarPdfResponse
                {
                    ContenidoPdf = pdfBytes,
                    NombreArchivo = string.Format("Orden_{0}.pdf", orden.NumeroOrden),
                    ContentType = "application/pdf",
                    TamanoBytes = pdfBytes.Length
                };

                _logger.LogInfo(
                    string.Format("PDF generado: {0}, {1} bytes", response.NombreArchivo, pdfBytes.Length),
                    context);

                return OperationResult<GenerarPdfResponse>.Ok(response, "PDF generado exitosamente");
            }
            catch (PdfValidationException ex)
            {
                _logger.LogWarning("Validación PDF fallida: " + ex.Message, context);
                return OperationResult<GenerarPdfResponse>.Fail(ex.Message, "VALIDATION_ERROR");
            }
            catch (PdfGenerationException ex)
            {
                _logger.LogError(ex, context);
                return OperationResult<GenerarPdfResponse>.Fail(
                    "Error al generar el PDF. Por favor intente nuevamente.",
                    "PDF_ERROR");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, context);
                return OperationResult<GenerarPdfResponse>.Fail(
                    "Error interno al generar el PDF",
                    "INTERNAL_ERROR");
            }
        }
    }
}
