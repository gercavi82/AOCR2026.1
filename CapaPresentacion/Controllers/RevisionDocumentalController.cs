using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Services;
using CapaPresentacion.Filters;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Models.ViewModels;

namespace CapaPresentacion.Controllers
{
    [AocrAuthorize(Roles = "Inspector,Administrador")]
    public class RevisionDocumentalController : Controller
    {
        private readonly RevisionDocumentalBandejaService _revisionDocumentalBandejaService;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBl;
        private readonly DocumentoBL _documentoBl;
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao;
        private readonly IUserContextAccessor _userContext;

        public RevisionDocumentalController()
        {
            _revisionDocumentalBandejaService = new RevisionDocumentalBandejaService();
            _solicitudDao = new SolicitudAOCRDAO();
            _solicitudAocrInfraBl = new SolicitudAocrInfraBL();
            _documentoBl = new DocumentoBL();
            _usuarioInternoRtDao = new UsuarioInternoRTDAO();
            _userContext = new UserContextAccessor();
        }

        public ActionResult Index()
        {
            var solicitudes = new List<RevisionDocumentalSolicitudRowViewModel>();
            var contextoInspector = ConstruirContextoInspectorActual();
            var itemsBandeja = EsAdmin()
                ? _revisionDocumentalBandejaService.ObtenerItemsBandejaInspector(Enumerable.Empty<int>(), Enumerable.Empty<string>(), true)
                : _revisionDocumentalBandejaService.ObtenerItemsBandejaInspector(contextoInspector.Ids, contextoInspector.Identificadores);

            foreach (var itemBandeja in itemsBandeja ?? Enumerable.Empty<RevisionDocumentalBandejaItem>())
            {
                var codigoSolicitud = itemBandeja.CodigoSolicitud;
                var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
                var estadoRevision = solicitud != null
                    ? _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud)
                    : null;

                if (solicitud == null)
                {
                    continue;
                }

                var fila = ConstruirFilaRevisionDocumental(solicitud, estadoRevision);
                if (fila == null)
                {
                    continue;
                }

                if (itemBandeja.MostrarAccionInspeccion)
                {
                    fila.CodigoInspeccion = itemBandeja.CodigoInspeccion;
                    fila.MostrarAccionInspeccion = true;
                    fila.EstadoDocumentalCodigo = "LISTO_INSPECCION_CAMPO";
                    fila.EstadoDocumentalNombre = "Lista para inspección de campo";
                    fila.EstadoDocumentalDetalle = "La fase documental fue confirmada. Continúe con la LV/EAE en el detalle de inspección.";
                }
                else if (itemBandeja.CodigoInspeccion.HasValue
                    && itemBandeja.CodigoInspeccion.Value > 0
                    && estadoRevision != null
                    && estadoRevision.DocumentacionAprobada)
                {
                    fila.CodigoInspeccion = itemBandeja.CodigoInspeccion;
                    fila.PendienteConfirmacionInspector = true;
                    fila.EstadoDocumentalCodigo = "PENDIENTE_CONFIRMACION_INSPECTOR";
                    fila.EstadoDocumentalNombre = "Pendiente confirmación del inspector";
                    fila.EstadoDocumentalDetalle = "Revise la documentación y confirme el cierre documental antes de habilitar la LV/EAE.";
                }

                solicitudes.Add(fila);
            }

            Trace.TraceInformation(
                "[DOC_FLOW] Accion=BANDEJA_INSPECTOR; Usuario=" + (Session["CodigoUsuario"] ?? User.Identity.Name ?? string.Empty) +
                "; TotalSolicitudes=" + solicitudes.Count +
                "; InspectorIds=" + string.Join(",", contextoInspector.Ids.OrderBy(x => x)) +
                "; Identificadores=" + string.Join(",", contextoInspector.Identificadores.OrderBy(x => x)));

