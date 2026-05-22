using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public class InspeccionCorreoService
    {
        private const string TipoNotificacionResultadoInformeTecnicoDireccion = "RESULTADO_INFORME_TECNICO_DIRDAC";
        private const string EventKeyResultadoInformeTecnicoDireccionPrefix = "RESULTADO_INFORME_TECNICO_DIRDAC_";

        private readonly IEmailQueueService _emailQueueService;
        private readonly ILoggingService _logger;
        private readonly NotificacionDestinatarioPolicyService _policyService;

        public InspeccionCorreoService()
            : this(new EmailQueueService(), new NotificacionDestinatarioPolicyService())
        {
        }

        public InspeccionCorreoService(IEmailQueueService emailQueueService, NotificacionDestinatarioPolicyService policyService)
        {
            _emailQueueService = emailQueueService;
            _policyService = policyService;
            _logger = LoggingServiceFactory.Create();
        }

        public ResultadoOperacion NotificarEvento(Inspeccion inspeccion, SolicitudAOCR solicitud, string evento, string observacion)
        {
            try
            {
                if (inspeccion == null || solicitud == null)
                {
                    return ResultadoOperacion.Error("No existe contexto suficiente para notificar el evento de inspección.");
                }

                var plantilla = ConstruirPlantilla(inspeccion, solicitud, evento, observacion);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Ok(null, "Evento sin plantilla de correo configurada.");
                }

                var destinatarios = _policyService.ResolverDestinatarios(solicitud, inspeccion, plantilla.GruposDestinatarios);
                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "Evento sin destinatarios de correo resolubles.");
                }

                foreach (var destinatario in destinatarios)
                {
                    var item = new EmailQueueItem
                    {
                        Para = destinatario.Email,
                        ParaNombre = destinatario.Nombre,
                        Asunto = plantilla.Asunto,
                        Cuerpo = ConstruirCuerpoHtml(destinatario.Nombre, plantilla, inspeccion, solicitud, observacion),
                        Estado = "PENDIENTE",
                        SolicitudId = solicitud.CodigoSolicitud,
                        TipoNotificacion = "INSPECCION_" + (evento ?? string.Empty).Trim().ToUpperInvariant(),
                        EsHtml = true
                    };

                    _emailQueueService.EncolarAsync(item).GetAwaiter().GetResult();
                }

                return ResultadoOperacion.Ok(destinatarios.Count, "Correos de inspección encolados correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.NotificarEvento: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible encolar correos del evento de inspección.");
            }
        }

        public ResultadoOperacion NotificarInformeTecnicoFirmadoFinal(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, byte[] pdfFirmado, string enlaceDocumento, string observacion)
        {
            try
            {
                if (inspeccion == null || solicitud == null)
                {
                    return ResultadoOperacion.Error("No existe contexto suficiente para notificar el informe técnico firmado.");
                }

                if (pdfFirmado == null || pdfFirmado.Length == 0)
                {
                    return ResultadoOperacion.Error("No existe un PDF firmado final para adjuntar.");
                }

                var plantilla = ConstruirPlantilla(inspeccion, solicitud, "INFORME_TECNICO_FIRMADO", observacion);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Error("No existe plantilla configurada para el informe técnico firmado.");
                }

                var destinatarios = _policyService.ResolverDestinatarios(
                    solicitud,
                    inspeccion,
                    NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                    NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                    NotificacionDestinatarioPolicyService.GrupoInspectorAsignado);

                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "No existen destinatarios resolubles para el informe técnico firmado.");
                }

                var servicioCorreo = new EnviarCorreo();
                var cuerpo = ConstruirCuerpoHtmlConEnlace(
                    plantilla,
                    inspeccion,
                    solicitud,
                    observacion,
                    enlaceDocumento,
                    null);
                var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
                var nombreAdjunto = string.Format("InformeTecnico_{0}_Firmado.pdf", numeroSolicitud.Replace("/", "_").Replace("\\", "_"));
                var enviados = 0;

                foreach (var destinatario in destinatarios)
                {
                    var html = cuerpo.Replace("@@DESTINATARIO@@", System.Web.HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(destinatario.Nombre) ? "Usuario AOCR" : destinatario.Nombre));
                    if (servicioCorreo.enviaMensajeCorreoConAdjunto(destinatario.Email, plantilla.Asunto, html, pdfFirmado, nombreAdjunto, "application/pdf"))
                    {
                        enviados++;
                    }
                }

                return enviados > 0
                    ? ResultadoOperacion.Ok(enviados, "Notificación final del informe técnico enviada correctamente.")
                    : ResultadoOperacion.Error("No fue posible enviar la notificación final del informe técnico.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.NotificarInformeTecnicoFirmadoFinal: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible enviar la notificación final del informe técnico firmado.");
            }
        }

        public ResultadoOperacion NotificarResultadoInformeTecnicoDireccion(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, string enlaceDocumento, string observacionDireccion)
        {
            return EnviarResultadoInformeTecnicoDesdeDireccion(
                inspeccion,
                solicitud,
                informe,
                0,
                enlaceDocumento,
                observacionDireccion,
                false);
        }

        public ResultadoOperacion EnviarResultadoInformeTecnicoDesdeDireccion(
            Inspeccion inspeccion,
            SolicitudAOCR solicitud,
            InspeccionInformeTecnico informe,
            int codigoUsuarioDireccion,
            string enlaceDocumento,
            string observacionDireccion,
            bool reenvioManual)
        {
            try
            {
                if (inspeccion == null || solicitud == null || informe == null)
                {
                    return ResultadoOperacion.Error("No existe contexto suficiente para notificar el resultado final del informe tecnico.");
                }

                if (!InformeTieneDecisionInstitucionalFinal(informe))
                {
                    return ResultadoOperacion.Error("El informe aun no cuenta con una decision institucional final que permita notificar al RT.");
                }

                if (!reenvioManual && (informe.NotificadoRt || informe.FechaNotificacionRt.HasValue))
                {
                    _logger.LogInfo("InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion: notificacion automatica omitida porque el informe ya figura notificado. InformeId=" + informe.CodigoInforme);
                    return ResultadoOperacion.Error("El informe ya registra una notificacion final al RT; se omite el reenvio automatico.");
                }

                var contexto = ConstruirContextoResultadoInformeDireccion(inspeccion, solicitud, informe, observacionDireccion);
                if (!string.IsNullOrWhiteSpace(contexto.MensajeError))
                {
                    return ResultadoOperacion.Error(contexto.MensajeError);
                }

                if (contexto.CamposFaltantes.Count > 0)
                {
                    _logger.LogWarning(
                        "InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion: datos opcionales faltantes. InformeId="
                        + informe.CodigoInforme
                        + ", Campos="
                        + string.Join(", ", contexto.CamposFaltantes.Distinct(StringComparer.OrdinalIgnoreCase)));
                }

                var eventKeyBase = ConstruirEventKeyBaseResultadoInformeDireccion(informe.CodigoInforme);
                if (!reenvioManual)
                {
                    var existeEnCola = _emailQueueService
                        .ExisteNotificacionAsync(TipoNotificacionResultadoInformeTecnicoDireccion, eventKeyBase, solicitud.CodigoSolicitud)
                        .GetAwaiter()
                        .GetResult();

                    if (existeEnCola)
                    {
                        _logger.LogInfo("InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion: ya existe una notificacion institucional en cola para el informe " + informe.CodigoInforme + ". Se omite reenvio automatico.");
                        return ResultadoOperacion.Error("El informe ya cuenta con una notificacion final previamente encolada para el RT; se omite el reenvio automatico.");
                    }
                }

                var asunto = ConstruirAsuntoResultadoInformeDireccion(contexto);
                var html = ConstruirHtmlResultadoInformeDireccion(contexto, asunto, enlaceDocumento);
                var eventKey = reenvioManual
                    ? eventKeyBase + "MANUAL_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)
                    : eventKeyBase.TrimEnd('_');

                var item = new EmailQueueItem
                {
                    Para = contexto.CorreoDestinatario,
                    ParaNombre = contexto.NombreDestinatario,
                    Asunto = asunto,
                    Cuerpo = html,
                    Estado = "PENDIENTE",
                    SolicitudId = solicitud.CodigoSolicitud,
                    TipoNotificacion = TipoNotificacionResultadoInformeTecnicoDireccion,
                    EsHtml = true,
                    EventKey = eventKey
                };

                var queueId = _emailQueueService.EncolarAsync(item).GetAwaiter().GetResult();
                if (queueId <= 0)
                {
                    return ResultadoOperacion.Error("No fue posible encolar la notificacion formal del resultado del informe tecnico al RT.");
                }

                var datos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "EmailQueueId", queueId.ToString(CultureInfo.InvariantCulture) },
                    { "Destinatario", contexto.CorreoDestinatario },
                    { "NombreDestinatario", contexto.NombreDestinatario },
                    { "CargoDestinatario", contexto.CargoDestinatario },
                    { "Asunto", asunto },
                    { "ResultadoTecnico", contexto.ResultadoTecnicoFinal },
                    { "TipoResultadoInsatisfactorio", contexto.TipoResultadoInsatisfactorio },
                    { "TipoNotificacion", TipoNotificacionResultadoInformeTecnicoDireccion },
                    { "CodigoInforme", informe.CodigoInforme.ToString(CultureInfo.InvariantCulture) },
                    { "CodigoSolicitud", solicitud.CodigoSolicitud.ToString(CultureInfo.InvariantCulture) },
                    { "CodigoInspeccion", inspeccion.CodigoInspeccion.ToString(CultureInfo.InvariantCulture) },
                    { "ReenvioManual", reenvioManual ? "true" : "false" },
                    { "UsuarioDireccion", codigoUsuarioDireccion.ToString(CultureInfo.InvariantCulture) }
                };

                _logger.LogInfo(
                    "InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion: correo institucional encolado. InformeId="
                    + informe.CodigoInforme
                    + ", SolicitudId="
                    + solicitud.CodigoSolicitud
                    + ", InspeccionId="
                    + inspeccion.CodigoInspeccion
                    + ", Destinatario="
                    + contexto.CorreoDestinatario
                    + ", QueueId="
                    + queueId
                    + ", ReenvioManual="
                    + reenvioManual);

                var mensajeExito = reenvioManual
                    ? "Se encolo correctamente el reenvio manual de la notificacion institucional al RT."
                    : "Se encolo correctamente la notificacion institucional del resultado del informe tecnico al RT.";

                return ResultadoOperacion.Ok(datos, mensajeExito);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible encolar la notificacion final del informe tecnico al RT.");
            }
        }

        private InformeTecnicoDireccionEmailContext ConstruirContextoResultadoInformeDireccion(
            Inspeccion inspeccion,
            SolicitudAOCR solicitud,
            InspeccionInformeTecnico informe,
            string observacionDireccion)
        {
            var contexto = new InformeTecnicoDireccionEmailContext();

            contexto.CorreoDestinatario = FirstNonEmpty(solicitud.CorreoRepresentanteTecnico, solicitud.Email);
            if (string.IsNullOrWhiteSpace(contexto.CorreoDestinatario))
            {
                contexto.MensajeError = "El informe fue aprobado, pero no se pudo notificar al RT porque no tiene correo registrado.";
                _logger.LogWarning("InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion: solicitud sin correo RT/representante. SolicitudId=" + solicitud.CodigoSolicitud + ", InformeId=" + informe.CodigoInforme);
                return contexto;
            }

            contexto.NombreDestinatario = FirstNonEmpty(
                !string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico) ? solicitud.TecnicoResponsableNombre : null,
                solicitud.RepresentanteLegal,
                solicitud.TecnicoResponsableNombre,
                "Representante autorizado");
            contexto.CargoDestinatario = DeterminarCargoDestinatario(solicitud);
            contexto.LineaDespacho = FirstNonEmpty(solicitud.Direccion, "En su Despacho");

            contexto.NumeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            contexto.CodigoInspeccionTexto = !string.IsNullOrWhiteSpace(inspeccion.NumeroInspeccion)
                ? inspeccion.NumeroInspeccion.Trim()
                : inspeccion.CodigoInspeccion.ToString(CultureInfo.InvariantCulture);
            contexto.Operadora = FirstNonEmpty(solicitud.NombreOperador, solicitud.RazonSocial, solicitud.NombreComercial, "Operadora / EAE");
            contexto.TipoTramite = FirstNonEmpty(solicitud.TipoOperacion, solicitud.DescripcionOperacion, "Tramite AOCR");
            contexto.EstacionInspeccionada = FirstNonEmpty(informe.EstacionesInspeccionManual, inspeccion.Lugar);
            contexto.Ciudad = FirstNonEmpty(solicitud.Ciudad, solicitud.Provincia);
            contexto.FechaInspeccionTexto = FirstNonEmpty(
                CompactarTexto(informe.FechasInspeccionManual),
                FormatearFecha(inspeccion.FechaProgramada));
            contexto.FechaAprobacionInstitucionalTexto = FirstNonEmpty(
                FormatearFecha(informe.FechaFirma2),
                FormatearFecha(informe.UpdatedAt));

            RegistrarFaltanteSiCorresponde(contexto.CamposFaltantes, contexto.CargoDestinatario, "CargoDestinatario");
            RegistrarFaltanteSiCorresponde(contexto.CamposFaltantes, contexto.EstacionInspeccionada, "EstacionInspeccionada");
            RegistrarFaltanteSiCorresponde(contexto.CamposFaltantes, contexto.Ciudad, "Ciudad");
            RegistrarFaltanteSiCorresponde(contexto.CamposFaltantes, contexto.FechaInspeccionTexto, "FechaInspeccion");
            RegistrarFaltanteSiCorresponde(contexto.CamposFaltantes, contexto.FechaAprobacionInstitucionalTexto, "FechaAprobacionInstitucional");

            contexto.CargoDestinatario = string.IsNullOrWhiteSpace(contexto.CargoDestinatario) ? "Representante autorizado" : contexto.CargoDestinatario;
            contexto.EstacionInspeccionada = string.IsNullOrWhiteSpace(contexto.EstacionInspeccionada) ? "No registrada" : contexto.EstacionInspeccionada;
            contexto.Ciudad = string.IsNullOrWhiteSpace(contexto.Ciudad) ? "No registrada" : contexto.Ciudad;
            contexto.FechaInspeccionTexto = string.IsNullOrWhiteSpace(contexto.FechaInspeccionTexto) ? "No registrada" : contexto.FechaInspeccionTexto;
            contexto.FechaAprobacionInstitucionalTexto = string.IsNullOrWhiteSpace(contexto.FechaAprobacionInstitucionalTexto) ? "No registrada" : contexto.FechaAprobacionInstitucionalTexto;

            contexto.ResultadoTecnicoFinal = NormalizarResultadoTecnico(FirstNonEmpty(informe.Resultado, inspeccion.Resultado));
            if (string.IsNullOrWhiteSpace(contexto.ResultadoTecnicoFinal))
            {
                contexto.MensajeError = "El informe fue aprobado, pero no se pudo notificar al RT porque el resultado técnico final no está definido.";
                _logger.LogWarning("InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion: resultado tecnico final vacio. InformeId=" + informe.CodigoInforme);
                return contexto;
            }

            contexto.TipoResultadoInsatisfactorio = NormalizarTipoResultadoInsatisfactorio(informe.TipoResultadoInsatisfactorio);
            if (string.Equals(contexto.ResultadoTecnicoFinal, "INSATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(contexto.TipoResultadoInsatisfactorio))
            {
                contexto.CamposFaltantes.Add("TipoResultadoInsatisfactorio");
                contexto.TipoResultadoInsatisfactorio = "NO_ESPECIFICADO";
            }

            contexto.Hallazgos = ConstruirDetalleHallazgos(inspeccion, informe, contexto.CamposFaltantes);
            contexto.NoConformidades = FirstNonEmpty(informe.NoConformidades, contexto.Hallazgos, "No se registran no conformidades adicionales.");
            contexto.ObservacionesGenerales = FirstNonEmpty(
                observacionDireccion,
                informe.Observaciones,
                inspeccion.ObservacionesGenerales,
                solicitud.Observaciones,
                "Sin observaciones adicionales.");
            contexto.ObservacionesOperaciones = ResolverObservacionesPorCategoria(
                contexto.CamposFaltantes,
                "ObservacionesOperaciones",
                new[] { "OPERAC", "OPERACIONAL", "VUELO", "TRIPUL", "RUTA" },
                informe.OperacionComercial,
                solicitud.ResumenOperacionesEae,
                solicitud.DescripcionOperacion,
                informe.Observaciones,
                informe.NoConformidades);
            contexto.ObservacionesMantenimiento = ResolverObservacionesPorCategoria(
                contexto.CamposFaltantes,
                "ObservacionesMantenimiento",
                new[] { "MANTEN", "TALLER", "AERONAVEGABIL", "MECAN", "MOTOR" },
                informe.ServiciosEstaciones,
                informe.Observaciones,
                informe.NoConformidades);
            contexto.Conclusiones = FirstNonEmpty(informe.Conclusiones, "No se registran conclusiones adicionales.");
            contexto.Recomendaciones = FirstNonEmpty(informe.Recomendaciones, "No se registran recomendaciones adicionales.");
            contexto.ObservacionDireccion = FirstNonEmpty(observacionDireccion, "Sin observacion institucional adicional.");
            contexto.PlazoSubsanacionTexto = ExtraerPlazoSubsanacion(
                observacionDireccion,
                informe.Recomendaciones,
                informe.Observaciones,
                informe.Conclusiones);

            if (string.IsNullOrWhiteSpace(contexto.PlazoSubsanacionTexto))
            {
                contexto.CamposFaltantes.Add("PlazoSubsanacion");
            }

            return contexto;
        }

        private string ConstruirAsuntoResultadoInformeDireccion(InformeTecnicoDireccionEmailContext contexto)
        {
            var operadora = string.IsNullOrWhiteSpace(contexto.Operadora) ? "AOCR" : contexto.Operadora.Trim();
            return string.Equals(contexto.ResultadoTecnicoFinal, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                ? operadora + " - Resultado satisfactorio de inspeccion"
                : operadora + " - Resultado insatisfactorio de inspeccion";
        }

        private string ConstruirHtmlResultadoInformeDireccion(InformeTecnicoDireccionEmailContext contexto, string asunto, string enlaceDocumento)
        {
            var resumen = new List<EmailFieldItem>
            {
                new EmailFieldItem("Solicitud AOCR", contexto.NumeroSolicitud),
                new EmailFieldItem("Código de inspección", contexto.CodigoInspeccionTexto),
                new EmailFieldItem("Tipo de trámite", contexto.TipoTramite),
                new EmailFieldItem("Operadora / EAE", contexto.Operadora),
                new EmailFieldItem("Estación inspeccionada", contexto.EstacionInspeccionada),
                new EmailFieldItem("Ciudad", contexto.Ciudad),
                new EmailFieldItem("Fecha de inspección", contexto.FechaInspeccionTexto),
                new EmailFieldItem("Fecha de aprobación institucional", contexto.FechaAprobacionInstitucionalTexto)
            };

            var contenido = new StringBuilder(4096);
            contenido.Append("<div style='font-size:14px; color:#243746; line-height:1.65;'>");
            contenido.Append("<p style='margin:0 0 16px 0;'>Senor/a<br /><strong>");
            contenido.Append(EncodeHtml(contexto.NombreDestinatario));
            contenido.Append("</strong><br />");
            contenido.Append(EncodeHtml(contexto.CargoDestinatario));
            contenido.Append("<br />");
            contenido.Append(EncodeHtml(contexto.Operadora));
            contenido.Append("<br />");
            contenido.Append(EncodeHtml(contexto.LineaDespacho));
            contenido.Append("</p>");

            contenido.Append("<p style='margin:0 0 16px 0;'>De mi consideracion:</p>");
            contenido.Append("<p style='margin:0 0 16px 0;'>");
            contenido.Append(EncodeHtml(ConstruirParrafoPrincipalResultado(contexto)));
            contenido.Append("</p>");

            contenido.Append("<div style='margin:0 0 18px 0; padding:16px 18px; border:1px solid #d7e3ee; border-left:5px solid ");
            contenido.Append(string.Equals(contexto.ResultadoTecnicoFinal, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase) ? "#2f8f46" : "#b56308");
            contenido.Append("; background-color:#f8fbfd; border-radius:6px;'>");
            contenido.Append("<div style='font-size:12px; letter-spacing:0.06em; text-transform:uppercase; color:#667786; margin-bottom:6px;'>Resultado tecnico final</div>");
            contenido.Append("<div style='font-size:22px; font-weight:bold; color:#143b57;'>");
            contenido.Append(EncodeHtml(ObtenerEtiquetaResultado(contexto)));
            contenido.Append("</div></div>");

            if (!string.Equals(contexto.ResultadoTecnicoFinal, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                contenido.Append("<p style='margin:0 0 16px 0;'>");
                contenido.Append(EncodeHtml(ConstruirBloqueResultadoInsatisfactorio(contexto)));
                contenido.Append("</p>");
            }

            AppendSection(contenido, "Detalle de observaciones / No Conformidades", null);
            AppendSection(contenido, "Operaciones", contexto.ObservacionesOperaciones);
            AppendSection(contenido, "Mantenimiento", contexto.ObservacionesMantenimiento);
            AppendSection(contenido, "Hallazgos", contexto.Hallazgos);
            AppendSection(contenido, "Observaciones generales", contexto.ObservacionesGenerales);
            AppendSection(contenido, "Observacion DIRDAC / Direccion - Jefatura", contexto.ObservacionDireccion);
            AppendSection(contenido, "Conclusiones", contexto.Conclusiones);
            AppendSection(contenido, "Recomendaciones", contexto.Recomendaciones);

            if (!string.IsNullOrWhiteSpace(contexto.PlazoSubsanacionTexto))
            {
                AppendSection(
                    contenido,
                    "Plazo para subsanacion",
                    contexto.PlazoSubsanacionTexto + "\nLa documentacion o evidencias deberan remitirse a traves de los canales establecidos por la DGAC dentro del plazo senalado.");
            }

            contenido.Append("<p style='margin:16px 0 0 0;'>Atentamente,</p>");
            contenido.Append("<p style='margin:12px 0 0 0;'><strong>Direccion General de Aviacion Civil</strong><br />DIRDAC / Direccion - Jefatura</p>");
            contenido.Append("</div>");

            var model = new EmailTemplateModel
            {
                Titulo = "Resultado de inspeccion aprobado institucionalmente",
                NombreDestinatario = contexto.NombreDestinatario,
                MostrarSaludo = false,
                MensajePrincipal = null,
                Resumen = resumen,
                ContenidoHtmlExtra = contenido.ToString(),
                EnlaceUrl = SanitizarEnlaceCorreo(enlaceDocumento),
                EnlaceTexto = "Abrir expediente en AOCR",
                TextoCierre = string.Equals(contexto.ResultadoTecnicoFinal, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                    ? "En consecuencia, se continuara con el tramite correspondiente conforme al flujo institucional establecido por la Direccion General de Aviacion Civil."
                    : "La presente comunicacion se emite una vez concluida la decision institucional final del Informe Tecnico dentro del flujo AOCR.",
                Footer = "Este es un mensaje institucional generado por el sistema AOCR - DGAC. Asunto: " + asunto
            };

            return EmailTemplateRenderer.Render(model);
        }

        private static string ConstruirParrafoPrincipalResultado(InformeTecnicoDireccionEmailContext contexto)
        {
            if (string.Equals(contexto.ResultadoTecnicoFinal, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "En referencia a la inspeccion realizada a su representada en la estacion de {0} el {1}, con el fin de verificar el cumplimiento de los requisitos tecnicos y operacionales aplicables dentro del proceso AOCR, comunico a usted que, una vez revisado y aprobado el Informe Tecnico por parte de DIRDAC / Direccion - Jefatura, el resultado de la inspeccion es Satisfactorio.",
                    contexto.EstacionInspeccionada,
                    contexto.FechaInspeccionTexto);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "En referencia a la inspeccion realizada a su representada en la estacion de {0} el {1}, con el fin de verificar el cumplimiento de los requisitos tecnicos y operacionales aplicables dentro del proceso AOCR, comunico a usted que, una vez revisado y aprobado el Informe Tecnico por parte de DIRDAC / Direccion - Jefatura, se detectaron observaciones y/o No Conformidades que deben ser atendidas por su representada.",
                contexto.EstacionInspeccionada,
                contexto.FechaInspeccionTexto);
        }

        private static string ConstruirBloqueResultadoInsatisfactorio(InformeTecnicoDireccionEmailContext contexto)
        {
            if (string.Equals(contexto.TipoResultadoInsatisfactorio, "CON_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(contexto.PlazoSubsanacionTexto)
                    ? "Por todo lo indicado, el resultado de la inspeccion es Insatisfactorio con inspeccion, por lo que su representada debera solicitar una nueva inspeccion una vez que haya implementado las acciones correctivas correspondientes, con el fin de demostrar ante esta Autoridad que las No Conformidades reportadas han sido solventadas. Mientras no se evidencie el cumplimiento de las acciones requeridas, el tramite correspondiente se mantendra pendiente conforme a las disposiciones institucionales aplicables."
                    : "Por todo lo indicado, el resultado de la inspeccion es Insatisfactorio con inspeccion, por lo que su representada debera solicitar una nueva inspeccion una vez que haya implementado las acciones correctivas correspondientes, con el fin de demostrar ante esta Autoridad que las No Conformidades reportadas han sido solventadas. Mientras no se evidencie el cumplimiento de las acciones requeridas, el tramite correspondiente se mantendra pendiente conforme a las disposiciones institucionales aplicables. Se concede a su representada plazo hasta el " + contexto.PlazoSubsanacionTexto + ", para solicitar una nueva inspeccion y presentar las evidencias de cumplimiento correspondientes.";
            }

            if (string.Equals(contexto.TipoResultadoInsatisfactorio, "SIN_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(contexto.PlazoSubsanacionTexto)
                    ? "Por todo lo indicado, el resultado de la inspeccion es Insatisfactorio sin inspeccion, por lo que su representada debera atender las observaciones senaladas y remitir las evidencias documentales correspondientes dentro del plazo establecido por esta Autoridad. En este caso, no se requiere una nueva inspeccion presencial, salvo que del analisis posterior de las evidencias presentadas se determine lo contrario."
                    : "Por todo lo indicado, el resultado de la inspeccion es Insatisfactorio sin inspeccion, por lo que su representada debera atender las observaciones senaladas y remitir las evidencias documentales correspondientes dentro del plazo establecido por esta Autoridad. En este caso, no se requiere una nueva inspeccion presencial, salvo que del analisis posterior de las evidencias presentadas se determine lo contrario. Se concede a su representada plazo hasta el " + contexto.PlazoSubsanacionTexto + ", para remitir la documentacion y evidencias que permitan verificar la subsanacion de las observaciones reportadas.";
            }

            return "El resultado de la inspeccion es Insatisfactorio y requiere la atencion de las observaciones registradas en el Informe Tecnico conforme al flujo institucional AOCR.";
        }

        private string ConstruirDetalleHallazgos(Inspeccion inspeccion, InspeccionInformeTecnico informe, IList<string> camposFaltantes)
        {
            var detalleInforme = CompactarTexto(informe.NoConformidades);
            if (!string.IsNullOrWhiteSpace(detalleInforme))
            {
                return detalleInforme;
            }

            try
            {
                var hallazgos = new HallazgoDAO().ObtenerPorInspeccion(inspeccion.CodigoInspeccion) ?? new List<Hallazgo>();
                var lineas = hallazgos
                    .Where(h => h != null && !string.IsNullOrWhiteSpace(h.Descripcion))
                    .Take(10)
                    .Select(h => "- " + h.Descripcion.Trim())
                    .ToList();

                if (lineas.Count > 0)
                {
                    return string.Join("\n", lineas);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.ConstruirDetalleHallazgos: " + ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(inspeccion.HallazgosPrincipales))
            {
                return inspeccion.HallazgosPrincipales.Trim();
            }

            camposFaltantes.Add("Hallazgos");
            return "No se registran hallazgos o no conformidades adicionales.";
        }

        private string ResolverObservacionesPorCategoria(
            IList<string> camposFaltantes,
            string nombreCampo,
            IEnumerable<string> palabrasClave,
            params string[] candidatos)
        {
            var keywords = (palabrasClave ?? Enumerable.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim().ToUpperInvariant())
                .ToList();

            foreach (var candidato in candidatos.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var lineasCoincidentes = candidato
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .Where(x => keywords.Count == 0 || keywords.Any(k => x.ToUpperInvariant().Contains(k)))
                    .ToList();

                if (lineasCoincidentes.Count > 0)
                {
                    return string.Join("\n", lineasCoincidentes);
                }
            }

            camposFaltantes.Add(nombreCampo);
            return string.Equals(nombreCampo, "ObservacionesOperaciones", StringComparison.OrdinalIgnoreCase)
                ? "No se registran observaciones especificas de operaciones."
                : "No se registran observaciones especificas de mantenimiento.";
        }

        private static string ExtraerPlazoSubsanacion(params string[] textos)
        {
            foreach (var texto in textos.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var valor = texto.Trim();
                var upper = valor.ToUpperInvariant();
                if (upper.IndexOf("PLAZO", StringComparison.OrdinalIgnoreCase) < 0
                    && upper.IndexOf("HASTA", StringComparison.OrdinalIgnoreCase) < 0
                    && upper.IndexOf("SUBSAN", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var match = Regex.Match(valor, @"\b\d{1,2}/\d{1,2}/\d{4}\b|\b\d{4}-\d{2}-\d{2}\b");
                if (!match.Success)
                {
                    continue;
                }

                DateTime fecha;
                if (DateTime.TryParse(match.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
                    || DateTime.TryParse(match.Value, CultureInfo.GetCultureInfo("es-EC"), DateTimeStyles.None, out fecha))
                {
                    return fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                }

                return match.Value;
            }

            return string.Empty;
        }

        private static string ConstruirEventKeyBaseResultadoInformeDireccion(int codigoInforme)
        {
            return EventKeyResultadoInformeTecnicoDireccionPrefix + codigoInforme.ToString(CultureInfo.InvariantCulture) + "_";
        }

        private static bool InformeTieneDecisionInstitucionalFinal(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return false;
            }

            var estado = FirstNonEmpty(informe.EstadoInforme).ToUpperInvariant();
            return informe.FirmadoDirdac
                || string.Equals(estado, "APROBADO_DIRECCION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, "FIRMADO_DIRECCION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, "FIRMADO_FINAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, "APROBADO_DIRDAC", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizarResultadoTecnico(string resultado)
        {
            var valor = FirstNonEmpty(resultado).ToUpperInvariant();
            if (string.Equals(valor, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                return "SATISFACTORIO";
            }

            if (string.Equals(valor, "INSATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                return "INSATISFACTORIO";
            }

            return string.Empty;
        }

        private static string NormalizarTipoResultadoInsatisfactorio(string tipoResultado)
        {
            var valor = FirstNonEmpty(tipoResultado).ToUpperInvariant();
            if (string.Equals(valor, "CON_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "CON_INSPECCION";
            }

            if (string.Equals(valor, "SIN_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "SIN_INSPECCION";
            }

            return string.Empty;
        }

        private static string ObtenerEtiquetaResultado(InformeTecnicoDireccionEmailContext contexto)
        {
            if (string.Equals(contexto.ResultadoTecnicoFinal, "SATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                return "Satisfactorio";
            }

            if (string.Equals(contexto.TipoResultadoInsatisfactorio, "CON_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "Insatisfactorio con inspeccion";
            }

            if (string.Equals(contexto.TipoResultadoInsatisfactorio, "SIN_INSPECCION", StringComparison.OrdinalIgnoreCase))
            {
                return "Insatisfactorio sin inspeccion";
            }

            return "Insatisfactorio";
        }

        private static string DeterminarCargoDestinatario(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico) || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                return "Representante Tecnico";
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal))
            {
                return "Representante Legal";
            }

            return string.Empty;
        }

        private static string FormatearFecha(DateTime? fecha)
        {
            return fecha.HasValue
                ? fecha.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static void RegistrarFaltanteSiCorresponde(ICollection<string> camposFaltantes, string valor, string nombreCampo)
        {
            if (camposFaltantes == null || !string.IsNullOrWhiteSpace(valor))
            {
                return;
            }

            camposFaltantes.Add(nombreCampo);
        }

        private static string CompactarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var lineas = texto
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            return lineas.Count == 0 ? string.Empty : string.Join("\n", lineas);
        }

        private static string SanitizarEnlaceCorreo(string enlaceDocumento)
        {
            if (string.IsNullOrWhiteSpace(enlaceDocumento))
            {
                return string.Empty;
            }

            Uri uri;
            if (!Uri.TryCreate(enlaceDocumento, UriKind.Absolute, out uri))
            {
                return string.Empty;
            }

            return uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : uri.ToString();
        }

        private static void AppendSection(StringBuilder contenido, string titulo, string valor)
        {
            contenido.Append("<div style='margin:0 0 14px 0;'>");
            contenido.Append("<div style='font-size:13px; font-weight:bold; color:#143b57; margin:0 0 4px 0;'>");
            contenido.Append(EncodeHtml(titulo));
            contenido.Append("</div>");
            if (!string.IsNullOrWhiteSpace(valor))
            {
                contenido.Append("<div style='white-space:pre-line; color:#3a4f5e;'>");
                contenido.Append(EncodeHtml(valor));
                contenido.Append("</div>");
            }
            contenido.Append("</div>");
        }

        private static string EncodeHtml(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static PlantillaCorreoInspeccion ConstruirPlantilla(Inspeccion inspeccion, SolicitudAOCR solicitud, string evento, string observacion)
        {
            var eventoNormalizado = (evento ?? string.Empty).Trim().ToUpperInvariant();
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            switch (eventoNormalizado)
            {
                case "NC_GENERADAS":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - No conformidades registradas en inspeccion #" + inspeccion.CodigoInspeccion,
                        Titulo = "No conformidades registradas",
                        Mensaje = "Se registraron no conformidades que requieren validacion de coordinacion y subsanacion del RT.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };
                case "DOCUMENTOS_SUBSANADOS":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Documentacion subsanada en inspeccion #" + inspeccion.CodigoInspeccion,
                        Titulo = "Documentacion subsanada",
                        Mensaje = "El RT actualizo documentos asociados a no conformidades y el expediente requiere revalidacion.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };
                case "DEVOLUCION_INSPECCION":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Tramite de inspeccion devuelto #" + inspeccion.CodigoInspeccion,
                        Titulo = "Tramite devuelto para correccion",
                        Mensaje = "La inspeccion fue devuelta para correccion o para programar una nueva inspeccion, segun observaciones registradas.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };
                case "APROBACION_INSPECCION":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Inspeccion aprobada #" + inspeccion.CodigoInspeccion,
                        Titulo = "Inspeccion aprobada",
                        Mensaje = "La inspeccion fue aprobada y el expediente queda listo para el siguiente tramo institucional.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionLegal,
                            NotificacionDestinatarioPolicyService.GrupoDireccionFinal
                        }
                    };
                case "REVALIDACION_OK":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Revalidacion satisfactoria #" + inspeccion.CodigoInspeccion,
                        Titulo = "Revalidacion satisfactoria",
                        Mensaje = "La revalidacion de la inspeccion fue satisfactoria y el tramite puede continuar.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante
                        }
                    };
                case "REVALIDACION_RECHAZADA":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Revalidacion con observaciones #" + inspeccion.CodigoInspeccion,
                        Titulo = "Revalidacion con observaciones",
                        Mensaje = "La revalidacion mantiene observaciones pendientes y se requiere una nueva subsanacion o ajuste del tramite.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante
                        }
                    };
                case "PENDIENTE_FIRMA_DIRDAC":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Documentos pendientes de revision DIRDAC / Direccion - Jefatura " + numeroSolicitud,
                        Titulo = "Documentos pendientes de revision institucional",
                        Mensaje = "Tiene documentos pendientes de revision institucional en la bandeja de DIRDAC / Direccion - Jefatura. "
                            + "El informe tecnico ya fue firmado por el inspector asignado y queda listo para decision final. "
                            + "La notificacion al RT se emitira una vez que Direccion / Jefatura registre la decision institucional correspondiente.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoDireccionJefaturaRevisionInforme
                        }
                    };
                case "INFORME_TECNICO_FIRMADO":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Informe técnico aprobado " + numeroSolicitud,
                        Titulo = "Informe técnico aprobado",
                        Mensaje = "El informe técnico ya cuenta con la aprobación institucional requerida y queda habilitado para el expediente AOCR.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado
                        }
                    };
                default:
                    return null;
            }
        }

        private static string ConstruirCuerpoHtml(string nombreDestino, PlantillaCorreoInspeccion plantilla, Inspeccion inspeccion, SolicitudAOCR solicitud, string observacion)
        {
            return ConstruirCuerpoHtmlConEnlace(plantilla, inspeccion, solicitud, observacion, null, nombreDestino);
        }

        private static string ConstruirCuerpoHtmlConEnlace(PlantillaCorreoInspeccion plantilla, Inspeccion inspeccion, SolicitudAOCR solicitud, string observacion, string enlaceDocumento, string nombreDestino)
        {
            var operador = string.IsNullOrWhiteSpace(solicitud.NombreOperador) ? (solicitud.RazonSocial ?? "Operador") : solicitud.NombreOperador;
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);

            var model = new EmailTemplateModel
            {
                Titulo = plantilla.Titulo,
                NombreDestinatario = string.IsNullOrWhiteSpace(nombreDestino) ? "@@DESTINATARIO@@" : nombreDestino,
                MensajePrincipal = plantilla.Mensaje,
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Inspeccion", "#" + inspeccion.CodigoInspeccion),
                    new EmailFieldItem("Solicitud AOCR", numeroSolicitud),
                    new EmailFieldItem("Operador", operador),
                    new EmailFieldItem("Estado actual", inspeccion.Estado ?? "PENDIENTE")
                },
                Observaciones = observacion,
                EnlaceUrl = enlaceDocumento,
                EnlaceTexto = "Abrir expediente",
                TextoCierre = "Puede revisar el expediente desde el sistema AOCR en el detalle de inspeccion correspondiente.",
                Footer = "Este es un mensaje automatico del workflow de inspeccion AOCR."
            };

            return EmailTemplateRenderer.Render(model);
        }

        private static string ObtenerNumeroSolicitudVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "N/D";
            }

            return string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? "DGAC-GOP-2026-AOCR" + solicitud.CodigoSolicitud
                : solicitud.NumeroSolicitud.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private sealed class InformeTecnicoDireccionEmailContext
        {
            public InformeTecnicoDireccionEmailContext()
            {
                CamposFaltantes = new List<string>();
            }

            public string MensajeError { get; set; }
            public List<string> CamposFaltantes { get; private set; }
            public string CorreoDestinatario { get; set; }
            public string NombreDestinatario { get; set; }
            public string CargoDestinatario { get; set; }
            public string LineaDespacho { get; set; }
            public string NumeroSolicitud { get; set; }
            public string CodigoInspeccionTexto { get; set; }
            public string Operadora { get; set; }
            public string TipoTramite { get; set; }
            public string EstacionInspeccionada { get; set; }
            public string Ciudad { get; set; }
            public string FechaInspeccionTexto { get; set; }
            public string FechaAprobacionInstitucionalTexto { get; set; }
            public string ResultadoTecnicoFinal { get; set; }
            public string TipoResultadoInsatisfactorio { get; set; }
            public string Hallazgos { get; set; }
            public string NoConformidades { get; set; }
            public string ObservacionesGenerales { get; set; }
            public string ObservacionesOperaciones { get; set; }
            public string ObservacionesMantenimiento { get; set; }
            public string Conclusiones { get; set; }
            public string Recomendaciones { get; set; }
            public string ObservacionDireccion { get; set; }
            public string PlazoSubsanacionTexto { get; set; }
        }

        private sealed class PlantillaCorreoInspeccion
        {
            public string Asunto { get; set; }
            public string Titulo { get; set; }
            public string Mensaje { get; set; }
            public string[] GruposDestinatarios { get; set; }
        }
    }
}
