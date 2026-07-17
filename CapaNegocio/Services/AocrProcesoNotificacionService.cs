using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public sealed class AocrProcesoNotificacionService
    {
        private const string TipoReconocimiento = "RECONOCIMIENTO";
        private const string TipoCondiciones = "CONDICIONES_LIMITACIONES";
        private const string EventoFinalRt = "PROCESO_AOCR_FINALIZADO_RT";

        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AocrFirmaDocumentoDAO _firmaDocumentoDao;
        private readonly AocrDocumentoGeneradoDAO _documentoGeneradoDao;
        private readonly IEmailQueueService _emailQueue;
        private readonly ILoggingService _logger;

        public AocrProcesoNotificacionService()
            : this(new SolicitudAOCRDAO(), new AocrFirmaDocumentoDAO(), new AocrDocumentoGeneradoDAO(), new EmailQueueService())
        {
        }

        public AocrProcesoNotificacionService(
            SolicitudAOCRDAO solicitudDao,
            AocrFirmaDocumentoDAO firmaDocumentoDao,
            AocrDocumentoGeneradoDAO documentoGeneradoDao,
            IEmailQueueService emailQueue)
        {
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _firmaDocumentoDao = firmaDocumentoDao ?? new AocrFirmaDocumentoDAO();
            _documentoGeneradoDao = documentoGeneradoDao ?? new AocrDocumentoGeneradoDAO();
            _emailQueue = emailQueue ?? new EmailQueueService();
            _logger = LoggingServiceFactory.Create();
        }

        public void NotificarAocrFirmado(int solicitudId)
        {
            NotificarEventoSimple(solicitudId, "AOCR_FIRMADO", "Sistema AOCR - Documento AOCR firmado",
                "El documento RECONOCIMIENTO DE CERTIFICADO DE EXPLOTADOR DE SERVICIOS AEREOS fue firmado correctamente.");
            RegistrarEventoGate8("DOCUMENTOS_FIRMADOS", solicitudId, "RECONOCIMIENTO");
        }

        public void NotificarCondicionesFirmadas(int solicitudId)
        {
            NotificarEventoSimple(solicitudId, "CONDICIONES_FIRMADAS", "Sistema AOCR - Documento Condiciones y Limitaciones firmado",
                "El documento CONDICIONES Y LIMITACIONES fue firmado correctamente.");
            RegistrarEventoGate8("DOCUMENTOS_FIRMADOS", solicitudId, "CONDICIONES_LIMITACIONES");
        }

        public bool NotificarProcesoAocrFinalizado(int solicitudId)
        {
            Trace.TraceInformation("[NOTIF_AOCR][EVENT_IN] SolicitudId=" + solicitudId + "; TipoEvento=" + EventoFinalRt + ";");
            Trace.TraceInformation("[AOCR_FINAL][VALIDAR_DOCS_IN] SolicitudId=" + solicitudId + ";");

            try
            {
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                if (solicitud == null)
                {
                    LogError(solicitudId, EventoFinalRt, string.Empty, "Solicitud no encontrada.");
                    return false;
                }

                var planCierre = new AocrCierrePorTipoTramiteService().Resolver(solicitud);
                if (!planCierre.EsValido)
                {
                    LogError(solicitudId, EventoFinalRt, string.Empty, planCierre.Motivo);
                    return false;
                }

                var rt = ResolverRt(solicitud);
                if (rt == null || !EsCorreoPermitido(rt.Email))
                {
                    var motivo = "No se pudo resolver correo real del RT.";
                    LogError(solicitudId, EventoFinalRt, rt != null ? rt.Email : string.Empty, motivo);
                    NotificarInternoSeguro(solicitud.CodigoUsuario, "Entrega final AOCR pendiente", motivo, solicitudId);
                    return false;
                }



                Trace.TraceInformation("[NOTIF_AOCR][DESTINATARIO_RESUELTO] SolicitudId=" + solicitudId + "; TipoEvento=" + EventoFinalRt + "; Email=" + rt.Email + "; UsuarioId=" + solicitud.CodigoUsuario + ";");

                var docAocr = ResolverDocumentoFirmado(solicitudId, TipoReconocimiento);
                var docCondiciones = ResolverDocumentoFirmado(solicitudId, TipoCondiciones);

                if ((planCierre.GenerarAocr && !docAocr.EsValido) || !docCondiciones.EsValido)
                {
                    var motivo = "Documentos finales incompletos. AOCR=" + docAocr.Motivo + "; Condiciones=" + docCondiciones.Motivo;
                    LogError(solicitudId, EventoFinalRt, rt.Email, motivo);
                    NotificarInternoSeguro(solicitud.CodigoUsuario, "Entrega final AOCR pendiente", motivo, solicitudId);
                    return false;
                }

                Trace.TraceInformation("[AOCR_FINAL][DOC_AOCR_OK] SolicitudId=" + solicitudId + "; Ruta=" + docAocr.RutaPersistida + "; Bytes=" + docAocr.Bytes + ";");
                Trace.TraceInformation("[AOCR_FINAL][DOC_CONDICIONES_OK] SolicitudId=" + solicitudId + "; Ruta=" + docCondiciones.RutaPersistida + "; Bytes=" + docCondiciones.Bytes + ";");
                Trace.TraceInformation("[AOCR_FINAL][DOCS_COMPLETOS] SolicitudId=" + solicitudId + ";");

                if (planCierre.GenerarAocr)
                {
                    LiberarDocumentoRt(solicitudId, TipoReconocimiento, docAocr);
                }
                LiberarDocumentoRt(solicitudId, TipoCondiciones, docCondiciones);

                var vAocr = docAocr.EsValido ? docAocr.Bytes.ToString() : "0"; // Idealmente seria version, pero usamos bytes como fallback
                var vCond = docCondiciones.EsValido ? docCondiciones.Bytes.ToString() : "0";
                var eventKey = $"DOCUMENTOS_FINALES_RT:{solicitudId}:{vAocr}:{vCond}:{rt.Email}";
                
                if (_emailQueue.ExisteNotificacionAsync(EventoFinalRt, eventKey, solicitudId).GetAwaiter().GetResult())
                {
                    Trace.TraceInformation("[NOTIF_AOCR][SKIP_DUPLICADO] SolicitudId=" + solicitudId + "; TipoEvento=" + EventoFinalRt + "; Email=" + rt.Email + ";");
                    return true;
                }

                var adjuntos = new List<EmailAttachmentItem>();
                if (planCierre.GenerarAocr)
                {
                    adjuntos.Add(new EmailAttachmentItem
                    {
                        FileName = NombreAdjunto(solicitud, "RECONOCIMIENTO_CERTIFICADO_EXPLOTADOR.pdf"),
                        ContentType = "application/pdf",
                        FilePath = docAocr.RutaPersistida,
                        FileSize = docAocr.Bytes
                    });
                }
                adjuntos.Add(new EmailAttachmentItem
                {
                    FileName = NombreAdjunto(solicitud, "CONDICIONES_Y_LIMITACIONES.pdf"),
                    ContentType = "application/pdf",
                    FilePath = docCondiciones.RutaPersistida,
                    FileSize = docCondiciones.Bytes
                });

                var asunto = $"AOCR y Condiciones y Limitaciones firmadas – Solicitud {NumeroSolicitud(solicitud)}";

                var item = new EmailQueueItem
                {
                    Para = rt.Email,
                    ParaNombre = rt.Nombre,
                    Asunto = asunto,
                    Cuerpo = ConstruirCuerpoFinal(solicitud, rt.Nombre, planCierre),
                    Estado = EstadoEmail.Pendiente,
                    SolicitudId = solicitudId,
                    EventKey = eventKey,
                    TipoNotificacion = EventoFinalRt,
                    CorrelationId = "AOCRFINAL-" + solicitudId,
                    EsHtml = true,
                    MaxIntentos = 5,
                    Adjuntos = adjuntos
                };

                _emailQueue.EncolarConAdjuntosAsync(item, item.Adjuntos).GetAwaiter().GetResult();
                Trace.TraceInformation("[NOTIF_AOCR][QUEUE_OK] SolicitudId=" + solicitudId + "; TipoEvento=" + EventoFinalRt + "; Email=" + rt.Email + ";");
                Trace.TraceInformation("[AOCR_FINAL][EMAIL_RT_ADJUNTOS_OK] SolicitudId=" + solicitudId + "; EmailRT=" + rt.Email + "; Adjuntos=2;");

                NotificarInternoSeguro(solicitud.CodigoUsuario, "Proceso AOCR finalizado",
                    "Los documentos finales firmados se encuentran disponibles para descarga.", solicitudId);
                NotificarInspectorFinal(solicitud, planCierre);
                RegistrarEventoGate8("DOCUMENTOS_LIBERADOS_RT", solicitudId, planCierre.Modulo);
                Trace.TraceInformation("[AOCR_FINAL][BANDEJA_RT_OK] SolicitudId=" + solicitudId + ";");
                return true;
            }
            catch (Exception ex)
            {
                LogError(solicitudId, EventoFinalRt, string.Empty, ex.Message);
                _logger.LogWarning("AocrProcesoNotificacionService.NotificarProcesoAocrFinalizado: " + ex.Message);
                return false;
            }
        }

        private void NotificarEventoSimple(int solicitudId, string tipoEvento, string asunto, string mensaje)
        {
            Trace.TraceInformation("[NOTIF_AOCR][EVENT_IN] SolicitudId=" + solicitudId + "; TipoEvento=" + tipoEvento + ";");

            try
            {
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                if (solicitud == null)
                {
                    return;
                }

                var destinatarios = ResolverDestinatariosInstitucionales(solicitud)
                    .Where(d => EsCorreoPermitido(d.Email))
                    .GroupBy(d => d.Email.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                foreach (var destinatario in destinatarios)
                {
                    var eventKey = SolicitudAocrCorreoService.BuildAocrEventKey(tipoEvento, solicitudId, null, null, destinatario.Email);
                    if (_emailQueue.ExisteNotificacionAsync(tipoEvento, eventKey, solicitudId).GetAwaiter().GetResult())
                    {
                        Trace.TraceInformation("[NOTIF_AOCR][SKIP_DUPLICADO] SolicitudId=" + solicitudId + "; TipoEvento=" + tipoEvento + "; Email=" + destinatario.Email + ";");
                        continue;
                    }

                    _emailQueue.EncolarAsync(new EmailQueueItem
                    {
                        Para = destinatario.Email,
                        ParaNombre = destinatario.Nombre,
                        Asunto = asunto,
                        Cuerpo = ConstruirCuerpoSimple(solicitud, destinatario.Nombre, mensaje),
                        Estado = EstadoEmail.Pendiente,
                        SolicitudId = solicitudId,
                        TipoNotificacion = tipoEvento,
                        EventKey = eventKey,
                        EsHtml = true
                    }).GetAwaiter().GetResult();

                    Trace.TraceInformation("[NOTIF_AOCR][QUEUE_OK] SolicitudId=" + solicitudId + "; TipoEvento=" + tipoEvento + "; Email=" + destinatario.Email + ";");
                }
            }
            catch (Exception ex)
            {
                LogError(solicitudId, tipoEvento, string.Empty, ex.Message);
            }
        }

        private DocumentoFinalInfo ResolverDocumentoFirmado(int solicitudId, string tipo)
        {
            var firma = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, tipo);
            if (firma == null && string.Equals(tipo, TipoCondiciones, StringComparison.OrdinalIgnoreCase))
            {
                firma = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES");
            }

            if (firma == null || string.IsNullOrWhiteSpace(firma.RutaDocumento))
            {
                return DocumentoFinalInfo.Invalido("sin firma registrada");
            }

            var rutaFisica = ResolverRutaFisica(firma.RutaDocumento);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !File.Exists(rutaFisica))
            {
                return DocumentoFinalInfo.Invalido("archivo no existe: " + (firma.RutaDocumento ?? string.Empty));
            }

            var bytes = new FileInfo(rutaFisica).Length;
            if (bytes <= 0)
            {
                return DocumentoFinalInfo.Invalido("archivo vacio");
            }

            if (string.IsNullOrWhiteSpace(firma.HashDocumento))
            {
                return DocumentoFinalInfo.Invalido("hash no registrado");
            }

            string hashReal;
            using (var stream = File.OpenRead(rutaFisica))
            using (var sha = SHA256.Create())
            {
                hashReal = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
            if (!string.Equals(hashReal, firma.HashDocumento.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return DocumentoFinalInfo.Invalido("hash no coincide");
            }

            return new DocumentoFinalInfo
            {
                EsValido = true,
                RutaPersistida = firma.RutaDocumento,
                RutaFisica = rutaFisica,
                Bytes = bytes,
                Hash = firma.HashDocumento,
                Firma = firma
            };
        }

        private void LiberarDocumentoRt(int solicitudId, string tipoDocumento, DocumentoFinalInfo doc)
        {
            if (doc == null || !doc.EsValido)
            {
                return;
            }

            _documentoGeneradoDao.MarcarLiberadoRt(
                solicitudId,
                tipoDocumento,
                doc.RutaPersistida,
                doc.Hash,
                doc.Bytes,
                doc.Firma != null ? doc.Firma.CodigoUsuario : null,
                doc.Firma != null ? doc.Firma.NombreArchivo : null);
        }

        private NotificacionDestinatario ResolverRt(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            var usuario = solicitud.CodigoUsuario > 0 ? UsuarioDAO.ObtenerPorId(solicitud.CodigoUsuario) : null;
            var email = FirstNonEmpty(solicitud.CorreoRepresentanteTecnico, solicitud.Email, usuario != null ? usuario.Email : null);
            var nombre = FirstNonEmpty(usuario != null ? usuario.NombreCompleto : null, usuario != null ? usuario.NombreUsuario : null, solicitud.RepresentanteLegal, solicitud.NombreOperador, "Representante Tecnico");

            return new NotificacionDestinatario
            {
                Email = email,
                Nombre = nombre
            };
        }

        private List<NotificacionDestinatario> ResolverDestinatariosInstitucionales(SolicitudAOCR solicitud)
        {
            var destinatarios = new List<NotificacionDestinatario>();
            AgregarUsuariosPorRol(destinatarios, "DirectorCertificacionesDcav");
            AgregarUsuariosPorRol(destinatarios, "Coordinador");
            AgregarUsuariosPorRol(destinatarios, "Direccion");
            AgregarUsuariosPorRol(destinatarios, "DIRDAC");
            AgregarUsuariosPorRol(destinatarios, "Dirección / Jefatura técnica");
            return destinatarios;
        }

        private static void AgregarUsuariosPorRol(List<NotificacionDestinatario> destinatarios, string rol)
        {
            try
            {
                foreach (var usuario in UsuarioDAO.ListarPorRol(rol) ?? new List<Usuario>())
                {
                    destinatarios.Add(new NotificacionDestinatario
                    {
                        Email = usuario.Email,
                        Nombre = FirstNonEmpty(usuario.NombreCompleto, usuario.NombreUsuario, "Usuario AOCR")
                    });
                }
            }
            catch
            {
            }
        }

        private void NotificarInspectorFinal(SolicitudAOCR solicitud, AocrCierrePorTipoTramitePlan plan)
        {
            try
            {
                var inspeccion = (new InspeccionDAO().ListarPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>())
                    .Where(i => i != null && i.CodigoInspector.HasValue).OrderByDescending(i => i.CodigoInspeccion).FirstOrDefault();
                var inspectorId = inspeccion != null ? inspeccion.CodigoInspector.GetValueOrDefault() : solicitud.CodigoTecnico.GetValueOrDefault();
                if (inspectorId <= 0) return;
                NotificarInternoSeguro(inspectorId, "Cierre " + plan.Modulo,
                    plan.TipoCierre == AocrTipoCierre.Modificacion
                        ? "Las Condiciones y Limitaciones modificadas fueron firmadas y liberadas al RT."
                        : "El AOCR y las Condiciones y Limitaciones fueron firmados y liberados al RT.",
                    solicitud.CodigoSolicitud);
            }
            catch { }
        }

        private static void RegistrarEventoGate8(string evento, int solicitudId, string versionDocumento)
        {
            try
            {
                var correlationId = HttpContext.Current != null ? HttpContext.Current.Items["CorrelationId"] as string : null;
                correlationId = Gate8Eventos.Correlation(correlationId, solicitudId);
                var key = Gate8Eventos.Key(evento, solicitudId, versionDocumento);
                new Gate8WorkflowEventService().PublicarPostCommit(new Gate8EventoRequest
                {
                    Registro = new Gate8EventoRegistro { Evento = evento, EventKey = key, CorrelationId = correlationId,
                        Modulo = "GATE_8", Accion = evento, Entidad = "aocr_tbsolicitud", EntidadId = solicitudId,
                        SolicitudId = solicitudId, Observacion = versionDocumento, Resultado = "REGISTRADO" }
                });
            }
            catch { /* La notificacion y auditoria nunca revierten el cierre principal. */ }
        }

        private static string ConstruirCuerpoFinal(SolicitudAOCR solicitud, string nombre, AocrCierrePorTipoTramitePlan plan)
        {
            var sb = new StringBuilder();
            sb.Append("<p>Se informa que el proceso AOCR correspondiente a la Solicitud <strong>")
              .Append(HttpUtility.HtmlEncode(NumeroSolicitud(solicitud)))
              .Append("</strong> ha finalizado correctamente.</p>");
            sb.Append("<p>Se adjuntan los documentos finales firmados:</p>");
            sb.Append("<ul>");
            if (plan != null && plan.GenerarAocr)
                sb.Append("<li>RECONOCIMIENTO DE CERTIFICADO DE EXPLOTADOR DE SERVICIOS AEREOS</li>");
            sb.Append("<li>CONDICIONES Y LIMITACIONES</li>");
            sb.Append("</ul>");
            sb.Append("<p>Los documentos tambien se encuentran disponibles para descarga en su bandeja del Sistema AOCR.</p>");
            return EmailTemplateRenderer.EnsureStandardLayout("Sistema AOCR - Proceso AOCR finalizado", sb.ToString(), nombre, "Este es un mensaje automatico del workflow AOCR.");
        }

        private static string ConstruirCuerpoSimple(SolicitudAOCR solicitud, string nombre, string mensaje)
        {
            var sb = new StringBuilder();
            sb.Append("<p>").Append(HttpUtility.HtmlEncode(mensaje ?? string.Empty)).Append("</p>");
            sb.Append("<table style=\"border-collapse:collapse;margin-top:12px;\">");
            sb.Append("<tr><td style=\"padding:6px 12px;border:1px solid #d8e2ea;font-weight:bold;\">Solicitud</td><td style=\"padding:6px 12px;border:1px solid #d8e2ea;\">")
              .Append(HttpUtility.HtmlEncode(NumeroSolicitud(solicitud))).Append("</td></tr>");
            sb.Append("<tr><td style=\"padding:6px 12px;border:1px solid #d8e2ea;font-weight:bold;\">Operadora</td><td style=\"padding:6px 12px;border:1px solid #d8e2ea;\">")
              .Append(HttpUtility.HtmlEncode(FirstNonEmpty(solicitud.NombreOperador, solicitud.RazonSocial, "No registrada"))).Append("</td></tr>");
            sb.Append("</table>");
            return EmailTemplateRenderer.EnsureStandardLayout("Sistema AOCR", sb.ToString(), nombre, "Este es un mensaje automatico del workflow AOCR.");
        }

        private static bool EsCorreoPermitido(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var correo = email.Trim();
            if (correo.EndsWith("@invalid.local", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (correo.Equals("financiero.aocr@aviacioncivil.gob.ec", StringComparison.OrdinalIgnoreCase)
                || correo.Equals("coordinador.aocr@aviacioncivil.gob.ec", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var addr = new MailAddress(correo);
                return string.Equals(addr.Address, correo, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolverRutaFisica(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(ruta))
                {
                    return Path.GetFullPath(ruta);
                }

                if (ruta.StartsWith("~"))
                {
                    var mapped = HostingEnvironment.MapPath(ruta);
                    if (!string.IsNullOrWhiteSpace(mapped))
                    {
                        return Path.GetFullPath(mapped);
                    }
                }

                var cleaned = ruta.TrimStart('~', '/', '\\').Replace('/', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cleaned));
            }
            catch
            {
                return null;
            }
        }

        private static string NombreAdjunto(SolicitudAOCR solicitud, string suffix)
        {
            var numero = NumeroSolicitud(solicitud)
                .Replace("/", "-")
                .Replace("\\", "-")
                .Replace(":", "-");
            return numero + "-" + suffix;
        }

        private static string NumeroSolicitud(SolicitudAOCR solicitud)
        {
            return FirstNonEmpty(solicitud != null ? solicitud.NumeroSolicitud : null, solicitud != null ? "Solicitud " + solicitud.CodigoSolicitud : "Solicitud AOCR");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return (values ?? new string[0]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
        }

        private static void NotificarInternoSeguro(int codigoUsuario, string titulo, string mensaje, int solicitudId)
        {
            try
            {
                if (codigoUsuario > 0)
                {
                    NotificacionBL.EnviarNotificacion(codigoUsuario, titulo, mensaje, "AOCR_FINAL", "/SolicitudAOCR/Detalle/" + solicitudId, "SolicitudAOCR", solicitudId, "SolicitudAOCR");
                }
            }
            catch
            {
            }
        }

        private static void LogError(int solicitudId, string tipoEvento, string email, string error)
        {
            Trace.TraceError("[NOTIF_AOCR][SEND_ERROR] SolicitudId=" + solicitudId + "; TipoEvento=" + tipoEvento + "; Email=" + (email ?? string.Empty) + "; Error=" + (error ?? string.Empty) + ";");
        }

        private sealed class DocumentoFinalInfo
        {
            public bool EsValido { get; set; }
            public string Motivo { get; set; }
            public string RutaPersistida { get; set; }
            public string RutaFisica { get; set; }
            public long Bytes { get; set; }
            public string Hash { get; set; }
            public AocrFirmaDocumento Firma { get; set; }

            public static DocumentoFinalInfo Invalido(string motivo)
            {
                return new DocumentoFinalInfo
                {
                    EsValido = false,
                    Motivo = motivo ?? "invalido"
                };
            }
        }
    }
}
