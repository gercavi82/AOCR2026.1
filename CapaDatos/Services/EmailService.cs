using System;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Threading.Tasks;

namespace CapaDatos.Services
{
    /// <summary>
    /// Resultado de envío de correo - DEFINIR SOLO AQUÍ
    /// </summary>
    public class EmailSendResult
    {
        public bool Success { get; set; }
        public string MessageId { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Interface para servicio de email
    /// </summary>
    public interface IEmailService
    {
        Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null);
    }

    /// <summary>
    /// Servicio de envío de correos
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly ISecureConfigurationService _config;
        private readonly ILoggingService _logger;

        public EmailService(ISecureConfigurationService config)
        {
            _config = config;
            _logger = LoggingServiceFactory.Create();
        }

        /// <summary>Constructor sin parámetros para compatibilidad</summary>
        public EmailService() : this(new SecureConfigurationService()) { }

        public Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null)
        {
            try
            {
                var creds = _config.GetEmailCredentials();
                var correoDirecto = new EnviarCorreo(_config);
                var from = string.IsNullOrWhiteSpace(creds != null ? creds.FromAddress : null) ? null : creds.FromAddress;

                var enviado = (adjunto != null && adjunto.Length > 0)
                    ? correoDirecto.enviaMensajeCorreoConAdjuntoDesde(from, para, asunto, cuerpo, adjunto, adjuntoNombre, "application/octet-stream")
                    : correoDirecto.enviaMensajeCorreoDesde(from, para, asunto, cuerpo);

                if (enviado)
                {
                    return Task.FromResult(new EmailSendResult
                    {
                        Success = true,
                        MessageId = Guid.NewGuid().ToString()
                    });
                }

                return Task.FromResult(new EmailSendResult
                {
                    Success = false,
                    Error = "No fue posible enviar el correo."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "EMAIL_ERROR" });

                return Task.FromResult(new EmailSendResult
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        // ============================================================
        // Métodos de compatibilidad usados en la capa de presentación
        // ============================================================
        public void EnviarFacturaGenerada(object orden, byte[] pdfBytes)
        {
            var correoDestino = ObtenerPropiedadComoTexto(orden, "Correo", "EmailContribuyente", "CorreoContribuyente", "Email");
            if (string.IsNullOrWhiteSpace(correoDestino))
            {
                _logger.LogWarning("No se envió correo de factura: la orden no tiene correo de destinatario.",
                    new LogContext
                    {
                        ErrorCode = "EMAIL_FACTURA_SIN_DESTINO",
                        AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["NumeroOrden"] = ObtenerPropiedadComoTexto(orden, "NumeroOrden")
                        }
                    });
                return;
            }

            var nombreDestino = ObtenerPropiedadComoTexto(orden, "NombreContribuyente", "Compania", "Nombre", "RazonSocial");
            if (string.IsNullOrWhiteSpace(nombreDestino))
            {
                nombreDestino = "Solicitante";
            }

            var numeroOrden = ObtenerPropiedadComoTexto(orden, "NumeroOrden", "Numero");
            var asunto = string.IsNullOrWhiteSpace(numeroOrden)
                ? "Pago aprobado - Factura generada"
                : string.Format("Pago aprobado - Factura orden {0}", numeroOrden);
            var cuerpo = ConstruirHtmlAprobacion(nombreDestino, numeroOrden);

            var adjunto = (pdfBytes != null && pdfBytes.Length > 0) ? pdfBytes : null;
            var adjuntoNombre = adjunto != null ? ConstruirNombreAdjuntoFactura(numeroOrden) : null;

            try
            {
                var resultado = EnviarAsync(correoDestino, nombreDestino, asunto, cuerpo, adjunto, adjuntoNombre).GetAwaiter().GetResult();
                if (!resultado.Success)
                {
                    _logger.LogError("Error enviando notificacion de factura: " + (resultado.Error ?? "Error desconocido"),
                        new LogContext
                        {
                            ErrorCode = "EMAIL_FACTURA_ERROR",
                            NumeroOrden = numeroOrden,
                            AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                            {
                                ["Destino"] = correoDestino
                            }
                        });
                    return;
                }

                _logger.LogInfo("Notificacion de factura enviada correctamente",
                    new LogContext
                    {
                        ErrorCode = "EMAIL_FACTURA_OK",
                        NumeroOrden = numeroOrden,
                        AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["Destino"] = correoDestino,
                            ["MessageId"] = resultado.MessageId ?? string.Empty
                        }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    ErrorCode = "EMAIL_FACTURA_EX",
                    NumeroOrden = numeroOrden,
                    AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["Destino"] = correoDestino
                    }
                });
            }
        }

