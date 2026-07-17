using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using CapaDatos.Constants;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public sealed class DocumentoSubsanacionClasificacion
    {
        public IList<Documento> DocumentosDevueltos { get; set; } = new List<Documento>();
        public IList<Documento> DocumentosBloqueados { get; set; } = new List<Documento>();
    }

    public sealed class DocumentoSubsanacionValidacion
    {
        public bool EsValido { get; set; }
        public string Mensaje { get; set; }
    }

    public class DocumentoSubsanacionService
    {
        private readonly RevisionDocumentalService _revisionDocumentalService;
        private readonly IEmailQueueService _emailQueueService;

        public DocumentoSubsanacionService(CapaDatos.Interfaces.IUsuarioAS400DAO usuarioAs400Dao = null, CapaDatos.Interfaces.IEmpresaAS400DAO empresaAs400Dao = null)
            : this(new RevisionDocumentalService(usuarioAs400Dao, empresaAs400Dao), new EmailQueueService())
        {
        }

        public DocumentoSubsanacionService(
            RevisionDocumentalService revisionDocumentalService,
            IEmailQueueService emailQueueService)
        {
            _revisionDocumentalService = revisionDocumentalService ?? new RevisionDocumentalService();
            _emailQueueService = emailQueueService ?? new EmailQueueService();
        }

        public bool PuedeRtSubsanarDocumento(
            Documento documento,
            IDictionary<int, Tuple<string, string>> revisiones,
            string estadoSolicitudNormalizado,
            bool esUsuarioRt)
        {
            if (!esUsuarioRt || documento == null || documento.CodigoDocumento <= 0)
            {
                return false;
            }

            if (!SolicitudPermiteSubsanacionRt(estadoSolicitudNormalizado))
            {
                return false;
            }

            var decision = ObtenerDecisionRevision(documento, revisiones);
            if (!DecisionIndicaDevolucionInspector(decision))
            {
                return false;
            }

            if (EstadoDocumentoInstitucional.Normalizar(documento.Estado) == EstadoDocumentoInstitucional.Aceptado)
            {
                return false;
            }

            return RequiereSubsanacion(documento, revisiones);
        }

        public bool EsDocumentoBloqueadoParaRt(
            Documento documento,
            IDictionary<int, Tuple<string, string>> revisiones,
            string estadoSolicitudNormalizado,
            bool esUsuarioRt)
        {
            if (!esUsuarioRt || documento == null)
            {
                return false;
            }

            if (!SolicitudPermiteSubsanacionRt(estadoSolicitudNormalizado))
            {
                return false;
            }

            return !PuedeRtSubsanarDocumento(documento, revisiones, estadoSolicitudNormalizado, true);
        }

        public DocumentoSubsanacionValidacion ValidarCargaSubsanacionRt(
            Documento documento,
            IDictionary<int, Tuple<string, string>> revisiones,
            string estadoSolicitudNormalizado,
            bool esUsuarioRt)
        {
            if (!esUsuarioRt)
            {
                return Invalido("Solo el Representante Técnico puede subsanar documentación observada.");
            }

            if (!SolicitudPermiteSubsanacionRt(estadoSolicitudNormalizado))
            {
                return Invalido("La solicitud no se encuentra en un estado que permita subsanación documental.");
            }

            if (documento == null || documento.CodigoDocumento <= 0)
            {
                return Invalido("El documento indicado no existe.");
            }

            if (!PuedeRtSubsanarDocumento(documento, revisiones, estadoSolicitudNormalizado, true))
            {
                return Invalido("No puede modificar este documento porque no fue devuelto por el Inspector para subsanación.");
            }

            return new DocumentoSubsanacionValidacion { EsValido = true };
        }

        public DocumentoSubsanacionClasificacion ClasificarDocumentosParaRt(
            IEnumerable<Documento> documentos,
            IDictionary<int, Tuple<string, string>> revisiones,
            string estadoSolicitudNormalizado)
        {
            var resultado = new DocumentoSubsanacionClasificacion();
            if (!SolicitudPermiteSubsanacionRt(estadoSolicitudNormalizado))
            {
                return resultado;
            }

            foreach (var documento in documentos ?? Enumerable.Empty<Documento>())
            {
                if (documento == null || documento.CodigoDocumento <= 0)
                {
                    continue;
                }

                if (PuedeRtSubsanarDocumento(documento, revisiones, estadoSolicitudNormalizado, true))
                {
                    resultado.DocumentosDevueltos.Add(documento);
                }
                else
                {
                    resultado.DocumentosBloqueados.Add(documento);
                }
            }

            return resultado;
        }

        public IList<Documento> ObtenerDocumentosPendientesSubsanacion(
            IEnumerable<Documento> documentos,
            IDictionary<int, Tuple<string, string>> revisiones)
        {
            return _revisionDocumentalService.ObtenerDocumentosPendientesSubsanacion(documentos, revisiones);
        }

        public string ConstruirEventKeyDocumentosDevueltos(int solicitudId, IEnumerable<int> codigosDocumentoDevueltos)
        {
            if (solicitudId <= 0)
            {
                return null;
            }

            var ids = (codigosDocumentoDevueltos ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (ids.Count == 0)
            {
                return null;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "DOCUMENTOS_DEVUELTOS_INSPECTOR_{0}_{1}",
                solicitudId,
                string.Join("_", ids));
        }

        public ResultadoOperacion EncolarCorreoDocumentosDevueltosInspector(
            SolicitudAOCR solicitud,
            IEnumerable<DocumentoDevueltoNotificacionItem> documentosDevueltos,
            string nombreInspector,
            string urlSistema,
            string revisionCorrelationId)
        {
            if (solicitud == null)
            {
                return ResultadoOperacion.Error("No existe solicitud para notificar documentos devueltos.");
            }

            var items = (documentosDevueltos ?? Enumerable.Empty<DocumentoDevueltoNotificacionItem>())
                .Where(x => x != null)
                .ToList();

            if (items.Count == 0)
            {
                return ResultadoOperacion.Ok(null, "No hay documentos devueltos para notificar.");
            }

            var destinatarios = ResolverDestinatariosRt(solicitud);
            if (destinatarios.Count == 0)
            {
                return ResultadoOperacion.Ok(null, "Sin destinatarios RT para correo de subsanación.");
            }

            var eventKeyBase = ConstruirEventKeyDocumentosDevueltos(
                solicitud.CodigoSolicitud,
                items.Select(x => x.CodigoDocumento));

            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            var operador = ObtenerOperadorVisible(solicitud);
            var cantidad = items.Count;
            var asunto = "Sistema AOCR - Documentos devueltos para subsanación";
            var cuerpo = ConstruirCuerpoCorreoDocumentosDevueltos(
                numeroSolicitud,
                operador,
                cantidad,
                items,
                nombreInspector,
                urlSistema);

            var encolados = 0;
            foreach (var destinatario in destinatarios)
            {
                var eventKey = string.IsNullOrWhiteSpace(eventKeyBase)
                    ? null
                    : eventKeyBase + "_" + destinatario.Trim().ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(eventKey))
                {
                    var existe = _emailQueueService
                        .ExisteNotificacionAsync("DOCUMENTOS_DEVUELTOS_INSPECTOR", eventKey, solicitud.CodigoSolicitud)
                        .GetAwaiter()
                        .GetResult();

                    if (existe)
                    {
                        continue;
                    }
                }

                _emailQueueService.EncolarAsync(new EmailQueueItem
                {
                    Para = destinatario,
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    Estado = "PENDIENTE",
                    SolicitudId = solicitud.CodigoSolicitud,
                    TipoNotificacion = "DOCUMENTOS_DEVUELTOS_INSPECTOR",
                    EventKey = eventKey,
                    CorrelationId = string.IsNullOrWhiteSpace(revisionCorrelationId) ? eventKeyBase : revisionCorrelationId.Trim(),
                    EsHtml = true
                }).GetAwaiter().GetResult();

                encolados++;
            }

            return ResultadoOperacion.Ok(encolados, "Correo de documentos devueltos encolado correctamente.");
        }

        public static bool SolicitudPermiteSubsanacionRt(string estadoSolicitudNormalizado)
        {
            var estado = EstadoSolicitud.Normalizar(estadoSolicitudNormalizado ?? string.Empty);
            return string.Equals(estado, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase);
        }

        public static bool RequiereSubsanacion(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return false;
            }

            var decision = ObtenerDecisionRevision(documento, revisiones);
            if (DecisionIndicaDevolucionInspector(decision))
            {
                return true;
            }

            return EstadoDocumentoInstitucional.EsEstadoSubsanableRt(documento.Estado);
        }

        public static bool DecisionIndicaDevolucionInspector(string decisionRevision)
        {
            return EstadoDocumentoInstitucional.DecisionIndicaDevolucionInspector(decisionRevision);
        }

        public static string ObtenerDecisionRevision(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return "PENDIENTE";
            }

            Tuple<string, string> revision;
            if (revisiones != null && revisiones.TryGetValue(documento.CodigoDocumento, out revision))
            {
                return EstadoDocumentoInstitucional.NormalizarDecisionRevision(revision.Item1);
            }

            return EstadoDocumentoInstitucional.NormalizarDecisionRevision(documento.Estado);
        }

        public static string ObtenerObservacionRevision(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            Tuple<string, string> revision;
            if (revisiones != null && revisiones.TryGetValue(documento.CodigoDocumento, out revision))
            {
                return (revision.Item2 ?? string.Empty).Trim();
            }

            return (documento.Observaciones ?? string.Empty).Trim();
        }

        private static DocumentoSubsanacionValidacion Invalido(string mensaje)
        {
            return new DocumentoSubsanacionValidacion
            {
                EsValido = false,
                Mensaje = mensaje
            };
        }

        private static List<string> ResolverDestinatariosRt(SolicitudAOCR solicitud)
        {
            return new[]
                {
                    solicitud.CorreoRepresentanteTecnico,
                    solicitud.Email
                }
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ObtenerNumeroSolicitudVisible(SolicitudAOCR solicitud)
        {
            if (!string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud))
            {
                return solicitud.NumeroSolicitud.Trim();
            }

            return "#" + solicitud.CodigoSolicitud;
        }

        private static string ObtenerOperadorVisible(SolicitudAOCR solicitud)
        {
            var candidatos = new[]
            {
                solicitud.NombreComercial,
                solicitud.NombreOperador,
                solicitud.RazonSocial
            };

            return candidatos
                .Select(x => (x ?? string.Empty).Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? "Operador";
        }

        private static string ConstruirCuerpoCorreoDocumentosDevueltos(
            string numeroSolicitud,
            string operador,
            int cantidad,
            IList<DocumentoDevueltoNotificacionItem> items,
            string nombreInspector,
            string urlSistema)
        {
            var sb = new StringBuilder();
            sb.Append("Estimado/a Representante Técnico:<br><br>");
            sb.Append("Se informa que el Inspector ha devuelto ");
            sb.Append(cantidad);
            sb.Append(" documento(s) de la solicitud ");
            sb.Append(HttpUtility.HtmlEncode(numeroSolicitud));
            sb.Append(" para subsanación.<br><br>");
            sb.Append("Debe ingresar al Sistema AOCR y actualizar únicamente los documentos observados. ");
            sb.Append("Los documentos aceptados o no observados permanecerán bloqueados y no podrán ser modificados.<br><br>");
            sb.Append("Una vez cargada la subsanación, deberá enviar nuevamente la documentación para revisión del Inspector.<br><br>");
            sb.Append("<strong>Número de solicitud:</strong> ");
            sb.Append(HttpUtility.HtmlEncode(numeroSolicitud));
            sb.Append("<br><strong>Operador:</strong> ");
            sb.Append(HttpUtility.HtmlEncode(operador));
            sb.Append("<br><strong>Cantidad de documentos devueltos:</strong> ");
            sb.Append(cantidad);
            sb.Append("<br><strong>Fecha de devolución:</strong> ");
            sb.Append(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            sb.Append("<br><strong>Inspector:</strong> ");
            sb.Append(HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreInspector) ? "Inspector asignado" : nombreInspector));
            sb.Append("<br><br><strong>Documentos devueltos:</strong><ul>");

            foreach (var item in items)
            {
                sb.Append("<li><strong>");
                sb.Append(HttpUtility.HtmlEncode(item.Etiqueta ?? "Documento"));
                sb.Append("</strong>");
                if (!string.IsNullOrWhiteSpace(item.Observacion))
                {
                    sb.Append(": ");
                    sb.Append(HttpUtility.HtmlEncode(item.Observacion));
                }
                sb.Append("</li>");
            }

            sb.Append("</ul>");

            if (!string.IsNullOrWhiteSpace(urlSistema))
            {
                sb.Append("<br><a href=\"");
                sb.Append(HttpUtility.HtmlAttributeEncode(urlSistema));
                sb.Append("\">Ingresar al Sistema AOCR</a><br>");
            }

            sb.Append("<br>Sistema AOCR<br>Dirección General de Aviación Civil");
            return sb.ToString();
        }
    }

    public sealed class DocumentoDevueltoNotificacionItem
    {
        public int CodigoDocumento { get; set; }
        public string Etiqueta { get; set; }
        public string Observacion { get; set; }
    }
}
