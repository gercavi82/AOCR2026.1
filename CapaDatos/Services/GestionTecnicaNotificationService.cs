using System;
using System.Net;
using CapaDatos.Models;

namespace CapaDatos.Services
{
    public class GestionTecnicaNotificationService
    {
        private readonly IEmailService _emailService;
        private readonly ILoggingService _logger;

        public GestionTecnicaNotificationService()
            : this(new EmailService())
        {
        }

        public GestionTecnicaNotificationService(IEmailService emailService)
        {
            _emailService = emailService;
            _logger = LoggingServiceFactory.Create();
        }

        public bool EnviarAsignacionUsuarioInterno(
            UsuarioInternoRTRegistro destinatario,
            int codigoSolicitud,
            string numeroSolicitud,
            string modulo,
            DateTime fechaAsignacion,
            string usuarioAsignador,
            string observacion,
            out string mensaje)
        {
            mensaje = string.Empty;

            if (destinatario == null)
            {
                mensaje = "No existe destinatario interno para la notificacion.";
                return false;
            }

            var correo = (destinatario.CorreoInstitucional ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correo))
            {
                mensaje = "El usuario interno no tiene correo institucional registrado.";
                return false;
            }

            var nombre = string.IsNullOrWhiteSpace(destinatario.NombreVisual)
                ? (string.IsNullOrWhiteSpace(destinatario.CodigoUsuario) ? "Usuario interno" : destinatario.CodigoUsuario)
                : destinatario.NombreVisual;
            var numero = string.IsNullOrWhiteSpace(numeroSolicitud)
                ? codigoSolicitud.ToString()
                : numeroSolicitud.Trim();
            var moduloTexto = string.IsNullOrWhiteSpace(modulo) ? "Gestión Técnica" : modulo.Trim();
            var actor = string.IsNullOrWhiteSpace(usuarioAsignador) ? "sistema" : usuarioAsignador.Trim();

            var asunto = "Nueva asignacion en Gestion Tecnica AOCR";
            var cuerpo = ConstruirHtmlAsignacionGestionTecnica(
                nombre,
                numero,
                moduloTexto,
                fechaAsignacion,
                actor,
                observacion);

            try
            {
                var resultado = _emailService
                    .EnviarAsync(correo, nombre, asunto, cuerpo)
                    .GetAwaiter()
                    .GetResult();

                if (!resultado.Success)
                {
                    mensaje = "La asignacion fue registrada, pero no se pudo enviar el correo institucional.";
                    _logger.LogWarning(mensaje,
                        new LogContext
                        {
                            ErrorCode = "GT_NOTIFY_FAIL",
                            CodigoSolicitud = numero,
                            AdditionalData =
                            {
                                ["Destino"] = correo,
                                ["Error"] = resultado.Error ?? string.Empty
                            }
                        });
                    return false;
                }

                _logger.LogInfo("Correo institucional de asignación enviado correctamente.",
                    new LogContext
                    {
                        ErrorCode = "GT_NOTIFY_OK",
                        CodigoSolicitud = numero,
                        AdditionalData =
                        {
                            ["Destino"] = correo,
                            ["UsuarioInterno"] = nombre,
                            ["Modulo"] = moduloTexto
                        }
                    });
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "La asignacion fue registrada, pero ocurrio un error al enviar el correo institucional.";
                _logger.LogError(ex,
                    new LogContext
                    {
                        ErrorCode = "GT_NOTIFY_EX",
                        CodigoSolicitud = numero,
                        AdditionalData =
                        {
                            ["Destino"] = correo,
                            ["UsuarioInterno"] = nombre,
                            ["Modulo"] = moduloTexto
                        }
                    });
                return false;
            }
        }

        private static string ConstruirHtmlAsignacionGestionTecnica(
            string nombreUsuario,
            string numeroSolicitud,
            string modulo,
            DateTime fechaAsignacion,
            string usuarioAsignador,
            string observacion)
        {
            var fechaTexto = fechaAsignacion.ToString("dd/MM/yyyy HH:mm");
            var observacionHtml = string.IsNullOrWhiteSpace(observacion)
                ? string.Empty
                : "<p><strong>Observacion de asignacion:</strong> " + WebUtility.HtmlEncode(observacion.Trim()) + "</p>";

            return "<p>Estimado/a " + WebUtility.HtmlEncode(nombreUsuario) + ":</p>"
                + "<p>Se le ha asignado una nueva solicitud en la bandeja de <strong>" + WebUtility.HtmlEncode(modulo) + "</strong> del sistema AOCR.</p>"
                + "<ul>"
                + "<li><strong>Solicitud:</strong> " + WebUtility.HtmlEncode(numeroSolicitud) + "</li>"
                + "<li><strong>Módulo:</strong> " + WebUtility.HtmlEncode(modulo) + "</li>"
                + "<li><strong>Fecha y hora de asignacion:</strong> " + WebUtility.HtmlEncode(fechaTexto) + "</li>"
                + "<li><strong>Asignado por:</strong> " + WebUtility.HtmlEncode(usuarioAsignador) + "</li>"
                + "</ul>"
                + observacionHtml
                + "<p>Por favor ingrese al sistema para revisar la solicitud asignada y continuar con la gestion respectiva.</p>"
                + "<p>Atentamente,<br/>Sistema AOCR<br/>DGAC</p>";
        }
    }
}