        public void EnviarNotificacionRechazo(object orden, string motivo)
        {
            var correoDestino = ObtenerPropiedadComoTexto(orden, "Correo", "EmailContribuyente", "CorreoContribuyente", "Email");
            if (string.IsNullOrWhiteSpace(correoDestino))
            {
                _logger.LogWarning("No se envió correo de rechazo: la orden no tiene correo de destinatario.",
                    new LogContext
                    {
                        ErrorCode = "EMAIL_RECHAZO_SIN_DESTINO",
                        AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["Motivo"] = motivo ?? string.Empty,
                            ["NumeroOrden"] = ObtenerPropiedadComoTexto(orden, "NumeroOrden")
                        }
                    });
                return;
            }

            var nombreDestino = ObtenerPropiedadComoTexto(orden, "NombreContribuyente", "Compania", "Nombre", "RazonSocial");
            if (string.IsNullOrWhiteSpace(nombreDestino))
            {
                nombreDestino = "Solicitante";
            }

            var numeroOrden = ObtenerPropiedadComoTexto(orden, "NumeroOrden", "Numero");
            var motivoLimpio = string.IsNullOrWhiteSpace(motivo) ? "No especificado." : motivo.Trim();
            var asunto = string.IsNullOrWhiteSpace(numeroOrden)
                ? "Notificacion de rechazo de orden de recaudacion"
                : string.Format("Notificacion de rechazo - Orden {0}", numeroOrden);

            var cuerpo = ConstruirHtmlRechazo(nombreDestino, numeroOrden, motivoLimpio);

