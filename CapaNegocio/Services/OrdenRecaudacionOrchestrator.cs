using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapaDatos.Interfaces;
using CapaNegocio.DTOs;
using CapaNegocio.Interfaces;
using CapaDatos.Services;
using DataEmailService = CapaDatos.Services.IEmailService;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Orquestador del flujo principal de Orden de recaudación.
    /// Implementa el patrón Orchestrator para coordinar múltiples servicios.
    /// </summary>
    public class OrdenRecaudacionOrchestrator : IOrdenRecaudacionOrchestrator
    {
        #region Dependencias

        private readonly IOrdenRecaudacionRepository _ordenRepository;
        private readonly IPagoRepository _pagoRepository;
        // Cambiar a object o remover si no se usa
        private readonly object _contribuyenteRepository;
        private readonly IPdfGeneratorService _pdfService;
        private readonly DataEmailService _emailService;
        private readonly IFileStorageService _fileService;
        private readonly OrdenRecaudacionCorreoService _ordenCorreoService;

        #endregion

        #region Configuración

        private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };
        private const long TamanoMaximoBytes = 5 * 1024 * 1024; // 5MB
        private const string DirectorioComprobantes = "~/Uploads/Comprobantes";

        #endregion

        #region Constructor

        public OrdenRecaudacionOrchestrator(
            IOrdenRecaudacionRepository ordenRepository,
            IPagoRepository pagoRepository,
            object contribuyenteRepository,  // Cambiar tipo
            IPdfGeneratorService pdfService,
            DataEmailService emailService,
            IFileStorageService fileService)
        {
            _ordenRepository = ordenRepository ?? throw new ArgumentNullException("ordenRepository");
            _pagoRepository = pagoRepository ?? throw new ArgumentNullException("pagoRepository");
            _contribuyenteRepository = contribuyenteRepository; // Puede ser null
            _pdfService = pdfService;
            _emailService = emailService;
            _fileService = fileService;
            _ordenCorreoService = new OrdenRecaudacionCorreoService();
        }

        #endregion

        #region Paso 1: Crear Orden

        public async Task<OperationResult<CrearOrdenResponse>> CrearOrdenAsync(CrearOrdenRequest request)
        {
            try
            {
                // Validar request
                var validacion = ValidarCrearOrden(request);
                if (!validacion.Success)
                {
                    return OperationResult<CrearOrdenResponse>.Fail(validacion.Message, "VALIDATION_ERROR");
                }

                // Generar número de orden
                var numeroOrden = await GenerarNumeroOrdenAsync();

                // Crear entidad de orden
                var orden = new CapaDatos.Entidades.OrdenRecaudacion
                {
                    NumeroOrden = numeroOrden,
                    SolicitudId = request.SolicitudId,
                    ConceptoId = request.ConceptoId,
                    ContribuyenteId = request.ContribuyenteId,
                    Subtotal = request.Subtotal,
                    Iva = request.Iva,
                    Total = request.Total,
                    Observaciones = request.Observaciones,
                    Estado = "PENDIENTE",
                    FechaCreacion = DateTime.Now,
                    UsuarioCreacion = request.UsuarioCreacion,
                    Activo = true
                };

                // Guardar orden
                var ordenId = await _ordenRepository.CrearAsync(orden);

                // Guardar detalles si existen
                if (request.Detalles != null && request.Detalles.Any())
                {
                    foreach (var detalle in request.Detalles)
                    {
                        var detalleEntidad = new CapaDatos.Entidades.DetalleOrden
                        {
                            OrdenId = ordenId,
                            ConceptoId = detalle.ConceptoId,
                            Descripcion = detalle.Descripcion,
                            Cantidad = detalle.Cantidad,
                            PrecioUnitario = detalle.PrecioUnitario,
                            Subtotal = detalle.Subtotal
                        };
                        await _ordenRepository.CrearDetalleAsync(detalleEntidad);
                    }
                }

                var response = new CrearOrdenResponse
                {
                    OrdenId = ordenId,
                    NumeroOrden = numeroOrden,
                    Estado = "PENDIENTE",
                    FechaCreacion = orden.FechaCreacion,
                    Total = orden.Total ?? 0m
                };

                return OperationResult<CrearOrdenResponse>.Ok(response, "Orden creada exitosamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al crear orden: " + ex.Message);
                return OperationResult<CrearOrdenResponse>.Fail("Error interno al crear la orden: " + ex.Message, "INTERNAL_ERROR");
            }
        }

        #endregion

        #region Paso 2: Registrar Pago

        public async Task<OperationResult<RegistrarPagoResponse>> RegistrarPagoAsync(RegistrarPagoRequest request)
        {
            try
            {
                // Validar request
                var validacion = ValidarRegistrarPago(request);
                if (!validacion.Success)
                {
                    return OperationResult<RegistrarPagoResponse>.Fail(validacion.Message, "VALIDATION_ERROR");
                }

                // Verificar que la orden existe y está en estado correcto
                var orden = await _ordenRepository.ObtenerPorIdAsync(request.OrdenId);
                if (orden == null)
                {
                    return OperationResult<RegistrarPagoResponse>.Fail("Orden no encontrada", "NOT_FOUND");
                }

                if (orden.Estado != "PENDIENTE")
                {
                    return OperationResult<RegistrarPagoResponse>.Fail(
                        "La orden no está en estado pendiente. Estado actual: " + orden.Estado,
                        "INVALID_STATE");
                }

                // Guardar archivo de comprobante
                string rutaComprobante = null;
                if (request.ContenidoArchivo != null && request.ContenidoArchivo.Length > 0)
                {
                    var validacionArchivo = ValidarArchivoComprobante(
                        request.NombreArchivo,
                        request.TipoArchivo,
                        request.TamanoArchivo);

                    if (!validacionArchivo.Success)
                    {
                        return OperationResult<RegistrarPagoResponse>.Fail(validacionArchivo.Message, "FILE_ERROR");
                    }

                    rutaComprobante = await GuardarArchivoComprobanteAsync(
                        request.OrdenId,
                        request.NombreArchivo,
                        request.ContenidoArchivo);
                }

                // Crear entidad de pago
                var pago = new CapaDatos.Entidades.Pago
                {
                    OrdenId = request.OrdenId,
                    NumeroComprobante = request.NumeroComprobante,
                    MontoPagado = request.MontoPagado,
                    FechaPago = request.FechaPago,
                    MetodoPago = request.MetodoPago,
                    BancoOrigen = request.BancoOrigen,
                    Observaciones = request.Observaciones,
                    RutaComprobante = rutaComprobante,
                    Estado = "PENDIENTE_VALIDACION",
                    FechaRegistro = DateTime.Now,
                    UsuarioRegistro = request.UsuarioRegistro
                };

                // Guardar pago
                var pagoId = await _pagoRepository.CrearAsync(pago);

                // Actualizar estado de orden
                await _ordenRepository.ActualizarEstadoAsync(request.OrdenId, "PROCESADA", request.UsuarioRegistro);

                var response = new RegistrarPagoResponse
                {
                    PagoId = pagoId,
                    OrdenId = request.OrdenId,
                    NumeroComprobante = request.NumeroComprobante,
                    EstadoOrden = "PROCESADA",
                    RutaComprobante = rutaComprobante
                };

                return OperationResult<RegistrarPagoResponse>.Ok(response, "Pago registrado exitosamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al registrar pago: " + ex.Message);
                return OperationResult<RegistrarPagoResponse>.Fail("Error interno al registrar el pago: " + ex.Message, "INTERNAL_ERROR");
            }
        }

        #endregion

        #region Paso 3: Validar Pago

        public async Task<OperationResult<ValidarPagoResponse>> ValidarPagoAsync(ValidarPagoRequest request)
        {
            try
            {
                // Obtener pago
                var pago = await _pagoRepository.ObtenerPorIdAsync(request.PagoId);
                if (pago == null)
                {
                    return OperationResult<ValidarPagoResponse>.Fail("Pago no encontrado", "NOT_FOUND");
                }

                if (pago.Estado != "PENDIENTE_VALIDACION")
                {
                    return OperationResult<ValidarPagoResponse>.Fail(
                        "El pago no está pendiente de validación. Estado actual: " + pago.Estado,
                        "INVALID_STATE");
                }

                var nuevoEstadoPago = request.Aprobado ? "VALIDADO" : "RECHAZADO";
                var nuevoEstadoOrden = request.Aprobado ? "FACTURADA" : "PENDIENTE";

                // Actualizar pago
                pago.Estado = nuevoEstadoPago;
                pago.Observacion = string.IsNullOrEmpty(pago.Observacion)
                    ? request.Observaciones
                    : pago.Observacion + " | " + request.Observaciones;
                pago.FechaValidacion = DateTime.Now;
                pago.UsuarioValidacion = request.UsuarioValidacion;

                await _pagoRepository.ActualizarAsync(pago);

                // Actualizar orden
                await _ordenRepository.ActualizarEstadoAsync(pago.OrdenId, nuevoEstadoOrden, request.UsuarioValidacion);

                var response = new ValidarPagoResponse
                {
                    PagoId = request.PagoId,
                    OrdenId = pago.OrdenId,
                    EstadoPago = nuevoEstadoPago,
                    EstadoOrden = nuevoEstadoOrden,
                    FacturaGenerada = false,
                    NotificacionEnviada = false
                };

                // Si se aprobó, generar factura y notificar
                if (request.Aprobado)
                {
                    // TODO: Integrar con sistema de facturación
                    response.FacturaGenerada = true;
                    response.NumeroFactura = "FAC-" + DateTime.Now.ToString("yyyyMMdd") + "-" + pago.OrdenId;

                    // Intentar enviar notificación
                    try
                    {
                        var orden = await _ordenRepository.ObtenerPorIdAsync(pago.OrdenId);
                        if (orden != null && !string.IsNullOrEmpty(orden.EmailContribuyente))
                        {
                            var asunto = "Pago Validado - Orden " + orden.NumeroOrden;
                            var cuerpo = ConstruirCuerpoPagoValidado(orden);
                            
                            var emailResult = await _emailService.EnviarAsync(
                                orden.EmailContribuyente,
                                orden.NombreContribuyente,
                                asunto,
                                cuerpo,
                                null,
                                null);
                                
                            response.NotificacionEnviada = emailResult.Success;
                        }
                    }
                    catch (Exception notifEx)
                    {
                        System.Diagnostics.Debug.WriteLine("Error al enviar notificación: " + notifEx.Message);
                        response.NotificacionEnviada = false;
                    }
                }

                return OperationResult<ValidarPagoResponse>.Ok(response,
                    request.Aprobado ? "Pago validado exitosamente" : "Pago rechazado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al validar pago: " + ex.Message);
                return OperationResult<ValidarPagoResponse>.Fail("Error interno al validar el pago: " + ex.Message, "INTERNAL_ERROR");
            }
        }

        #endregion

        #region Paso 4: Generar PDF

        public async Task<OperationResult<GenerarPdfResponse>> GenerarPdfAsync(GenerarPdfRequest request)
        {
            try
            {
                if (_pdfService == null)
                {
                    return OperationResult<GenerarPdfResponse>.Fail("Servicio de PDF no disponible", "SERVICE_UNAVAILABLE");
                }

                var orden = await _ordenRepository.ObtenerPorIdAsync(request.OrdenId);
                if (orden == null)
                {
                    return OperationResult<GenerarPdfResponse>.Fail("Orden no encontrada", "NOT_FOUND");
                }

                byte[] contenidoPdf;
                string nombreArchivo;

                switch (request.TipoDocumento.ToUpperInvariant())
                {
                    case "ORDEN":
                        contenidoPdf = await _pdfService.GenerarOrdenRecaudacionPdfAsync(orden);
                        nombreArchivo = "Orden_" + orden.NumeroOrden + ".pdf";
                        break;

                    case "FACTURA":
                        contenidoPdf = await _pdfService.GenerarFacturaPdfAsync(orden);
                        nombreArchivo = "Factura_" + orden.NumeroOrden + ".pdf";
                        break;

                    default:
                        return OperationResult<GenerarPdfResponse>.Fail(
                            "Tipo de documento no soportado: " + request.TipoDocumento,
                            "INVALID_TYPE");
                }

                var response = new GenerarPdfResponse
                {
                    ContenidoPdf = contenidoPdf,
                    NombreArchivo = nombreArchivo,
                    ContentType = "application/pdf",
                    TamanoBytes = contenidoPdf.Length
                };

                return OperationResult<GenerarPdfResponse>.Ok(response, "PDF generado exitosamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al generar PDF: " + ex.Message);
                return OperationResult<GenerarPdfResponse>.Fail("Error interno al generar el PDF: " + ex.Message, "INTERNAL_ERROR");
            }
        }

        #endregion

        #region Paso 5: Enviar Notificación

        public async Task<OperationResult<EnviarNotificacionResponse>> EnviarNotificacionAsync(EnviarNotificacionRequest request)
        {
            try
            {
                var orden = await _ordenRepository.ObtenerPorIdAsync(request.OrdenId);
                if (orden == null)
                {
                    return OperationResult<EnviarNotificacionResponse>.Fail("Orden no encontrada", "NOT_FOUND");
                }

                if (string.IsNullOrWhiteSpace(orden.Correo) && string.IsNullOrWhiteSpace(request.EmailDestino))
                {
                    return OperationResult<EnviarNotificacionResponse>.Fail("Email destino requerido", "VALIDATION_ERROR");
                }

                var resultadoOperacion = _ordenCorreoService.NotificarEvento(
                    orden,
                    request.TipoNotificacion,
                    request.EmailDestino,
                    request.NombreDestino,
                    request.AdjuntarPdf ? request.AdjuntoPdf : null,
                    request.AdjuntarPdf ? request.NombreAdjunto : null);
                var resultado = resultadoOperacion.Exitoso;

                var response = new EnviarNotificacionResponse
                {
                    Enviado = resultado,
                    MessageId = resultado ? Guid.NewGuid().ToString() : null,
                    FechaEnvio = resultado ? DateTime.Now : (DateTime?)null,
                    Error = resultado ? null : resultadoOperacion.Mensaje
                };

                return resultado
                    ? OperationResult<EnviarNotificacionResponse>.Ok(response, "Notificación enviada exitosamente")
                    : OperationResult<EnviarNotificacionResponse>.Fail("Error al enviar notificación", "EMAIL_ERROR");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al enviar notificación: " + ex.Message);
                return OperationResult<EnviarNotificacionResponse>.Fail("Error interno al enviar notificación: " + ex.Message, "INTERNAL_ERROR");
            }
        }

        #endregion

        #region Operaciones de Consulta

        public async Task<OperationResult<FlujoOrdenCompleto>> ObtenerEstadoFlujoAsync(int ordenId)
        {
            try
            {
                var orden = await _ordenRepository.ObtenerPorIdAsync(ordenId);
                if (orden == null)
                {
                    return OperationResult<FlujoOrdenCompleto>.Fail("Orden no encontrada", "NOT_FOUND");
                }

                var flujo = new FlujoOrdenCompleto
                {
                    EstadoActual = orden.Estado,
                    Orden = new CrearOrdenResponse
                    {
                        OrdenId = orden.Id,
                        NumeroOrden = orden.NumeroOrden,
                        Estado = orden.Estado,
                        FechaCreacion = orden.FechaCreacion,
                        Total = orden.Total ?? 0m
                    }
                };

                // Agregar pasos completados según estado
                flujo.PasosCompletados.Add("ORDEN_CREADA");

                if (orden.Estado == "PROCESADA" || orden.Estado == "FACTURADA" || orden.Estado == "COMPLETADA")
                {
                    flujo.PasosCompletados.Add("PAGO_REGISTRADO");

                    var pago = await _pagoRepository.ObtenerPorOrdenIdAsync(ordenId);
                    if (pago != null)
                    {
                        flujo.Pago = new RegistrarPagoResponse
                        {
                            PagoId = pago.Id,
                            OrdenId = pago.OrdenId,
                            NumeroComprobante = pago.NumeroComprobante,
                            EstadoOrden = orden.Estado,
                            RutaComprobante = pago.RutaComprobante
                        };
                    }
                }

                if (orden.Estado == "FACTURADA" || orden.Estado == "COMPLETADA")
                {
                    flujo.PasosCompletados.Add("PAGO_VALIDADO");
                    flujo.PasosCompletados.Add("FACTURA_GENERADA");
                }

                if (orden.Estado == "COMPLETADA")
                {
                    flujo.PasosCompletados.Add("NOTIFICACION_ENVIADA");
                }

                return OperationResult<FlujoOrdenCompleto>.Ok(flujo, "Estado del flujo obtenido");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener estado del flujo: " + ex.Message);
                return OperationResult<FlujoOrdenCompleto>.Fail("Error interno: " + ex.Message, "INTERNAL_ERROR");
            }
        }

        public async Task<OperationResult<bool>> PuedeAvanzarAsync(int ordenId, string siguientePaso)
        {
            try
            {
                var orden = await _ordenRepository.ObtenerPorIdAsync(ordenId);
                if (orden == null)
                {
                    return OperationResult<bool>.Fail("Orden no encontrada", "NOT_FOUND");
                }

                bool puedeAvanzar = false;
                string mensaje = "";

                switch (siguientePaso.ToUpperInvariant())
                {
                    case "REGISTRAR_PAGO":
                        puedeAvanzar = orden.Estado == "PENDIENTE";
                        mensaje = puedeAvanzar ? "Puede registrar pago" : "La orden no está en estado PENDIENTE";
                        break;

                    case "VALIDAR_PAGO":
                        puedeAvanzar = orden.Estado == "PROCESADA";
                        mensaje = puedeAvanzar ? "Puede validar pago" : "La orden no está en estado PROCESADA";
                        break;

                    case "GENERAR_FACTURA":
                        puedeAvanzar = orden.Estado == "FACTURADA";
                        mensaje = puedeAvanzar ? "Puede generar factura" : "La orden no está en estado FACTURADA";
                        break;

                    default:
                        mensaje = "Paso desconocido: " + siguientePaso;
                        break;
                }

                return OperationResult<bool>.Ok(puedeAvanzar, mensaje);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Error interno: " + ex.Message, "INTERNAL_ERROR");
            }
        }

        #endregion

        #region Validaciones

        public OperationResult ValidarCrearOrden(CrearOrdenRequest request)
        {
            var errores = new List<string>();

            if (request == null)
            {
                return OperationResult.Fail("Request es nulo");
            }

            if (request.ContribuyenteId <= 0)
            {
                errores.Add("ContribuyenteId es requerido");
            }

            if (request.ConceptoId <= 0)
            {
                errores.Add("ConceptoId es requerido");
            }

            if (request.Total <= 0)
            {
                errores.Add("El total debe ser mayor a cero");
            }

            if (request.Subtotal < 0)
            {
                errores.Add("El subtotal no puede ser negativo");
            }

            if (request.Iva < 0)
            {
                errores.Add("El IVA no puede ser negativo");
            }

            if (string.IsNullOrWhiteSpace(request.UsuarioCreacion))
            {
                errores.Add("UsuarioCreacion es requerido");
            }

            if (errores.Any())
            {
                var resultado = OperationResult.Fail(string.Join(", ", errores));
                resultado.ValidationErrors = errores;
                return resultado;
            }

            return OperationResult.Ok();
        }

        public OperationResult ValidarRegistrarPago(RegistrarPagoRequest request)
        {
            var errores = new List<string>();

            if (request == null)
            {
                return OperationResult.Fail("Request es nulo");
            }

            if (request.OrdenId <= 0)
            {
                errores.Add("OrdenId es requerido");
            }

            if (string.IsNullOrWhiteSpace(request.NumeroComprobante))
            {
                errores.Add("Número de comprobante es requerido");
            }

            if (request.MontoPagado <= 0)
            {
                errores.Add("El monto pagado debe ser mayor a cero");
            }

            if (request.FechaPago == DateTime.MinValue)
            {
                errores.Add("Fecha de pago es requerida");
            }

            if (request.FechaPago > DateTime.Now)
            {
                errores.Add("La fecha de pago no puede ser futura");
            }

            if (string.IsNullOrWhiteSpace(request.UsuarioRegistro))
            {
                errores.Add("UsuarioRegistro es requerido");
            }

            if (errores.Any())
            {
                var resultado = OperationResult.Fail(string.Join(", ", errores));
                resultado.ValidationErrors = errores;
                return resultado;
            }

            return OperationResult.Ok();
        }

        public OperationResult ValidarArchivoComprobante(string nombreArchivo, string tipoArchivo, long tamanoBytes)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return OperationResult.Fail("Nombre de archivo es requerido");
            }

            var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
            if (!ExtensionesPermitidas.Contains(extension))
            {
                return OperationResult.Fail(
                    "Extensión no permitida. Permitidas: " + string.Join(", ", ExtensionesPermitidas));
            }

            if (tamanoBytes <= 0)
            {
                return OperationResult.Fail("El archivo está vacío");
            }

            if (tamanoBytes > TamanoMaximoBytes)
            {
                return OperationResult.Fail(
                    "El archivo excede el tamaño máximo permitido de " + (TamanoMaximoBytes / 1024 / 1024) + "MB");
            }

            return OperationResult.Ok();
        }

        #endregion

        #region Métodos Privados

        private async Task<string> GenerarNumeroOrdenAsync()
        {
            var fecha = DateTime.Now;
            // Generar número único con timestamp para evitar duplicados
            var timestamp = fecha.ToString("yyyyMMddHHmmss");
            var consecutivo = await _ordenRepository.ObtenerConsecutivoDiarioAsync(fecha);
            return string.Format("OR-{0}-{1}", timestamp, consecutivo + 1);
        }

        private async Task<string> GuardarArchivoComprobanteAsync(int ordenId, string nombreArchivo, byte[] contenido)
        {
            if (_fileService != null)
            {
                return await _fileService.GuardarArchivoAsync(
                    DirectorioComprobantes,
                    string.Format("{0}_{1}", ordenId, nombreArchivo),
                    contenido);
            }

            // Fallback: guardar localmente
            var directorio = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads", "Comprobantes");
            if (!Directory.Exists(directorio))
            {
                Directory.CreateDirectory(directorio);
            }

            var nombreFinal = string.Format("{0}_{1:yyyyMMddHHmmss}_{2}", ordenId, DateTime.Now, nombreArchivo);
            var rutaCompleta = Path.Combine(directorio, nombreFinal);

            File.WriteAllBytes(rutaCompleta, contenido);

            return "~/Uploads/Comprobantes/" + nombreFinal;
        }

        private string ConstruirCuerpoOrdenCreada(CapaDatos.Entidades.OrdenRecaudacion orden)
        {
            return string.Format(@"
<html>
<body style='font-family: Arial, sans-serif;'>
<h2>Nueva Orden de recaudación</h2>
<p>Se ha generado una nueva orden de recaudación con los siguientes datos:</p>
<table style='border-collapse: collapse;'>
<tr><td style='padding: 5px; font-weight: bold;'>Número de Orden:</td><td style='padding: 5px;'>{0}</td></tr>
<tr><td style='padding: 5px; font-weight: bold;'>Fecha:</td><td style='padding: 5px;'>{1:dd/MM/yyyy}</td></tr>
<tr><td style='padding: 5px; font-weight: bold;'>Total:</td><td style='padding: 5px;'>${2:N2}</td></tr>
</table>
<p>Por favor, realice el pago correspondiente y suba el comprobante en el sistema.</p>
<p>Saludos,<br/>Sistema AOCR</p>
</body>
</html>", orden.NumeroOrden, orden.FechaCreacion, orden.Total);
        }

        private string ConstruirCuerpoPagoRegistrado(CapaDatos.Entidades.OrdenRecaudacion orden)
        {
            return string.Format(@"
<html>
<body style='font-family: Arial, sans-serif;'>
<h2>Pago Registrado</h2>
<p>Se ha registrado el pago para la orden <strong>{0}</strong>.</p>
<p>El comprobante está siendo revisado por el área financiera.</p>
<p>Le notificaremos cuando el pago sea validado.</p>
<p>Saludos,<br/>Sistema AOCR</p>
</body>
</html>", orden.NumeroOrden);
        }

        private string ConstruirCuerpoPagoValidado(CapaDatos.Entidades.OrdenRecaudacion orden)
        {
            return string.Format(@"
<html>
<body style='font-family: Arial, sans-serif;'>
<h2>Pago Validado</h2>
<p>El pago para la orden <strong>{0}</strong> ha sido validado correctamente.</p>
<p>Su factura será generada en breve.</p>
<p>Saludos,<br/>Sistema AOCR</p>
</body>
</html>", orden.NumeroOrden);
        }

        private string ConstruirCuerpoFacturaGenerada(CapaDatos.Entidades.OrdenRecaudacion orden)
        {
            return string.Format(@"
<html>
<body style='font-family: Arial, sans-serif;'>
<h2>Factura Generada</h2>
<p>Se ha generado la factura para la orden <strong>{0}</strong>.</p>
<p>Puede descargar su factura desde el sistema.</p>
<p>Saludos,<br/>Sistema AOCR</p>
</body>
</html>", orden.NumeroOrden);
        }

        #endregion
    }
}

