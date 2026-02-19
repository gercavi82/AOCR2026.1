using System;
using CapaDatos.Services;

namespace CapaPresentacion.Helpers
{
    /// <summary>
    /// Clase auxiliar para envío de correos de notificación del sistema
    /// </summary>
    public static class NotificacionCorreoHelper
    {
        /// <summary>
        /// Envía notificación de nueva orden de recaudación
        /// </summary>
        public static bool NotificarNuevaOrden(int ordenId, string destinatario, decimal monto, string concepto)
        {
            try
            {
                var emailService = new EnviarCorreo();
                
                string asunto = $"Nueva Orden de recaudación #{ordenId}";
                string mensaje = GenerarHtmlNuevaOrden(ordenId, monto, concepto);
                
                return emailService.enviaMensajeCorreo(destinatario, asunto, mensaje);
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error enviando notificación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Envía notificación de pago recibido
        /// </summary>
        public static bool NotificarPagoRecibido(int ordenId, string destinatario, decimal monto, string referencia)
        {
            try
            {
                var emailService = new EnviarCorreo();
                
                string asunto = $"Pago Recibido - Orden #{ordenId}";
                string mensaje = GenerarHtmlPagoRecibido(ordenId, monto, referencia);
                
                return emailService.enviaMensajeCorreo(destinatario, asunto, mensaje);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enviando notificación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Envía notificación de cambio de estado
        /// </summary>
        public static bool NotificarCambioEstado(int ordenId, string destinatario, string estadoAnterior, string estadoNuevo)
        {
            try
            {
                var emailService = new EnviarCorreo();
                
                string asunto = $"Cambio de Estado - Orden #{ordenId}";
                string mensaje = GenerarHtmlCambioEstado(ordenId, estadoAnterior, estadoNuevo);
                
                return emailService.enviaMensajeCorreo(destinatario, asunto, mensaje);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enviando notificación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Envía notificación de vencimiento próximo
        /// </summary>
        public static bool NotificarVencimientoProximo(int ordenId, string destinatario, DateTime fechaVencimiento, decimal monto)
        {
            try
            {
                var emailService = new EnviarCorreo();
                
                string asunto = $"Recordatorio de Vencimiento - Orden #{ordenId}";
                string mensaje = GenerarHtmlVencimientoProximo(ordenId, fechaVencimiento, monto);
                
                return emailService.enviaMensajeCorreo(destinatario, asunto, mensaje);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enviando notificación: {ex.Message}");
                return false;
            }
        }

        #region Generadores de HTML

        private static string GenerarHtmlNuevaOrden(int ordenId, decimal monto, string concepto)
        {
            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 0; padding: 0; }}
                    .header {{ background-color: #003366; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 30px; background-color: #f9f9f9; }}
                    .info-box {{ background-color: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    .table {{ width: 100%; border-collapse: collapse; }}
                    .table td {{ padding: 10px; border-bottom: 1px solid #ddd; }}
                    .table td:first-child {{ font-weight: bold; width: 40%; }}
                    .footer {{ background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 12px; color: #666; }}
                    .button {{ background-color: #003366; color: white; padding: 12px 30px; text-decoration: none; 
                              display: inline-block; border-radius: 4px; margin: 10px 0; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <h2>Sistema AOCR - Dirección General de Aviación Civil</h2>
                </div>
                <div class='content'>
                    <h3 style='color: #003366;'>Nueva Orden de recaudación</h3>
                    <p>Estimado/a usuario/a,</p>
                    <p>Se ha generado una nueva orden de recaudación con los siguientes detalles:</p>
                    
                    <div class='info-box'>
                        <table class='table'>
                            <tr>
                                <td>Número de Orden:</td>
                                <td><strong>#{ordenId:D6}</strong></td>
                            </tr>
                            <tr>
                                <td>Fecha de Emisión:</td>
                                <td>{DateTime.Now:dd/MM/yyyy}</td>
                            </tr>
                            <tr>
                                <td>Concepto:</td>
                                <td>{concepto}</td>
                            </tr>
                            <tr>
                                <td>Monto Total:</td>
                                <td><strong style='color: #003366; font-size: 18px;'>${monto:N2}</strong></td>
                            </tr>
                            <tr>
                                <td>Estado:</td>
                                <td><span style='background-color: #ffc107; padding: 5px 10px; border-radius: 3px;'>Pendiente</span></td>
                            </tr>
                        </table>
                    </div>

                    <p>Por favor, proceda con el pago en los plazos establecidos.</p>
                    <p style='text-align: center;'>
                        <a href='#' class='button'>Acceder al Sistema</a>
                    </p>
                </div>
                <div class='footer'>
                    <p>Este es un correo automático generado por el Sistema AOCR.</p>
                    <p>Por favor no responder a este mensaje.</p>
                    <p>&copy; {DateTime.Now.Year} Dirección General de Aviación Civil - Ecuador</p>
                </div>
            </body>
            </html>";
        }

        private static string GenerarHtmlPagoRecibido(int ordenId, decimal monto, string referencia)
        {
            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 0; padding: 0; }}
                    .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 30px; background-color: #f9f9f9; }}
                    .success-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; color: #155724; 
                                   padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    .info-box {{ background-color: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    .footer {{ background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 12px; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <h2>✓ Pago Recibido</h2>
                </div>
                <div class='content'>
                    <div class='success-box'>
                        <h3 style='margin-top: 0;'>¡Pago Confirmado!</h3>
                        <p>Hemos recibido su pago correctamente.</p>
                    </div>
                    
                    <div class='info-box'>
                        <p><strong>Número de Orden:</strong> #{ordenId:D6}</p>
                        <p><strong>Monto Pagado:</strong> ${monto:N2}</p>
                        <p><strong>Referencia:</strong> {referencia}</p>
                        <p><strong>Fecha:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                    </div>

                    <p>Gracias por su pago puntual.</p>
                </div>
                <div class='footer'>
                    <p>&copy; {DateTime.Now.Year} DGAC - Ecuador</p>
                </div>
            </body>
            </html>";
        }

        private static string GenerarHtmlCambioEstado(int ordenId, string estadoAnterior, string estadoNuevo)
        {
            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 0; padding: 0; }}
                    .header {{ background-color: #003366; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 30px; }}
                    .estado {{ padding: 10px 20px; border-radius: 5px; display: inline-block; margin: 5px; }}
                    .footer {{ background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 12px; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <h2>Cambio de Estado - Orden #{ordenId}</h2>
                </div>
                <div class='content'>
                    <p>Se ha actualizado el estado de su orden:</p>
                    <p style='text-align: center; margin: 30px 0;'>
                        <span class='estado' style='background-color: #ffc107;'>{estadoAnterior}</span>
                        <span style='font-size: 24px;'>→</span>
                        <span class='estado' style='background-color: #28a745; color: white;'>{estadoNuevo}</span>
                    </p>
                    <p><strong>Fecha del cambio:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                </div>
                <div class='footer'>
                    <p>&copy; {DateTime.Now.Year} DGAC - Ecuador</p>
                </div>
            </body>
            </html>";
        }

        private static string GenerarHtmlVencimientoProximo(int ordenId, DateTime fechaVencimiento, decimal monto)
        {
            var diasRestantes = (fechaVencimiento - DateTime.Now).Days;
            
            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 0; padding: 0; }}
                    .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 30px; }}
                    .warning-box {{ background-color: #fff3cd; border: 2px solid #ffc107; 
                                   padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    .footer {{ background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 12px; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <h2>⚠️ Recordatorio de Vencimiento</h2>
                </div>
                <div class='content'>
                    <div class='warning-box'>
                        <h3 style='margin-top: 0; color: #856404;'>Atención: Su orden está próxima a vencer</h3>
                        <p><strong>Orden:</strong> #{ordenId:D6}</p>
                        <p><strong>Fecha de Vencimiento:</strong> {fechaVencimiento:dd/MM/yyyy}</p>
                        <p><strong>Días Restantes:</strong> {diasRestantes} días</p>
                        <p><strong>Monto Pendiente:</strong> ${monto:N2}</p>
                    </div>
                    
                    <p>Le recordamos realizar el pago antes de la fecha de vencimiento para evitar recargos.</p>
                </div>
                <div class='footer'>
                    <p>&copy; {DateTime.Now.Year} DGAC - Ecuador</p>
                </div>
            </body>
            </html>";
        }

        #endregion
    }
}
