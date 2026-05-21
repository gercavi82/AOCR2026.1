using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaNegocio;
using CapaPresentacion.Filters;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Models.ViewModels;

namespace CapaPresentacion.Controllers
{
    [AocrAuthorize(Roles = "Inspector,Administrador")]
    public class RevisionDocumentalController : Controller
    {
        private readonly RevisionDocumentalDAO _revisionDocumentalDao;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBl;
        private readonly DocumentoBL _documentoBl;
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao;
        private readonly IUserContextAccessor _userContext;

        public RevisionDocumentalController()
        {
            _revisionDocumentalDao = new RevisionDocumentalDAO();
            _solicitudDao = new SolicitudAOCRDAO();
            _solicitudAocrInfraBl = new SolicitudAocrInfraBL();
            _documentoBl = new DocumentoBL();
            _usuarioInternoRtDao = new UsuarioInternoRTDAO();
            _userContext = new UserContextAccessor();
        }

        public ActionResult Index()
        {
            var solicitudes = new List<RevisionDocumentalSolicitudRowViewModel>();
            var solicitudesRegistradas = new HashSet<int>();

            foreach (var inspectorId in ObtenerIdsInspectorActual().Where(id => id > 0))
            {
                var codigoSolicitudes = _revisionDocumentalDao.ObtenerPendientesRevisionInspector(inspectorId) ?? new List<int>();
                foreach (var codigoSolicitud in codigoSolicitudes)
                {
                    if (!solicitudesRegistradas.Add(codigoSolicitud))
                    {
                        continue;
                    }

                    var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
                    if (solicitud == null || !PuedeAccederRevisionDocumental(solicitud))
                    {
                        continue;
                    }

                    var fila = ConstruirFilaRevisionDocumental(solicitud);
                    if (fila == null)
                    {
                        continue;
                    }

                    if (string.Equals(fila.EstadoDocumentalCodigo, "DOCUMENTACION_APROBADA", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    solicitudes.Add(fila);
                }
            }

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

        private RevisionDocumentalSolicitudRowViewModel ConstruirFilaRevisionDocumental(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            var documentos = ObtenerDocumentosVigentes(_documentoBl.ObtenerPorSolicitud(solicitud.CodigoSolicitud));
            var estadoRevision = _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud)
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
            if (solicitud == null)
            {
                return false;
            }

            if (EsAdmin())
            {
                return true;
            }

            var inspectorIds = ObtenerIdsInspectorActual();
            if (solicitud.CodigoTecnico.HasValue && inspectorIds.Contains(solicitud.CodigoTecnico.Value))
            {
                return true;
            }

            var inspecciones = _solicitudAocrInfraBl.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            return inspecciones.Any(inspeccion =>
                inspeccion != null && inspeccion.CodigoInspector.HasValue && inspectorIds.Contains(inspeccion.CodigoInspector.Value));
        }

        private HashSet<int> ObtenerIdsInspectorActual()
        {
            var ids = new HashSet<int>();
            var usuarioIdActual = ObtenerIdUsuarioActual();
            var codigoUsuarioTexto = ObtenerCodigoUsuarioSesion();
            var codigoUsuarioNumerico = ObtenerCodigoUsuario();

            if (usuarioIdActual > 0)
            {
                ids.Add(usuarioIdActual);
            }

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
                    }

                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                    }
                }
            }
            catch
            {
                // La bandeja tolera ambientes donde el catálogo RT no esté completo.
            }

            if (codigoUsuarioNumerico > 0)
            {
                ids.Add(codigoUsuarioNumerico);
            }

            return ids;
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
    }
}