            try
            {
                var resultado = EnviarAsync(correoDestino, nombreDestino, asunto, cuerpo).GetAwaiter().GetResult();
                if (!resultado.Success)
                {
                    _logger.LogError("Error enviando notificacion de rechazo: " + (resultado.Error ?? "Error desconocido"),
                        new LogContext
                        {
                            ErrorCode = "EMAIL_RECHAZO_ERROR",
                            NumeroOrden = numeroOrden,
                            AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                            {
                                ["Destino"] = correoDestino,
                                ["Motivo"] = motivoLimpio
                            }
                        });
                    return;
                }

                _logger.LogInfo("Notificacion de rechazo enviada correctamente",
                    new LogContext
                    {
                        ErrorCode = "EMAIL_RECHAZO_OK",
                        NumeroOrden = numeroOrden,
                        AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["Destino"] = correoDestino,
                            ["MessageId"] = resultado.MessageId ?? string.Empty
                        }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    ErrorCode = "EMAIL_RECHAZO_EX",
                    NumeroOrden = numeroOrden,
                    AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["Destino"] = correoDestino,
                        ["Motivo"] = motivoLimpio
                    }
                });
            }
        }

        private static string ObtenerPropiedadComoTexto(object origen, params string[] candidatos)
        {
            if (origen == null || candidatos == null || candidatos.Length == 0)
            {
                return null;
            }

            var tipo = origen.GetType();
            foreach (var candidato in candidatos)
            {
                if (string.IsNullOrWhiteSpace(candidato))
                {
                    continue;
                }

                var prop = tipo.GetProperty(candidato, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (prop == null)
                {
                    continue;
                }

                var valor = prop.GetValue(origen, null);
                if (valor == null)
                {
                    continue;
                }

                var texto = valor.ToString();
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    return texto.Trim();
                }
            }

            return null;
        }

        private static string ConstruirHtmlRechazo(string nombreDestino, string numeroOrden, string motivo)
        {
            var nombreSeguro = WebUtility.HtmlEncode(nombreDestino ?? "Solicitante");
            var ordenSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(numeroOrden) ? "N/A" : numeroOrden);
            var motivoSeguro = WebUtility.HtmlEncode(motivo ?? "No especificado.");
            var fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            return string.Format(@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; margin:0; padding:20px; background:#f4f6f8;'>
  <div style='max-width:640px; margin:0 auto; background:#ffffff; border:1px solid #d9dee5; border-radius:8px; padding:24px;'>
    <h2 style='margin:0 0 16px 0; color:#1f3a5f;'>Notificacion de rechazo de orden</h2>
    <p style='margin:0 0 12px 0;'>Estimado/a <strong>{0}</strong>,</p>
    <p style='margin:0 0 12px 0;'>La orden de recaudacion <strong>{1}</strong> ha sido rechazada por el area financiera.</p>
    <div style='margin:16px 0; padding:12px; background:#fff3f3; border:1px solid #f3c4c4; border-radius:6px;'>
      <strong>Motivo del rechazo:</strong><br />
      <span>{2}</span>
    </div>
    <p style='margin:0 0 10px 0;'>Fecha: {3}</p>
    <p style='margin:0;'>Por favor, revise la informacion y gestione una nueva carga o correccion en el sistema.</p>
    <hr style='margin:20px 0; border:none; border-top:1px solid #e8ecf1;' />
    <p style='margin:0; font-size:12px; color:#6b7785;'>Mensaje automatico del sistema AOCR - DGAC.</p>
  </div>
</body>
</html>",
                nombreSeguro,
                ordenSeguro,
                motivoSeguro,
                WebUtility.HtmlEncode(fecha));
        }

        private static string ConstruirHtmlAprobacion(string nombreDestino, string numeroOrden)
        {
            var nombreSeguro = WebUtility.HtmlEncode(nombreDestino ?? "Solicitante");
            var ordenSeguro = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(numeroOrden) ? "N/A" : numeroOrden);
            var fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            return string.Format(@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; margin:0; padding:20px; background:#f4f6f8;'>
  <div style='max-width:640px; margin:0 auto; background:#ffffff; border:1px solid #d9dee5; border-radius:8px; padding:24px;'>
    <h2 style='margin:0 0 16px 0; color:#1f3a5f;'>Pago aprobado - Factura generada</h2>
    <p style='margin:0 0 12px 0;'>Estimado/a <strong>{0}</strong>,</p>
    <p style='margin:0 0 12px 0;'>Su orden de recaudacion <strong>{1}</strong> ha sido aprobada por el area financiera.</p>
    <p style='margin:0 0 12px 0;'>Adjunto a este correo encontrará la factura/comprobante generado.</p>
    <p style='margin:0 0 10px 0;'>Fecha: {2}</p>
    <hr style='margin:20px 0; border:none; border-top:1px solid #e8ecf1;' />
    <p style='margin:0; font-size:12px; color:#6b7785;'>Mensaje automatico del sistema AOCR - DGAC.</p>
  </div>
</body>
</html>",
                nombreSeguro,
                ordenSeguro,
                WebUtility.HtmlEncode(fecha));
        }

        private static string ConstruirNombreAdjuntoFactura(string numeroOrden)
        {
            if (string.IsNullOrWhiteSpace(numeroOrden))
            {
                return "Factura_AOCR.pdf";
            }

            var buffer = new char[numeroOrden.Length];
            var pos = 0;
            foreach (var ch in numeroOrden)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                {
                    buffer[pos++] = ch;
                }
            }

            var limpio = pos > 0 ? new string(buffer, 0, pos) : "AOCR";
            return string.Format("Factura_{0}.pdf", limpio);
        }
    }
    /// <summary>
    /// Null Object para escenarios donde el servicio real no está disponible.
    /// </summary>
    public class NoOpEmailService : IEmailService
    {
        public Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null)
        {
            return Task.FromResult(new EmailSendResult
            {
                Success = false,
                Error = "EMAIL_SERVICE_DISABLED"
            });
        }
    }

    /// <summary>
    /// Facade que encola correos
    /// </summary>
    public class QueuedEmailService : IEmailService
    {
        private readonly IEmailQueueService _queueService;
        private readonly ILoggingService _logger;

        public QueuedEmailService(IEmailQueueService queueService)
        {
            _queueService = queueService;
            _logger = LoggingServiceFactory.Create();
        }

        public async Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null)
        {
            try
            {
                var item = new EmailQueueItem
                {
                    Para = para,
                    ParaNombre = paraNombre,
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    EsHtml = true,
                    AdjuntoNombre = adjuntoNombre,
                    AdjuntoContenido = adjunto,
                    MaxIntentos = 3
                };

                var id = await _queueService.EncolarAsync(item);

                return new EmailSendResult
                {
                    Success = true,
                    MessageId = "QUEUED-" + id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "QUEUE_ERROR" });
                return new EmailSendResult
                {
                    Success = false,
                    Error = "Error al encolar: " + ex.Message
                };
            }
        }

        // Nota: los métodos de compatibilidad están en EmailService.
    }
}