            var modelo = new RevisionDocumentalIndexViewModel
            {
                Solicitudes = solicitudes
                    .OrderByDescending(item => item.FechaCargaDocumentos ?? DateTime.MinValue)
                    .ThenByDescending(item => item.CodigoSolicitud)
                    .ToList(),
                TotalSolicitudesPendientes = solicitudes.Count,
                TotalDocumentosPendientes = solicitudes.Sum(item => item.DocumentosPendientes),
                TotalSolicitudesEnRevision = solicitudes.Count(item => string.Equals(item.EstadoDocumentalCodigo, "EN_REVISION_DOCUMENTAL", StringComparison.OrdinalIgnoreCase)),
                TotalDocumentosObservados = solicitudes.Sum(item => item.DocumentosObservados),
                TotalDocumentosAceptados = solicitudes.Sum(item => item.DocumentosAceptados),
                TotalDocumentosSubsanados = solicitudes.Sum(item => item.DocumentosSubsanados)
            };

            return View("~/Views/RevisionDocumental/Index.cshtml", modelo);
        }

        public ActionResult Detalle(int id)
        {
            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null)
            {
                return HttpNotFound("Solicitud no encontrada.");
            }

            if (!PuedeAccederRevisionDocumental(solicitud))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "No tiene permisos para revisar esta documentación.");
            }

            return RedirectToAction("Lista", "Documento", new { solicitudId = id, modo = "revision" });
        }

        private RevisionDocumentalSolicitudRowViewModel ConstruirFilaRevisionDocumental(SolicitudAOCR solicitud, EstadoRevisionDocumental estadoRevision)
        {
            if (solicitud == null)
            {
                return null;
            }

            var documentos = ObtenerDocumentosVigentes(_documentoBl.ObtenerPorSolicitud(solicitud.CodigoSolicitud));
            estadoRevision = estadoRevision
                ?? _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud)
                ?? new EstadoRevisionDocumental { CodigoSolicitud = solicitud.CodigoSolicitud, TienePendientes = true };

            var estadoDocumental = ResolverEstadoDocumental(estadoRevision, documentos.Count);
            var fechaCargaDocumentos = documentos
                .Select(d => d != null
                    ? (d.FechaCarga ?? d.FechaSubida)
                    : (DateTime?)null)
                .OrderByDescending(fecha => fecha ?? DateTime.MinValue)
                .FirstOrDefault();

            return new RevisionDocumentalSolicitudRowViewModel
            {
                CodigoSolicitud = solicitud.CodigoSolicitud,
                NumeroSolicitud = string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                    ? "AOCR" + solicitud.CodigoSolicitud
                    : solicitud.NumeroSolicitud.Trim(),
                Operadora = ObtenerOperadoraVisible(solicitud),
                Responsable = ObtenerResponsableVisible(solicitud),
                EstadoSolicitud = (solicitud.Estado ?? string.Empty).Trim(),
                EstadoDocumentalCodigo = estadoDocumental.Item1,
                EstadoDocumentalNombre = estadoDocumental.Item2,
                EstadoDocumentalDetalle = estadoDocumental.Item3,
                FechaCargaDocumentos = fechaCargaDocumentos,
                DocumentosCargados = estadoRevision.TotalDocumentosVigentes,
                DocumentosPendientes = estadoRevision.DocumentosPendientesRevision,
                DocumentosObservados = estadoRevision.DocumentosObservadosDevueltos,
                DocumentosAceptados = estadoRevision.DocumentosAceptados,
                DocumentosSubsanados = estadoRevision.DocumentosSubsanadosPendientes,
                TieneDocumentosCargados = estadoRevision.TotalDocumentosVigentes > 0
            };
        }

        private bool PuedeAccederRevisionDocumental(SolicitudAOCR solicitud)
        {
            return PuedeAccederRevisionDocumental(
                solicitud,
                solicitud != null ? _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud) : null,
                ConstruirContextoInspectorActual());
        }

        private bool PuedeAccederRevisionDocumental(SolicitudAOCR solicitud, EstadoRevisionDocumental estadoRevision, InspectorIdentityContext contextoInspector)
        {
            if (solicitud == null)
            {
                return false;
            }

            if (EsAdmin())
            {
                return true;
            }

            var inspecciones = _solicitudAocrInfraBl.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            return RevisionDocumentalBandejaService.PuedeAccederRevisionDocumental(
                solicitud,
                estadoRevision,
                inspecciones,
                contextoInspector != null ? contextoInspector.Ids : new HashSet<int>(),
                contextoInspector != null ? contextoInspector.Identificadores : new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private HashSet<int> ObtenerIdsInspectorActual()
        {
            return ConstruirContextoInspectorActual().Ids;
        }

        private InspectorIdentityContext ConstruirContextoInspectorActual()
        {
            var ids = new HashSet<int>();
            var identificadores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usuarioIdActual = ObtenerIdUsuarioActual();
            var codigoUsuarioTexto = ObtenerCodigoUsuarioSesion();
            var codigoUsuarioNumerico = ObtenerCodigoUsuario();

            if (usuarioIdActual > 0)
            {
                ids.Add(usuarioIdActual);
            }

            AgregarIdentificadorInspector(identificadores, codigoUsuarioTexto);

            try
            {
                UsuarioInternoRTRegistro inspectorActual = null;

                if (usuarioIdActual > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(usuarioIdActual);
                }

                if (inspectorActual == null && !string.IsNullOrWhiteSpace(codigoUsuarioTexto))
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerActivoPorCodigoUsuario(codigoUsuarioTexto)
                        ?? _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(codigoUsuarioTexto);
                }

                if (inspectorActual == null && codigoUsuarioNumerico > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(codigoUsuarioNumerico);
                }

                if (inspectorActual != null)
                {
                    if (inspectorActual.UsuarioId.HasValue && inspectorActual.UsuarioId.Value > 0)
                    {
                        ids.Add(inspectorActual.UsuarioId.Value);
                        AgregarIdentificadorInspector(identificadores, inspectorActual.UsuarioId.Value.ToString());
                    }

                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                        AgregarIdentificadorInspector(identificadores, inspectorActual.TecnicoId.Value.ToString());
                    }

                    AgregarIdentificadorInspector(identificadores, inspectorActual.CodigoUsuario);
                    AgregarIdentificadorInspector(identificadores, inspectorActual.Identificacion);
                    AgregarIdentificadorInspector(identificadores, inspectorActual.UsuarioLogin);
                }
            }
            catch
            {
                // La bandeja tolera ambientes donde el catálogo RT no esté completo.
            }

            if (codigoUsuarioNumerico > 0)
            {
                ids.Add(codigoUsuarioNumerico);
                AgregarIdentificadorInspector(identificadores, codigoUsuarioNumerico.ToString());
            }

            return new InspectorIdentityContext
            {
                Ids = ids,
                Identificadores = identificadores
            };
        }

        private List<Documento> ObtenerDocumentosVigentes(IEnumerable<Documento> documentos)
        {
            return (documentos ?? Enumerable.Empty<Documento>())
                .Where(documento => documento != null && documento.CodigoDocumento > 0)
                .GroupBy(ObtenerClaveDocumentoRevision, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(documento => documento.Version ?? 0)
                    .ThenByDescending(documento => documento.FechaCarga ?? documento.FechaSubida ?? DateTime.MinValue)
                    .ThenByDescending(documento => documento.CodigoDocumento)
                    .First())
                .ToList();
        }

        private static Tuple<string, string, string> ResolverEstadoDocumental(EstadoRevisionDocumental estadoRevision, int totalDocumentos)
        {
            if (totalDocumentos <= 0)
            {
                return Tuple.Create(
                    "PENDIENTE_CARGA_DOCUMENTAL",
                    "Pendiente de carga documental",
                    "El RT todavía no ha cargado documentos habilitantes para iniciar la revisión documental.");
            }

            if (estadoRevision != null && estadoRevision.DocumentacionAprobada)
            {
                return Tuple.Create(
                    "DOCUMENTACION_APROBADA",
                    "Documentación aprobada",
                    "Todos los documentos vigentes fueron aceptados y la fase documental quedó cerrada.");
            }

            if (estadoRevision != null && estadoRevision.TieneDocumentosObservados)
            {
                return Tuple.Create(
                    "DOCUMENTACION_OBSERVADA",
                    "Documentación observada",
                    "Existen documentos observados o devueltos pendientes de subsanación por parte del RT.");
            }

            if (estadoRevision != null && estadoRevision.TieneDocumentosSubsanadosPendientes)
            {
                return Tuple.Create(
                    "DOCUMENTACION_SUBSANADA",
                    "Documentación subsanada",
                    "El RT ya subsanó documentos y requieren una nueva revisión del inspector.");
            }

            if (estadoRevision != null && estadoRevision.DocumentosAceptados > 0 && estadoRevision.DocumentosPendientesRevision > 0)
            {
                return Tuple.Create(
                    "EN_REVISION_DOCUMENTAL",
                    "En revisión documental",
                    "La revisión documental está en curso: existen documentos aceptados y otros pendientes de decisión.");
            }

            return Tuple.Create(
                "DOCUMENTOS_CARGADOS",
                "Documentos cargados",
                "La documentación habilitante ya fue cargada y está pendiente de revisión por el inspector.");
        }

        private static string ObtenerClaveDocumentoRevision(Documento documento)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            var tipoDocumento = (documento.TipoDocumento ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(tipoDocumento)
                ? tipoDocumento.ToUpperInvariant()
                : "__DOC_" + documento.CodigoDocumento;
        }

        private static string ObtenerOperadoraVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "No disponible";
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RazonSocial))
            {
                return solicitud.RazonSocial.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.NombreOperador))
            {
                return solicitud.NombreOperador.Trim();
            }

            return "No disponible";
        }

        private static string ObtenerResponsableVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "No disponible";
            }

            if (!string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                return solicitud.TecnicoResponsableNombre.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal))
            {
                return solicitud.RepresentanteLegal.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico))
            {
                return solicitud.CorreoRepresentanteTecnico.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.Email))
            {
                return solicitud.Email.Trim();
            }

            return "No disponible";
        }

        private bool EsAdmin()
        {
            return User != null && User.IsInRole("Administrador");
        }

        private static bool CoincideIdentificadorInspector(string valor, HashSet<string> identificadores)
        {
            return !string.IsNullOrWhiteSpace(valor)
                && identificadores != null
                && identificadores.Contains(valor.Trim().ToUpperInvariant());
        }

        private static void AgregarIdentificadorInspector(HashSet<string> identificadores, string valor)
        {
            if (identificadores == null || string.IsNullOrWhiteSpace(valor))
            {
                return;
            }

            identificadores.Add(valor.Trim().ToUpperInvariant());
        }

        private int ObtenerCodigoUsuario()
        {
            int id;
            return _userContext.TryGetCodigoUsuario(Session, out id) ? id : 0;
        }

        private int ObtenerIdUsuarioActual()
        {
            int id;
            return _userContext.TryGetUserId(Session, out id) ? id : 0;
        }

        private string ObtenerCodigoUsuarioSesion()
        {
            var codigoUsuario = Session != null ? Session["CodigoUsuario"] as string : null;
            if (!string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return codigoUsuario.Trim();
            }

            if (User != null && User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrWhiteSpace(User.Identity.Name))
            {
                return User.Identity.Name.Trim();
            }

            return string.Empty;
        }

        private sealed class InspectorIdentityContext
        {
            public HashSet<int> Ids { get; set; }
            public HashSet<string> Identificadores { get; set; }
        }
    }
}