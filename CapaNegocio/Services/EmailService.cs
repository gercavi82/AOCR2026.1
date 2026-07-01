using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    public class EmailSendResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        public static EmailSendResult Ok()
        {
            return new EmailSendResult { Success = true };
        }

        public static EmailSendResult Fail(string error)
        {
            return new EmailSendResult { Success = false, Error = error };
        }
    }

    public class EmailSendAttachment
    {
        public byte[] Content { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
    }

    public interface IEmailService
    {
        void EnviarConAdjunto(string para, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre);
        Task<EmailSendResult> EnviarAsync(string para, string nombrePara, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre);
        Task<EmailSendResult> EnviarAsync(string para, string nombrePara, string asunto, string html, IEnumerable<EmailSendAttachment> adjuntos);
    }

    /// <summary>
    /// Fachada de compatibilidad. Delega en AocrEmailService institucional.
    /// </summary>
    public class EmailService : IEmailService
    {
        private static readonly Regex EmailRegex = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
        private readonly AocrEmailService _emailService = new AocrEmailService();

        public void EnviarConAdjunto(string para, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre)
        {
            if (string.IsNullOrWhiteSpace(para) || !EmailRegex.IsMatch(para))
                throw new ArgumentException("Correo destino inválido.");

            if (string.IsNullOrWhiteSpace(asunto))
                throw new ArgumentException("Asunto requerido.");

            if (adjuntoBytes == null || adjuntoBytes.Length == 0)
                throw new ArgumentException("Adjunto vacío.");

            adjuntoNombre = string.IsNullOrWhiteSpace(adjuntoNombre) ? "documento.pdf" : adjuntoNombre;

            if (!_emailService.EnviarMensajeCorreoConAdjunto(
                para,
                asunto,
                html,
                adjuntoBytes,
                adjuntoNombre,
                AocrEmailService.AliasDefault,
                "application/pdf"))
            {
                throw new Exception(_emailService.LastError ?? "No fue posible enviar el correo con adjunto.");
            }
        }

        public Task<EmailSendResult> EnviarAsync(string para, string nombrePara, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(para) || !EmailRegex.IsMatch(para))
                    return Task.FromResult(EmailSendResult.Fail("Correo destino inválido"));

                if (string.IsNullOrWhiteSpace(asunto))
                    return Task.FromResult(EmailSendResult.Fail("Asunto requerido"));

                bool enviado;
                if (adjuntoBytes != null && adjuntoBytes.Length > 0)
                {
                    enviado = _emailService.EnviarMensajeCorreoConAdjunto(
                        para,
                        asunto,
                        html,
                        adjuntoBytes,
                        adjuntoNombre,
                        AocrEmailService.AliasDefault,
                        "application/pdf");
                }
                else
                {
                    enviado = _emailService.EnviarMensajeCorreo(para, asunto, html, AocrEmailService.AliasDefault);
                }

                return Task.FromResult(enviado
                    ? EmailSendResult.Ok()
                    : EmailSendResult.Fail(_emailService.LastError ?? "No fue posible enviar el correo."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(EmailSendResult.Fail(ex.Message));
            }
        }

        public Task<EmailSendResult> EnviarAsync(string para, string nombrePara, string asunto, string html, IEnumerable<EmailSendAttachment> adjuntos)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(para) || !EmailRegex.IsMatch(para))
                    return Task.FromResult(EmailSendResult.Fail("Correo destino invÃ¡lido"));

                if (string.IsNullOrWhiteSpace(asunto))
                    return Task.FromResult(EmailSendResult.Fail("Asunto requerido"));

                var lista = (adjuntos ?? Enumerable.Empty<EmailSendAttachment>())
                    .Where(a => a != null && a.Content != null && a.Content.Length > 0)
                    .Select(a => Tuple.Create(a.Content, string.IsNullOrWhiteSpace(a.FileName) ? "documento.pdf" : a.FileName, string.IsNullOrWhiteSpace(a.ContentType) ? "application/pdf" : a.ContentType))
                    .ToList();

                bool enviado = lista.Count > 0
                    ? _emailService.EnviarMensajeCorreoConAdjuntos(para, asunto, html, lista, AocrEmailService.AliasDefault)
                    : _emailService.EnviarMensajeCorreo(para, asunto, html, AocrEmailService.AliasDefault);

                return Task.FromResult(enviado
                    ? EmailSendResult.Ok()
                    : EmailSendResult.Fail(_emailService.LastError ?? "No fue posible enviar el correo."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(EmailSendResult.Fail(ex.Message));
            }
        }
    }
}
