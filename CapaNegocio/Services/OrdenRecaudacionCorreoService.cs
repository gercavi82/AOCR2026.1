using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public class OrdenRecaudacionCorreoService
    {
        private readonly IEmailQueueService _emailQueueService;
        private readonly ILoggingService _logger;
        private readonly NotificacionDestinatarioPolicyService _policyService;

        public OrdenRecaudacionCorreoService()
            : this(new EmailQueueService(), new NotificacionDestinatarioPolicyService())
        {
        }

        public OrdenRecaudacionCorreoService(IEmailQueueService emailQueueService, NotificacionDestinatarioPolicyService policyService)
        {
            _emailQueueService = emailQueueService;
            _policyService = policyService;
            _logger = LoggingServiceFactory.Create();
        }

        public ResultadoOperacion NotificarEvento(
            OrdenRecaudacion orden,
            string evento,
            string emailDestino = null,
            string nombreDestino = null,
            byte[] adjuntoPdf = null,
            string nombreAdjunto = null,
            string observacion = null,
            string eventKeySuffix = null)
        {
            try
            {
                if (orden == null)
                {
                    return ResultadoOperacion.Error("No existe orden para notificar.");
                }

                var plantilla = ConstruirPlantilla(orden, evento);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Ok(null, "Evento de orden sin plantilla de correo configurada.");
                }

                var destinatarios = ResolverDestinatarios(orden, plantilla, emailDestino, nombreDestino);
                if (destinatarios.Count == 0)
                {
                    if (string.Equals((evento ?? string.Empty).Trim(), "ORDEN_RECAUDACION_GENERADA_FINANCIERO", StringComparison.OrdinalIgnoreCase))
                    {
                        return ResultadoOperacion.Error("No existe un correo institucional activo para FINANCIERO_AOCR.");
                    }

                    return ResultadoOperacion.Ok(null, "Evento de orden sin destinatarios resolubles.");
                }

                var solicitudId = orden.CodigoSolicitud.HasValue && orden.CodigoSolicitud.Value > 0
                    ? (int?)orden.CodigoSolicitud.Value
                    : null;
                if (!solicitudId.HasValue)
                {
                    return ResultadoOperacion.Error("La orden no tiene una solicitud AOCR asociada para registrar la notificación.");
                }

                var tipoNotificacion = (evento ?? string.Empty).Trim().ToUpperInvariant();

                foreach (var destinatario in destinatarios)
                {
                    var correoDestinoNormalizado = (destinatario.Email ?? string.Empty).Trim();
                    if (!CorreoInstitucionalService.EsCorreoValido(correoDestinoNormalizado))
                    {
                        _logger.LogWarning("OrdenRecaudacionCorreoService.NotificarEvento: correo omitido por formato inválido: " + correoDestinoNormalizado);
                        continue;
                    }

                    var item = new EmailQueueItem
                    {
                        Para = correoDestinoNormalizado,
                        ParaNombre = destinatario.Nombre,
                        Asunto = plantilla.Asunto,
                        Cuerpo = ConstruirCuerpoHtml(destinatario.Nombre, plantilla, orden, observacion),
                        Estado = "PENDIENTE",
                        SolicitudId = solicitudId.Value,
                        OrdenId = orden.Id > 0 ? (int?)orden.Id : null,
                        TipoNotificacion = tipoNotificacion,
                        EventKey = ConstruirEventKey(
                            tipoNotificacion,
                            orden,
                            solicitudId.Value,
                            correoDestinoNormalizado,
                            eventKeySuffix),
                        EsHtml = true,
                        AdjuntoContenido = adjuntoPdf,
                        AdjuntoNombre = adjuntoPdf != null ? (nombreAdjunto ?? (orden.NumeroOrden ?? "orden") + ".pdf") : null,
                        AdjuntoMimeType = adjuntoPdf != null ? "application/pdf" : null
                    };

                    _emailQueueService.EncolarAsync(item).GetAwaiter().GetResult();
                }

                return ResultadoOperacion.Ok(destinatarios.Count, "Correos de orden encolados correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    ErrorCode = "OR_CORREO_QUEUE_ERROR",
                    CodigoSolicitud = orden != null && orden.CodigoSolicitud.HasValue ? orden.CodigoSolicitud.Value.ToString(CultureInfo.InvariantCulture) : null,
                    NumeroOrden = orden != null ? orden.NumeroOrden : null
                });
                return ResultadoOperacion.Error("No fue posible encolar correos del evento de orden.");
            }
        }

        private static string ConstruirEventKey(
            string tipoNotificacion,
            OrdenRecaudacion orden,
            int solicitudId,
            string correoDestino,
            string eventKeySuffix)
        {
            var tipo = (tipoNotificacion ?? string.Empty).Trim().ToUpperInvariant();
            var ordenId = orden != null && orden.Id > 0 ? orden.Id : 0;

            switch (tipo)
            {
                case "ORDEN_CREADA":
                case "ORDEN_GENERADA_RT":
                    return string.Format(CultureInfo.InvariantCulture, "ORDEN_GENERADA_RT_{0}", ordenId);
                case "COMPROBANTE_CARGADO_FINANCIERO":
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "COMPROBANTE_CARGADO_FINANCIERO_{0}_{1}",
                        ordenId,
                        string.IsNullOrWhiteSpace(eventKeySuffix) ? "0" : eventKeySuffix.Trim());
                case "PAGO_APROBADO_RT":
                    return string.Format(CultureInfo.InvariantCulture, "PAGO_APROBADO_RT_{0}", ordenId);
                case "PAGO_OBSERVADO_RT":
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "PAGO_OBSERVADO_RT_{0}_{1}",
                        ordenId,
                        string.IsNullOrWhiteSpace(eventKeySuffix) ? "0" : eventKeySuffix.Trim());
                default:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}_{1}_{2}",
                        tipo,
                        solicitudId,
                        (correoDestino ?? string.Empty).Trim().ToUpperInvariant());
            }
        }

        private static PlantillaOrdenCorreo ConstruirPlantilla(OrdenRecaudacion orden, string evento)
        {
            switch ((evento ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "ORDEN_RECAUDACION_GENERADA_FINANCIERO":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Nueva Orden de Recaudación generada para solicitud AOCR #" + (orden.CodigoSolicitud.HasValue ? orden.CodigoSolicitud.Value.ToString(CultureInfo.InvariantCulture) : "N/D"),
                        Titulo = "Orden de Recaudación generada",
                        Mensaje = "Se ha generado una nueva Orden de Recaudación asociada a una Solicitud AOCR y queda pendiente la revisión del pago por el área financiera.",
                        GruposDestinatarios = new[] { CorreoInstitucionalService.FinancieroAocr }
                    };
                case "ORDEN_CREADA":
                case "ORDEN_GENERADA_RT":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Nueva Orden de recaudación - " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Orden de recaudación generada",
                        Mensaje = "Su Orden de Recaudación ha sido generada correctamente. Descargue el documento y cargue el comprobante de depósito o transferencia para que el área Financiera pueda realizar la revisión correspondiente.",
                        GruposDestinatarios = new[] { NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante }
                    };
                case "COMPROBANTE_CARGADO_FINANCIERO":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Sistema AOCR - Pago pendiente de revisión",
                        Titulo = "Pago pendiente de revisión",
                        Mensaje = "Se informa que el Representante Técnico ha cargado el comprobante de depósito o transferencia correspondiente a la orden "
                            + (orden.NumeroOrden ?? ("#" + orden.Id))
                            + " de la solicitud "
                            + (orden.CodigoSolicitud.HasValue ? orden.CodigoSolicitud.Value.ToString(CultureInfo.InvariantCulture) : "N/D")
                            + ". Debe ingresar al Sistema AOCR para revisar el comprobante y aprobar u observar el pago.",
                        GruposDestinatarios = new[] { CorreoInstitucionalService.FinancieroAocr }
                    };
                case "PAGO_REGISTRADO":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Pago registrado - Orden " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Pago registrado",
                        Mensaje = "El pago de la orden fue registrado y queda pendiente de validación por el área financiera.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoFinanciero
                        }
                    };
                case "PAGO_VALIDADO":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Pago validado - Orden " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Pago validado",
                        Mensaje = "El pago de la orden fue validado correctamente y el trámite financiero puede continuar.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoFinanciero
                        }
                    };
                case "FACTURA_GENERADA":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Factura generada - Orden " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Factura generada",
                        Mensaje = "La factura asociada a la orden fue generada y queda disponible para su consulta.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoFinanciero
                        }
                    };
                default:
                    return null;
            }
        }

        private List<NotificacionDestinatario> ResolverDestinatarios(OrdenRecaudacion orden, PlantillaOrdenCorreo plantilla, string emailDestino, string nombreDestino)
        {
            if (!string.IsNullOrWhiteSpace(emailDestino))
            {
                orden.Correo = emailDestino;
            }

            if (!string.IsNullOrWhiteSpace(nombreDestino))
            {
                orden.NombreContribuyente = nombreDestino;
                orden.Compania = nombreDestino;
            }

            var grupos = plantilla.GruposDestinatarios ?? new string[0];
            if (grupos.Any(g => string.Equals(g, CorreoInstitucionalService.FinancieroAocr, StringComparison.OrdinalIgnoreCase)))
            {
                var institucional = new CorreoInstitucionalService().ObtenerDestinatariosPorArea(CorreoInstitucionalService.FinancieroAocr);
                if (institucional == null)
                {
                    _logger.LogWarning("OrdenRecaudacionCorreoService.NotificarEvento: no existe correo institucional activo para FINANCIERO_AOCR.");
                    return new List<NotificacionDestinatario>();
                }

                return institucional.ObtenerTodosLosCorreos()
                    .Where(CorreoInstitucionalService.EsCorreoValido)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(correo => new NotificacionDestinatario
                    {
                        Email = correo,
                        Nombre = string.IsNullOrWhiteSpace(institucional.NombreArea) ? "Financiero AOCR" : institucional.NombreArea
                    })
                    .ToList();
            }

            return _policyService.ResolverDestinatarios(orden, grupos);
        }

        private static string ConstruirCuerpoHtml(string nombreDestino, PlantillaOrdenCorreo plantilla, OrdenRecaudacion orden, string observacion)
        {
            var observacionEsHtml = !string.IsNullOrWhiteSpace(observacion)
                && observacion.IndexOf('<') >= 0
                && observacion.IndexOf('>') > observacion.IndexOf('<');
            var solicitud = ObtenerSolicitud(orden);
            var numeroSolicitud = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? solicitud.NumeroSolicitud.Trim()
                : (orden.CodigoSolicitud.HasValue ? orden.CodigoSolicitud.Value.ToString() : "N/D");
            var nombreOperadora = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NombreOperador)
                ? solicitud.NombreOperador.Trim()
                : (!string.IsNullOrWhiteSpace(solicitud != null ? solicitud.RazonSocial : null)
                    ? solicitud.RazonSocial.Trim()
                    : (orden.Compania ?? "Contribuyente"));
            var ruc = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.Ruc)
                ? solicitud.Ruc.Trim()
                : (orden.RucCedula ?? string.Empty);
            var nombreRt = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal)
                ? solicitud.RepresentanteLegal.Trim()
                : "Representante Técnico";

            var model = new EmailTemplateModel
            {
                Titulo = plantilla.Titulo,
                NombreDestinatario = string.IsNullOrWhiteSpace(nombreDestino) ? "Usuario AOCR" : nombreDestino,
                MensajePrincipal = plantilla.Mensaje,
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Número de Orden de Recaudación", orden.NumeroOrden ?? ("#" + orden.Id)),
                    new EmailFieldItem("Número de Solicitud AOCR", numeroSolicitud),
                    new EmailFieldItem("Operadora / Compañía", nombreOperadora),
                    new EmailFieldItem("RUC", ruc),
                    new EmailFieldItem("Representante Técnico", nombreRt),
                    new EmailFieldItem("Monto", "$" + string.Format(CultureInfo.InvariantCulture, "{0:N2}", orden.Total ?? 0m)),
                    new EmailFieldItem("Fecha de generación", DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                    new EmailFieldItem("Estado actual", "Orden generada / Pendiente de pago")
                },
                Observaciones = observacionEsHtml ? null : observacion,
                ContenidoHtmlExtra = observacionEsHtml ? observacion : null,
                TextoCierre = "Puede revisar el detalle desde el sistema AOCR.",
                Footer = "Este es un mensaje automatico del workflow financiero AOCR."
            };

            return EmailTemplateRenderer.Render(model);
        }

        private static SolicitudAOCR ObtenerSolicitud(OrdenRecaudacion orden)
        {
            if (orden == null || !orden.CodigoSolicitud.HasValue || orden.CodigoSolicitud.Value <= 0)
            {
                return null;
            }

            try
            {
                return new SolicitudDAO().ObtenerPorId(orden.CodigoSolicitud.Value);
            }
            catch
            {
                return null;
            }
        }

        private sealed class PlantillaOrdenCorreo
        {
            public string Asunto { get; set; }
            public string Titulo { get; set; }
            public string Mensaje { get; set; }
            public string[] GruposDestinatarios { get; set; }
        }
    }
}
