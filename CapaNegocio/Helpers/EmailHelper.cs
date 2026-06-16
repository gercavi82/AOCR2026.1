using System;
using System.Collections.Generic;
using CapaDatos.Services;
using CapaModelo.Common;

namespace CapaNegocio.Helpers
{
    public static class EmailHelper
    {
        private static readonly AocrEmailService EmailService = new AocrEmailService();

        public static bool EnviarEmail(string destinatario, string asunto, string cuerpoHtml)
        {
            return EnviarEmail(destinatario, asunto, cuerpoHtml, null);
        }

        public static bool EnviarEmail(string destinatario, string asunto, string cuerpoHtml, string aliasCorreo)
        {
            try
            {
                return EmailService.EnviarMensajeCorreo(destinatario, asunto, cuerpoHtml, aliasCorreo);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al enviar correo: " + ex.Message, ex);
            }
        }

        public static bool EnviarRecordatorioVencimiento(string destinatario, string nombreOperador, string numeroCertificado, int diasRestantes)
        {
            string asunto = "Recordatorio: Certificado " + numeroCertificado + " próximo a vencer";

            var model = new EmailTemplateModel
            {
                Titulo = "Recordatorio de vencimiento",
                NombreDestinatario = nombreOperador,
                MensajePrincipal = "Su certificado está próximo a vencer. Le recomendamos iniciar el proceso de renovación lo antes posible.",
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Número de certificado", numeroCertificado),
                    new EmailFieldItem("Días restantes", diasRestantes + " días")
                },
                TextoCierre = "Puede gestionar la renovación desde el sistema AOCR.",
                Footer = "Este es un correo automático, por favor no responder."
            };

            string cuerpo = EmailTemplateRenderer.Render(model);
            return EnviarEmail(destinatario, asunto, cuerpo, AocrEmailService.AliasDefault);
        }
    }
}
