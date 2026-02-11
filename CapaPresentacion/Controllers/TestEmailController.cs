using System;
using System.Web.Mvc;
using CapaDatos.Services;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Controlador de prueba para envÃ­o de correos
    /// </summary>
    public class TestEmailController : Controller
    {
        /// <summary>
        /// Formulario de prueba de envÃ­o de correo
        /// GET: /TestEmail
        /// </summary>
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// MÃ©todo para probar el envÃ­o de correo
        /// POST: /TestEmail/EnviarPrueba
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult EnviarPrueba(string destinatario, string asunto, string mensaje)
        {
            try
            {
                // Validar parÃ¡metros
                if (string.IsNullOrEmpty(destinatario))
                {
                    return Json(new { 
                        success = false, 
                        message = "Debe proporcionar un destinatario" 
                    });
                }

                // Crear instancia del servicio de correo
                var emailService = new EnviarCorreo();

                // Enviar correo
                bool resultado = emailService.enviaMensajeCorreo(
                    coreoPara: destinatario,
                    asunto: asunto ?? "Prueba de correo - Sistema AOCR",
                    mensajeDetalle: mensaje ?? "<h2>Correo de Prueba</h2><p>Este es un correo de prueba desde el Sistema AOCR.</p>"
                );

                if (resultado)
                {
                    return Json(new { 
                        success = true, 
                        message = "Correo enviado exitosamente a " + destinatario 
                    });
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = "Error al enviar el correo. Verifique la configuraciÃ³n SMTP." 
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Error: " + ex.Message 
                });
            }
        }

        /// <summary>
        /// MÃ©todo para probar envÃ­o con remitente personalizado
        /// POST: /TestEmail/EnviarConRemitente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult EnviarConRemitente(string remitente, string destinatario, string asunto, string mensaje)
        {
            try
            {
                if (string.IsNullOrEmpty(destinatario))
                {
                    return Json(new { 
                        success = false, 
                        message = "Debe proporcionar un destinatario" 
                    });
                }

                var emailService = new EnviarCorreo();

                bool resultado = emailService.enviaMensajeCorreoDesde(
                    coreoDesde: remitente ?? "no_reply@aviacioncivil.gob.ec",
                    coreoPara: destinatario,
                    asunto: asunto ?? "Prueba de correo con remitente - Sistema AOCR",
                    mensajeDetalle: mensaje ?? "<h2>Correo de Prueba</h2><p>Este es un correo con remitente personalizado.</p>"
                );

                if (resultado)
                {
                    return Json(new { 
                        success = true, 
                        message = $"Correo enviado desde {remitente} a {destinatario}" 
                    });
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = "Error al enviar el correo" 
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Error: " + ex.Message 
                });
            }
        }

        /// <summary>
        /// MÃ©todo para enviar correo de notificaciÃ³n de orden de recaudaciÃ³n
        /// POST: /TestEmail/NotificarOrden
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult NotificarOrden(int ordenId, string destinatario)
        {
            try
            {
                var emailService = new EnviarCorreo();

                string asunto = $"NotificaciÃ³n de Orden de RecaudaciÃ³n #{ordenId}";
                string mensaje = GenerarMensajeOrden(ordenId);

                bool resultado = emailService.enviaMensajeCorreo(
                    coreoPara: destinatario,
                    asunto: asunto,
                    mensajeDetalle: mensaje
                );

                if (resultado)
                {
                    return Json(new { 
                        success = true, 
                        message = "NotificaciÃ³n enviada correctamente" 
                    });
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = "Error al enviar la notificaciÃ³n" 
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Error: " + ex.Message 
                });
            }
        }

        /// <summary>
        /// Genera el HTML del mensaje para notificaciÃ³n de orden
        /// </summary>
        private string GenerarMensajeOrden(int ordenId)
        {
            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; }}
                    .header {{ background-color: #003366; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 20px; }}
                    .footer {{ background-color: #f0f0f0; padding: 10px; text-align: center; font-size: 12px; }}
                    .button {{ background-color: #003366; color: white; padding: 10px 20px; text-decoration: none; display: inline-block; margin: 10px 0; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <h2>Sistema AOCR - DirecciÃ³n General de AviaciÃ³n Civil</h2>
                </div>
                <div class='content'>
                    <h3>NotificaciÃ³n de Orden de RecaudaciÃ³n</h3>
                    <p>Estimado/a usuario/a,</p>
                    <p>Se ha generado una nueva orden de recaudaciÃ³n con los siguientes datos:</p>
                    <ul>
                        <li><strong>NÃºmero de Orden:</strong> #{ordenId}</li>
                        <li><strong>Fecha:</strong> {DateTime.Now:dd/MM/yyyy}</li>
                        <li><strong>Estado:</strong> Pendiente</li>
                    </ul>
                    <p>Por favor, ingrese al sistema para revisar los detalles.</p>
                    <a href='#' class='button'>Acceder al Sistema</a>
                </div>
                <div class='footer'>
                    <p>Este es un correo automÃ¡tico. Por favor no responder.</p>
                    <p>&copy; {DateTime.Now.Year} DirecciÃ³n General de AviaciÃ³n Civil - Ecuador</p>
                </div>
            </body>
            </html>";
        }
    }
}
