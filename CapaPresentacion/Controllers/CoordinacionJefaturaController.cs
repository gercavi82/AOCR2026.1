using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaModelo.Common;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaPresentacion.Helpers;
using CapaPresentacion.Filters;
using CapaPresentacion.Models;
using Npgsql;
using Rotativa;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DirectorCertificacionesDcav,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
    public class CoordinacionJefaturaController : Controller
    {
        private readonly CapaNegocio.Interfaces.IUsuarioContextoService _usuarioContexto = System.Web.Mvc.DependencyResolver.Current.GetService<CapaNegocio.Interfaces.IUsuarioContextoService>() ?? new CapaNegocio.Services.UsuarioContextoService();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly CertificadoDAO _certificadoDao = new CertificadoDAO();
        private readonly AeronaveSolicitudDAO _aeronaveSolicitudDao = new AeronaveSolicitudDAO();
        private readonly HistorialEstadoDAO _historialEstadoDao = new HistorialEstadoDAO();
        private readonly AocrFirmaDocumentoDAO _aocrFirmaDocumentoDao = new AocrFirmaDocumentoDAO();
        private readonly AocrFirmaPosicionDocumentoDAO _aocrFirmaPosicionDocumentoDao = new AocrFirmaPosicionDocumentoDAO();
        private readonly FirmaDigitalService _firmaDigitalService = new FirmaDigitalService();
        private readonly DashboardInspeccionDAO _dashboardInspeccionDao = new DashboardInspeccionDAO();
        private readonly UsuarioInternoRTDAO _usuarioInternoRTDAO = new UsuarioInternoRTDAO();
        private readonly SolicitudEstadoTransitionBL _solicitudEstadoTransitionBL = new SolicitudEstadoTransitionBL();
        private readonly AocrContextResolverService _aocrContextResolverService = new AocrContextResolverService();
        private readonly AocrEstadoService _aocrEstadoService = new AocrEstadoService();
        private readonly InformeTecnicoEstadoService _informeTecnicoEstadoService = new InformeTecnicoEstadoService();
        private readonly AocrFinalizacionService _aocrFinalizacionService = new AocrFinalizacionService();
        private readonly AocrProcesoNotificacionService _aocrProcesoNotificacionService = new AocrProcesoNotificacionService();

        [Authorize(Roles = "Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DashboardGerencial()
        {
            return RedirectToAction("DashboardGerencial", "Direccion");
        }

        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "DashboardInspeccion")]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion")]
        public ActionResult DashboardInspeccion(string compania = null, string inspector = null, string estado = null, string quickFilter = null)
        {
            var urlHelper = new UrlHelper(ControllerContext.RequestContext);
            var puedeGestionarAsignacion = User.IsInRole("Administrador")
                || User.IsInRole("Coordinador")
                || User.IsInRole("CoordinadorInspecciones");
            var puedeVerPendientesDirdac = User.IsInRole("Administrador") || User.IsInRole("DIRDAC") || User.IsInRole("Direccion") || User.IsInRole("DireccionJefaturaTecnica") || User.IsInRole("Director") || User.IsInRole("JefaturaTecnica") || User.IsInRole("Jefe");
            var puedeValidarAocr = User.IsInRole("Administrador")
                || User.IsInRole("CoordinacionLegal")
                || User.IsInRole("CoordinadorLegal")
                || User.IsInRole("Coordinador")
                || User.IsInRole("CoordinadorInspecciones")
                || User.IsInRole("DIRDAC")
                || User.IsInRole("Direccion")
                || User.IsInRole("DirectorGeneral")
                || User.IsInRole("JefaturaTecnica");
            var quickFilterNormalizado = NormalizarQuickFilter(quickFilter);

            var inspecciones = _dashboardInspeccionDao.ObtenerInspeccionesEnSeguimiento();
            var documentos = _dashboardInspeccionDao.ObtenerControlDocumental();
            var firmas = _dashboardInspeccionDao.ObtenerPendientesFirma();
            var noConformidades = new List<DashboardInspeccionNcData>();

            try
            {
                noConformidades = _dashboardInspeccionDao.ObtenerNoConformidades();
            }
            catch (Exception)
            {
                TempData["Warning"] = "El tablero se cargó sin el módulo de observaciones/NC por una inconsistencia en la consulta de base de datos.";
            }

            var gestionIntegral = ConstruirGestionIntegralAocr(inspecciones, documentos, urlHelper, puedeGestionarAsignacion, puedeVerPendientesDirdac, puedeValidarAocr);

            var companiasDisponibles = gestionIntegral.Select(x => x.Compania)
                .Concat(firmas.Select(x => x.Compania))
                .Concat(noConformidades.Select(x => x.Compania))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var inspectoresDisponibles = gestionIntegral.Select(x => x.Inspector)
                .Concat(firmas.Select(x => x.InspectorAsignado))
                .Concat(noConformidades.Select(x => x.InspectorAsignado))
                .Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, "No asignado", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var estadosDisponibles = gestionIntegral.Select(x => x.EstadoGeneral)
                .Concat(gestionIntegral.Select(x => x.EstadoDocumental))
                .Concat(gestionIntegral.Select(x => x.EstadoInspeccion))
                .Concat(gestionIntegral.Select(x => x.EtapaActual))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (!string.IsNullOrWhiteSpace(compania))
            {
                gestionIntegral = gestionIntegral.Where(x => ContieneTexto(x.Compania, compania)).ToList();
                firmas = firmas.Where(x => ContieneTexto(x.Compania, compania)).ToList();
                noConformidades = noConformidades.Where(x => ContieneTexto(x.Compania, compania)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(inspector))
            {
                gestionIntegral = gestionIntegral.Where(x => ContieneTexto(x.Inspector, inspector)).ToList();
                firmas = firmas.Where(x => ContieneTexto(x.InspectorAsignado, inspector)).ToList();
                noConformidades = noConformidades.Where(x => ContieneTexto(x.InspectorAsignado, inspector)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                gestionIntegral = gestionIntegral.Where(x =>
                        ContieneTexto(x.EstadoGeneral, estado)
                        || ContieneTexto(x.EstadoDocumental, estado)
                        || ContieneTexto(x.EstadoInspeccion, estado)
                        || ContieneTexto(x.EtapaActual, estado))
                    .ToList();
                firmas = firmas.Where(x => ContieneTexto(x.Estado, estado) || ContieneTexto(x.FirmanteRequerido, estado)).ToList();
                noConformidades = noConformidades.Where(x => ContieneTexto(x.Estado, estado)).ToList();
            }

            if (!string.Equals(quickFilterNormalizado, "TODOS", StringComparison.OrdinalIgnoreCase))
            {
                gestionIntegral = gestionIntegral.Where(x => CumpleQuickFilter(x, quickFilterNormalizado)).ToList();
            }

            var model = new DashboardInspeccionViewModel
            {
                CompaniaFiltro = compania,
                InspectorFiltro = inspector,
                EstadoFiltro = estado,
                QuickFilter = quickFilterNormalizado,
                CompaniasDisponibles = companiasDisponibles,
                InspectoresDisponibles = inspectoresDisponibles,
                EstadosDisponibles = estadosDisponibles,
                GestionIntegralAocr = gestionIntegral,
                TableroAocr = ConstruirTableroAocr(gestionIntegral),
                PendientesFirma = firmas.Select(x => new DashboardInspeccionFirmaItemViewModel
                {
                    CodigoInspeccion = x.CodigoInspeccion,
                    CodigoSolicitud = x.CodigoSolicitud,
                    NumeroSolicitud = x.NumeroSolicitud,
                    Compania = x.Compania,
                    Documento = x.Documento,
                    FirmanteRequerido = x.FirmanteRequerido,
                    Estado = x.Estado,
                    FechaEnvio = x.FechaEnvio,
                    UrlAccion = string.Equals(x.FirmanteRequerido, "DIRDAC", StringComparison.OrdinalIgnoreCase) && puedeVerPendientesDirdac
                        ? urlHelper.Action("PendientesDireccion", "Inspeccion")
                        : urlHelper.Action("Detalle", "Inspeccion", new { id = x.CodigoInspeccion }),
                    TextoAccion = string.Equals(x.FirmanteRequerido, "DIRDAC", StringComparison.OrdinalIgnoreCase) && puedeVerPendientesDirdac
                        ? "Revisar informe"
                        : "Abrir detalle"
                }).ToList(),
                ObservacionesNc = noConformidades.Select(x => new DashboardInspeccionNcItemViewModel
                {
                    CodigoInspeccion = x.CodigoInspeccion,
                    CodigoSolicitud = x.CodigoSolicitud,
                    NumeroSolicitud = x.NumeroSolicitud,
                    Compania = x.Compania,
                    TipoNc = x.TipoNc,
                    Descripcion = x.Descripcion,
                    Estado = x.Estado,
                    Responsable = x.Responsable,
                    Fecha = x.Fecha,
                    UrlAccion = urlHelper.Action("Detalle", "Inspeccion", new { id = x.CodigoInspeccion })
                }).ToList()
            };

            return View("~/Views/CoordinacionJefatura/DashboardInspeccion.cshtml", model);
        }

        private List<DashboardGestionIntegralItemViewModel> ConstruirGestionIntegralAocr(
            IEnumerable<DashboardInspeccionSeguimientoData> inspecciones,
            IEnumerable<DashboardInspeccionDocumentoData> documentos,
            UrlHelper urlHelper,
            bool puedeGestionarAsignacion,
            bool puedeVerPendientesDirdac,
            bool puedeValidarAocr)
        {
            var inspeccionesPorSolicitud = (inspecciones ?? Enumerable.Empty<DashboardInspeccionSeguimientoData>())
                .GroupBy(x => x.CodigoSolicitud)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.UltimaActualizacion ?? x.FechaAsignacion ?? DateTime.MinValue)
                        .ThenByDescending(x => x.CodigoInspeccion)
                        .First());

            var documentosPorSolicitud = (documentos ?? Enumerable.Empty<DashboardInspeccionDocumentoData>())
                .GroupBy(x => x.CodigoSolicitud)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.FechaUltimaActualizacion ?? DateTime.MinValue)
                        .ThenByDescending(x => x.CodigoInspeccion ?? 0)
                        .First());

            var pendientesAsignacionPorId = new Dictionary<int, SolicitudAOCR>();
            if (puedeGestionarAsignacion)
            {
                foreach (var solicitudPendiente in new CoordinacionBandejaService().ObtenerPendientesAsignacion() ?? new List<SolicitudAOCR>())
                {
                    if (solicitudPendiente == null || solicitudPendiente.CodigoSolicitud <= 0)
                    {
                        continue;
                    }

                    pendientesAsignacionPorId[solicitudPendiente.CodigoSolicitud] = solicitudPendiente;
                }
            }

            var solicitudIds = documentosPorSolicitud.Keys
                .Concat(inspeccionesPorSolicitud.Keys)
                .Concat(pendientesAsignacionPorId.Keys)
                .Distinct()
                .ToList();

            var items = new List<DashboardGestionIntegralItemViewModel>();

            foreach (var codigoSolicitud in solicitudIds)
            {
                DashboardInspeccionSeguimientoData inspeccion;
                DashboardInspeccionDocumentoData documento;
                SolicitudAOCR solicitudPendienteAsignacion;
                inspeccionesPorSolicitud.TryGetValue(codigoSolicitud, out inspeccion);
                documentosPorSolicitud.TryGetValue(codigoSolicitud, out documento);
                pendientesAsignacionPorId.TryGetValue(codigoSolicitud, out solicitudPendienteAsignacion);

                var numeroSolicitud = FirstNonEmpty(documento != null ? documento.NumeroSolicitud : null, inspeccion != null ? inspeccion.NumeroSolicitud : null, solicitudPendienteAsignacion != null ? solicitudPendienteAsignacion.NumeroSolicitud : null, codigoSolicitud.ToString());
                var compania = FirstNonEmpty(documento != null ? documento.Compania : null, inspeccion != null ? inspeccion.Compania : null, solicitudPendienteAsignacion != null ? solicitudPendienteAsignacion.NombreComercial : null, solicitudPendienteAsignacion != null ? solicitudPendienteAsignacion.NombreOperador : null, solicitudPendienteAsignacion != null ? solicitudPendienteAsignacion.RazonSocial : null, "No especificada");
                var tipo = ResolverTipoGestion(FirstNonEmpty(documento != null ? documento.TipoOperacion : null, inspeccion != null ? inspeccion.TipoOperacion : null));
                var estadoDocumental = string.IsNullOrWhiteSpace(documento != null ? documento.EstadoDocumento : null)
                    ? (solicitudPendienteAsignacion != null ? "EN_REVISION" : "PENDIENTE")
                    : documento.EstadoDocumento;
                var estadoInspeccion = string.IsNullOrWhiteSpace(inspeccion != null ? inspeccion.EstadoVisual : null) ? "NO_ASIGNADO" : inspeccion.EstadoVisual;
                var inspector = FirstNonEmpty(inspeccion != null ? inspeccion.InspectorAsignado : null, documento != null ? documento.InspectorAsignado : null, "No asignado");
                var firmaInspector = documento != null && documento.FirmadoInspector;
                var firmaDirdac = documento != null && documento.FirmadoDirdac;
                var tieneInspector = !string.Equals(inspector, "No asignado", StringComparison.OrdinalIgnoreCase);
                var listoParaFirma = documento != null &&
                    ((firmaInspector && !firmaDirdac)
                    || (!firmaInspector && string.Equals(estadoInspeccion, "FINALIZADA", StringComparison.OrdinalIgnoreCase)));
                var etapaActual = DeterminarEtapaActual(estadoDocumental, estadoInspeccion, tieneInspector, firmaInspector, firmaDirdac, listoParaFirma);
                var estadoGeneral = DeterminarEstadoGeneral(estadoDocumental, estadoInspeccion, firmaDirdac, listoParaFirma, etapaActual);
                var fecha = MaxDate(
                    documento != null ? documento.FechaUltimaActualizacion : null,
                    inspeccion != null ? inspeccion.UltimaActualizacion : null,
                    inspeccion != null ? inspeccion.FechaAsignacion : null,
                    solicitudPendienteAsignacion != null ? solicitudPendienteAsignacion.UpdatedAt : null,
                    solicitudPendienteAsignacion != null ? solicitudPendienteAsignacion.FechaSolicitud : null,
                    solicitudPendienteAsignacion != null ? solicitudPendienteAsignacion.CreatedAt : null);

                var urlDetalle = urlHelper.Action("Detalle", "SolicitudAOCR", new { id = codigoSolicitud });
                var urlVerDocumento = documento != null && documento.CodigoInspeccion.HasValue && documento.TienePdf
                    ? urlHelper.Action("VerInforme", "Inspeccion", new { id = documento.CodigoInspeccion.Value })
                    : null;
                var urlDescargarPdf = documento != null && documento.CodigoInspeccion.HasValue && documento.TienePdf
                    ? urlHelper.Action("DescargarInforme", "Inspeccion", new { id = documento.CodigoInspeccion.Value })
                    : null;
                var urlRevisar = urlHelper.Action("RevisionVerificacion", "CoordinacionJefatura");
                var urlFirmar = listoParaFirma && firmaInspector && !firmaDirdac && puedeVerPendientesDirdac
                    ? urlHelper.Action("PendientesDireccion", "Inspeccion")
                    : null;
                var urlValidarAocr = puedeValidarAocr
                    ? urlHelper.Action("Index", "FirmaAocr", new { solicitudId = codigoSolicitud })
                    : null;
                var puedeAsignarInspector = puedeGestionarAsignacion
                    && !tieneInspector
                    && (solicitudPendienteAsignacion != null || inspeccion == null || inspeccion.PuedeAsignarInspector);
                var puedeValidarFila = puedeValidarAocr
                    && string.Equals(etapaActual, "LEGALIZACION", StringComparison.OrdinalIgnoreCase)
                    && firmaDirdac;

                var textoAccionPrincipal = "Ver";
                var urlAccionPrincipal = urlDetalle;

                if (!string.IsNullOrWhiteSpace(urlFirmar))
                {
                    textoAccionPrincipal = "Firmar";
                    urlAccionPrincipal = urlFirmar;
                }
                else if (listoParaFirma && !string.IsNullOrWhiteSpace(urlValidarAocr))
                {
                    textoAccionPrincipal = "Revisar AOCR";
                    urlAccionPrincipal = urlValidarAocr;
                }
                else if (puedeAsignarInspector)
                {
                    textoAccionPrincipal = "Asignar inspector";
                    urlAccionPrincipal = urlHelper.Action("AsignarInspector", "Tecnico", new { solicitudId = codigoSolicitud });
                }
                else if (string.Equals(estadoDocumental, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoInspeccion, "OBSERVADA", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoGeneral, "OBSERVADO", StringComparison.OrdinalIgnoreCase))
                {
                    textoAccionPrincipal = "Ver observaciones";
                    urlAccionPrincipal = urlRevisar;
                }
                else if (string.Equals(etapaActual, "INSPECCION", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(etapaActual, "VERIFICACION", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoGeneral, "EN_PROCESO", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoGeneral, "EN_VERIFICACION", StringComparison.OrdinalIgnoreCase))
                {
                    textoAccionPrincipal = string.Equals(etapaActual, "INSPECCION", StringComparison.OrdinalIgnoreCase)
                        ? "Revisar inspección"
                        : "Revisar";
                    urlAccionPrincipal = urlRevisar;
                }
                else if (puedeValidarFila && !string.IsNullOrWhiteSpace(urlValidarAocr))
                {
                    textoAccionPrincipal = "Firma institucional AOCR";
                    urlAccionPrincipal = urlValidarAocr;
                }
                else if (!string.IsNullOrWhiteSpace(urlVerDocumento) && string.Equals(estadoGeneral, "FINALIZADO", StringComparison.OrdinalIgnoreCase))
                {
                    textoAccionPrincipal = "Ver AOCR";
                    urlAccionPrincipal = urlVerDocumento;
                }

                var columnaKanban = DeterminarColumnaKanban(estadoDocumental, estadoInspeccion, estadoGeneral, etapaActual, listoParaFirma);
                var colorKanban = DeterminarColorKanban(columnaKanban);
                var resumenKanban = ConstruirResumenKanban(estadoDocumental, estadoInspeccion, listoParaFirma, firmaInspector, firmaDirdac);
                var esUrgente = string.Equals(columnaKanban, "OBSERVADOS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoDocumental, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoInspeccion, "OBSERVADA", StringComparison.OrdinalIgnoreCase);

                items.Add(new DashboardGestionIntegralItemViewModel
                {
                    CodigoSolicitud = codigoSolicitud,
                    CodigoInspeccion = documento != null ? documento.CodigoInspeccion : (inspeccion != null ? (int?)inspeccion.CodigoInspeccion : null),
                    NumeroSolicitud = numeroSolicitud,
                    Compania = compania,
                    Tipo = tipo,
                    EstadoGeneral = estadoGeneral,
                    EstadoDocumental = estadoDocumental,
                    EstadoInspeccion = estadoInspeccion,
                    Inspector = inspector,
                    EtapaActual = etapaActual,
                    FirmaInspector = firmaInspector,
                    FirmaDirdac = firmaDirdac,
                    ListoParaFirma = listoParaFirma,
                    Fecha = fecha,
                    UrlDetalle = urlDetalle,
                    UrlVerDocumento = urlVerDocumento,
                    UrlDescargarPdf = urlDescargarPdf,
                    UrlAccionPrincipal = urlAccionPrincipal,
                    TextoAccionPrincipal = textoAccionPrincipal,
                    ColumnaKanban = columnaKanban,
                    ColorKanban = colorKanban,
                    EsUrgente = esUrgente,
                    ResumenKanban = resumenKanban
                });
            }

            return items
                .OrderByDescending(x => x.Fecha ?? DateTime.MinValue)
                .ThenByDescending(x => x.CodigoSolicitud)
                .ToList();
        }

        private static string ResolverTipoGestion(string tipo)
        {
            var valor = (tipo ?? string.Empty).Trim().ToUpperInvariant();
            if (valor.Contains("AIR") || valor.Contains("AERONAVEG"))
            {
                return "AIR";
            }

            if (valor.Contains("OPS") || valor.Contains("OPERAC"))
            {
                return "OPS";
            }

            return string.IsNullOrWhiteSpace(valor) ? "OPS" : valor;
        }

        private static string DeterminarEtapaActual(string estadoDocumental, string estadoInspeccion, bool tieneInspector, bool firmaInspector, bool firmaDirdac, bool listoParaFirma)
        {
            if (string.Equals(estadoDocumental, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "OBSERVADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "SUBSANADA", StringComparison.OrdinalIgnoreCase))
            {
                return "SUBSANACION";
            }

            if (firmaDirdac && string.Equals(estadoInspeccion, "FINALIZADA", StringComparison.OrdinalIgnoreCase))
            {
                return "LEGALIZACION";
            }

            if (listoParaFirma || (firmaInspector && !firmaDirdac))
            {
                return "FIRMA";
            }

            if (string.Equals(estadoInspeccion, "ASIGNADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "EN_PROCESO", StringComparison.OrdinalIgnoreCase))
            {
                return "INSPECCION";
            }

            if (!tieneInspector
                || string.Equals(estadoDocumental, "PENDIENTE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "EN_REVISION", StringComparison.OrdinalIgnoreCase))
            {
                return "VERIFICACION";
            }

            return "VERIFICACION";
        }

        private static string DeterminarEstadoGeneral(string estadoDocumental, string estadoInspeccion, bool firmaDirdac, bool listoParaFirma, string etapaActual)
        {
            if (string.Equals(estadoDocumental, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "OBSERVADA", StringComparison.OrdinalIgnoreCase))
            {
                return "OBSERVADO";
            }

            if (firmaDirdac && string.Equals(estadoInspeccion, "FINALIZADA", StringComparison.OrdinalIgnoreCase))
            {
                return "FINALIZADO";
            }

            if (string.Equals(etapaActual, "VERIFICACION", StringComparison.OrdinalIgnoreCase) || listoParaFirma)
            {
                return "EN_VERIFICACION";
            }

            if (string.Equals(estadoDocumental, "PENDIENTE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "NO_ASIGNADO", StringComparison.OrdinalIgnoreCase))
            {
                return "PENDIENTE";
            }

            if (string.Equals(estadoInspeccion, "ASIGNADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "EN_PROCESO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(etapaActual, "INSPECCION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "APROBADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "EN_REVISION", StringComparison.OrdinalIgnoreCase))
            {
                return "EN_PROCESO";
            }

            return "PENDIENTE";
        }

        private static DateTime? MaxDate(params DateTime?[] values)
        {
            return (values ?? new DateTime?[0])
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .DefaultIfEmpty()
                .Max();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return (values ?? new string[0]).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        private static string NormalizarQuickFilter(string quickFilter)
        {
            var valor = (quickFilter ?? string.Empty).Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(valor) ? "TODOS" : valor;
        }

        private static bool CumpleQuickFilter(DashboardGestionIntegralItemViewModel item, string quickFilter)
        {
            switch (quickFilter)
            {
                case "PENDIENTES":
                    return string.Equals(item.EstadoGeneral, "PENDIENTE", StringComparison.OrdinalIgnoreCase);
                case "EN_PROCESO":
                    return string.Equals(item.EstadoGeneral, "EN_PROCESO", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.EstadoGeneral, "EN_VERIFICACION", StringComparison.OrdinalIgnoreCase);
                case "OBSERVADOS":
                    return string.Equals(item.EstadoGeneral, "OBSERVADO", StringComparison.OrdinalIgnoreCase);
                case "LISTOS_FIRMA":
                    return item.ListoParaFirma;
                default:
                    return true;
            }
        }

        private static TableroAocrViewModel ConstruirTableroAocr(IEnumerable<DashboardGestionIntegralItemViewModel> items)
        {
            var tablero = new TableroAocrViewModel();

            foreach (var item in items ?? Enumerable.Empty<DashboardGestionIntegralItemViewModel>())
            {
                switch ((item.ColumnaKanban ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "OBSERVADOS":
                        tablero.Observados.Add(item);
                        break;
                    case "LISTOS_FIRMA":
                        tablero.ListosFirma.Add(item);
                        break;
                    case "FINALIZADOS":
                        tablero.Finalizados.Add(item);
                        break;
                    case "EN_INSPECCION":
                        tablero.EnInspeccion.Add(item);
                        break;
                    case "EN_REVISION":
                        tablero.EnRevision.Add(item);
                        break;
                    default:
                        tablero.Pendientes.Add(item);
                        break;
                }
            }

            return tablero;
        }

        private static string DeterminarColumnaKanban(string estadoDocumental, string estadoInspeccion, string estadoGeneral, string etapaActual, bool listoParaFirma)
        {
            if (string.Equals(estadoGeneral, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "OBSERVADA", StringComparison.OrdinalIgnoreCase))
            {
                return "OBSERVADOS";
            }

            if (string.Equals(estadoGeneral, "FINALIZADO", StringComparison.OrdinalIgnoreCase))
            {
                return "FINALIZADOS";
            }

            if (listoParaFirma
                || string.Equals(etapaActual, "FIRMA", StringComparison.OrdinalIgnoreCase))
            {
                return "LISTOS_FIRMA";
            }

            if (string.Equals(etapaActual, "INSPECCION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "ASIGNADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "EN_PROCESO", StringComparison.OrdinalIgnoreCase))
            {
                return "EN_INSPECCION";
            }

            if (string.Equals(estadoGeneral, "EN_VERIFICACION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(etapaActual, "VERIFICACION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "EN_REVISION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "APROBADO", StringComparison.OrdinalIgnoreCase))
            {
                return "EN_REVISION";
            }

            return "PENDIENTES";
        }

        private static string DeterminarColorKanban(string columnaKanban)
        {
            switch ((columnaKanban ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "OBSERVADOS":
                    return "danger";
                case "LISTOS_FIRMA":
                case "FINALIZADOS":
                    return "success";
                case "EN_INSPECCION":
                    return "warning";
                case "EN_REVISION":
                    return "info";
                default:
                    return "secondary";
            }
        }

        private static string ConstruirResumenKanban(string estadoDocumental, string estadoInspeccion, bool listoParaFirma, bool firmaInspector, bool firmaDirdac)
        {
            if (string.Equals(estadoDocumental, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "OBSERVADA", StringComparison.OrdinalIgnoreCase))
            {
                return "Requiere subsanación antes de continuar el trámite.";
            }

            if (listoParaFirma)
            {
                return firmaInspector && !firmaDirdac
                    ? "Pendiente de firma DIRDAC para continuar la emisión."
                    : "Listo para completar el circuito de firmas.";
            }

            if (string.Equals(estadoInspeccion, "EN_PROCESO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInspeccion, "ASIGNADA", StringComparison.OrdinalIgnoreCase))
            {
                return "Inspección activa con seguimiento técnico en curso.";
            }

            if (string.Equals(estadoDocumental, "EN_REVISION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "APROBADO", StringComparison.OrdinalIgnoreCase))
            {
                return "Expediente en revisión documental para habilitar la siguiente etapa.";
            }

            if (firmaInspector && firmaDirdac)
            {
                return "Trámite completo y documentado para consulta o auditoría.";
            }

            return "Pendiente de gestión inicial o asignación operativa.";
        }

        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "RevisionVerificacion")]
        public ActionResult RevisionVerificacion()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            var inspecciones = _inspeccionDao.ListarTodas() ?? new List<Inspeccion>();

            var model = new CoordinacionJefaturaRevisionViewModel
            {
                SolicitudesControlDocumental = solicitudes
                    .Where(s =>
                    {
                        var estado = EstadoSolicitud.Normalizar(s.Estado);
                        return estado == EstadoSolicitud.Pendiente
                            || estado == EstadoSolicitud.EnRevision
                            || estado == EstadoSolicitud.Observada
                            || estado == EstadoSolicitud.AceptacionDocumental;
                    })
                    .OrderByDescending(s => s.FechaSolicitud ?? DateTime.MinValue)
                    .Take(30)
                    .ToList(),

                SolicitudesAocrRevision = solicitudes
                    .Where(s =>
                    {
                        var estado = EstadoSolicitud.Normalizar(s.Estado);
                        return estado == EstadoSolicitud.AOCR_EnElaboracion
                            || estado == EstadoSolicitud.AOCR_EnRevision;
                    })
                    .OrderByDescending(s => s.FechaSolicitud ?? DateTime.MinValue)
                    .Take(30)
                    .ToList(),

                InspeccionesSeguimiento = inspecciones
                    .Where(i =>
                    {
                        var estado = EstadosInspeccion.NormalizarEstado(i.Estado);
                        return EstadosInspeccion.EsEstadoBloqueCoordinacionJefatura(estado)
                            || string.Equals(estado, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(estado, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(estado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderByDescending(i => i.CodigoInspeccion)
                    .Take(30)
                    .ToList()
            };

            model.InspeccionesSeguimientoItems = ConstruirItemsSeguimientoInspeccion(model.InspeccionesSeguimiento);

            return View(model);
        }

        [AllowAnonymous]
        public ActionResult PdfBrandingHeader()
        {
            return View("~/Views/Shared/_PdfBrandingHeader.cshtml");
        }

        [AllowAnonymous]
        public ActionResult PdfBrandingFooter()
        {
            return View("~/Views/Shared/_PdfBrandingFooter.cshtml");
        }

        private List<CoordinacionJefaturaInspeccionSeguimientoItemViewModel> ConstruirItemsSeguimientoInspeccion(IEnumerable<Inspeccion> inspecciones)
        {
            var items = new List<CoordinacionJefaturaInspeccionSeguimientoItemViewModel>();
            foreach (var inspeccion in inspecciones ?? Enumerable.Empty<Inspeccion>())
            {
                if (inspeccion == null)
                {
                    continue;
                }

                var solicitud = _solicitudDao.ObtenerPorId(inspeccion.CodigoSolicitud);
                var estadoNormalizado = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                var estadosPermitidos = EstadosInspeccion.ObtenerEstadosPermitidos(estadoNormalizado);

                items.Add(new CoordinacionJefaturaInspeccionSeguimientoItemViewModel
                {
                    Inspeccion = inspeccion,
                    Solicitud = solicitud,
                    NumeroInspeccion = !string.IsNullOrWhiteSpace(inspeccion.NumeroInspeccion) ? inspeccion.NumeroInspeccion : inspeccion.CodigoInspeccion.ToString(),
                    NumeroSolicitud = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud) ? solicitud.NumeroSolicitud : inspeccion.CodigoSolicitud.ToString(),
                    OperadorNombre = ObtenerNombreOperadorSeguimiento(solicitud),
                    EstadoNormalizado = estadoNormalizado,
                    EtapaActual = EstadosInspeccion.ObtenerDescripcion(estadoNormalizado),
                    InspectorAsignado = ObtenerInspectorAsignadoSeguimiento(inspeccion, solicitud),
                    MensajeOperativo = ConstruirMensajeOperativoSeguimiento(estadoNormalizado, inspeccion),
                    PuedeAceptarSolicitud = estadosPermitidos.Any(x => string.Equals(EstadosInspeccion.NormalizarEstado(x), EstadosInspeccion.ACEPTADA, System.StringComparison.OrdinalIgnoreCase)),
                    PuedeObservar = estadosPermitidos.Any(x => string.Equals(EstadosInspeccion.NormalizarEstado(x), EstadosInspeccion.OBSERVADA, System.StringComparison.OrdinalIgnoreCase)),
                    PuedeCerrar = estadosPermitidos.Any(x => string.Equals(EstadosInspeccion.NormalizarEstado(x), EstadosInspeccion.CERRADA, System.StringComparison.OrdinalIgnoreCase)),
                    PuedeAsignarInspector = PuedeAsignarInspectorEnSeguimiento(estadoNormalizado, inspeccion, solicitud)
                });
            }

            return items;
        }

        private static string ObtenerNombreOperadorSeguimiento(SolicitudAOCR solicitud)
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

        private string ObtenerInspectorAsignadoSeguimiento(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            // 1) Nombre ya persistido en la inspección (asignación via AsignarInspector o informe técnico).
            if (inspeccion != null && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre))
            {
                return inspeccion.InspectorPrincipalNombre.Trim();
            }

            // 2) Nombre en la solicitud (técnico responsable).
            if (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                return solicitud.TecnicoResponsableNombre.Trim();
            }

            // 3) Fallback RT: si hay código/cédula, consultar catálogo oficial.
            try
            {
                UsuarioInternoRTRegistro registro = null;

                if (inspeccion != null && inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
                {
                    registro = _usuarioInternoRTDAO.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(inspeccion.CodigoInspector.Value);
                }

                if (registro == null && solicitud != null && solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
                {
                    registro = _usuarioInternoRTDAO.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(solicitud.CodigoTecnico.Value);
                }

                var cedula = inspeccion != null && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula)
                    ? inspeccion.InspectorPrincipalCedula
                    : (solicitud != null ? solicitud.TecnicoResponsableCedula : null);

                if (registro == null && !string.IsNullOrWhiteSpace(cedula))
                {
                    registro = _usuarioInternoRTDAO.ObtenerInspectorAsignableActivo(cedula);
                }

                if (registro != null && !string.IsNullOrWhiteSpace(registro.NombreVisual))
                {
                    return registro.NombreVisual.Trim();
                }

                if (!string.IsNullOrWhiteSpace(cedula))
                {
                    return cedula.Trim();
                }
            }
            catch
            {
                // Silenciar: no interrumpir la bandeja si el catálogo RT falla.
            }

            return "No asignado";
        }

        private bool PuedeAsignarInspectorEnSeguimiento(string estadoNormalizado, Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (inspeccion == null)
            {
                return false;
            }

            var inspectorAsignado = ObtenerInspectorAsignadoSeguimiento(inspeccion, solicitud);
            if (!string.Equals(inspectorAsignado, "No asignado", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var estado = EstadosInspeccion.NormalizarEstado(estadoNormalizado);

            return string.Equals(estado, EstadosInspeccion.SOLICITUD_INSPECCION_CREADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.PAGO_VALIDADO, StringComparison.OrdinalIgnoreCase);
        }

        private static string ConstruirMensajeOperativoSeguimiento(string estadoNormalizado, Inspeccion inspeccion)
        {
            if (string.Equals(estadoNormalizado, EstadosInspeccion.VERIFICACION_SOLICITUD, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La solicitud se encuentra en verificacion por Direccion / Jefatura. Desde esta bandeja puede aceptar, observar o cerrar el tramite.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.ACEPTADA, System.StringComparison.OrdinalIgnoreCase))
            {
                return inspeccion != null && inspeccion.CodigoInspector.HasValue
                    ? "La solicitud fue aceptada y ya cuenta con inspector asignado. Puede continuar el seguimiento del avance."
                    : "La solicitud fue aceptada. El siguiente paso operativo es gestionar la asignación desde el módulo Asignación de Inspectores del menú Coordinador.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.SUBSANADA, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La compania subsano observaciones. Corresponde validar la informacion y definir si continua la inspeccion.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.EN_INSPECCION, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La inspeccion se encuentra en ejecucion por el inspector asignado. Direccion / Jefatura puede revisar el avance y el contexto del tramite.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.INFORME_ELABORADO, System.StringComparison.OrdinalIgnoreCase))
            {
                return "El inspector ya elaboro el informe tecnico. Corresponde revisar resultados, observaciones y siguientes pasos del tramite.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.OBSERVADA, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, System.StringComparison.OrdinalIgnoreCase))
            {
                return "El tramite mantiene observaciones pendientes. Revise el contexto y defina si procede subsanacion, continuidad o cierre.";
            }

            if (string.Equals(estadoNormalizado, EstadosInspeccion.CERRADA, System.StringComparison.OrdinalIgnoreCase))
            {
                return "La inspeccion se encuentra cerrada y no tiene transiciones BPMN pendientes para esta bandeja.";
            }

            return "Revise el estado actual del tramite y ejecute solo acciones alineadas con la etapa operativa.";
        }

        private static bool ContieneTexto(string origen, string filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(origen)
                && origen.IndexOf(filtro.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "ValidarAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult ValidarAocr(int? solicitudId = null, int? aocrId = null)
        {
            if (solicitudId.HasValue && solicitudId.Value > 0)
            {
                return RedirectToAction("Index", "FirmaAocr", new { solicitudId = solicitudId.Value });
            }

            if (UsarFirmaAocrNueva())
            {
                return RedirectToAction("PendientesDireccion", "Inspeccion");
            }

            // Evitar fuga de TempData["Error"] establecido por otras acciones.
            TempData.Remove("Error");
            RegistrarLogValidarAocrEntrada(solicitudId, aocrId);

            try
            {
                var contexto = _aocrContextResolverService.ResolverContextoAocr(solicitudId, aocrId);
                if (contexto == null || !contexto.Ok)
                {
                    var mensaje = contexto != null && !string.IsNullOrWhiteSpace(contexto.Mensaje)
                        ? contexto.Mensaje
                        : "No se recibio el identificador de la solicitud o del AOCR.";
                    RegistrarLogValidarAocrEstados(contexto);
                    RegistrarLogValidarAocrPrecondiciones(contexto, null, mensaje);
                    return View("~/Views/CoordinacionJefatura/ValidarAocr.cshtml", new ValidarAocrViewModel
                    {
                        MensajeError = mensaje
                    });
                }

                RegistrarLogValidarAocrEstados(contexto);

                var informeAprobadoDireccion = _informeTecnicoEstadoService.EstaAprobadoPorDireccion(contexto.InformeTecnico);
                if (!_aocrEstadoService.PuedeDireccionValidarAocr(contexto.EstadoSolicitud, contexto.EstadoAocr)
                    && !informeAprobadoDireccion
                    && !User.IsInRole("Administrador"))
                {
                    var motivo = "El tramite no se encuentra en la etapa requerida para esta accion.";
                    RegistrarLogValidarAocrPrecondiciones(contexto, null, motivo);
                    return View("~/Views/CoordinacionJefatura/ValidarAocr.cshtml", new ValidarAocrViewModel
                    {
                        MensajeError = motivo
                    });
                }

                var items = ConstruirItemsValidacionAocr();
                items = items
                    .Where(item => item != null
                        && item.Solicitud != null
                        && contexto.SolicitudId.HasValue
                        && item.Solicitud.CodigoSolicitud == contexto.SolicitudId.Value)
                    .ToList();

                if (!items.Any())
                {
                    var motivo = "No se encontro el AOCR asociado a la solicitud.";
                    RegistrarLogValidarAocrPrecondiciones(contexto, null, motivo);
                    return View("~/Views/CoordinacionJefatura/ValidarAocr.cshtml", new ValidarAocrViewModel
                    {
                        MensajeError = motivo
                    });
                }

                RegistrarLogValidarAocrPrecondiciones(contexto, items.FirstOrDefault(), "Contexto valido.");

                var model = ConstruirValidarAocrViewModel(contexto, items.FirstOrDefault());
                model.Items = items;
                model.MensajeInformativo = "Informe tecnico aprobado por Direccion. AOCR y Condiciones disponibles para validacion y firma institucional.";
                RegistrarLogValidarAocrViewModel(model);

                return View("~/Views/CoordinacionJefatura/ValidarAocr.cshtml", model);
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("ValidarAocr.CargarBandeja", exPg);
                TempData["Error"] = "No se pudo cargar la bandeja de Firma institucional AOCR por un error de base de datos. Ref: " + referencia;
                return RedirectToAction("DashboardInspeccion");
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("ValidarAocr.CargarBandeja", ex);
                TempData["Error"] = "No se pudo cargar la bandeja de Firma institucional AOCR. Ref: " + referencia;
                return RedirectToAction("DashboardInspeccion");
            }
        }

        [HttpGet]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "ValidarAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "DirectorCertificacionesDcav,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult FirmaAocr(int solicitudId)
        {
            if (UsarFirmaAocrNueva())
            {
                return RedirectToAction("Index", "FirmaAocr", new { solicitudId = solicitudId });
            }

            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR_PAGE][IN] SolicitudId=" + solicitudId +
                "; Usuario=" + ObtenerLoginActual() +
                "; Rol=" + ObtenerRolActualLog());

            try
            {
                var model = ConstruirFirmaAocrInstitucionalViewModel(solicitudId);
                System.Diagnostics.Trace.TraceInformation(
                    "[FIRMA_AOCR_PAGE][MODEL] SolicitudId=" + solicitudId +
                    "; EstadoSolicitud=" + (model != null ? model.EstadoSolicitud : string.Empty) +
                    "; AocrId=" + (model != null ? model.AocrId.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                    "; EstadoAocr=" + (model != null ? model.EstadoAocr : string.Empty) +
                    "; PdfExiste=" + (model != null && model.PdfExiste) +
                    "; PdfFirmadoExiste=" + (model != null && model.PdfFirmadoExiste) +
                    "; PuedeFirmar=" + (model != null && model.PuedeFirmar) +
                    "; Motivo=" + (model != null ? model.MotivoBloqueo : "Modelo no disponible"));

                return View("~/Views/CoordinacionJefatura/FirmaAocr.cshtml", model);
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("FirmaAocr.CargarPantalla", exPg, solicitudId);
                TempData["Error"] = "No se pudo cargar la pantalla de firma AOCR. Ref: " + referencia;
                return RedirectToAction("DashboardInspeccion");
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("FirmaAocr.CargarPantalla", ex, solicitudId);
                TempData["Error"] = "No se pudo cargar la pantalla de firma AOCR. Ref: " + referencia;
                return RedirectToAction("DashboardInspeccion");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "GenerarDocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "DirectorCertificacionesDcav,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult GenerarPdfAocr(int solicitudId)
        {
            if (UsarFirmaAocrNueva())
            {
                return RedirectToAction("Index", "FirmaAocr", new { solicitudId = solicitudId });
            }

            System.Diagnostics.Trace.TraceInformation(
                "[AOCR_OFICIAL_GENERATE][IN] SolicitudId=" + solicitudId +
                "; Usuario=" + ObtenerLoginActual() +
                "; Rol=" + ObtenerRolActualLog());

            var resultado = GenerarPdfOficialAocrFisico(solicitudId);
            Response.StatusCode = resultado.Ok ? 200 : resultado.Code;
            return Json(new
            {
                ok = resultado.Ok,
                message = resultado.Message,
                data = resultado.Ok ? new
                {
                    solicitudId = resultado.SolicitudId,
                    aocrId = resultado.AocrId,
                    ruta = resultado.RutaOrigen,
                    bytes = resultado.TamanioPdfFirmado,
                    urlVer = Url.Action("VerPdfAocr", "CoordinacionJefatura", new { solicitudId = solicitudId, firmado = false }),
                    urlDescargar = Url.Action("DescargarPdfAocr", "CoordinacionJefatura", new { solicitudId = solicitudId, firmado = false })
                } : null
            });
        }

        [HttpGet]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "DocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult VerPdfAocr(int solicitudId, bool firmado = false)
        {
            return RedirectToAction("VerPdf", "FirmaAocr", new { solicitudId = solicitudId, firmado = firmado });
        }

        [HttpGet]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "DocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DescargarPdfAocr(int solicitudId, bool firmado = false)
        {
            return RedirectToAction("DescargarPdf", "FirmaAocr", new { solicitudId = solicitudId, firmado = firmado });
        }

        [HttpGet]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "DocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DescargarAocrFirmado(int solicitudId)
        {
            return RedirectToAction("DescargarFirmado", "FirmaAocr", new { solicitudId = solicitudId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "GenerarDocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult FirmarAocrInstitucional(FirmarAocrInstitucionalRequest request, int solicitudId = 0)
        {
            if (UsarFirmaAocrNueva())
            {
                var idLegacy = solicitudId > 0 ? solicitudId : (request != null ? request.SolicitudId : 0);
                Response.StatusCode = 409;
                return Json(new
                {
                    ok = false,
                    message = "La firma AOCR institucional fue migrada al modulo /FirmaAocr/Index.",
                    data = new
                    {
                        solicitudId = idLegacy,
                        redirectUrl = Url.Action("Index", "FirmaAocr", new { solicitudId = idLegacy })
                    }
                });
            }

            request = request ?? new FirmarAocrInstitucionalRequest();
            if (request.SolicitudId <= 0)
            {
                request.SolicitudId = solicitudId;
            }
            if (request.CertificadoDigital == null && Request != null && Request.Files != null)
            {
                request.CertificadoDigital = Request.Files["certificadoDigital"];
            }

            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR][IN] SolicitudId=" + request.SolicitudId +
                "; AocrId=" + (request.AocrId.HasValue ? request.AocrId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; Usuario=" + ObtenerLoginActual() +
                "; Rol=" + ObtenerRolActualLog() +
                "; TieneCertificado=" + (request.CertificadoDigital != null && request.CertificadoDigital.ContentLength > 0) +
                "; TienePassword=" + !string.IsNullOrWhiteSpace(request.PasswordCertificado) +
                "; PaginaFirma=" + request.PaginaFirma +
                "; PosicionFirma=" + (request.PosicionFirma ?? string.Empty));

            var result = FirmarAocrInstitucionalSeguro(request);
            Response.StatusCode = result.Ok ? 200 : 400;
            return Json(new
            {
                ok = result.Ok,
                message = result.Message,
                data = result.Ok ? new
                {
                    solicitudId = result.SolicitudId,
                    aocrId = result.AocrId,
                    estadoAocr = result.EstadoAocrNuevo,
                    estadoSolicitud = result.EstadoSolicitudNuevo,
                    rutaFirmada = result.RutaPdfFirmado,
                    hash = result.HashPdfFirmado,
                    bytes = result.TamanioPdfFirmado,
                    urlDescarga = result.UrlDescarga
                } : null
            });
        }

        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "DocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DocumentoValidacionAocr(int solicitudId, string tipo, bool descargar = false)
        {
            if (UsarFirmaAocrNueva())
            {
                return RedirectToAction(descargar ? "DescargarPdf" : "VerPdf", "FirmaAocr", new { solicitudId = solicitudId, firmado = false });
            }

            try
            {
                RegistrarLogDocumentoValidacionRequest(solicitudId, tipo, descargar);

                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                if (solicitud == null)
                {
                    return RespuestaDocumentoValidacionNoDisponible(solicitudId, tipo, null, "La solicitud AOCR indicada no existe.", 404);
                }

                var inspeccionesSolicitud = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
                var item = ConstruirItemValidacionAocr(solicitud, inspeccionesSolicitud);
                if (item == null)
                {
                    return RespuestaDocumentoValidacionNoDisponible(solicitudId, tipo, solicitud, "No existe contexto disponible para el documento AOCR solicitado.", 404);
                }

                var tipoNormalizado = NormalizarTipoDocumento(tipo);
                if (tipoNormalizado == null)
                {
                    return RespuestaDocumentoValidacionNoDisponible(solicitudId, tipo, solicitud, "Tipo de documento AOCR no valido. Use RECONOCIMIENTO, CONDICIONES_LIMITACIONES o UNIFICADO_AOCR.", 400);
                }
                if (tipoNormalizado == null)
                {
                    return new HttpStatusCodeResult(400, "Tipo de documento AOCR no válido.");
                }

                var habilitadoPorModificacion = PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado);
                if (!item.FirmaCompleta && !habilitadoPorModificacion)
                {
                    return RespuestaDocumentoValidacionNoDisponible(solicitudId, tipoNormalizado, solicitud, "El documento AOCR aun no esta disponible. El informe tecnico debe estar aprobado por Direccion antes de visualizarlo.", 409);
                }
                if (!item.FirmaCompleta && !habilitadoPorModificacion)
                {
                    return new HttpStatusCodeResult(409, "La firma del informe técnico aún no está completa para habilitar este documento.");
                }

                RegistrarTrazabilidadDocumento(item.Solicitud, tipoNormalizado, descargar ? "DESCARGA" : "VISUALIZACION");

                var usarPlantillaOficial = item.FirmaCompleta && !habilitadoPorModificacion;
                var documentoModel = ConstruirDocumentoPdfModel(item, null, tipoNormalizado);
                var camposFaltantes = usarPlantillaOficial
                    ? ObtenerCamposObligatoriosFaltantesAocrOficial(documentoModel)
                    : ObtenerCamposObligatoriosFaltantesDocumentoAocr(documentoModel, tipoNormalizado);
                if (camposFaltantes.Any())
                {
                    return RespuestaDocumentoValidacionNoDisponible(solicitudId, tipoNormalizado, solicitud, "El documento AOCR no puede generarse porque faltan campos obligatorios: " + string.Join(", ", camposFaltantes) + ".", 409);
                }

                if (camposFaltantes.Any())
                {
                    return new HttpStatusCodeResult(409, "El documento AOCR no puede generarse porque faltan campos obligatorios: " + string.Join(", ", camposFaltantes) + ".");
                }

                if (usarPlantillaOficial)
                {
                    var rutaExistenteCertificado = item.Certificado != null ? item.Certificado.RutaDocumento : null;
                    var rutaFisicaCertificado = ResolverRutaDocumento(rutaExistenteCertificado);
                    if (!string.IsNullOrWhiteSpace(rutaFisicaCertificado) && System.IO.File.Exists(rutaFisicaCertificado))
                    {
                        var nombreArchivoExistente = ConstruirNombrePdfDocumentoValidacion(item.Solicitud, tipoNormalizado, item.Certificado != null ? item.Certificado.FechaEmision : (DateTime?)null);
                        Response.Headers["X-Content-Type-Options"] = "nosniff";
                        PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, nombreArchivoExistente);
                        RegistrarLogDocumentoValidacionOk(solicitudId, tipoNormalizado, rutaFisicaCertificado);
                        return ServirDocumentoGate7(item.Certificado.CodigoCertificado, solicitudId,
                            rutaExistenteCertificado, nombreArchivoExistente, descargar);
                    }
                }

                if (!usarPlantillaOficial && tipoNormalizado == "RECONOCIMIENTO")
                {
                    var rutaExistente = item.Certificado != null ? item.Certificado.RutaDocumento : null;
                    var rutaFisica = ResolverRutaDocumento(rutaExistente);
                    if (!string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica))
                    {
                        var nombreArchivoExistente = ConstruirNombrePdfDocumentoValidacion(item.Solicitud, tipoNormalizado, item.Certificado != null ? item.Certificado.FechaEmision : (DateTime?)null);
                        Response.Headers["X-Content-Type-Options"] = "nosniff";
                        PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, nombreArchivoExistente);
                        RegistrarLogDocumentoValidacionOk(solicitudId, tipoNormalizado, rutaFisica);
                        return ServirDocumentoGate7(item.Certificado.CodigoCertificado, solicitudId,
                            rutaExistente, nombreArchivoExistente, descargar);
                    }
                }

                if (!usarPlantillaOficial && habilitadoPorModificacion && string.Equals(tipoNormalizado, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase))
                {
                    var documentoFirmado = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(item.Solicitud.CodigoSolicitud, tipoNormalizado);
                    var rutaFirmada = ResolverRutaDocumento(documentoFirmado != null ? documentoFirmado.RutaDocumento : null);
                    if (!string.IsNullOrWhiteSpace(rutaFirmada) && System.IO.File.Exists(rutaFirmada))
                    {
                        var nombreArchivoFirmado = ConstruirNombrePdfDocumentoValidacion(item.Solicitud, tipoNormalizado, documentoFirmado != null ? documentoFirmado.FechaFirma : (DateTime?)null);
                        Response.Headers["X-Content-Type-Options"] = "nosniff";
                        PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, nombreArchivoFirmado);
                        RegistrarLogDocumentoValidacionOk(solicitudId, tipoNormalizado, rutaFirmada);
                        return ServirDocumentoGate7(documentoFirmado.CodigoFirma, solicitudId,
                            documentoFirmado.RutaDocumento, nombreArchivoFirmado, descargar);
                    }
                }

                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                    : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml";
                var pdfModel = (object)documentoModel;
                var nombreArchivo = ConstruirNombrePdfDocumentoValidacion(item.Solicitud, tipoNormalizado);

                var pdf = new ViewAsPdf(viewName, pdfModel)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    CustomSwitches = ConstruirSwitchesPdfValidacionAocr()
                };

                var pdfBytes = pdf.BuildFile(ControllerContext);
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, nombreArchivo);
                RegistrarLogDocumentoValidacionOk(solicitudId, tipoNormalizado, "PDF_GENERADO_EN_MEMORIA", pdfBytes != null ? pdfBytes.LongLength : 0);
                return File(pdfBytes, "application/pdf");
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("DocumentoValidacionAocr", exPg, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error de base de datos al generar documento AOCR. Ref: " + referencia);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("DocumentoValidacionAocr", ex, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error interno al generar documento AOCR. Ref: " + referencia);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult EditarDocumentoValidacionAocr(int solicitudId, string tipo, string modo = null)
        {
            if (UsarFirmaAocrNueva())
            {
                return RedirectToAction("Index", "FirmaAocr", new { solicitudId = solicitudId });
            }

            if (Request != null)
            {
                return EditarDocumentoValidacionAocrSeguro(solicitudId, tipo);
            }

            try
            {
                var tipoNormalizado = NormalizarTipoDocumento(tipo);
                if (tipoNormalizado == null)
                {
                    return new HttpStatusCodeResult(400, "Tipo de documento AOCR no válido.");
                }

                var item = ObtenerContextoDocumentoValidacion(solicitudId);
                if (item == null)
                {
                    return HttpNotFound("No existe contexto disponible para el documento AOCR solicitado.");
                }

                if (!item.FirmaCompleta && !PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado))
                {
                    return new HttpStatusCodeResult(409, "La firma del informe técnico aún no está completa para habilitar este documento.");
                }

                var model = ConstruirDocumentoEdicionModel(item, tipoNormalizado);
                AplicarPosicionFirmaAocr(model, tipoNormalizado);
                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/EditarReconocimientoAocr.cshtml"
                    : "~/Views/CoordinacionJefatura/EditarCondicionesLimitacionesAocr.cshtml";

                return View(viewName, model);
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("EditarDocumentoValidacionAocr", exPg, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error de base de datos al cargar la plantilla AOCR. Ref: " + referencia);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("EditarDocumentoValidacionAocr", ex, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error interno al cargar la plantilla AOCR. Ref: " + referencia);
            }
        }

        private ActionResult EditarDocumentoValidacionAocrSeguro(int solicitudId, string tipo)
        {
            var tipoNormalizado = NormalizarTipoDocumento(tipo);
            SolicitudAOCR solicitud = null;
            ValidarAocrSolicitudItemViewModel item = null;

            try
            {
                RegistrarLogAocrEdit(solicitudId, tipo, null, null, true, null, "Inicio");

                if (solicitudId <= 0)
                {
                    return VistaDocumentoValidacionNoDisponible(solicitudId, tipoNormalizado ?? tipo, null, "identificador de solicitud AOCR valido.", 400);
                }

                if (tipoNormalizado == null)
                {
                    return VistaDocumentoValidacionNoDisponible(solicitudId, tipo, null, "tipo de documento AOCR valido. Use RECONOCIMIENTO o CONDICIONES_LIMITACIONES.", 400);
                }

                solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                if (solicitud == null)
                {
                    return VistaDocumentoValidacionNoDisponible(solicitudId, tipoNormalizado, null, "solicitud AOCR existente.", 200);
                }

                item = ObtenerContextoDocumentoValidacion(solicitudId);
                if (item == null)
                {
                    return VistaDocumentoValidacionNoDisponible(
                        solicitudId,
                        tipoNormalizado,
                        solicitud,
                        "contexto documental AOCR en etapa de revision, informe tecnico firmado o flujo de condiciones/limitaciones habilitado.",
                        200);
                }

                if (!item.FirmaCompleta && !PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado))
                {
                    return VistaDocumentoValidacionNoDisponible(solicitudId, tipoNormalizado, solicitud, "firma completa del informe tecnico para habilitar este documento.", 409);
                }

                var model = ConstruirDocumentoEdicionModel(item, tipoNormalizado);
                AplicarPosicionFirmaAocr(model, tipoNormalizado);
                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/EditarReconocimientoAocr.cshtml"
                    : "~/Views/CoordinacionJefatura/EditarCondicionesLimitacionesAocr.cshtml";

                RegistrarLogAocrEdit(solicitudId, tipoNormalizado, solicitud, item, true, null, "OK");
                return View(viewName, model);
            }
            catch (PostgresException exPg)
            {
                RegistrarLogAocrEdit(solicitudId, tipoNormalizado ?? tipo, solicitud, item, false, exPg.MessageText, "Error");
                var referencia = RegistrarErrorValidacionAocr("EditarDocumentoValidacionAocr", exPg, solicitudId, null, tipo);
                return VistaDocumentoValidacionNoDisponible(solicitudId, tipoNormalizado ?? tipo, solicitud, "carga correcta de datos desde base de datos. Ref: " + referencia, 500);
            }
            catch (Exception ex)
            {
                RegistrarLogAocrEdit(solicitudId, tipoNormalizado ?? tipo, solicitud, item, false, ex.Message, "Error");
                var referencia = RegistrarErrorValidacionAocr("EditarDocumentoValidacionAocr", ex, solicitudId, null, tipo);
                return VistaDocumentoValidacionNoDisponible(solicitudId, tipoNormalizado ?? tipo, solicitud, "carga interna de la pantalla de edicion. Ref: " + referencia, 500);
            }
        }

        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult PreviewDocumentoValidacionAocr(int solicitudId, string tipo)
        {
            try
            {
                var tipoNormalizado = NormalizarTipoDocumento(tipo);
                if (tipoNormalizado == null)
                {
                    return new HttpStatusCodeResult(400, "Tipo de documento AOCR no válido.");
                }

                var item = ObtenerContextoDocumentoValidacion(solicitudId);
                if (item == null)
                {
                    return HttpNotFound("No existe contexto disponible para el documento AOCR solicitado.");
                }

                if (!item.FirmaCompleta && !PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado))
                {
                    return new HttpStatusCodeResult(409, "La firma del informe técnico aún no está completa para habilitar este documento.");
                }

                var modelEdicion = ConstruirDocumentoEdicionModel(item, tipoNormalizado);
                var documentoModel = ConstruirDocumentoPdfModel(item, modelEdicion, tipoNormalizado);
                var usarPlantillaOficial = item.FirmaCompleta && !PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado);
                var camposFaltantes = usarPlantillaOficial
                    ? ObtenerCamposObligatoriosFaltantesAocrOficial(documentoModel)
                    : ObtenerCamposObligatoriosFaltantesDocumentoAocr(documentoModel, tipoNormalizado);
                if (camposFaltantes.Any())
                {
                    return new HttpStatusCodeResult(409, "La vista previa AOCR requiere completar estos campos obligatorios: " + string.Join(", ", camposFaltantes) + ".");
                }

                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                    : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml";

                return View(viewName, documentoModel);
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("PreviewDocumentoValidacionAocr", exPg, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error de base de datos al cargar la vista previa AOCR. Ref: " + referencia);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("PreviewDocumentoValidacionAocr", ex, solicitudId, null, tipo);
                return new HttpStatusCodeResult(500, "Error interno al cargar la vista previa AOCR. Ref: " + referencia);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public JsonResult CargarDatosFirmaDigitalAocr(HttpPostedFileBase certificadoDigital, string passwordCertificado)
        {
            if (UsarFirmaAocrNueva())
            {
                Response.StatusCode = 409;
                return Json(new
                {
                    ok = false,
                    mensaje = "La carga de certificado AOCR fue migrada al modulo /FirmaAocr/Index.",
                    redirectUrl = Url.Action("Index", "FirmaAocr")
                });
            }

            string mensajeValidacion;
            if (!EsCertificadoDigitalValido(certificadoDigital, out mensajeValidacion))
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, mensaje = mensajeValidacion });
            }

            if (string.IsNullOrWhiteSpace(passwordCertificado))
            {
                Response.StatusCode = 400;
                return Json(new { ok = false, mensaje = "Debe ingresar la contraseña del certificado digital." });
            }

            using (var ms = new MemoryStream())
            {
                certificadoDigital.InputStream.CopyTo(ms);
                var info = _firmaDigitalService.LeerCertificado(ms.ToArray(), passwordCertificado);
                if (!info.Exitoso)
                {
                    Response.StatusCode = 400;
                    return Json(new { ok = false, mensaje = info.Mensaje });
                }

                return Json(new
                {
                    ok = true,
                    nombreTitular = info.NombreTitular,
                    sujetoCertificado = info.SujetoCertificado,
                    vigenteDesde = info.VigenteDesde.HasValue ? info.VigenteDesde.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty,
                    vigenteHasta = info.VigenteHasta.HasValue ? info.VigenteHasta.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "GenerarDocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "DirectorCertificacionesDcav,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult FirmarAocr(FirmarAocrRequest request, int solicitudId = 0, int? aocrId = null)
        {
            if (UsarFirmaAocrNueva())
            {
                var idLegacy = solicitudId > 0 ? solicitudId : (request != null ? request.SolicitudId : 0);
                Response.StatusCode = 409;
                return Json(new
                {
                    ok = false,
                    message = "La firma AOCR fue migrada al modulo /FirmaAocr/Index.",
                    data = new
                    {
                        solicitudId = idLegacy,
                        redirectUrl = Url.Action("Index", "FirmaAocr", new { solicitudId = idLegacy })
                    }
                });
            }

            request = request ?? new FirmarAocrRequest();
            if (request.SolicitudId <= 0)
            {
                request.SolicitudId = solicitudId;
            }
            if (!request.AocrId.HasValue)
            {
                request.AocrId = aocrId;
            }

            return JsonFirmaAocr(FirmarDocumentoValidacionAocr(request, "RECONOCIMIENTO"));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "GenerarDocumentoValidacionAocr", CodigoSolicitudParameter = "solicitudId")]
        [Authorize(Roles = "DirectorCertificacionesDcav,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult FirmarCondiciones(FirmarAocrRequest request, int solicitudId = 0, int? condicionesId = null)
        {
            if (UsarFirmaAocrNueva())
            {
                var idLegacy = solicitudId > 0 ? solicitudId : (request != null ? request.SolicitudId : 0);
                Response.StatusCode = 409;
                return Json(new
                {
                    ok = false,
                    message = "La firma de Condiciones AOCR fue migrada al modulo unificado /FirmaAocr/Index.",
                    data = new
                    {
                        solicitudId = idLegacy,
                        redirectUrl = Url.Action("Index", "FirmaAocr", new { solicitudId = idLegacy })
                    }
                });
            }

            request = request ?? new FirmarAocrRequest();
            if (request.SolicitudId <= 0)
            {
                request.SolicitudId = solicitudId;
            }
            if (!request.DocumentoId.HasValue)
            {
                request.DocumentoId = condicionesId;
            }

            return JsonFirmaAocr(FirmarDocumentoValidacionAocr(request, "CONDICIONES_LIMITACIONES"));
        }

        private ActionResult PrepararFirmaDocumentoValidacionAocr(int solicitudId, int? documentoId, string tipoDocumento, string nombreAccion)
        {
            if (!UsuarioActualPuedeFirmarDocumentoValidacionAocr())
            {
                return new HttpStatusCodeResult(403, "Solo Direccion/Jefatura tecnica puede firmar documentos AOCR finales.");
            }

            if (solicitudId <= 0)
            {
                return new HttpStatusCodeResult(400, "No se recibio un identificador de solicitud AOCR valido.");
            }

            var item = ObtenerContextoDocumentoValidacion(solicitudId);
            if (item == null || item.Solicitud == null)
            {
                return HttpNotFound("No existe contexto disponible para la solicitud AOCR indicada.");
            }

            if (!item.FirmaCompleta && !PuedeEditarCondicionesLimitacionesModificacion(item, tipoDocumento))
            {
                return new HttpStatusCodeResult(409, "El informe tecnico no se encuentra aprobado por Direccion para firmar este documento.");
            }

            if (!_aocrEstadoService.PuedeDireccionValidarAocr(item.EstadoSolicitud, item.Certificado != null ? item.Certificado.Estado : null)
                && !_informeTecnicoEstadoService.EstaAprobadoPorDireccion(item.Informe)
                && !User.IsInRole("Administrador"))
            {
                return new HttpStatusCodeResult(409, "El tramite no se encuentra en una etapa firmable por Direccion.");
            }

            var firmaExistente = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, tipoDocumento);
            if (firmaExistente != null && !string.IsNullOrWhiteSpace(firmaExistente.RutaDocumento))
            {
                TempData["Info"] = "El documento ya fue firmado previamente.";
                return RedirectToAction("DocumentoValidacionAocr", new { solicitudId = solicitudId, tipo = tipoDocumento, descargar = false });
            }

            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR][PREP] SolicitudId=" + solicitudId +
                "; DocumentoId=" + (documentoId.HasValue ? documentoId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; TipoDocumento=" + tipoDocumento +
                "; Usuario=" + ObtenerLoginActual() +
                "; Rol=" + ObtenerRolActualLog() +
                "; Accion=" + nombreAccion);

            TempData["Info"] = "Cargue el certificado digital y seleccione Firmar oficialmente AOCR.";
            return RedirectToAction("EditarDocumentoValidacionAocr", new { solicitudId = solicitudId, tipo = tipoDocumento });
        }

        private ActionResult JsonFirmaAocr(FirmaAocrResult result)
        {
            result = result ?? CrearResultadoFirmaAocr(false, 500, "No se obtuvo respuesta del proceso de firma.", 0);
            Response.StatusCode = result.Ok ? 200 : result.Code;
            return Json(new
            {
                ok = result.Ok,
                code = result.Code,
                message = result.Message,
                data = result.Ok ? new
                {
                    solicitudId = result.SolicitudId,
                    aocrId = result.AocrId,
                    estado = result.EstadoNuevo,
                    rutaFirmada = result.RutaFirmada,
                    hash = result.HashPdfFirmado,
                    bytes = result.TamanioPdfFirmado,
                    urlDescarga = result.UrlDescarga,
                    redirectUrl = result.RedirectUrl
                } : null
            });
        }

        private FirmaAocrResult FirmarDocumentoValidacionAocr(FirmarAocrRequest request, string tipoDocumentoForzado)
        {
            request = request ?? new FirmarAocrRequest();
            var solicitudId = request.SolicitudId;
            var tipoNormalizado = NormalizarTipoDocumento(!string.IsNullOrWhiteSpace(tipoDocumentoForzado) ? tipoDocumentoForzado : request.TipoDocumento);
            ValidarAocrSolicitudItemViewModel item = null;

            try
            {
                CompletarDocumentoEdicionDesdeFormulario(request);
                request.TipoDocumento = tipoNormalizado;
                AplicarAliasFormularioFirmaAocr(request);
                solicitudId = request.SolicitudId;

                RegistrarLogFirmaAocrIn(request, tipoNormalizado);

                if (solicitudId <= 0 || tipoNormalizado == null)
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, false, false, "Solicitud o tipo de documento no validos.");
                    return CrearResultadoFirmaAocr(false, 400, "No se recibieron datos validos para firmar el AOCR.", solicitudId);
                }

                if (!UsuarioActualPuedeFirmarDocumentoValidacionAocr())
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, false, false, "Rol no autorizado.");
                    return CrearResultadoFirmaAocr(false, 403, "Solo Direccion/Jefatura tecnica puede firmar documentos AOCR finales.", solicitudId);
                }

                string motivoAuth;
                if (!AocrPresentacionAuthorizationHelper.EsPermitido(
                    HttpContext,
                    "CoordinacionJefatura",
                    "GenerarDocumentoValidacionAocr",
                    out motivoAuth,
                    solicitudId))
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, false, false, motivoAuth ?? "No autorizado.");
                    return CrearResultadoFirmaAocr(false, 403, motivoAuth ?? "No autorizado para firmar el documento AOCR.", solicitudId);
                }

                item = ObtenerContextoDocumentoValidacion(solicitudId);
                if (item == null || item.Solicitud == null)
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, false, false, "Contexto documental no encontrado.");
                    return CrearResultadoFirmaAocr(false, 404, "No existe contexto disponible para la solicitud AOCR indicada.", solicitudId);
                }

                var estadoFirmable = (item.FirmaCompleta || PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado))
                    && (_aocrEstadoService.PuedeDireccionValidarAocr(item.EstadoSolicitud, item.Certificado != null ? item.Certificado.Estado : null)
                        || _informeTecnicoEstadoService.EstaAprobadoPorDireccion(item.Informe)
                        || User.IsInRole("Administrador"));
                if (!estadoFirmable)
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, false, false, "Estado no firmable.");
                    return CrearResultadoFirmaAocr(false, 409, "El tramite no se encuentra en una etapa firmable por Direccion.", solicitudId);
                }

                var firmaExistente = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, tipoNormalizado);
                if (firmaExistente != null && RutaDocumentoExiste(firmaExistente.RutaDocumento))
                {
                    RegistrarLogFirmaAocrValidation(request, true, true, true, false, "Documento firmado previamente.");
                    return CrearResultadoFirmaAocr(false, 409, "El documento AOCR ya fue firmado previamente.", solicitudId);
                }

                var certificado = request.CertificadoDigital ?? (Request != null && Request.Files != null ? Request.Files["certificadoDigital"] : null);
                var tieneCertificado = certificado != null && certificado.ContentLength > 0;
                if (!tieneCertificado)
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, estadoFirmable, false, "Certificado no recibido.");
                    return CrearResultadoFirmaAocr(false, 400, "Debe seleccionar el certificado digital .p12 o .pfx.", solicitudId);
                }

                if (string.IsNullOrWhiteSpace(request.PasswordCertificado))
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, estadoFirmable, false, "Password no recibido.");
                    return CrearResultadoFirmaAocr(false, 400, "Debe ingresar la contrasena del certificado.", solicitudId);
                }

                string mensajeCertificado;
                if (!EsCertificadoDigitalValido(certificado, out mensajeCertificado))
                {
                    RegistrarLogFirmaAocrValidation(request, false, false, estadoFirmable, false, mensajeCertificado);
                    return CrearResultadoFirmaAocr(false, 400, mensajeCertificado, solicitudId);
                }

                var posicionFirmaVisual = ConstruirPosicionFirmaVisualPdfRequerida(request);
                if (posicionFirmaVisual == null || !posicionFirmaVisual.EsValida)
                {
                    RegistrarLogFirmaAocrValidation(request, false, true, estadoFirmable, false, "Coordenadas invalidas.");
                    return CrearResultadoFirmaAocr(false, 400, "Debe guardar una posicion de firma valida antes de continuar.", solicitudId);
                }

                var documentoModel = ConstruirDocumentoPdfModel(item, request, tipoNormalizado);
                var usarPlantillaOficial = item.FirmaCompleta && !PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado);
                var camposFaltantes = usarPlantillaOficial
                    ? ObtenerCamposObligatoriosFaltantesAocrOficial(documentoModel)
                    : ObtenerCamposObligatoriosFaltantesDocumentoAocr(documentoModel, tipoNormalizado);
                if (camposFaltantes.Any())
                {
                    RegistrarLogFirmaAocrValidation(request, false, true, estadoFirmable, false, "Campos obligatorios faltantes: " + string.Join(", ", camposFaltantes));
                    return CrearResultadoFirmaAocr(false, 409, "Primero complete los campos obligatorios antes de firmar: " + string.Join(", ", camposFaltantes) + ".", solicitudId);
                }

                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                    : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml";
                var pdf = new ViewAsPdf(viewName, (object)documentoModel)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    CustomSwitches = ConstruirSwitchesPdfValidacionAocr()
                };

                var pdfBytes = pdf.BuildFile(ControllerContext);
                var pdfExiste = pdfBytes != null && pdfBytes.LongLength > 0;
                RegistrarLogFirmaAocrPdfOrigen("PDF_GENERADO_EN_MEMORIA", pdfExiste, pdfBytes != null ? pdfBytes.LongLength : 0);
                if (!pdfExiste)
                {
                    RegistrarLogFirmaAocrValidation(request, false, true, estadoFirmable, false, "PDF origen no generado.");
                    return CrearResultadoFirmaAocr(false, 409, "Primero debe generar el PDF AOCR antes de firmar.", solicitudId);
                }

                RegistrarLogFirmaAocrValidation(request, true, true, estadoFirmable, true, "Validacion correcta.");
                RegistrarLogFirmaAocrCertificado(certificado, true);

                byte[] certificadoBytes;
                using (var ms = new MemoryStream())
                {
                    certificado.InputStream.CopyTo(ms);
                    certificadoBytes = ms.ToArray();
                }

                var infoCertificado = _firmaDigitalService.LeerCertificado(certificadoBytes, request.PasswordCertificado);
                if (!infoCertificado.Exitoso)
                {
                    return CrearResultadoFirmaAocr(false, 400, "No se pudo abrir el certificado digital. Verifique archivo y contrasena.", solicitudId);
                }

                var nombreFirmante = PrimerValorNoVacio(request.FirmanteNombre, request.NombreFirmante, infoCertificado.NombreTitular);
                var cargoFirmante = PrimerValorNoVacio(request.FirmanteCargo, request.CargoFirmante);
                request.FirmanteNombre = nombreFirmante;
                request.FirmanteCargo = cargoFirmante;
                var motivoFirma = tipoNormalizado == "RECONOCIMIENTO"
                    ? "Firma digital del reconocimiento AOCR"
                    : "Firma digital del documento de condiciones y limitaciones AOCR";
                var contenidoQr = ConstruirContenidoQrFirmaAocr(item, request, tipoNormalizado, infoCertificado, nombreFirmante);

                var resultadoFirma = _firmaDigitalService.FirmarPdf(
                    pdfBytes,
                    certificadoBytes,
                    request.PasswordCertificado,
                    nombreFirmante,
                    motivoFirma,
                    "Sistema AOCR DGAC",
                    "AOCR_FIRMANTE",
                    contenidoQr,
                    posicionFirmaVisual);

                if (!resultadoFirma.Exitoso || resultadoFirma.PdfFirmado == null || resultadoFirma.PdfFirmado.LongLength <= 0)
                {
                    return CrearResultadoFirmaAocr(false, 400, resultadoFirma.Mensaje ?? "No se pudo firmar digitalmente el AOCR.", solicitudId);
                }

                var nombreArchivo = Path.GetFileNameWithoutExtension(ConstruirNombrePdfDocumentoValidacion(item.Solicitud, tipoNormalizado)) + "_Firmado.pdf";
                var rutaDocumentoFirmado = GuardarDocumentoFirmadoAocr(solicitudId, tipoNormalizado, nombreArchivo, resultadoFirma.PdfFirmado);
                var rutaFisicaFirmada = ResolverRutaDocumento(rutaDocumentoFirmado);
                var existeFirmada = !string.IsNullOrWhiteSpace(rutaFisicaFirmada) && System.IO.File.Exists(rutaFisicaFirmada);
                var bytesFirmado = existeFirmada ? new FileInfo(rutaFisicaFirmada).Length : 0;
                var hashFirmado = !string.IsNullOrWhiteSpace(resultadoFirma.HashSha256)
                    ? resultadoFirma.HashSha256
                    : (existeFirmada ? CalcularSha256Hex(System.IO.File.ReadAllBytes(rutaFisicaFirmada)) : null);

                RegistrarLogFirmaAocrPdfFirmado(rutaDocumentoFirmado, existeFirmada, bytesFirmado, hashFirmado);
                if (!existeFirmada || bytesFirmado <= 0 || string.IsNullOrWhiteSpace(hashFirmado))
                {
                    return CrearResultadoFirmaAocr(false, 500, "La firma se genero, pero no se pudo verificar el archivo PDF firmado.", solicitudId);
                }

                var estadoAnterior = item.Solicitud.Estado;
                RegistrarFirmaDigitalAocr(
                    item,
                    request,
                    tipoNormalizado,
                    nombreArchivo,
                    rutaDocumentoFirmado,
                    hashFirmado,
                    contenidoQr,
                    infoCertificado,
                    nombreFirmante,
                    usarPlantillaOficial);

                NotificarDocumentoAocrFirmadoSeguro(solicitudId, tipoNormalizado);
                GuardarPosicionFirmaAocr(item, request, tipoNormalizado, posicionFirmaVisual, "PUNTERO");

                var estadoNuevo = tipoNormalizado == "RECONOCIMIENTO" ? "AOCR_FIRMADO_DIRECCION" : "CONDICIONES_FIRMADAS";
                System.Diagnostics.Trace.TraceInformation(
                    "[FIRMA_AOCR][DB_UPDATE] AocrId=" + ObtenerAocrIdLog(item) +
                    "; EstadoAnterior=" + (estadoAnterior ?? string.Empty) +
                    "; EstadoNuevo=" + estadoNuevo +
                    "; FilasAfectadas=1");

                var finalizacionDocumentoValidacion = _aocrFinalizacionService.IntentarFinalizarEmision(
                    solicitudId,
                    ObtenerUsuarioActualIdSeguro(),
                    RutaDocumentoExiste);
                if (finalizacionDocumentoValidacion != null && finalizacionDocumentoValidacion.Finalizado)
                {
                    _aocrProcesoNotificacionService.NotificarProcesoAocrFinalizado(solicitudId);
                }

                if (item.Solicitud.TipoSolicitud.GetValueOrDefault() == 3
                    && string.Equals(tipoNormalizado, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(EstadoSolicitud.Normalizar(item.Solicitud.Estado), EstadoSolicitud.EnviadoDcav, StringComparison.OrdinalIgnoreCase))
                {
                    string mensajeCambio;
                    _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                        solicitudId,
                        EstadoSolicitud.FirmadoDcav,
                        "Condiciones y Limitaciones firmadas por DCAV/DGAC.",
                        ObtenerUsuarioActualIdSeguro(),
                        _ => true,
                        out mensajeCambio);
                }

                var urlDescarga = Url.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = solicitudId, tipo = tipoNormalizado, descargar = true });
                var redirectUrl = Url.Action("ValidarAocr", "CoordinacionJefatura", new { solicitudId = solicitudId });
                System.Diagnostics.Trace.TraceInformation(
                    "[FIRMA_AOCR][OK] SolicitudId=" + solicitudId +
                    "; AocrId=" + ObtenerAocrIdLog(item) +
                    "; RutaFirmada=" + (rutaDocumentoFirmado ?? string.Empty) +
                    "; Hash=" + (hashFirmado ?? string.Empty) +
                    "; Bytes=" + bytesFirmado);

                return new FirmaAocrResult
                {
                    Ok = true,
                    Code = 200,
                    Message = tipoNormalizado == "RECONOCIMIENTO" ? "AOCR firmada correctamente." : "Condiciones y Limitaciones firmadas correctamente.",
                    SolicitudId = solicitudId,
                    AocrId = ObtenerAocrIdValor(item),
                    RutaOrigen = "PDF_GENERADO_EN_MEMORIA",
                    RutaFirmada = rutaDocumentoFirmado,
                    HashPdfFirmado = hashFirmado,
                    TamanioPdfFirmado = bytesFirmado,
                    EstadoNuevo = estadoNuevo,
                    UrlDescarga = urlDescarga,
                    RedirectUrl = redirectUrl
                };
            }
            catch (PostgresException exPg)
            {
                RegistrarLogFirmaAocrError(solicitudId, exPg.MessageText, exPg);
                var referencia = RegistrarErrorValidacionAocr("FirmarAocr", exPg, solicitudId, null, tipoNormalizado);
                return CrearResultadoFirmaAocr(false, 500, "Error de base de datos al firmar AOCR. Ref: " + referencia, solicitudId);
            }
            catch (Exception ex)
            {
                RegistrarLogFirmaAocrError(solicitudId, ex.Message, ex);
                var referencia = RegistrarErrorValidacionAocr("FirmarAocr", ex, solicitudId, null, tipoNormalizado);
                return CrearResultadoFirmaAocr(false, 500, "Error interno al firmar AOCR. Ref: " + referencia, solicitudId);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult GenerarDocumentoValidacionAocr(int? solicitudId = null, string tipo = null)
        {
            var id = solicitudId.GetValueOrDefault();
            if (id > 0)
            {
                TempData["Info"] = "Esta accion fue migrada a la firma institucional AOCR.";
                return RedirectToAction("Index", "FirmaAocr", new { solicitudId = id });
            }

            return VistaDocumentoValidacionNoDisponible(
                0,
                tipo,
                null,
                "Esta accion debe ejecutarse desde la pantalla Firma institucional AOCR.",
                200);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult GenerarDocumentoValidacionAocr(AocrDocumentoEdicionViewModel model, string accion = null, HttpPostedFileBase certificadoDigital = null, string passwordCertificado = null)
        {
            if (UsarFirmaAocrNueva())
            {
                var idLegacy = model != null ? model.SolicitudId : 0;
                return RedirectToAction("Index", "FirmaAocr", new { solicitudId = idLegacy });
            }

            try
            {
                model = CompletarDocumentoEdicionDesdeFormulario(model);
                var tipoNormalizado = NormalizarTipoDocumento(model != null ? model.TipoDocumento : null);
                if (model == null || model.SolicitudId <= 0 || tipoNormalizado == null)
                {
                    return RespuestaDocumentoValidacionNoDisponible(model != null ? model.SolicitudId : 0, model != null ? model.TipoDocumento : null, null, "No se recibieron datos validos para generar el documento AOCR.", 400);
                }

                System.Diagnostics.Trace.TraceInformation(
                    "[GENERAR_VALIDACION_AOCR][IN] SolicitudId=" + model.SolicitudId +
                    "; Tipo=" + tipoNormalizado +
                    "; Usuario=" + ObtenerLoginActual() +
                    "; Rol=" + ObtenerRolActualLog());

                string motivoAuth;
                if (!AocrPresentacionAuthorizationHelper.EsPermitido(
                    HttpContext,
                    "CoordinacionJefatura",
                    "GenerarDocumentoValidacionAocr",
                    out motivoAuth,
                    model.SolicitudId))
                {
                    return new HttpStatusCodeResult(403, motivoAuth ?? "No autorizado para generar el documento AOCR.");
                }

                var item = ObtenerContextoDocumentoValidacion(model.SolicitudId);
                if (item == null)
                {
                    return HttpNotFound("No existe contexto disponible para el documento AOCR solicitado.");
                }

                var documentoModel = ConstruirDocumentoPdfModel(item, model, tipoNormalizado);
                var usarPlantillaOficial = item.FirmaCompleta && !PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado);
                var camposFaltantes = usarPlantillaOficial
                    ? ObtenerCamposObligatoriosFaltantesAocrOficial(documentoModel)
                    : ObtenerCamposObligatoriosFaltantesDocumentoAocr(documentoModel, tipoNormalizado);
                if (camposFaltantes.Any())
                {
                    return RespuestaDocumentoValidacionNoDisponible(model.SolicitudId, tipoNormalizado, item.Solicitud, "No se puede generar el documento AOCR porque faltan campos obligatorios: " + string.Join(", ", camposFaltantes) + ".", 400);
                }

                var viewName = tipoNormalizado == "RECONOCIMIENTO"
                    ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                    : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml";
                var pdfModel = (object)documentoModel;
                var nombreArchivo = ConstruirNombrePdfDocumentoValidacion(item.Solicitud, tipoNormalizado);
                var descargar = string.Equals(accion, "DESCARGAR", StringComparison.OrdinalIgnoreCase);
                var firmarDigitalmente = string.Equals(accion, "FIRMAR_DESCARGAR", StringComparison.OrdinalIgnoreCase);

                if (firmarDigitalmente && !UsuarioActualPuedeFirmarDocumentoValidacionAocr())
                {
                    return new HttpStatusCodeResult(403, "Solo los roles institucionales autorizados pueden firmar digitalmente este documento AOCR.");
                }

                RegistrarTrazabilidadDocumento(item.Solicitud, tipoNormalizado, firmarDigitalmente ? "PDF_PARA_FIRMA_AOCR" : (descargar ? "DESCARGA_DESDE_PLANTILLA" : "VISUALIZACION_DESDE_PLANTILLA"));

                var pdf = new ViewAsPdf(viewName, pdfModel)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    CustomSwitches = ConstruirSwitchesPdfValidacionAocr()
                };

                var pdfBytes = pdf.BuildFile(ControllerContext);
                System.Diagnostics.Trace.TraceInformation(
                    "[GENERAR_VALIDACION_AOCR][OK] SolicitudId=" + model.SolicitudId +
                    "; Tipo=" + tipoNormalizado +
                    "; Ruta=PDF_GENERADO_EN_MEMORIA" +
                    "; Bytes=" + (pdfBytes != null ? pdfBytes.LongLength : 0));
                if (firmarDigitalmente)
                {
                    // Si el usuario no sube certificado, intentar usar el certificado institucional.
                    byte[] certificadoBytesInstitucional = null;
                    string passwordInstitucional = null;
                    var usandoInstitucional = false;
                    if (certificadoDigital == null || certificadoDigital.ContentLength <= 0)
                    {
                        string errorInstitucional;
                        if (!TryCargarCertificadoInstitucional(out certificadoBytesInstitucional, out passwordInstitucional, out errorInstitucional))
                        {
                            return new HttpStatusCodeResult(400, errorInstitucional);
                        }
                        usandoInstitucional = true;
                    }
                    else
                    {
                        string mensajeValidacion;
                        if (!EsCertificadoDigitalValido(certificadoDigital, out mensajeValidacion))
                        {
                            return new HttpStatusCodeResult(400, mensajeValidacion);
                        }
                    }

                    using (var ms = new MemoryStream())
                    {
                        byte[] certificadoBytes;
                        string passwordCert;
                        if (usandoInstitucional)
                        {
                            certificadoBytes = certificadoBytesInstitucional;
                            passwordCert = passwordInstitucional;
                        }
                        else
                        {
                            certificadoDigital.InputStream.CopyTo(ms);
                            certificadoBytes = ms.ToArray();
                            passwordCert = passwordCertificado;
                        }

                        var infoCertificado = _firmaDigitalService.LeerCertificado(certificadoBytes, passwordCert);
                        if (!infoCertificado.Exitoso)
                        {
                            return new HttpStatusCodeResult(400, infoCertificado.Mensaje);
                        }

                        var nombreFirmante = !string.IsNullOrWhiteSpace(model.FirmanteNombre)
                            ? model.FirmanteNombre
                            : infoCertificado.NombreTitular;
                        var motivoFirma = tipoNormalizado == "RECONOCIMIENTO"
                            ? "Firma digital del reconocimiento AOCR"
                            : "Firma digital del documento de condiciones y limitaciones AOCR";
                        var contenidoQr = ConstruirContenidoQrFirmaAocr(item, model, tipoNormalizado, infoCertificado, nombreFirmante);
                        var posicionFirmaVisual = ConstruirPosicionFirmaVisualPdf(model);

                        var resultadoFirma = _firmaDigitalService.FirmarPdf(
                            pdfBytes,
                            certificadoBytes,
                            passwordCert,
                            nombreFirmante,
                            motivoFirma,
                            "Sistema AOCR DGAC",
                            "AOCR_FIRMANTE",
                            contenidoQr,
                            posicionFirmaVisual);

                        if (!resultadoFirma.Exitoso)
                        {
                            return new HttpStatusCodeResult(400, resultadoFirma.Mensaje);
                        }

                        pdfBytes = resultadoFirma.PdfFirmado;
                        descargar = true;
                        nombreArchivo = Path.GetFileNameWithoutExtension(nombreArchivo) + "_Firmado.pdf";

                        var rutaDocumentoFirmado = GuardarDocumentoFirmadoAocr(model.SolicitudId, tipoNormalizado, nombreArchivo, pdfBytes);
                        RegistrarFirmaDigitalAocr(
                            item,
                            model,
                            tipoNormalizado,
                            nombreArchivo,
                            rutaDocumentoFirmado,
                            resultadoFirma.HashSha256,
                            contenidoQr,
                            infoCertificado,
                            nombreFirmante,
                            usarPlantillaOficial);

                        NotificarDocumentoAocrFirmadoSeguro(model.SolicitudId, tipoNormalizado);
                        System.Diagnostics.Trace.TraceInformation(
                            (string.Equals(tipoNormalizado, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase) ? "[FIRMA_CONDICIONES][OK]" : "[FIRMA_AOCR][OK]") +
                            " SolicitudId=" + model.SolicitudId +
                            "; AocrId=" + (item != null && item.Certificado != null ? item.Certificado.CodigoCertificado.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                            "; CondicionesId=" + (item != null && item.Certificado != null ? item.Certificado.CodigoCertificado.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                            "; RutaFirmada=" + (rutaDocumentoFirmado ?? string.Empty) +
                            "; Hash=" + (resultadoFirma.HashSha256 ?? string.Empty) +
                            "; Bytes=" + (pdfBytes != null ? pdfBytes.LongLength : 0) +
                            "; Existe=" + RutaDocumentoExiste(rutaDocumentoFirmado));

                        var finalizacionDescargaFirma = _aocrFinalizacionService.IntentarFinalizarEmision(
                            model.SolicitudId,
                            ObtenerUsuarioActualIdSeguro(),
                            RutaDocumentoExiste);
                        if (finalizacionDescargaFirma != null && finalizacionDescargaFirma.Finalizado)
                        {
                            _aocrProcesoNotificacionService.NotificarProcesoAocrFinalizado(model.SolicitudId);
                        }

                        if (posicionFirmaVisual != null && posicionFirmaVisual.EsValida)
                        {
                            GuardarPosicionFirmaAocr(item, model, tipoNormalizado, posicionFirmaVisual, "PUNTERO");
                        }

                        if (item != null
                            && item.Solicitud != null
                            && item.Solicitud.TipoSolicitud.GetValueOrDefault() == 3
                            && string.Equals(tipoNormalizado, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(EstadoSolicitud.Normalizar(item.Solicitud.Estado), EstadoSolicitud.EnviadoDcav, StringComparison.OrdinalIgnoreCase))
                        {
                            string mensajeCambio;
                            _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                                model.SolicitudId,
                                EstadoSolicitud.FirmadoDcav,
                                "Condiciones y Limitaciones firmadas por DCAV/DGAC.",
                                ObtenerUsuarioActualIdSeguro(),
                                _ => true,
                                out mensajeCambio);
                        }
                    }
                }

                Response.Headers["X-Content-Type-Options"] = "nosniff";
                PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, nombreArchivo);
                return File(pdfBytes, "application/pdf");
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("GenerarDocumentoValidacionAocr", exPg, model != null ? (int?)model.SolicitudId : null, model != null ? model.InspeccionId : null, model != null ? model.TipoDocumento : null);
                return new HttpStatusCodeResult(500, "Error de base de datos al generar documento AOCR. Ref: " + referencia);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("GenerarDocumentoValidacionAocr", ex, model != null ? (int?)model.SolicitudId : null, model != null ? model.InspeccionId : null, model != null ? model.TipoDocumento : null);
                return new HttpStatusCodeResult(500, "Error interno al generar documento AOCR. Ref: " + referencia);
            }
        }

        private string ConstruirNombrePdfDocumentoValidacion(SolicitudAOCR solicitud, string tipoDocumento, DateTime? fecha = null)
        {
            var numeroSolicitud = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? solicitud.NumeroSolicitud
                : (solicitud != null ? solicitud.CodigoSolicitud.ToString(CultureInfo.InvariantCulture) : string.Empty);
            var nombreOperador = solicitud == null
                ? string.Empty
                : PdfFileNameHelper.PrimerValorNoVacio(
                    PdfFileNameHelper.CombinarSegmentos(solicitud.Ruc, solicitud.NombreOperador),
                    PdfFileNameHelper.CombinarSegmentos(solicitud.Ruc, solicitud.NombreComercial),
                    PdfFileNameHelper.CombinarSegmentos(solicitud.Ruc, solicitud.RazonSocial),
                    solicitud.NombreOperador,
                    solicitud.NombreComercial,
                    solicitud.RazonSocial,
                    solicitud.Ruc);
            var fechaDocumento = fecha ?? (solicitud != null ? (solicitud.UpdatedAt ?? solicitud.FechaSolicitud ?? solicitud.CreatedAt) : (DateTime?)null);

            return string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase)
                ? PdfFileNameHelper.CrearNombreReconocimientoAocr(numeroSolicitud, nombreOperador, fechaDocumento)
                : PdfFileNameHelper.CrearNombreCondicionesLimitaciones(numeroSolicitud, nombreOperador, fechaDocumento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DireccionJefaturaTecnica,DirectorGeneral,Administrador")]
        public JsonResult GuardarPosicionFirmaAocr(AocrFirmaPosicionEdicionViewModel model)
        {
            if (UsarFirmaAocrNueva())
            {
                Response.StatusCode = 409;
                return Json(new
                {
                    ok = false,
                    mensaje = "La firma por puntero fue desactivada. Use la posicion institucional fija en /FirmaAocr/Index.",
                    redirectUrl = Url.Action("Index", "FirmaAocr", new { solicitudId = model != null ? model.SolicitudId : 0 })
                });
            }

            try
            {
                var tipoNormalizado = NormalizarTipoDocumento(model != null ? model.TipoDocumento : null);
                if (model == null || tipoNormalizado == null)
                {
                    Response.StatusCode = 400;
                    return Json(new { ok = false, mensaje = "No se recibieron coordenadas validas para la firma AOCR." });
                }

                var item = ObtenerContextoDocumentoValidacion(model.SolicitudId);
                if (item == null)
                {
                    Response.StatusCode = 404;
                    return Json(new { ok = false, mensaje = "No existe contexto disponible para el documento AOCR solicitado." });
                }

                if (!item.FirmaCompleta)
                {
                    Response.StatusCode = 409;
                    return Json(new { ok = false, mensaje = "La firma del informe técnico aún no está completa para habilitar este documento." });
                }

                var posicionFirmaVisual = ConstruirPosicionFirmaVisualPdf(model);
                if (posicionFirmaVisual == null || !posicionFirmaVisual.EsValida)
                {
                    Response.StatusCode = 400;
                    return Json(new { ok = false, mensaje = "Las coordenadas de firma son invalidas o incompletas." });
                }

                GuardarPosicionFirmaAocr(item, model, tipoNormalizado, posicionFirmaVisual, "PUNTERO");
                return Json(new { ok = true, mensaje = "La posicion de firma fue guardada correctamente." });
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("GuardarPosicionFirmaAocr", exPg, model != null ? (int?)model.SolicitudId : null, model != null ? model.InspeccionId : null, model != null ? model.TipoDocumento : null);
                Response.StatusCode = 500;
                return Json(new { ok = false, mensaje = "Error de base de datos al guardar la posicion de firma. Ref: " + referencia });
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("GuardarPosicionFirmaAocr", ex, model != null ? (int?)model.SolicitudId : null, model != null ? model.InspeccionId : null, model != null ? model.TipoDocumento : null);
                Response.StatusCode = 500;
                return Json(new { ok = false, mensaje = "Error interno al guardar la posicion de firma. Ref: " + referencia });
            }
        }

        private List<ValidarAocrSolicitudItemViewModel> ConstruirItemsValidacionAocr()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            var inspecciones = _inspeccionDao.ListarTodas() ?? new List<Inspeccion>();
            var inspeccionesPorSolicitud = inspecciones
                .Where(i => i != null)
                .GroupBy(i => i.CodigoSolicitud)
                .ToDictionary(g => g.Key, g => g.ToList());

            return solicitudes
                .Select(solicitud => ConstruirItemValidacionAocr(
                    solicitud,
                    inspeccionesPorSolicitud.ContainsKey(solicitud.CodigoSolicitud)
                        ? inspeccionesPorSolicitud[solicitud.CodigoSolicitud]
                        : new List<Inspeccion>()))
                .Where(item => item != null)
                .OrderByDescending(item => item.FechaDisponibilidad ?? item.FechaFirmaFinal ?? item.Solicitud.FechaSolicitud ?? DateTime.MinValue)
                .ThenByDescending(item => item.Solicitud.CodigoSolicitud)
                .ToList();
        }

        private ValidarAocrSolicitudItemViewModel ConstruirItemValidacionAocr(SolicitudAOCR solicitud, IEnumerable<Inspeccion> inspeccionesSolicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud.Estado);
            var inspecciones = (inspeccionesSolicitud ?? new List<Inspeccion>())
                .Where(i => i != null)
                .ToList();

            var informes = new List<dynamic>();
            foreach (var inspeccion in inspecciones)
            {
                try
                {
                    var informe = _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion);
                    if (informe != null)
                    {
                        informes.Add(new
                        {
                            Inspeccion = inspeccion,
                            Informe = informe
                        });
                    }
                }
                catch (Exception ex)
                {
                    RegistrarErrorValidacionAocr(
                        "ConstruirItemValidacionAocr.ObtenerUltimoPorInspeccion",
                        ex,
                        solicitud.CodigoSolicitud,
                        inspeccion.CodigoInspeccion);
                    throw;
                }
            }

            informes = informes
                .OrderByDescending(x => x.Informe.FechaFirma2 ?? x.Informe.FechaFirma1 ?? x.Informe.FechaFinalizacion ?? x.Informe.UpdatedAt ?? DateTime.MinValue)
                .ToList();

            var esModificacionDirecta = EsSolicitudModificacionDirectaSinInspeccion(solicitud, estadoSolicitud);
            var informeFirmado = informes
                .FirstOrDefault(x => InformeTecnicoHabilitaAocr(x.Informe));

            var firmaCompleta = !esModificacionDirecta && informeFirmado != null;

            // Incluir tambien solicitudes con informe tecnico firmado que aun no
            // han transicionado al estado AOCR En Revision (flujo incompleto en datos).
            var estadoPermitidoConFirma = firmaCompleta
                && (string.Equals(estadoSolicitud, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase));

            var estadoIncluido = string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, "ENVIADO_A_JEFATURA", StringComparison.OrdinalIgnoreCase)
                || _aocrEstadoService.PuedeDireccionValidarAocr(estadoSolicitud, null)
                || estadoPermitidoConFirma
                || esModificacionDirecta;

            if (!estadoIncluido)
            {
                return null;
            }

            var contextoActivo = informeFirmado ?? informes.FirstOrDefault();
            var certificado = _certificadoDao.ObtenerPorSolicitud(solicitud.CodigoSolicitud);
            var aeronaves = _aeronaveSolicitudDao.ObtenerPorSolicitud(solicitud.CodigoSolicitud) ?? new List<AeronaveSolicitud>();
            var numeroAocr = certificado != null && !string.IsNullOrWhiteSpace(certificado.NumeroCertificado)
                ? certificado.NumeroCertificado
                : GenerarNumeroAocr(solicitud.CodigoSolicitud, (contextoActivo != null ? contextoActivo.Informe.FechaFirma2 : null) ?? DateTime.Now);

            var item = new ValidarAocrSolicitudItemViewModel
            {
                Solicitud = solicitud,
                Inspeccion = contextoActivo != null ? contextoActivo.Inspeccion : null,
                Informe = contextoActivo != null ? contextoActivo.Informe : null,
                Certificado = certificado,
                Aeronaves = aeronaves,
                FirmaCompleta = firmaCompleta,
                NumeroAocr = numeroAocr,
                FechaFirmaFinal = contextoActivo != null ? contextoActivo.Informe.FechaFirma2 : null,
                FechaDisponibilidad = (contextoActivo != null ? contextoActivo.Informe.FechaFirma2 : null) ?? (certificado != null ? certificado.UpdatedAt : null) ?? solicitud.UpdatedAt,
                Firmantes = ConstruirFirmantes(contextoActivo != null ? contextoActivo.Informe : null),
                EstadoSolicitud = estadoSolicitud,
                ListoParaEnvioRt = string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
            };

            item.Documentos = ConstruirDocumentosValidacion(item);
            var camposObligatoriosFaltantes = new List<string>();
            if (esModificacionDirecta)
            {
                camposObligatoriosFaltantes.AddRange(ObtenerCamposObligatoriosFaltantesDocumentoAocr(ConstruirDocumentoPdfModel(item, null, "CONDICIONES_LIMITACIONES"), "CONDICIONES_LIMITACIONES"));
            }
            else
            {
                camposObligatoriosFaltantes.AddRange(ObtenerCamposObligatoriosFaltantesDocumentoAocr(ConstruirDocumentoPdfModel(item, null, "RECONOCIMIENTO"), "RECONOCIMIENTO"));
                camposObligatoriosFaltantes.AddRange(ObtenerCamposObligatoriosFaltantesDocumentoAocr(ConstruirDocumentoPdfModel(item, null, "CONDICIONES_LIMITACIONES"), "CONDICIONES_LIMITACIONES"));
            }
            item.CamposFaltantes = string.Join(", ", camposObligatoriosFaltantes.Where(nombre => !string.IsNullOrWhiteSpace(nombre)).Distinct(StringComparer.OrdinalIgnoreCase));
            var tieneCamposObligatoriosPendientes = !string.IsNullOrWhiteSpace(item.CamposFaltantes);
            item.PuedeEnviarADirdac = item.FirmaCompleta
                && item.Documentos.All(d => d != null && d.Disponible)
                && !tieneCamposObligatoriosPendientes
                && string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase);
            item.PuedeAprobarFinal = item.FirmaCompleta
                && item.Documentos.All(d => d != null && d.Disponible)
                && !tieneCamposObligatoriosPendientes
                && (string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, "ENVIADO_A_JEFATURA", StringComparison.OrdinalIgnoreCase));
            item.PuedeSolicitarModificacion = item.FirmaCompleta
                && !item.ListoParaEnvioRt
                && (string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, "ENVIADO_A_JEFATURA", StringComparison.OrdinalIgnoreCase));
            item.PuedeContinuar = item.FirmaCompleta
                && item.Documentos.All(d => d.Disponible)
                && (string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, "ENVIADO_A_JEFATURA", StringComparison.OrdinalIgnoreCase));

            if (!item.FirmaCompleta && esModificacionDirecta)
            {
                if (string.Equals(estadoSolicitud, EstadoSolicitud.GeneradoCondicionesLimitaciones, StringComparison.OrdinalIgnoreCase))
                {
                    item.MensajeEstado = "Condiciones y Limitaciones listas para preparación";
                    item.MensajeAdvertencia = "La modificación no requiere nueva inspección. El documento institucional puede completarse desde esta bandeja.";
                }
                else if (string.Equals(estadoSolicitud, EstadoSolicitud.EnRevisionCoordinadorFinal, StringComparison.OrdinalIgnoreCase))
                {
                    item.MensajeEstado = "Condiciones y Limitaciones en revisión final";
                    item.MensajeAdvertencia = "La coordinación debe revisar el documento antes de enviarlo a DCAV/DGAC.";
                }
                else if (string.Equals(estadoSolicitud, EstadoSolicitud.EnviadoDcav, StringComparison.OrdinalIgnoreCase))
                {
                    item.MensajeEstado = "Pendiente de firma DCAV/DGAC";
                    item.MensajeAdvertencia = "El documento ya está listo para firma institucional.";
                }
                else
                {
                    item.MensajeEstado = "Documento firmado disponible";
                    item.MensajeAdvertencia = "La modificación fue firmada y ya puede descargarse por el RT.";
                }
            }
            else if (!item.FirmaCompleta)
            {
                item.MensajeEstado = "Pendiente de firma del informe técnico";
                item.MensajeAdvertencia = "La firma institucional del informe técnico aún no está completa; por eso los documentos AOCR no se habilitan todavía.";
            }
            else if (_informeTecnicoEstadoService.EstaAprobadoPorDireccion(item.Informe)
                && string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase))
            {
                item.MensajeEstado = "Informe Tecnico aprobado por Direccion.";
                item.MensajeAdvertencia = "AOCR y Condiciones disponibles para validacion y firma institucional.";
            }
            else if (item.PuedeEnviarADirdac)
            {
                item.MensajeEstado = "La AOCR está lista para revisión de Coordinación.";
                item.MensajeAdvertencia = string.IsNullOrWhiteSpace(item.CamposFaltantes)
                    ? "Revise la vista previa, registre observaciones si aplica o apruebe el envío a DIRDAC."
                    : "La AOCR tiene campos obligatorios pendientes: " + item.CamposFaltantes + ". Solicite modificación al Inspector.";
            }
            else if (item.PuedeAprobarFinal)
            {
                item.MensajeEstado = "La AOCR está pendiente de firma DIRDAC.";
                item.MensajeAdvertencia = "El documento ya fue remitido a revisión institucional final.";
            }
            else if (item.Documentos.All(d => d.Disponible))
            {
                item.MensajeEstado = item.ListoParaEnvioRt
                    ? "Listo para envio al RT"
                    : "Documentos AOCR listos para validacion";
            }
            else
            {
                item.MensajeEstado = "Documentacion AOCR incompleta";
                item.MensajeAdvertencia = string.Join(" ", item.Documentos.Where(d => !d.Disponible).Select(d => d.Observacion).Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            RegistrarTrazaAocrCoordinacion(item);

            return item;
        }

        private bool InformeTecnicoHabilitaAocr(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return false;
            }

            return _informeTecnicoEstadoService.EstaAprobadoPorDireccion(informe)
                || (informe.Finalizado && informe.FirmadoInspector && informe.FirmadoDirdac);
        }

        private ValidarAocrViewModel ConstruirValidarAocrViewModel(AocrContextoResolucion contexto, ValidarAocrSolicitudItemViewModel item)
        {
            var solicitud = item != null ? item.Solicitud : (contexto != null ? contexto.Solicitud : null);
            var certificado = item != null ? item.Certificado : (contexto != null ? contexto.Aocr : null);
            var informe = item != null ? item.Informe : (contexto != null ? contexto.InformeTecnico : null);
            var solicitudId = solicitud != null ? solicitud.CodigoSolicitud : (contexto != null && contexto.SolicitudId.HasValue ? contexto.SolicitudId.Value : 0);
            var firmaAocr = solicitudId > 0 ? _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO") : null;
            var firmaCondiciones = solicitudId > 0 ? _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES") : null;
            var aocrRutaDisponible = RutaDocumentoExiste(certificado != null ? certificado.RutaDocumento : null)
                || RutaDocumentoExiste(firmaAocr != null ? firmaAocr.RutaDocumento : null);
            var condicionesRutaDisponible = RutaDocumentoExiste(firmaCondiciones != null ? firmaCondiciones.RutaDocumento : null)
                || aocrRutaDisponible;
            var informeAprobado = _informeTecnicoEstadoService.EstaAprobadoPorDireccion(informe);
            var aocrFirmada = firmaAocr != null && RutaDocumentoExiste(firmaAocr.RutaDocumento);
            var condicionesFirmadas = firmaCondiciones != null && RutaDocumentoExiste(firmaCondiciones.RutaDocumento);
            var puedeFirmar = UsuarioActualPuedeFirmarDocumentoValidacionAocr()
                && _aocrEstadoService.PuedeDireccionValidarAocr(solicitud != null ? solicitud.Estado : null, certificado != null ? certificado.Estado : null);

            var model = new ValidarAocrViewModel
            {
                SolicitudId = solicitudId,
                CodigoSolicitud = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud) ? solicitud.NumeroSolicitud : solicitudId.ToString(CultureInfo.InvariantCulture),
                Operadora = solicitud != null ? (solicitud.RazonSocial ?? solicitud.NombreOperador ?? solicitud.NombreComercial) : string.Empty,
                EstadoSolicitud = solicitud != null ? solicitud.Estado : (contexto != null ? contexto.EstadoSolicitud : string.Empty),
                CodigoAocr = item != null ? item.NumeroAocr : (certificado != null ? certificado.NumeroCertificado : string.Empty),
                FechaGeneracionAocr = certificado != null ? (certificado.FechaEmision ?? certificado.CreatedAt ?? certificado.UpdatedAt) : null,
                InformeAprobadoDireccion = informeAprobado,
                AocrExiste = certificado != null && certificado.CodigoCertificado > 0,
                AocrFirmada = aocrFirmada,
                CondicionesExisten = condicionesRutaDisponible || (item != null && item.FirmaCompleta),
                CondicionesFirmadas = condicionesFirmadas || (aocrFirmada && DocumentoAocrEsUnificado(item)),
                DocumentoUnificado = DocumentoAocrEsUnificado(item),
                UsuarioFirma = ObtenerLoginActual(),
                RolFirma = ObtenerRolActualLog(),
                FirmaDigitalCargada = false
            };

            model.PuedeGenerarAocr = informeAprobado && !aocrRutaDisponible && UsuarioActualPuedeFirmarDocumentoValidacionAocr();
            model.PuedeVerAocr = aocrRutaDisponible;
            model.PuedeDescargarAocr = aocrRutaDisponible;
            model.PuedeFirmarAocr = aocrRutaDisponible && puedeFirmar && !aocrFirmada;
            model.PuedeFirmarCondiciones = condicionesRutaDisponible && puedeFirmar && !model.CondicionesFirmadas;
            model.PuedeFinalizar = model.AocrFirmada && model.CondicionesFirmadas;
            model.DocumentosFirma = ConstruirDocumentosFirmaAocr(model, item, aocrRutaDisponible, condicionesRutaDisponible, firmaAocr, firmaCondiciones);
            return model;
        }

        private IList<DocumentoFirmaAocrViewModel> ConstruirDocumentosFirmaAocr(
            ValidarAocrViewModel model,
            ValidarAocrSolicitudItemViewModel item,
            bool aocrRutaDisponible,
            bool condicionesRutaDisponible,
            AocrFirmaDocumento firmaAocr,
            AocrFirmaDocumento firmaCondiciones)
        {
            var documentos = new List<DocumentoFirmaAocrViewModel>();
            var solicitudId = model != null ? model.SolicitudId : 0;
            var fechaAocr = model != null ? model.FechaGeneracionAocr : null;
            var fechaCondiciones = item != null ? item.FechaDisponibilidad : fechaAocr;

            documentos.Add(new DocumentoFirmaAocrViewModel
            {
                Tipo = "RECONOCIMIENTO",
                Titulo = "AOCR oficial",
                Descripcion = "Reconocimiento de Certificado de Explotador de Servicios Aereos.",
                Estado = model != null && model.AocrFirmada ? "FIRMADO" : (aocrRutaDisponible ? "DISPONIBLE" : "PENDIENTE"),
                EstadoVisible = model != null && model.AocrFirmada ? "Firmado" : (aocrRutaDisponible ? "Disponible para firma" : "Documento no generado"),
                Fecha = firmaAocr != null && firmaAocr.FechaFirma != DateTime.MinValue ? firmaAocr.FechaFirma : fechaAocr,
                RutaDisponible = aocrRutaDisponible,
                PuedeGenerar = model != null && model.PuedeGenerarAocr,
                PuedeVer = aocrRutaDisponible,
                PuedeDescargar = aocrRutaDisponible,
                PuedeFirmar = model != null && model.PuedeFirmarAocr,
                EstaFirmado = model != null && model.AocrFirmada,
                EsUnificado = model != null && model.DocumentoUnificado,
                UrlGenerar = Url.Action("Index", "FirmaAocr", new { solicitudId = solicitudId }),
                UrlVer = aocrRutaDisponible ? Url.Action("VerPdf", "FirmaAocr", new { solicitudId = solicitudId, firmado = false }) : null,
                UrlDescargar = aocrRutaDisponible ? Url.Action("DescargarPdf", "FirmaAocr", new { solicitudId = solicitudId, firmado = false }) : null,
                UrlFirmar = model != null && model.PuedeFirmarAocr ? Url.Action("Index", "FirmaAocr", new { solicitudId = solicitudId }) : null,
                ErrorDocumento = aocrRutaDisponible ? null : "Genere el documento antes de visualizarlo o firmarlo."
            });

            documentos.Add(new DocumentoFirmaAocrViewModel
            {
                Tipo = "CONDICIONES_LIMITACIONES",
                Titulo = "Condiciones y Limitaciones",
                Descripcion = "Condiciones asociadas al reconocimiento AOCR.",
                Estado = model != null && model.CondicionesFirmadas ? "FIRMADO" : (condicionesRutaDisponible ? "DISPONIBLE" : "PENDIENTE"),
                EstadoVisible = model != null && model.CondicionesFirmadas ? "Firmado" : (condicionesRutaDisponible ? "Disponible para firma" : "Documento no generado"),
                Fecha = firmaCondiciones != null && firmaCondiciones.FechaFirma != DateTime.MinValue ? firmaCondiciones.FechaFirma : fechaCondiciones,
                RutaDisponible = condicionesRutaDisponible,
                PuedeGenerar = model != null && model.InformeAprobadoDireccion && !condicionesRutaDisponible && UsuarioActualPuedeFirmarDocumentoValidacionAocr(),
                PuedeVer = condicionesRutaDisponible,
                PuedeDescargar = condicionesRutaDisponible,
                PuedeFirmar = model != null && model.PuedeFirmarCondiciones,
                EstaFirmado = model != null && model.CondicionesFirmadas,
                EsUnificado = model != null && model.DocumentoUnificado,
                UrlGenerar = Url.Action("Index", "FirmaAocr", new { solicitudId = solicitudId }),
                UrlVer = condicionesRutaDisponible ? Url.Action("VerPdf", "FirmaAocr", new { solicitudId = solicitudId, firmado = false }) : null,
                UrlDescargar = condicionesRutaDisponible ? Url.Action("DescargarPdf", "FirmaAocr", new { solicitudId = solicitudId, firmado = false }) : null,
                UrlFirmar = model != null && model.PuedeFirmarCondiciones ? Url.Action("Index", "FirmaAocr", new { solicitudId = solicitudId }) : null,
                ErrorDocumento = condicionesRutaDisponible ? null : "Genere Condiciones y Limitaciones antes de visualizar o firmar."
            });

            return documentos;
        }

        private FirmaAocrInstitucionalViewModel ConstruirFirmaAocrInstitucionalViewModel(int solicitudId)
        {
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return new FirmaAocrInstitucionalViewModel
                {
                    SolicitudId = solicitudId,
                    EstadoSolicitud = "No registrada",
                    EstadoAocr = "No generado",
                    MotivoBloqueo = "La solicitud AOCR indicada no existe.",
                    UrlVolverBandeja = Url.Action("DashboardInspeccion", "CoordinacionJefatura")
                };
            }

            var item = ObtenerContextoDocumentoValidacion(solicitudId);
            var certificado = item != null ? item.Certificado : _certificadoDao.ObtenerPorSolicitud(solicitudId);
            var firma = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
            var rutaPdf = certificado != null ? certificado.RutaDocumento : null;
            var rutaPdfFisica = ResolverRutaDocumento(rutaPdf);
            var pdfExiste = !string.IsNullOrWhiteSpace(rutaPdfFisica) && System.IO.File.Exists(rutaPdfFisica);
            var rutaFirmada = firma != null ? firma.RutaDocumento : null;
            var rutaFirmadaFisica = ResolverRutaDocumento(rutaFirmada);
            var pdfFirmadoExiste = !string.IsNullOrWhiteSpace(rutaFirmadaFisica) && System.IO.File.Exists(rutaFirmadaFisica);
            var informe = item != null ? item.Informe : null;
            var informeAprobado = _informeTecnicoEstadoService.EstaAprobadoPorDireccion(informe);
            var documentoModel = item != null ? ConstruirDocumentoPdfModel(item, null, "RECONOCIMIENTO") : null;
            var camposFaltantes = item != null
                ? ObtenerCamposObligatoriosFaltantesAocrOficial(documentoModel)
                : new List<string> { "Contexto documental AOCR" };
            var documentoCompleto = !camposFaltantes.Any();
            var usuarioPuedeFirmar = UsuarioActualPuedeFirmarDocumentoValidacionAocr();
            var estadoFirmable = item != null
                && item.Solicitud != null
                && (item.FirmaCompleta
                    || informeAprobado
                    || _aocrEstadoService.PuedeDireccionValidarAocr(item.EstadoSolicitud, certificado != null ? certificado.Estado : null)
                    || User.IsInRole("Administrador"));

            var motivo = string.Empty;
            if (!usuarioPuedeFirmar)
            {
                motivo = "Solo Direccion / DIRDAC puede firmar el AOCR final.";
            }
            else if (item == null)
            {
                motivo = "No existe contexto documental AOCR disponible para esta solicitud.";
            }
            else if (!informeAprobado && !item.FirmaCompleta && !User.IsInRole("Administrador"))
            {
                motivo = "El informe tecnico aun no esta aprobado por Direccion.";
            }
            else if (!documentoCompleto)
            {
                motivo = "AOCR incompleto.";
            }
            else if (!pdfExiste)
            {
                motivo = "Primero genere el PDF oficial AOCR.";
            }
            else if (pdfFirmadoExiste)
            {
                motivo = "El AOCR ya fue firmado oficialmente.";
            }
            else if (!estadoFirmable)
            {
                motivo = "El tramite no se encuentra en estado firmable.";
            }

            var model = new FirmaAocrInstitucionalViewModel
            {
                SolicitudId = solicitudId,
                AocrId = certificado != null ? certificado.CodigoCertificado : 0,
                NumeroSolicitud = !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud) ? solicitud.NumeroSolicitud : solicitudId.ToString(CultureInfo.InvariantCulture),
                Operadora = PrimerTextoAocrNoVacio(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial, "No registrado"),
                CodigoAocr = item != null ? item.NumeroAocr : certificado != null ? certificado.NumeroCertificado : GenerarNumeroAocr(solicitudId, DateTime.Now),
                EstadoSolicitud = solicitud.Estado,
                EstadoAocr = pdfFirmadoExiste ? "Firmado" : (pdfExiste ? "Pendiente de firma" : "No generado"),
                InformeTecnicoEstado = informeAprobado ? "Aprobado por Direccion" : "Pendiente",
                ResultadoTecnico = informe != null ? PrimerTextoAocrNoVacio(informe.Resultado, informe.EstadoInforme, "No registrado") : "No registrado",
                ResponsableFirma = "Direccion / DIRDAC",
                UsuarioActual = ObtenerLoginActual(),
                RolActual = ObtenerRolActualLog(),
                CargoFirmante = "Direccion General de Aviacion Civil",
                FechaGeneracion = certificado != null ? (certificado.FechaEmision ?? certificado.UpdatedAt ?? certificado.CreatedAt) : null,
                FechaFirma = firma != null && firma.FechaFirma != DateTime.MinValue ? (DateTime?)firma.FechaFirma : null,
                NombreArchivoPdf = pdfExiste ? Path.GetFileName(rutaPdfFisica) : null,
                NombreArchivoFirmado = pdfFirmadoExiste ? Path.GetFileName(rutaFirmadaFisica) : null,
                PdfExiste = pdfExiste,
                PdfFirmadoExiste = pdfFirmadoExiste,
                TamanioPdf = pdfExiste ? new FileInfo(rutaPdfFisica).Length : 0,
                TamanioPdfFirmado = pdfFirmadoExiste ? new FileInfo(rutaFirmadaFisica).Length : 0,
                HashPdfFirmado = firma != null ? firma.HashDocumento : null,
                RutaPdf = rutaPdf,
                RutaPdfFirmado = rutaFirmada,
                PuedeGenerar = usuarioPuedeFirmar && item != null && documentoCompleto && !pdfFirmadoExiste,
                PuedeRegenerar = usuarioPuedeFirmar && pdfExiste && !pdfFirmadoExiste,
                PuedeFirmar = usuarioPuedeFirmar && estadoFirmable && documentoCompleto && pdfExiste && !pdfFirmadoExiste,
                InformeAprobado = informeAprobado,
                DocumentoCompleto = documentoCompleto,
                MotivoBloqueo = motivo,
                CamposFaltantes = camposFaltantes,
                UrlGenerar = Url.Action("GenerarPdfAocr", "CoordinacionJefatura"),
                UrlVerPdf = pdfExiste ? Url.Action("VerPdfAocr", "CoordinacionJefatura", new { solicitudId = solicitudId, firmado = false }) : null,
                UrlDescargarPdf = pdfExiste ? Url.Action("DescargarPdfAocr", "CoordinacionJefatura", new { solicitudId = solicitudId, firmado = false }) : null,
                UrlVerPdfFirmado = pdfFirmadoExiste ? Url.Action("VerPdfAocr", "CoordinacionJefatura", new { solicitudId = solicitudId, firmado = true }) : null,
                UrlDescargarFirmado = pdfFirmadoExiste ? Url.Action("DescargarAocrFirmado", "CoordinacionJefatura", new { solicitudId = solicitudId }) : null,
                UrlFirmar = Url.Action("FirmarAocrInstitucional", "CoordinacionJefatura"),
                UrlVolverBandeja = Url.Action("DashboardInspeccion", "CoordinacionJefatura"),
                UrlCompletarDatos = Url.Action("Index", "FirmaAocr", new { solicitudId = solicitudId })
            };

            return model;
        }

        private FirmaAocrResult GenerarPdfOficialAocrFisico(int solicitudId)
        {
            try
            {
                if (solicitudId <= 0)
                {
                    return CrearResultadoFirmaAocr(false, 400, "No se recibio un identificador de solicitud AOCR valido.", solicitudId);
                }

                if (!UsuarioActualPuedeFirmarDocumentoValidacionAocr())
                {
                    return CrearResultadoFirmaAocr(false, 403, "Solo Direccion / DIRDAC puede generar el PDF oficial AOCR final.", solicitudId);
                }

                var item = ObtenerContextoDocumentoValidacion(solicitudId);
                if (item == null || item.Solicitud == null)
                {
                    return CrearResultadoFirmaAocr(false, 404, "No existe contexto documental AOCR para generar el PDF oficial.", solicitudId);
                }

                var firmaExistente = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
                if (firmaExistente != null && RutaDocumentoExiste(firmaExistente.RutaDocumento))
                {
                    return CrearResultadoFirmaAocr(false, 409, "El AOCR ya esta firmado; no se puede regenerar el PDF preliminar.", solicitudId);
                }

                var documentoModel = ConstruirDocumentoPdfModel(item, null, "RECONOCIMIENTO");
                var camposFaltantes = ObtenerCamposObligatoriosFaltantesAocrOficial(documentoModel);
                System.Diagnostics.Trace.TraceInformation(
                    "[AOCR_OFICIAL_GENERATE][VALIDATION] SolicitudId=" + solicitudId +
                    "; Ok=" + !camposFaltantes.Any() +
                    "; CamposFaltantes=" + string.Join(", ", camposFaltantes));

                if (camposFaltantes.Any())
                {
                    return CrearResultadoFirmaAocr(false, 409, "AOCR incompleto. Faltan campos obligatorios: " + string.Join(", ", camposFaltantes) + ".", solicitudId);
                }

                var pdf = new ViewAsPdf("~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml", (object)documentoModel)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    CustomSwitches = ConstruirSwitchesPdfValidacionAocr()
                };

                var pdfBytes = pdf.BuildFile(ControllerContext);
                if (pdfBytes == null || pdfBytes.LongLength <= 0)
                {
                    return CrearResultadoFirmaAocr(false, 500, "No se pudo generar el PDF oficial AOCR.", solicitudId);
                }

                var nombreArchivo = ConstruirNombrePdfDocumentoValidacion(item.Solicitud, "RECONOCIMIENTO");
                var rutaRelativa = GuardarPdfOficialAocr(solicitudId, nombreArchivo, pdfBytes);
                var rutaFisica = ResolverRutaDocumento(rutaRelativa);
                var existe = !string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica);
                var bytes = existe ? new FileInfo(rutaFisica).Length : 0;
                if (!existe || bytes <= 0)
                {
                    return CrearResultadoFirmaAocr(false, 500, "El PDF oficial se genero, pero no se pudo verificar el archivo fisico.", solicitudId);
                }

                SincronizarCertificadoPdfOficial(item, documentoModel, rutaRelativa);

                System.Diagnostics.Trace.TraceInformation(
                    "[AOCR_OFICIAL_GENERATE][OK] SolicitudId=" + solicitudId +
                    "; AocrId=" + ObtenerAocrIdLog(item) +
                    "; Ruta=" + rutaRelativa +
                    "; Bytes=" + bytes +
                    "; Paginas=2");

                return new FirmaAocrResult
                {
                    Ok = true,
                    Code = 200,
                    Message = "PDF oficial AOCR generado correctamente.",
                    SolicitudId = solicitudId,
                    AocrId = ObtenerAocrIdValor(item),
                    RutaOrigen = rutaRelativa,
                    TamanioPdfFirmado = bytes
                };
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("GenerarPdfAocr", exPg, solicitudId);
                return CrearResultadoFirmaAocr(false, 500, "Error de base de datos al generar PDF AOCR. Ref: " + referencia, solicitudId);
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("GenerarPdfAocr", ex, solicitudId);
                return CrearResultadoFirmaAocr(false, 500, "Error interno al generar PDF AOCR. Ref: " + referencia, solicitudId);
            }
        }

        private ActionResult ServirPdfAocrInstitucional(int solicitudId, bool firmado, bool descargar)
        {
            var item = ObtenerContextoDocumentoValidacion(solicitudId);
            var solicitud = item != null ? item.Solicitud : _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return HttpNotFound("La solicitud AOCR indicada no existe.");
            }

            string rutaRelativa;
            DateTime? fechaDocumento = null;
            if (firmado)
            {
                var firma = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
                rutaRelativa = firma != null ? firma.RutaDocumento : null;
                fechaDocumento = firma != null && firma.FechaFirma != DateTime.MinValue ? (DateTime?)firma.FechaFirma : null;
            }
            else
            {
                var certificado = item != null ? item.Certificado : _certificadoDao.ObtenerPorSolicitud(solicitudId);
                rutaRelativa = certificado != null ? certificado.RutaDocumento : null;
                fechaDocumento = certificado != null ? certificado.FechaEmision : null;
            }

            var rutaFisica = ResolverRutaDocumento(rutaRelativa);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
            {
                return new HttpStatusCodeResult(404, firmado ? "No existe AOCR firmado para descargar." : "No existe PDF oficial AOCR generado.");
            }

            var nombre = firmado
                ? Path.GetFileNameWithoutExtension(ConstruirNombrePdfDocumentoValidacion(solicitud, "RECONOCIMIENTO", fechaDocumento)) + "-firmado.pdf"
                : ConstruirNombrePdfDocumentoValidacion(solicitud, "RECONOCIMIENTO", fechaDocumento);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, nombre);
            var documentoId = firmado
                ? (_aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO")?.CodigoFirma ?? solicitudId)
                : ((item != null ? item.Certificado : _certificadoDao.ObtenerPorSolicitud(solicitudId))?.CodigoCertificado ?? solicitudId);
            return ServirDocumentoGate7(documentoId, solicitudId, rutaRelativa, nombre, descargar);
        }

        private ActionResult ServirDocumentoGate7(int documentoId, int solicitudId, string ruta, string nombre, bool descargar)
        {
            var seguro = new DocumentoSeguroService(new[] { Server.MapPath("~/App_Data") },
                evento => System.Diagnostics.Trace.TraceInformation("[GATE7] " + evento + ";Usuario=" + (User != null ? User.Identity.Name : string.Empty)));
            var archivo = seguro.Resolver(documentoId, solicitudId, solicitudId, ruta, nombre, ResolverRutaDocumento);
            if (!archivo.EsValido)
                return archivo.Error == DocumentoSeguroError.NoEncontrado || archivo.Error == DocumentoSeguroError.Vacio
                    ? (ActionResult)HttpNotFound(archivo.MensajePublico)
                    : new HttpStatusCodeResult(403, archivo.MensajePublico);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, archivo.NombreDescarga);
            return File(archivo.RutaFisica, archivo.Mime);
        }

        private FirmarAocrInstitucionalResult FirmarAocrInstitucionalSeguro(FirmarAocrInstitucionalRequest request)
        {
            var solicitudId = request != null ? request.SolicitudId : 0;
            var logRequest = new FirmarAocrRequest { SolicitudId = solicitudId };
            try
            {
                if (request == null || solicitudId <= 0)
                {
                    RegistrarLogFirmaAocrValidation(logRequest, false, false, false, false, "Solicitud invalida.");
                    return CrearResultadoFirmaInstitucional(false, "No se recibieron datos validos para firmar el AOCR.", solicitudId);
                }

                if (!UsuarioActualPuedeFirmarDocumentoValidacionAocr())
                {
                    RegistrarLogFirmaAocrValidation(logRequest, false, false, false, false, "Rol no autorizado.");
                    return CrearResultadoFirmaInstitucional(false, "Solo Direccion / DIRDAC puede firmar el AOCR final.", solicitudId);
                }

                var item = ObtenerContextoDocumentoValidacion(solicitudId);
                if (item == null || item.Solicitud == null)
                {
                    RegistrarLogFirmaAocrValidation(logRequest, false, false, false, false, "Contexto no encontrado.");
                    return CrearResultadoFirmaInstitucional(false, "No existe contexto documental AOCR para firmar.", solicitudId);
                }

                var firmaExistente = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
                if (firmaExistente != null && RutaDocumentoExiste(firmaExistente.RutaDocumento))
                {
                    RegistrarLogFirmaAocrValidation(logRequest, true, true, true, false, "Documento firmado previamente.");
                    return CrearResultadoFirmaInstitucional(false, "El AOCR ya fue firmado oficialmente.", solicitudId);
                }

                var certificadoAocr = item.Certificado ?? _certificadoDao.ObtenerPorSolicitud(solicitudId);
                var rutaPdfOrigen = certificadoAocr != null ? certificadoAocr.RutaDocumento : null;
                var rutaFisicaOrigen = ResolverRutaDocumento(rutaPdfOrigen);
                var pdfExiste = !string.IsNullOrWhiteSpace(rutaFisicaOrigen) && System.IO.File.Exists(rutaFisicaOrigen);
                var bytesOrigen = pdfExiste ? new FileInfo(rutaFisicaOrigen).Length : 0;
                RegistrarLogFirmaAocrPdfOrigen(rutaPdfOrigen, pdfExiste, bytesOrigen);
                if (!pdfExiste || bytesOrigen <= 0)
                {
                    RegistrarLogFirmaAocrValidation(logRequest, false, false, false, false, "PDF origen no existe.");
                    return CrearResultadoFirmaInstitucional(false, "Primero debe generar el PDF oficial AOCR.", solicitudId);
                }

                var documentoModel = ConstruirDocumentoPdfModel(item, null, "RECONOCIMIENTO");
                var camposFaltantes = ObtenerCamposObligatoriosFaltantesAocrOficial(documentoModel);
                var estadoFirmable = item.FirmaCompleta
                    || _informeTecnicoEstadoService.EstaAprobadoPorDireccion(item.Informe)
                    || _aocrEstadoService.PuedeDireccionValidarAocr(item.EstadoSolicitud, certificadoAocr != null ? certificadoAocr.Estado : null)
                    || User.IsInRole("Administrador");
                if (!estadoFirmable || camposFaltantes.Any())
                {
                    RegistrarLogFirmaAocrValidation(logRequest, pdfExiste, false, estadoFirmable, false, camposFaltantes.Any() ? string.Join(", ", camposFaltantes) : "Estado no firmable.");
                    return CrearResultadoFirmaInstitucional(false, camposFaltantes.Any()
                        ? "AOCR incompleto. Faltan campos obligatorios: " + string.Join(", ", camposFaltantes) + "."
                        : "El tramite no se encuentra en estado firmable.", solicitudId);
                }

                var archivoCertificado = request.CertificadoDigital;
                if (archivoCertificado == null || archivoCertificado.ContentLength <= 0)
                {
                    RegistrarLogFirmaAocrValidation(logRequest, pdfExiste, false, estadoFirmable, false, "Certificado no recibido.");
                    return CrearResultadoFirmaInstitucional(false, "Debe seleccionar el certificado digital .p12 o .pfx.", solicitudId);
                }

                if (string.IsNullOrWhiteSpace(request.PasswordCertificado))
                {
                    RegistrarLogFirmaAocrValidation(logRequest, pdfExiste, false, estadoFirmable, false, "Password no recibido.");
                    return CrearResultadoFirmaInstitucional(false, "Debe ingresar la contrasena del certificado.", solicitudId);
                }

                string mensajeCertificado;
                if (!EsCertificadoDigitalValido(archivoCertificado, out mensajeCertificado))
                {
                    RegistrarLogFirmaAocrValidation(logRequest, pdfExiste, false, estadoFirmable, false, mensajeCertificado);
                    return CrearResultadoFirmaInstitucional(false, mensajeCertificado, solicitudId);
                }

                byte[] certificadoBytes;
                using (var ms = new MemoryStream())
                {
                    archivoCertificado.InputStream.CopyTo(ms);
                    certificadoBytes = ms.ToArray();
                }

                var infoCertificado = _firmaDigitalService.LeerCertificado(certificadoBytes, request.PasswordCertificado);
                RegistrarLogFirmaAocrCertificado(archivoCertificado, true, infoCertificado != null && infoCertificado.Exitoso, infoCertificado != null && infoCertificado.Exitoso);
                if (infoCertificado == null || !infoCertificado.Exitoso)
                {
                    return CrearResultadoFirmaInstitucional(false, "No se pudo abrir el certificado digital. Verifique archivo y contrasena.", solicitudId);
                }

                RegistrarLogFirmaAocrValidation(logRequest, pdfExiste, true, estadoFirmable, true, "Validacion correcta.");

                var pdfBytes = System.IO.File.ReadAllBytes(rutaFisicaOrigen);
                var nombreFirmante = PrimerValorNoVacio(infoCertificado.NombreTitular, ObtenerLoginActual());
                var edicion = ConstruirDocumentoEdicionModel(item, "RECONOCIMIENTO");
                edicion.FirmanteNombre = nombreFirmante;
                edicion.FirmanteCargo = PrimerValorNoVacio(edicion.FirmanteCargo, "Direccion General de Aviacion Civil");
                var contenidoQr = ConstruirContenidoQrFirmaAocr(item, edicion, "RECONOCIMIENTO", infoCertificado, nombreFirmante);
                var posicionFirma = ConstruirPosicionFirmaInstitucionalFija(request);

                var resultadoFirma = _firmaDigitalService.FirmarPdf(
                    pdfBytes,
                    certificadoBytes,
                    request.PasswordCertificado,
                    nombreFirmante,
                    "Firma institucional AOCR",
                    "Sistema AOCR DGAC",
                    "AOCR_FIRMANTE",
                    contenidoQr,
                    posicionFirma);

                if (!resultadoFirma.Exitoso || resultadoFirma.PdfFirmado == null || resultadoFirma.PdfFirmado.LongLength <= 0)
                {
                    return CrearResultadoFirmaInstitucional(false, resultadoFirma.Mensaje ?? "No se pudo firmar digitalmente el AOCR.", solicitudId);
                }

                var nombreArchivoFirmado = Path.GetFileNameWithoutExtension(ConstruirNombrePdfDocumentoValidacion(item.Solicitud, "RECONOCIMIENTO")) + "_Firmado.pdf";
                var rutaDocumentoFirmado = GuardarDocumentoFirmadoAocr(solicitudId, "RECONOCIMIENTO", nombreArchivoFirmado, resultadoFirma.PdfFirmado);
                var rutaFisicaFirmada = ResolverRutaDocumento(rutaDocumentoFirmado);
                var existeFirmada = !string.IsNullOrWhiteSpace(rutaFisicaFirmada) && System.IO.File.Exists(rutaFisicaFirmada);
                var bytesFirmado = existeFirmada ? new FileInfo(rutaFisicaFirmada).Length : 0;
                var hashFirmado = !string.IsNullOrWhiteSpace(resultadoFirma.HashSha256)
                    ? resultadoFirma.HashSha256
                    : (existeFirmada ? CalcularSha256Hex(System.IO.File.ReadAllBytes(rutaFisicaFirmada)) : null);
                RegistrarLogFirmaAocrPdfFirmado(rutaDocumentoFirmado, existeFirmada, bytesFirmado, hashFirmado);

                if (!existeFirmada || bytesFirmado <= 0 || string.IsNullOrWhiteSpace(hashFirmado))
                {
                    return CrearResultadoFirmaInstitucional(false, "La firma se genero, pero no se pudo verificar el archivo PDF firmado.", solicitudId);
                }

                var estadoAnterior = item.Solicitud.Estado;
                RegistrarFirmaDigitalAocr(
                    item,
                    edicion,
                    "RECONOCIMIENTO",
                    nombreArchivoFirmado,
                    rutaDocumentoFirmado,
                    hashFirmado,
                    contenidoQr,
                    infoCertificado,
                    nombreFirmante,
                    false,
                    bytesFirmado,
                    "DIRECCION_DIRDAC");

                _aocrProcesoNotificacionService.NotificarAocrFirmado(solicitudId);
                GuardarPosicionFirmaAocr(item, edicion, "RECONOCIMIENTO", posicionFirma, "FIJA_INSTITUCIONAL");
                System.Diagnostics.Trace.TraceInformation(
                    "[FIRMA_AOCR][DB_UPDATE] AocrId=" + ObtenerAocrIdLog(item) +
                    "; EstadoAnterior=" + (estadoAnterior ?? string.Empty) +
                    "; EstadoNuevo=AOCR_FIRMADO_DIRECCION; FilasAfectadas=1");

                var finalizacion = _aocrFinalizacionService.IntentarFinalizarEmision(
                    solicitudId,
                    ObtenerUsuarioActualIdSeguro(),
                    RutaDocumentoExiste);
                if (finalizacion != null && finalizacion.Finalizado)
                {
                    _aocrProcesoNotificacionService.NotificarProcesoAocrFinalizado(solicitudId);
                }
                var estadoSolicitudNuevo = finalizacion != null && !string.IsNullOrWhiteSpace(finalizacion.EstadoNuevo)
                    ? finalizacion.EstadoNuevo
                    : EstadoSolicitud.AOCR_Legalizado;
                var urlDescarga = Url.Action("DescargarAocrFirmado", "CoordinacionJefatura", new { solicitudId = solicitudId });

                System.Diagnostics.Trace.TraceInformation(
                    "[FIRMA_AOCR][OK] SolicitudId=" + solicitudId +
                    "; AocrId=" + ObtenerAocrIdLog(item) +
                    "; RutaFirmada=" + (rutaDocumentoFirmado ?? string.Empty) +
                    "; Hash=" + (hashFirmado ?? string.Empty) +
                    "; Bytes=" + bytesFirmado);

                return new FirmarAocrInstitucionalResult
                {
                    Ok = true,
                    Message = "AOCR firmada oficialmente por Direccion / DIRDAC.",
                    SolicitudId = solicitudId,
                    AocrId = ObtenerAocrIdValor(item),
                    RutaPdfOrigen = rutaPdfOrigen,
                    RutaPdfFirmado = rutaDocumentoFirmado,
                    HashPdfFirmado = hashFirmado,
                    TamanioPdfFirmado = bytesFirmado,
                    EstadoAocrNuevo = "AOCR_FIRMADO_DIRECCION",
                    EstadoSolicitudNuevo = estadoSolicitudNuevo,
                    UrlDescarga = urlDescarga
                };
            }
            catch (Exception ex)
            {
                RegistrarLogFirmaAocrError(solicitudId, ex.Message, ex);
                return CrearResultadoFirmaInstitucional(false, "Error interno al firmar AOCR. " + ex.Message, solicitudId);
            }
        }

        private static FirmarAocrInstitucionalResult CrearResultadoFirmaInstitucional(bool ok, string message, int solicitudId)
        {
            return new FirmarAocrInstitucionalResult
            {
                Ok = ok,
                Message = message,
                SolicitudId = solicitudId
            };
        }

        private PosicionFirmaVisualPdf ConstruirPosicionFirmaInstitucionalFija(FirmarAocrInstitucionalRequest request)
        {
            return new PosicionFirmaVisualPdf
            {
                NumeroPagina = request != null && request.PaginaFirma > 0 ? request.PaginaFirma : 1,
                PosicionXRatio = 0.02f,
                PosicionYRatio = 0.06f,
                AnchoRatio = 0.94f,
                AltoRatio = 0.82f
            };
        }

        private string GuardarPdfOficialAocr(int solicitudId, string nombreArchivo, byte[] contenido)
        {
            var carpetaRelativa = "~/App_Data/Uploads/AOCR/Oficiales/" + solicitudId;
            var carpetaAbsoluta = Server.MapPath(carpetaRelativa);
            if (!Directory.Exists(carpetaAbsoluta))
            {
                Directory.CreateDirectory(carpetaAbsoluta);
            }

            var nombreSeguro = "aocr_oficial_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + solicitudId + ".pdf";
            var rutaAbsoluta = Path.Combine(carpetaAbsoluta, nombreSeguro);
            System.IO.File.WriteAllBytes(rutaAbsoluta, contenido ?? new byte[0]);
            return VirtualPathUtility.ToAbsolute(carpetaRelativa.TrimStart('~') + "/" + nombreSeguro);
        }

        private void SincronizarCertificadoPdfOficial(ValidarAocrSolicitudItemViewModel item, AocrDocumentoPdfViewModel documentoModel, string rutaRelativa)
        {
            if (item == null || item.Solicitud == null)
            {
                return;
            }

            var certificado = item.Certificado ?? _certificadoDao.ObtenerPorSolicitud(item.Solicitud.CodigoSolicitud);
            if (certificado == null || certificado.CodigoCertificado <= 0)
            {
                certificado = new Certificado
                {
                    CodigoSolicitud = item.Solicitud.CodigoSolicitud,
                    NumeroCertificado = item.NumeroAocr,
                    Tipo = "AOCR",
                    Estado = "GENERADO",
                    FechaEmision = DateTime.Now,
                    FechaVencimiento = documentoModel != null ? documentoModel.FechaVencimiento : null,
                    RutaDocumento = rutaRelativa,
                    EmitidoPor = ObtenerLoginActual(),
                    CreatedAt = DateTime.Now,
                    CreatedBy = ObtenerUsuarioActualIdSeguro(),
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = ObtenerUsuarioActualIdSeguro()
                };
                var id = _certificadoDao.Crear(certificado);
                certificado.CodigoCertificado = id;
                item.Certificado = certificado;
                return;
            }

            certificado.NumeroCertificado = string.IsNullOrWhiteSpace(certificado.NumeroCertificado) ? item.NumeroAocr : certificado.NumeroCertificado;
            certificado.RutaDocumento = rutaRelativa;
            certificado.RutaPdf = rutaRelativa;
            certificado.Estado = "GENERADO";
            certificado.FechaEmision = certificado.FechaEmision ?? DateTime.Now;
            certificado.FechaVencimiento = documentoModel != null ? documentoModel.FechaVencimiento : certificado.FechaVencimiento;
            certificado.EmitidoPor = string.IsNullOrWhiteSpace(certificado.EmitidoPor) ? ObtenerLoginActual() : certificado.EmitidoPor;
            certificado.UpdatedAt = DateTime.Now;
            certificado.UpdatedBy = ObtenerUsuarioActualIdSeguro();
            _certificadoDao.Actualizar(certificado);
            item.Certificado = certificado;
        }

        private bool RutaDocumentoExiste(string ruta)
        {
            var rutaFisica = ResolverRutaDocumento(ruta);
            return !string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica);
        }

        private static FirmaAocrResult CrearResultadoFirmaAocr(bool ok, int code, string message, int solicitudId)
        {
            return new FirmaAocrResult
            {
                Ok = ok,
                Code = code,
                Message = message,
                SolicitudId = solicitudId
            };
        }

        private void AplicarAliasFormularioFirmaAocr(FirmarAocrRequest request)
        {
            if (request == null || Request == null || Request.Form == null)
            {
                return;
            }

            if (request.CertificadoDigital == null && Request.Files != null)
            {
                request.CertificadoDigital = Request.Files["certificadoDigital"];
            }
            request.PasswordCertificado = PrimerValorNoVacio(request.PasswordCertificado, Request.Form["passwordCertificado"]);
            request.ModoFirma = PrimerValorNoVacio(request.ModoFirma, Request.Form["modoFirma"]);
            request.NombreFirmante = PrimerValorNoVacio(request.NombreFirmante, Request.Form["nombreFirmante"], request.FirmanteNombre);
            request.CargoFirmante = PrimerValorNoVacio(request.CargoFirmante, Request.Form["cargoFirmante"], request.FirmanteCargo);
            request.RutaPdfOrigen = PrimerValorNoVacio(request.RutaPdfOrigen, Request.Form["rutaPdfOrigen"]);

            int intValue;
            if (!request.PaginaFirma.HasValue && int.TryParse(Request.Form["paginaFirma"], out intValue))
            {
                request.PaginaFirma = intValue;
            }
            if (request.PaginaFirma.HasValue && request.NumeroPaginaFirma <= 0)
            {
                request.NumeroPaginaFirma = request.PaginaFirma.Value;
            }

            decimal decimalValue;
            if (!request.PosicionX.HasValue && TryParseDecimalInvariant(Request.Form["posicionX"], out decimalValue))
            {
                request.PosicionX = decimalValue;
            }
            if (!request.PosicionY.HasValue && TryParseDecimalInvariant(Request.Form["posicionY"], out decimalValue))
            {
                request.PosicionY = decimalValue;
            }
            if (!request.AnchoFirmaDecimal.HasValue && TryParseDecimalInvariant(Request.Form["anchoFirma"], out decimalValue))
            {
                request.AnchoFirmaDecimal = decimalValue;
            }
            if (!request.AltoFirmaDecimal.HasValue && TryParseDecimalInvariant(Request.Form["altoFirma"], out decimalValue))
            {
                request.AltoFirmaDecimal = decimalValue;
            }
        }

        private static string PrimerValorNoVacio(params string[] valores)
        {
            if (valores == null)
            {
                return null;
            }

            return valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }

        private PosicionFirmaVisualPdf ConstruirPosicionFirmaVisualPdfRequerida(FirmarAocrRequest request)
        {
            if (request == null)
            {
                return null;
            }

            decimal posicionX;
            decimal posicionY;
            decimal ancho;
            decimal alto;

            if (request.PosicionX.HasValue)
            {
                posicionX = request.PosicionX.Value;
            }
            else if (!TryParseDecimalInvariant(request.PosicionFirmaX, out posicionX))
            {
                return null;
            }

            if (request.PosicionY.HasValue)
            {
                posicionY = request.PosicionY.Value;
            }
            else if (!TryParseDecimalInvariant(request.PosicionFirmaY, out posicionY))
            {
                return null;
            }

            if (request.AnchoFirmaDecimal.HasValue)
            {
                ancho = request.AnchoFirmaDecimal.Value;
            }
            else if (!TryParseDecimalInvariant(request.AnchoFirma, out ancho))
            {
                return null;
            }

            if (request.AltoFirmaDecimal.HasValue)
            {
                alto = request.AltoFirmaDecimal.Value;
            }
            else if (!TryParseDecimalInvariant(request.AltoFirma, out alto))
            {
                return null;
            }

            if (posicionX < 0 || posicionX > 1 || posicionY < 0 || posicionY > 1 || ancho <= 0 || ancho > 1 || alto <= 0 || alto > 1)
            {
                return null;
            }

            return new PosicionFirmaVisualPdf
            {
                NumeroPagina = request.PaginaFirma.GetValueOrDefault(request.NumeroPaginaFirma > 0 ? request.NumeroPaginaFirma : 1),
                PosicionXRatio = (float)posicionX,
                PosicionYRatio = (float)posicionY,
                AnchoRatio = (float)ancho,
                AltoRatio = (float)alto
            };
        }

        private static string CalcularSha256Hex(byte[] contenido)
        {
            if (contenido == null || contenido.Length == 0)
            {
                return null;
            }

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(contenido)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static int ObtenerAocrIdValor(ValidarAocrSolicitudItemViewModel item)
        {
            return item != null && item.Certificado != null ? item.Certificado.CodigoCertificado : 0;
        }

        private static string ObtenerAocrIdLog(ValidarAocrSolicitudItemViewModel item)
        {
            var valor = ObtenerAocrIdValor(item);
            return valor > 0 ? valor.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private void RegistrarLogFirmaAocrIn(FirmarAocrRequest request, string tipoDocumento)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR][IN] SolicitudId=" + (request != null ? request.SolicitudId.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; AocrId=" + (request != null && request.AocrId.HasValue ? request.AocrId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; TipoDocumento=" + (tipoDocumento ?? string.Empty) +
                "; Usuario=" + ObtenerLoginActual() +
                "; Rol=" + ObtenerRolActualLog() +
                "; TieneCertificado=" + (request != null && request.CertificadoDigital != null && request.CertificadoDigital.ContentLength > 0) +
                "; TienePassword=" + (request != null && !string.IsNullOrWhiteSpace(request.PasswordCertificado)) +
                "; PosX=" + (request != null ? (request.PosicionX.HasValue ? request.PosicionX.Value.ToString(CultureInfo.InvariantCulture) : request.PosicionFirmaX) : string.Empty) +
                "; PosY=" + (request != null ? (request.PosicionY.HasValue ? request.PosicionY.Value.ToString(CultureInfo.InvariantCulture) : request.PosicionFirmaY) : string.Empty) +
                "; Pagina=" + (request != null ? (request.PaginaFirma.GetValueOrDefault(request.NumeroPaginaFirma)).ToString(CultureInfo.InvariantCulture) : string.Empty));
        }

        private void RegistrarLogFirmaAocrValidation(FirmarAocrRequest request, bool pdfExiste, bool certificadoValido, bool estadoFirmable, bool puedeFirmar, string motivo)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR][VALIDATION] SolicitudId=" + (request != null ? request.SolicitudId.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; PdfExiste=" + pdfExiste +
                "; CertificadoValido=" + certificadoValido +
                "; EstadoFirmable=" + estadoFirmable +
                "; PuedeFirmar=" + puedeFirmar +
                "; Motivo=" + (motivo ?? string.Empty));
        }

        private static void RegistrarLogFirmaAocrPdfOrigen(string ruta, bool existe, long bytes)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR][PDF_ORIGEN] RutaPdf=" + (ruta ?? string.Empty) +
                "; Existe=" + existe +
                "; Bytes=" + bytes);
        }

        private static void RegistrarLogFirmaAocrCertificado(HttpPostedFileBase certificado, bool passwordRecibida, bool tieneClavePrivada = true, bool vigente = true)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR][CERT] Archivo=" + (certificado != null ? (certificado.FileName ?? string.Empty) : string.Empty) +
                "; Extension=" + (certificado != null ? Path.GetExtension(certificado.FileName ?? string.Empty) : string.Empty) +
                "; Bytes=" + (certificado != null ? certificado.ContentLength : 0) +
                "; PasswordRecibida=" + passwordRecibida +
                "; TieneClavePrivada=" + tieneClavePrivada +
                "; Vigente=" + vigente);
        }

        private static void RegistrarLogFirmaAocrPdfFirmado(string rutaFirmada, bool existe, long bytes, string hash)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[FIRMA_AOCR][PDF_FIRMADO] RutaFirmada=" + (rutaFirmada ?? string.Empty) +
                "; Existe=" + existe +
                "; Bytes=" + bytes +
                "; Hash=" + (hash ?? string.Empty));
        }

        private void RegistrarLogFirmaAocrError(int solicitudId, string motivo, Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                "[FIRMA_AOCR][ERROR] SolicitudId=" + solicitudId +
                "; Motivo=" + (motivo ?? string.Empty) +
                "; Exception=" + (ex != null ? ex.GetType().FullName + ": " + ex.Message : string.Empty));
        }

        private static bool DocumentoAocrEsUnificado(ValidarAocrSolicitudItemViewModel item)
        {
            return item != null && item.FirmaCompleta;
        }

        private void RegistrarLogValidarAocrEntrada(int? solicitudId, int? aocrId)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[VALIDAR_AOCR][IN] Usuario=" + ObtenerLoginActual() +
                "; Rol=" + ObtenerRolActualLog() +
                "; SolicitudId=" + (solicitudId.HasValue ? solicitudId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; AocrId=" + (aocrId.HasValue ? aocrId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty));
        }

        private void RegistrarLogValidarAocrEstados(AocrContextoResolucion contexto)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[VALIDAR_AOCR][ESTADOS] SolicitudId=" + (contexto != null && contexto.SolicitudId.HasValue ? contexto.SolicitudId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; EstadoSolicitud=" + (contexto != null ? (contexto.EstadoSolicitud ?? string.Empty) : string.Empty) +
                "; EstadoInforme=" + (contexto != null && contexto.InformeTecnico != null ? (contexto.InformeTecnico.EstadoInforme ?? string.Empty) : string.Empty) +
                "; AocrId=" + (contexto != null && contexto.AocrId.HasValue ? contexto.AocrId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                "; EstadoAocr=" + (contexto != null ? (contexto.EstadoAocr ?? string.Empty) : string.Empty) +
                "; CondicionesId=" + ObtenerCondicionesIdLog(contexto) +
                "; EstadoCondiciones=" + ObtenerEstadoCondicionesLog(contexto));
        }

        private void RegistrarLogValidarAocrPrecondiciones(AocrContextoResolucion contexto, ValidarAocrSolicitudItemViewModel item, string motivo)
        {
            var informeAprobado = contexto != null && _informeTecnicoEstadoService.EstaAprobadoPorDireccion(contexto.InformeTecnico);
            var aocrExiste = contexto != null && contexto.ExisteAocr;
            var condicionesExiste = item != null && item.Documentos != null && item.Documentos.Any(d => d != null && string.Equals(d.TipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase) && d.Disponible);
            var puedeVerAocr = item != null && item.Documentos != null && item.Documentos.Any(d => d != null && string.Equals(d.TipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.UrlVer));
            var puedeFirmarAocr = item != null && item.Documentos != null && item.Documentos.Any(d => d != null && string.Equals(d.TipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.UrlFirmar));
            var puedeFirmarCondiciones = item != null && item.Documentos != null && item.Documentos.Any(d => d != null && string.Equals(d.TipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.UrlFirmar));

            System.Diagnostics.Trace.TraceInformation(
                "[VALIDAR_AOCR][PRECONDICIONES] InformeAprobadoDireccion=" + informeAprobado +
                "; AocrExiste=" + aocrExiste +
                "; CondicionesExiste=" + condicionesExiste +
                "; PuedeVerAocr=" + puedeVerAocr +
                "; PuedeFirmarAocr=" + puedeFirmarAocr +
                "; PuedeFirmarCondiciones=" + puedeFirmarCondiciones +
                "; Motivo=" + (motivo ?? string.Empty));
        }

        private void RegistrarLogValidarAocrViewModel(ValidarAocrViewModel model)
        {
            if (model == null)
            {
                return;
            }

            System.Diagnostics.Trace.TraceInformation(
                "[VALIDAR_AOCR][VIEWMODEL] SolicitudId=" + model.SolicitudId +
                "; EstadoSolicitud=" + (model.EstadoSolicitud ?? string.Empty) +
                "; InformeAprobado=" + model.InformeAprobadoDireccion +
                "; AocrExiste=" + model.AocrExiste +
                "; AocrFirmada=" + model.AocrFirmada +
                "; CondicionesExiste=" + model.CondicionesExisten +
                "; CondicionesFirmadas=" + model.CondicionesFirmadas +
                "; PuedeFirmarAocr=" + model.PuedeFirmarAocr +
                "; PuedeFirmarCondiciones=" + model.PuedeFirmarCondiciones);
        }

        private void RegistrarLogDocumentoValidacionRequest(int solicitudId, string tipo, bool descargar)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[DOCUMENTO_VALIDACION_AOCR][REQUEST] SolicitudId=" + solicitudId +
                "; Tipo=" + (tipo ?? string.Empty) +
                "; Descargar=" + descargar +
                "; Usuario=" + ObtenerLoginActual() +
                "; Rol=" + ObtenerRolActualLog());
        }

        private void RegistrarLogDocumentoValidacionOk(int solicitudId, string tipo, string ruta)
        {
            var bytes = 0L;
            try
            {
                if (!string.IsNullOrWhiteSpace(ruta) && System.IO.File.Exists(ruta))
                {
                    bytes = new FileInfo(ruta).Length;
                }
            }
            catch
            {
            }

            RegistrarLogDocumentoValidacionOk(solicitudId, tipo, ruta, bytes);
        }

        private void RegistrarLogDocumentoValidacionOk(int solicitudId, string tipo, string ruta, long bytes)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[DOCUMENTO_VALIDACION_AOCR][OK] SolicitudId=" + solicitudId +
                "; Tipo=" + (tipo ?? string.Empty) +
                "; Ruta=" + (ruta ?? string.Empty) +
                "; Bytes=" + bytes);
        }

        private void RegistrarLogDocumentoValidacionBloqueado(int solicitudId, string tipo, string motivo)
        {
            System.Diagnostics.Trace.TraceWarning(
                "[DOCUMENTO_VALIDACION_AOCR][409] SolicitudId=" + solicitudId +
                "; Tipo=" + (tipo ?? string.Empty) +
                "; Motivo=" + (motivo ?? string.Empty));
        }

        private string ObtenerCondicionesIdLog(AocrContextoResolucion contexto)
        {
            if (contexto == null || !contexto.SolicitudId.HasValue)
            {
                return string.Empty;
            }

            var firma = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(contexto.SolicitudId.Value, "CONDICIONES_LIMITACIONES");
            return firma != null && firma.CodigoFirma > 0 ? firma.CodigoFirma.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private string ObtenerEstadoCondicionesLog(AocrContextoResolucion contexto)
        {
            if (contexto == null || !contexto.SolicitudId.HasValue)
            {
                return string.Empty;
            }

            var firma = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(contexto.SolicitudId.Value, "CONDICIONES_LIMITACIONES");
            return firma != null && !string.IsNullOrWhiteSpace(firma.RutaDocumento) ? "FIRMADO" : "PENDIENTE";
        }

        private void NotificarDocumentoAocrFirmadoSeguro(int solicitudId, string tipoDocumento)
        {
            try
            {
                var tipo = NormalizarTipoDocumento(tipoDocumento);
                if (string.Equals(tipo, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase))
                {
                    _aocrProcesoNotificacionService.NotificarAocrFirmado(solicitudId);
                }
                else if (string.Equals(tipo, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase))
                {
                    _aocrProcesoNotificacionService.NotificarCondicionesFirmadas(solicitudId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[NOTIF_AOCR][SEND_ERROR] SolicitudId=" + solicitudId + "; TipoEvento=FIRMA_DOCUMENTO; Email=; Error=" + ex.Message + ";");
            }
        }

        private string ObtenerLoginActual()
        {
            return User != null && User.Identity != null ? (User.Identity.Name ?? string.Empty) : string.Empty;
        }

        private string ObtenerRolActualLog()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return "ANONIMO";
            }

            return new[] { "Administrador", "DIRDAC", "Direccion", "DireccionJefaturaTecnica", "JefaturaTecnica", "DirectorGeneral", "Coordinacion", "Coordinador", "CoordinadorInspecciones", "CoordinacionLegal", "CoordinadorLegal", "Inspector" }
                .FirstOrDefault(rol => User.IsInRole(rol)) ?? "AUTENTICADO";
        }

        private void RegistrarTrazaAocrCoordinacion(ValidarAocrSolicitudItemViewModel item)
        {
            if (item == null || item.Solicitud == null)
            {
                return;
            }

            var documentoAocr = item.Certificado != null ? item.Certificado.CodigoCertificado : 0;
            var rolActual = User != null && User.Identity != null && User.Identity.IsAuthenticated
                ? (new[] { "Coordinador", "CoordinadorInspecciones", "CoordinacionLegal", "CoordinadorLegal", "DIRDAC", "Direccion", "JefaturaTecnica", "DirectorGeneral", "Administrador" }
                    .FirstOrDefault(rol => User.IsInRole(rol)) ?? "AUTENTICADO")
                : "ANONIMO";

            System.Diagnostics.Debug.WriteLine("[AOCR_COORD] SolicitudId=" + item.Solicitud.CodigoSolicitud
                + " InspeccionId=" + (item.Inspeccion != null ? item.Inspeccion.CodigoInspeccion : 0)
                + " AOCRId=" + documentoAocr
                + " EstadoAOCR=" + (item.EstadoSolicitud ?? string.Empty)
                + " EstadoInforme=" + (item.Informe != null ? (item.Informe.EstadoInforme ?? string.Empty) : string.Empty)
                + " ResultadoInforme=" + (item.Informe != null ? (item.Informe.Resultado ?? string.Empty) : string.Empty)
                + " Usuario=" + ((User != null && User.Identity != null) ? User.Identity.Name : string.Empty)
                + " Rol=" + rolActual
                + " PuedeRevisar=" + item.FirmaCompleta
                + " PuedeSolicitarModificacion=" + item.PuedeSolicitarModificacion
                + " PuedeEnviarDIRDAC=" + item.PuedeEnviarADirdac
                + " PuedeGenerarPdfFirma=" + item.Documentos.Any(d => d != null && !string.IsNullOrWhiteSpace(d.UrlVer))
                + " MotivoBloqueo=" + (item.MensajeAdvertencia ?? string.Empty)
                + " CamposFaltantes=" + (item.CamposFaltantes ?? string.Empty));
        }

        private List<ValidarAocrDocumentoItemViewModel> ConstruirDocumentosValidacion(ValidarAocrSolicitudItemViewModel item)
        {
            var urlHelper = new UrlHelper(ControllerContext.RequestContext);
            var fechaBase = item.FechaFirmaFinal ?? item.FechaDisponibilidad ?? DateTime.Now;
            var estadoSolicitud = EstadoSolicitud.Normalizar(item != null && item.Solicitud != null ? item.Solicitud.Estado : null);
            var esModificacionDirecta = EsSolicitudModificacionDirectaSinInspeccion(item != null ? item.Solicitud : null, estadoSolicitud);
            var condicionesFirmadas = string.Equals(estadoSolicitud, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase);
            var solicitudId = item != null && item.Solicitud != null ? item.Solicitud.CodigoSolicitud : 0;
            var firmaAocr = solicitudId > 0 ? _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO") : null;
            var firmaCondiciones = solicitudId > 0 ? _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES") : null;
            var aocrFirmado = firmaAocr != null && !string.IsNullOrWhiteSpace(firmaAocr.RutaDocumento);
            var condicionesDocumentoFirmado = condicionesFirmadas || (firmaCondiciones != null && !string.IsNullOrWhiteSpace(firmaCondiciones.RutaDocumento));
            var puedeFirmar = UsuarioActualPuedeFirmarDocumentoValidacionAocr();

            return new List<ValidarAocrDocumentoItemViewModel>
            {
                new ValidarAocrDocumentoItemViewModel
                {
                    TipoDocumento = "RECONOCIMIENTO",
                    NombreVisible = item.FirmaCompleta
                        ? "AOCR oficial unificada (paginas 1 y 2)"
                        : "Reconocimiento de Certificado de Explotador de Servicios Aereos",
                    Estado = aocrFirmado ? "Firmado" : (item.FirmaCompleta ? "Disponible" : (esModificacionDirecta ? "No aplica" : "Pendiente")),
                    Observacion = item.FirmaCompleta
                        ? "La salida oficial AOCR1 integra reconocimiento y condiciones/limitaciones en un solo PDF institucional."
                        : (esModificacionDirecta
                            ? "La modificación directa de Condiciones y Limitaciones no genera un reconocimiento adicional."
                            : "Falta firma final del informe tecnico para habilitar este documento."),
                    UrlEditar = item.FirmaCompleta ? urlHelper.Action("Index", "FirmaAocr", new { solicitudId = item.Solicitud.CodigoSolicitud }) : null,
                    UrlVer = item.FirmaCompleta ? urlHelper.Action("VerPdf", "FirmaAocr", new { solicitudId = item.Solicitud.CodigoSolicitud, firmado = false }) : null,
                    UrlDescargar = item.FirmaCompleta ? urlHelper.Action("DescargarPdf", "FirmaAocr", new { solicitudId = item.Solicitud.CodigoSolicitud, firmado = false }) : null,
                    UrlFirmar = item.FirmaCompleta && puedeFirmar && !aocrFirmado ? urlHelper.Action("FirmarAocr", "CoordinacionJefatura") : null,
                    FechaDocumento = item.Certificado != null ? (item.Certificado.UpdatedAt ?? item.Certificado.FechaEmision ?? fechaBase) : fechaBase,
                    Disponible = item.FirmaCompleta,
                    Firmado = aocrFirmado
                },
                new ValidarAocrDocumentoItemViewModel
                {
                    TipoDocumento = "CONDICIONES_LIMITACIONES",
                    NombreVisible = item.FirmaCompleta
                        ? "Hoja 2 - Condiciones y Limitaciones (incluida en AOCR oficial)"
                        : "Condiciones y Limitaciones",
                    Estado = item.FirmaCompleta ? "Disponible" : (esModificacionDirecta ? (condicionesFirmadas ? "Firmado" : "En preparación") : "Pendiente"),
                    Observacion = item.FirmaCompleta
                        ? "La segunda pagina forma parte del mismo PDF oficial AOCR1 utilizado para preview, firma y descarga final."
                        : (esModificacionDirecta
                            ? (condicionesFirmadas
                                ? "Documento firmado institucionalmente y listo para descarga final."
                                : "Documento habilitado para edición y revisión en el flujo de modificación sin inspección.")
                            : "Falta firma final del informe tecnico para habilitar este documento."),
                    UrlEditar = (item.FirmaCompleta || esModificacionDirecta) ? urlHelper.Action("Index", "FirmaAocr", new { solicitudId = item.Solicitud.CodigoSolicitud }) : null,
                    UrlVer = (item.FirmaCompleta || esModificacionDirecta) ? urlHelper.Action("VerPdf", "FirmaAocr", new { solicitudId = item.Solicitud.CodigoSolicitud, firmado = false }) : null,
                    UrlDescargar = item.FirmaCompleta
                        ? urlHelper.Action("DescargarPdf", "FirmaAocr", new { solicitudId = item.Solicitud.CodigoSolicitud, firmado = false })
                        : (condicionesFirmadas ? urlHelper.Action("DescargarFirmado", "FirmaAocr", new { solicitudId = item.Solicitud.CodigoSolicitud }) : null),
                    UrlFirmar = (item.FirmaCompleta || esModificacionDirecta) && puedeFirmar && !condicionesDocumentoFirmado ? urlHelper.Action("FirmarCondiciones", "CoordinacionJefatura") : null,
                    FechaDocumento = fechaBase,
                    Disponible = item.FirmaCompleta || condicionesDocumentoFirmado,
                    Firmado = condicionesDocumentoFirmado
                }
            };
        }

        private static bool EsSolicitudModificacionDirectaSinInspeccion(SolicitudAOCR solicitud, string estadoSolicitud)
        {
            if (solicitud == null || solicitud.TipoSolicitud.GetValueOrDefault() != 3)
            {
                return false;
            }

            var estadoNormalizado = EstadoSolicitud.Normalizar(estadoSolicitud);
            return string.Equals(estadoNormalizado, EstadoSolicitud.GeneradoCondicionesLimitaciones, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.EnRevisionCoordinadorFinal, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.EnviadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PuedeEditarCondicionesLimitacionesModificacion(ValidarAocrSolicitudItemViewModel item, string tipoDocumento)
        {
            return string.Equals(tipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase)
                && item != null
                && EsSolicitudModificacionDirectaSinInspeccion(item.Solicitud, item.Solicitud != null ? item.Solicitud.Estado : null);
        }

        private bool UsuarioActualPuedeFirmarDocumentoValidacionAocr()
        {
            return User != null
                && (User.IsInRole("Administrador")
                    || User.IsInRole("DIRDAC")
                    || User.IsInRole("Direccion")
                    || User.IsInRole("DireccionJefaturaTecnica")
                    || User.IsInRole("DirectorGeneral")
                    || User.IsInRole("JefaturaTecnica"));
        }

        private static bool UsarFirmaAocrNueva()
        {
            return true;
        }

        private AocrDocumentoPdfViewModel ConstruirDocumentoPdfModel(ValidarAocrSolicitudItemViewModel item, AocrDocumentoEdicionViewModel edicion, string tipoDocumento)
        {
            var firmanteFinal = item.Certificado != null && !string.IsNullOrWhiteSpace(item.Certificado.AprobadoPor)
                ? item.Certificado.AprobadoPor
                : item.Certificado != null && !string.IsNullOrWhiteSpace(item.Certificado.EmitidoPor)
                    ? item.Certificado.EmitidoPor
                    : item.Solicitud != null && !string.IsNullOrWhiteSpace(item.Solicitud.Director)
                        ? item.Solicitud.Director
                        : item.Informe != null
                            ? item.Informe.UsuarioFirma2
                            : null;

            var cargoFirmante = item.Solicitud != null && !string.IsNullOrWhiteSpace(item.Solicitud.CargoDirector)
                ? item.Solicitud.CargoDirector
                : "Direccion General de Aviacion Civil";

            var operador = item.Solicitud != null ? (item.Solicitud.RazonSocial ?? item.Solicitud.NombreOperador ?? item.Solicitud.NombreComercial) : null;
            var correoExplotador = item.Solicitud != null ? item.Solicitud.Email : null;
            var telefonoExplotador = item.Solicitud != null ? item.Solicitud.Telefono : null;
            var puntoContacto = item.Solicitud != null ? item.Solicitud.RepresentanteLegal : null;
            var contactoCorreo = item.Solicitud != null ? (item.Solicitud.CorreoRepresentanteTecnico ?? item.Solicitud.Email) : null;
            var restricciones = item.Certificado != null ? item.Certificado.Observaciones : (item.Informe != null ? item.Informe.Recomendaciones : null);
            var condicionBase = item.Solicitud != null
                ? string.Join(" / ", new[] { item.Solicitud.TipoOperacion, item.Solicitud.AeropuertosEcuador }.Where(x => !string.IsNullOrWhiteSpace(x)))
                : null;
            var aeronavesCondiciones = ConstruirFilasAeronavesCondiciones(item.Aeronaves);

            if (edicion != null && edicion.AeronavesCondiciones != null && edicion.AeronavesCondiciones.Any())
            {
                aeronavesCondiciones = edicion.AeronavesCondiciones;
            }

            return new AocrDocumentoPdfViewModel
            {
                Solicitud = item.Solicitud,
                Inspeccion = item.Inspeccion,
                Informe = item.Informe,
                Certificado = item.Certificado,
                Aeronaves = item.Aeronaves ?? new List<AeronaveSolicitud>(),
                NumeroAocr = item.NumeroAocr,
                FirmanteFinal = edicion != null && !string.IsNullOrWhiteSpace(edicion.FirmanteNombre) ? edicion.FirmanteNombre : firmanteFinal,
                CargoFirmante = edicion != null && !string.IsNullOrWhiteSpace(edicion.FirmanteCargo) ? edicion.FirmanteCargo : cargoFirmante,
                FechaEmisionDocumento = edicion != null ? edicion.FechaEmisionDocumento : item.FechaFirmaFinal ?? item.FechaDisponibilidad ?? DateTime.Now,
                FechaExpedicion = edicion != null ? edicion.FechaExpedicion : item.FechaFirmaFinal ?? item.FechaDisponibilidad,
                FechaRenovacion = edicion != null ? edicion.FechaRenovacion : null,
                FechaVencimiento = edicion != null ? edicion.FechaVencimiento : item.Certificado != null ? item.Certificado.FechaVencimiento : null,
                AocOriginalNumero = edicion != null ? edicion.AocOriginalNumero : item.Certificado != null ? item.Certificado.NumeroCertificado : item.NumeroAocr,
                EstadoOtorgante = edicion != null ? edicion.EstadoOtorgante : "Estado del Operador",
                NombreExplotador = edicion != null ? edicion.NombreExplotador : operador,
                EstadoExplotador = edicion != null ? edicion.EstadoExplotador : item.Solicitud != null ? item.Solicitud.Pais : null,
                RazonSocial = edicion != null ? edicion.RazonSocial : item.Solicitud != null ? item.Solicitud.RazonSocial : null,
                DireccionExplotador = edicion != null ? edicion.DireccionExplotador : item.Solicitud != null ? item.Solicitud.Direccion : null,
                TelefonoExplotador = edicion != null ? edicion.TelefonoExplotador : telefonoExplotador,
                CorreoExplotador = edicion != null ? edicion.CorreoExplotador : correoExplotador,
                PuntoContactoEcuador = edicion != null ? edicion.PuntoContactoEcuador : puntoContacto,
                ContactoDireccion = edicion != null ? edicion.ContactoDireccion : item.Solicitud != null ? item.Solicitud.Direccion : null,
                ContactoTelefono = edicion != null ? edicion.ContactoTelefono : telefonoExplotador,
                ContactoCorreo = edicion != null ? edicion.ContactoCorreo : contactoCorreo,
                PuntosContactoOperacionales = edicion != null ? edicion.PuntosContactoOperacionales : ConstruirPuntosContactoOperacionales(item.Solicitud),
                BaseLegalReferencia = edicion != null ? edicion.BaseLegalReferencia : item.Solicitud != null ? (item.Solicitud.AprobacionesEspecialesOtros ?? item.Solicitud.AprobacionesEspeciales ?? item.Solicitud.DescripcionOperacion) : null,
                ObservacionesReconocimiento = edicion != null ? edicion.ObservacionesReconocimiento : item.Certificado != null ? item.Certificado.Observaciones : item.Informe != null ? (item.Informe.Conclusiones ?? item.Informe.Observaciones) : null,
                RepresentanteTecnico = edicion != null ? edicion.RepresentanteTecnico : item.Solicitud != null ? (item.Solicitud.TecnicoResponsableNombre ?? item.Solicitud.RepresentanteLegal) : null,
                CondicionBaseOperacion = edicion != null ? edicion.CondicionBaseOperacion : condicionBase,
                RestriccionesCondiciones = edicion != null ? edicion.RestriccionesCondiciones : restricciones,
                CondicionesAdicionales = edicion != null ? edicion.CondicionesAdicionales : null,
                ObservacionesValidacionFinal = edicion != null ? edicion.ObservacionesValidacionFinal : null,
                ElaboradoPor = edicion != null ? edicion.ElaboradoPor : item.Certificado != null ? item.Certificado.EmitidoPor : null,
                RevisadoPor = edicion != null ? edicion.RevisadoPor : item.Informe != null ? item.Informe.UsuarioFirma2 : null,
                AeronavesCondiciones = aeronavesCondiciones
            };
        }

        private List<string> ObtenerCamposObligatoriosFaltantesDocumentoAocr(AocrDocumentoPdfViewModel model, string tipoDocumento)
        {
            var faltantes = new List<string>();
            if (model == null)
            {
                faltantes.Add("contexto del documento AOCR");
                return faltantes;
            }

            var tipoNormalizado = NormalizarTipoDocumento(tipoDocumento);
            if (tipoNormalizado == "RECONOCIMIENTO")
            {
                AgregarCampoFaltante(faltantes, model.NumeroAocr, "AOCR #");
                AgregarCampoFaltante(faltantes, model.AocOriginalNumero, "AOC base");
                AgregarCampoFaltante(faltantes, model.EstadoOtorgante, "Estado otorgante");
                AgregarCampoFaltante(faltantes, model.NombreExplotador, "Nombre del explotador");
                AgregarCampoFaltante(faltantes, model.EstadoExplotador, "Estado del explotador");
                AgregarCampoFaltante(faltantes, model.PuntoContactoEcuador, "Punto de contacto Ecuador");
                AgregarCampoFaltante(faltantes, model.PuntosContactoOperacionales, "Puntos de contacto operacionales");
                AgregarCampoFaltante(faltantes, model.RepresentanteTecnico, "Representante técnico");

                if (model.FechaEmisionDocumento == default(DateTime))
                {
                    faltantes.Add("Fecha de emisión");
                }

                if (!model.FechaVencimiento.HasValue)
                {
                    faltantes.Add("Fecha de vencimiento");
                }
            }
            else if (tipoNormalizado == "CONDICIONES_LIMITACIONES")
            {
                AgregarCampoFaltante(faltantes, model.NumeroAocr, "AOCR #");
                AgregarCampoFaltante(faltantes, model.RepresentanteTecnico, "Representante técnico");
                AgregarCampoFaltante(faltantes, model.CondicionBaseOperacion, "Aeropuertos autorizados / condición base");

                if (!model.FechaVencimiento.HasValue)
                {
                    faltantes.Add("Fecha de vencimiento");
                }

                if (model.AeronavesCondiciones == null || !model.AeronavesCondiciones.Any(fila => fila != null && !string.IsNullOrWhiteSpace(fila.ModeloTipo)))
                {
                    faltantes.Add("Tabla de aeronaves autorizadas");
                }
            }

            return faltantes;
        }

        private List<string> ObtenerCamposObligatoriosFaltantesAocrOficial(AocrDocumentoPdfViewModel model)
        {
            return ObtenerCamposObligatoriosFaltantesDocumentoAocr(model, "RECONOCIMIENTO")
                .Concat(ObtenerCamposObligatoriosFaltantesDocumentoAocr(model, "CONDICIONES_LIMITACIONES"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private CertificadoAOCRViewModel ConstruirCertificadoAocrViewModelOficial(AocrDocumentoPdfViewModel model)
        {
            var solicitud = model != null ? model.Solicitud : null;
            var aeropuertosAutorizados = string.Join(" / ", new[]
            {
                solicitud != null ? solicitud.AeropuertosEcuador : null,
                solicitud != null ? solicitud.AeropuertosEcuadorOtros : null,
                model != null ? model.CondicionBaseOperacion : null
            }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));

            var aeronavesCondiciones = ((model != null ? model.AeronavesCondiciones : null) ?? new List<AocrCondicionAeronaveFilaViewModel>())
                .Select(fila => new CertificadoAOCRAeronaveFilaViewModel
                {
                    ModeloTipo = PrimerTextoAocrNoVacio(fila != null ? fila.ModeloTipo : null, "No aplica"),
                    Matricula = PrimerTextoAocrNoVacio(fila != null ? fila.Matricula : null, "No aplica"),
                    Serie = PrimerTextoAocrNoVacio(fila != null ? fila.Serie : null, "No aplica"),
                    Uio = PrimerTextoAocrNoVacio(fila != null ? fila.Uio : null, "No aplica"),
                    Gye = PrimerTextoAocrNoVacio(fila != null ? fila.Gye : null, "No aplica"),
                    Mec = PrimerTextoAocrNoVacio(fila != null ? fila.Mec : null, "No aplica"),
                    Ltx = PrimerTextoAocrNoVacio(fila != null ? fila.Ltx : null, "No aplica")
                })
                .ToList();

            while (aeronavesCondiciones.Count < 4)
            {
                aeronavesCondiciones.Add(new CertificadoAOCRAeronaveFilaViewModel
                {
                    ModeloTipo = "No aplica",
                    Matricula = "No aplica",
                    Serie = "No aplica",
                    Uio = "No aplica",
                    Gye = "No aplica",
                    Mec = "No aplica",
                    Ltx = "No aplica"
                });
            }

            return new CertificadoAOCRViewModel
            {
                NumeroAOCR = model != null ? model.NumeroAocr : null,
                NumeroAOCBase = PrimerTextoAocrNoVacio(model != null ? model.AocOriginalNumero : null, solicitud != null ? solicitud.NumeroAOC : null, model != null ? model.NumeroAocr : null),
                PermisoOperacionCNAC = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.NumeroAOC : null, solicitud != null ? solicitud.NumeroSolicitud : null, "No aplica"),
                NumeroEnmienda = 1,
                FechaEmision = model != null && model.FechaEmisionDocumento != default(DateTime) ? model.FechaEmisionDocumento : DateTime.Now,
                FechaVencimiento = model != null ? model.FechaVencimiento : null,
                FechaRenovacion = model != null ? model.FechaRenovacion : null,
                Solicitud = solicitud,
                NombreExplotador = PrimerTextoAocrNoVacio(model != null ? model.NombreExplotador : null, solicitud != null ? solicitud.NombreOperador : null, solicitud != null ? solicitud.NombreComercial : null, "No aplica"),
                EstadoExplotador = PrimerTextoAocrNoVacio(model != null ? model.EstadoExplotador : null, solicitud != null ? solicitud.Pais : null, "No aplica"),
                RazonSocial = PrimerTextoAocrNoVacio(model != null ? model.RazonSocial : null, solicitud != null ? solicitud.RazonSocial : null, "No aplica"),
                RUC = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.Ruc : null, "No aplica"),
                DireccionExplotador = PrimerTextoAocrNoVacio(model != null ? model.DireccionExplotador : null, solicitud != null ? solicitud.Direccion : null, "No aplica"),
                TelefonoExplotador = PrimerTextoAocrNoVacio(model != null ? model.TelefonoExplotador : null, solicitud != null ? solicitud.Telefono : null, "No aplica"),
                CorreoExplotador = PrimerTextoAocrNoVacio(model != null ? model.CorreoExplotador : null, solicitud != null ? solicitud.Email : null, "No aplica"),
                PuntoContactoEcuador = PrimerTextoAocrNoVacio(model != null ? model.PuntoContactoEcuador : null, solicitud != null ? solicitud.RepresentanteLegal : null, "No aplica"),
                DireccionContactoEcuador = PrimerTextoAocrNoVacio(model != null ? model.ContactoDireccion : null, solicitud != null ? solicitud.Direccion : null, "No aplica"),
                TelefonoContactoEcuador = PrimerTextoAocrNoVacio(model != null ? model.ContactoTelefono : null, solicitud != null ? solicitud.Telefono : null, "No aplica"),
                CorreoContactoEcuador = PrimerTextoAocrNoVacio(model != null ? model.ContactoCorreo : null, solicitud != null ? solicitud.Email : null, "No aplica"),
                DireccionOperacional = PrimerTextoAocrNoVacio(model != null ? model.PuntosContactoOperacionales : null, solicitud != null ? solicitud.DescripcionOperacion : null, "No aplica"),
                TelefonoOperacional = PrimerTextoAocrNoVacio(model != null ? model.ContactoTelefono : null, solicitud != null ? solicitud.Telefono : null, "No aplica"),
                CorreoOperacional = PrimerTextoAocrNoVacio(model != null ? model.ContactoCorreo : null, solicitud != null ? solicitud.Email : null, "No aplica"),
                GerenciaSeguridadOperacional = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.ResumenOperacionesEae : null, model != null ? model.PuntosContactoOperacionales : null, "No aplica"),
                DireccionGSO = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.Direccion : null, "No aplica"),
                TelefonoGSO = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.Telefono : null, "No aplica"),
                CorreoGSO = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.Email : null, "No aplica"),
                RepresentanteTecnico = PrimerTextoAocrNoVacio(model != null ? model.RepresentanteTecnico : null, solicitud != null ? solicitud.TecnicoResponsableNombre : null, "No aplica"),
                DireccionRT = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.Direccion : null, "No aplica"),
                TelefonoRT = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.Telefono : null, "No aplica"),
                CorreoRT = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.CorreoRepresentanteTecnico : null, solicitud != null ? solicitud.Email : null, "No aplica"),
                RepresentanteLegal = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.RepresentanteLegal : null, "No aplica"),
                TipoOperacion = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.TipoOperacion : null, "No aplica"),
                AlcanceOperacion = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.DescripcionOperacion : null, "No aplica"),
                AeronavesDetalle = string.Join(Environment.NewLine, aeronavesCondiciones.Select(fila => string.Join(" | ", new[] { fila.ModeloTipo, fila.Matricula, fila.Serie }))),
                AeropuertosAutorizados = PrimerTextoAocrNoVacio(aeropuertosAutorizados, "No aplica"),
                TiposOperacionAutorizados = PrimerTextoAocrNoVacio(solicitud != null ? solicitud.TipoOperacion : null, solicitud != null ? solicitud.DescripcionOperacion : null, "No aplica"),
                RestriccionesCondiciones = PrimerTextoAocrNoVacio(model != null ? model.RestriccionesCondiciones : null, model != null ? model.ObservacionesReconocimiento : null, "No aplica"),
                CondicionesAdicionales = PrimerTextoAocrNoVacio(model != null ? model.CondicionesAdicionales : null, solicitud != null ? solicitud.AprobacionesEspecialesOtros : null, solicitud != null ? solicitud.AprobacionesEspeciales : null, "No aplica"),
                AeronavesCondiciones = aeronavesCondiciones,
                NombreFirmante = PrimerTextoAocrNoVacio(model != null ? model.FirmanteFinal : null, solicitud != null ? solicitud.Director : null, "DIRECTOR GENERAL DE AVIACION CIVIL"),
                CargoFirmante = PrimerTextoAocrNoVacio(model != null ? model.CargoFirmante : null, solicitud != null ? solicitud.CargoDirector : null, "Director General de Aviacion Civil"),
                TituloFirmante = "DIRECTOR GENERAL DE AVIACION CIVIL",
                CargoFirmanteCondiciones = "Director de Certificacion Aeronautica y Vigilancia Continua",
                TituloFirmanteCondiciones = "Director de Certificacion Aeronautica y Vigilancia Continua",
                TextoLegalEs = ConstruirTextoLegalCertificadoEs(),
                TextoLegalEn = ConstruirTextoLegalCertificadoEn(),
                Observaciones = PrimerTextoAocrNoVacio(model != null ? model.ObservacionesReconocimiento : null, solicitud != null ? solicitud.Observaciones : null, "No aplica")
            };
        }

        private static string ConstruirTextoLegalCertificadoEs()
        {
            return "Este certificado se emite con base en el AOC del explotador y en las condiciones y limitaciones aprobadas por la DGAC. Cualquier cambio que afecte la vigencia, la flota, los puntos de contacto o las especificaciones operacionales debera notificarse formalmente a esta Autoridad Aeronautica dentro de los plazos regulatorios aplicables.\nLa vigencia de este reconocimiento queda sujeta a la validez del AOC de origen, a las especificaciones operacionales aprobadas y a cualquier accion de suspension, revocatoria, cancelacion o restriccion emitida por la autoridad competente.";
        }

        private static string ConstruirTextoLegalCertificadoEn()
        {
            return "This certificate is issued based on the operator's valid AOC and on the conditions and limitations approved by the DGAC. Any change affecting validity, fleet, contact points or operational specifications shall be formally notified to this Civil Aviation Authority within the applicable regulatory deadlines.\nThe validity of this recognition remains subject to the source AOC, the approved operational specifications and any suspension, revocation, cancellation or restriction issued by the competent authority.";
        }

        private static string PrimerTextoAocrNoVacio(params string[] valores)
        {
            foreach (var valor in valores)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                {
                    return valor.Trim();
                }
            }

            return null;
        }

        private static void AgregarCampoFaltante(List<string> faltantes, string valor, string nombreCampo)
        {
            if (faltantes == null || string.IsNullOrWhiteSpace(nombreCampo))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(valor))
            {
                faltantes.Add(nombreCampo);
            }
        }

        private ValidarAocrSolicitudItemViewModel ObtenerContextoDocumentoValidacion(int solicitudId)
        {
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return null;
            }

            var inspeccionesSolicitud = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
            return ConstruirItemValidacionAocr(solicitud, inspeccionesSolicitud);
        }

        private ActionResult RespuestaDocumentoValidacionNoDisponible(int solicitudId, string tipoDocumento, SolicitudAOCR solicitud, string motivo, int statusCode)
        {
            RegistrarLogDocumentoValidacionBloqueado(solicitudId, tipoDocumento, motivo);
            if (EsSolicitudJson())
            {
                Response.StatusCode = statusCode;
                return Json(new
                {
                    ok = false,
                    code = statusCode,
                    message = motivo,
                    data = new
                    {
                        solicitudId = solicitudId,
                        tipo = tipoDocumento
                    }
                }, JsonRequestBehavior.AllowGet);
            }

            return VistaDocumentoValidacionNoDisponible(solicitudId, tipoDocumento, solicitud, motivo, statusCode);
        }

        private bool EsSolicitudJson()
        {
            var accept = Request != null ? Request.Headers["Accept"] : null;
            var requestedWith = Request != null ? Request.Headers["X-Requested-With"] : null;
            return (Request != null && Request.IsAjaxRequest())
                || string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(accept) && accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private ActionResult VistaDocumentoValidacionNoDisponible(
            int solicitudId,
            string tipoDocumento,
            SolicitudAOCR solicitud,
            string motivo,
            int statusCode)
        {
            if (statusCode >= 400)
            {
                Response.StatusCode = statusCode;
            }

            var model = new AocrDocumentoValidacionNoDisponibleViewModel
            {
                SolicitudId = solicitud != null ? solicitud.CodigoSolicitud : solicitudId,
                TipoDocumento = string.IsNullOrWhiteSpace(tipoDocumento) ? "No registrado" : tipoDocumento,
                NumeroSolicitud = solicitud != null ? solicitud.NumeroSolicitud : null,
                NumeroAocr = solicitud != null ? solicitud.NumeroAOC : null,
                NombreExplotador = solicitud != null
                    ? PrimerTextoAocrNoVacio(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial, "No registrado")
                    : "No registrado",
                EstadoSolicitud = solicitud != null ? solicitud.Estado : "No registrado",
                Motivo = motivo,
                Referencia = "[AOCR_EDIT] SolicitudId=" + solicitudId + " Tipo=" + (tipoDocumento ?? "N/A"),
                PuedeAbrirExpediente = solicitud != null && solicitud.CodigoSolicitud > 0
            };

            RegistrarLogAocrEdit(solicitudId, tipoDocumento, solicitud, null, false, motivo, "Bloqueo");
            return View("~/Views/CoordinacionJefatura/DocumentoValidacionAocrNoDisponible.cshtml", model);
        }

        private void RegistrarLogAocrEdit(
            int solicitudId,
            string tipoDocumento,
            SolicitudAOCR solicitud,
            ValidarAocrSolicitudItemViewModel item,
            bool puedeEditar,
            string motivoBloqueo,
            string resultado)
        {
            try
            {
                var usuario = User != null && User.Identity != null ? User.Identity.Name : string.Empty;
                var roles = Session != null
                    ? Convert.ToString(Session["RolesRaw"] ?? Session["Roles"] ?? Session["Rol"])
                    : string.Empty;
                var estadoSolicitud = solicitud != null
                    ? solicitud.Estado
                    : item != null && item.Solicitud != null
                        ? item.Solicitud.Estado
                        : string.Empty;

                var mensaje = "[AOCR_EDIT] SolicitudId=" + solicitudId
                    + " Tipo=" + (tipoDocumento ?? string.Empty)
                    + " Usuario=" + usuario
                    + " Roles=" + roles
                    + " ExisteAccion=True"
                    + " ExisteSolicitud=" + (solicitud != null || (item != null && item.Solicitud != null))
                    + " EstadoSolicitud=" + (estadoSolicitud ?? string.Empty)
                    + " EstadoAOCR=" + (item != null ? (item.EstadoSolicitud ?? string.Empty) : string.Empty)
                    + " PuedeEditar=" + puedeEditar
                    + " Resultado=" + (resultado ?? string.Empty)
                    + " MotivoBloqueo=" + (motivoBloqueo ?? string.Empty);

                LogBL.RegistrarInfo(mensaje, "CoordinacionJefaturaController", ObtenerUsuarioActualIdSeguro());
            }
            catch
            {
                // La trazabilidad no debe bloquear la pantalla.
            }
        }

        private AocrDocumentoEdicionViewModel ConstruirDocumentoEdicionModel(ValidarAocrSolicitudItemViewModel item, string tipoDocumento)
        {
            var pdfModel = ConstruirDocumentoPdfModel(item, null, tipoDocumento);
            return new AocrDocumentoEdicionViewModel
            {
                SolicitudId = item.Solicitud != null ? item.Solicitud.CodigoSolicitud : 0,
                InspeccionId = item.Inspeccion != null ? (int?)item.Inspeccion.CodigoInspeccion : null,
                TipoDocumento = tipoDocumento,
                NumeroAocr = pdfModel.NumeroAocr,
                NombreDocumento = tipoDocumento == "RECONOCIMIENTO" ? "Reconocimiento AOCR" : "Condiciones y Limitaciones",
                AocOriginalNumero = pdfModel.AocOriginalNumero,
                EstadoOtorgante = pdfModel.EstadoOtorgante,
                NombreExplotador = pdfModel.NombreExplotador,
                EstadoExplotador = pdfModel.EstadoExplotador,
                RazonSocial = pdfModel.RazonSocial,
                DireccionExplotador = pdfModel.DireccionExplotador,
                TelefonoExplotador = pdfModel.TelefonoExplotador,
                CorreoExplotador = pdfModel.CorreoExplotador,
                PuntoContactoEcuador = pdfModel.PuntoContactoEcuador,
                ContactoDireccion = pdfModel.ContactoDireccion,
                ContactoTelefono = pdfModel.ContactoTelefono,
                ContactoCorreo = pdfModel.ContactoCorreo,
                PuntosContactoOperacionales = pdfModel.PuntosContactoOperacionales,
                BaseLegalReferencia = pdfModel.BaseLegalReferencia,
                ObservacionesReconocimiento = pdfModel.ObservacionesReconocimiento,
                RepresentanteTecnico = pdfModel.RepresentanteTecnico,
                CondicionBaseOperacion = pdfModel.CondicionBaseOperacion,
                RestriccionesCondiciones = pdfModel.RestriccionesCondiciones,
                CondicionesAdicionales = pdfModel.CondicionesAdicionales,
                ObservacionesValidacionFinal = pdfModel.ObservacionesValidacionFinal,
                FechaEmisionDocumento = pdfModel.FechaEmisionDocumento,
                FechaExpedicion = pdfModel.FechaExpedicion,
                FechaRenovacion = pdfModel.FechaRenovacion,
                FechaVencimiento = pdfModel.FechaVencimiento,
                ElaboradoPor = pdfModel.ElaboradoPor,
                RevisadoPor = pdfModel.RevisadoPor,
                FirmanteNombre = pdfModel.FirmanteFinal,
                FirmanteCargo = pdfModel.CargoFirmante,
                AeronavesCondiciones = pdfModel.AeronavesCondiciones
            };
        }

        private static string NormalizarTipoDocumento(string tipo)
        {
            var tipoNormalizado = (tipo ?? string.Empty).Trim().ToUpperInvariant();
            if (tipoNormalizado == "RECONOCIMIENTO"
                || tipoNormalizado == "UNIFICADO_AOCR"
                || tipoNormalizado == "AOCR_UNIFICADO")
            {
                return "RECONOCIMIENTO";
            }

            if (tipoNormalizado == "CONDICIONES"
                || tipoNormalizado == "LIMITACIONES"
                || tipoNormalizado == "CONDICIONES_LIMITACIONES")
            {
                return "CONDICIONES_LIMITACIONES";
            }

            return null;
        }

        private AocrDocumentoEdicionViewModel CompletarDocumentoEdicionDesdeFormulario(AocrDocumentoEdicionViewModel model)
        {
            var form = Request != null ? Request.Form : null;
            if (form == null || form.Count == 0)
            {
                return model;
            }

            var hydrated = model ?? new AocrDocumentoEdicionViewModel();

            hydrated.SolicitudId = ObtenerEnteroFormulario(form, "SolicitudId", hydrated.SolicitudId);
            hydrated.InspeccionId = ObtenerEnteroNullableFormulario(form, "InspeccionId", hydrated.InspeccionId);
            hydrated.TipoDocumento = ObtenerTextoFormulario(form, "TipoDocumento", hydrated.TipoDocumento);
            hydrated.NumeroAocr = ObtenerTextoFormulario(form, "NumeroAocr", hydrated.NumeroAocr);
            hydrated.NombreDocumento = ObtenerTextoFormulario(form, "NombreDocumento", hydrated.NombreDocumento);
            hydrated.AocOriginalNumero = ObtenerTextoFormulario(form, "AocOriginalNumero", hydrated.AocOriginalNumero);
            hydrated.EstadoOtorgante = ObtenerTextoFormulario(form, "EstadoOtorgante", hydrated.EstadoOtorgante);
            hydrated.NombreExplotador = ObtenerTextoFormulario(form, "NombreExplotador", hydrated.NombreExplotador);
            hydrated.EstadoExplotador = ObtenerTextoFormulario(form, "EstadoExplotador", hydrated.EstadoExplotador);
            hydrated.RazonSocial = ObtenerTextoFormulario(form, "RazonSocial", hydrated.RazonSocial);
            hydrated.DireccionExplotador = ObtenerTextoFormulario(form, "DireccionExplotador", hydrated.DireccionExplotador);
            hydrated.TelefonoExplotador = ObtenerTextoFormulario(form, "TelefonoExplotador", hydrated.TelefonoExplotador);
            hydrated.CorreoExplotador = ObtenerTextoFormulario(form, "CorreoExplotador", hydrated.CorreoExplotador);
            hydrated.PuntoContactoEcuador = ObtenerTextoFormulario(form, "PuntoContactoEcuador", hydrated.PuntoContactoEcuador);
            hydrated.ContactoDireccion = ObtenerTextoFormulario(form, "ContactoDireccion", hydrated.ContactoDireccion);
            hydrated.ContactoTelefono = ObtenerTextoFormulario(form, "ContactoTelefono", hydrated.ContactoTelefono);
            hydrated.ContactoCorreo = ObtenerTextoFormulario(form, "ContactoCorreo", hydrated.ContactoCorreo);
            hydrated.PuntosContactoOperacionales = ObtenerTextoFormulario(form, "PuntosContactoOperacionales", hydrated.PuntosContactoOperacionales);
            hydrated.BaseLegalReferencia = ObtenerTextoFormulario(form, "BaseLegalReferencia", hydrated.BaseLegalReferencia);
            hydrated.ObservacionesReconocimiento = ObtenerTextoFormulario(form, "ObservacionesReconocimiento", hydrated.ObservacionesReconocimiento);
            hydrated.RepresentanteTecnico = ObtenerTextoFormulario(form, "RepresentanteTecnico", hydrated.RepresentanteTecnico);
            hydrated.CondicionBaseOperacion = ObtenerTextoFormulario(form, "CondicionBaseOperacion", hydrated.CondicionBaseOperacion);
            hydrated.RestriccionesCondiciones = ObtenerTextoFormulario(form, "RestriccionesCondiciones", hydrated.RestriccionesCondiciones);
            hydrated.CondicionesAdicionales = ObtenerTextoFormulario(form, "CondicionesAdicionales", hydrated.CondicionesAdicionales);
            hydrated.ObservacionesValidacionFinal = ObtenerTextoFormulario(form, "ObservacionesValidacionFinal", hydrated.ObservacionesValidacionFinal);
            hydrated.ElaboradoPor = ObtenerTextoFormulario(form, "ElaboradoPor", hydrated.ElaboradoPor);
            hydrated.RevisadoPor = ObtenerTextoFormulario(form, "RevisadoPor", hydrated.RevisadoPor);
            hydrated.FirmanteNombre = ObtenerTextoFormulario(form, "FirmanteNombre", hydrated.FirmanteNombre);
            hydrated.FirmanteCargo = ObtenerTextoFormulario(form, "FirmanteCargo", hydrated.FirmanteCargo);
            hydrated.UsaPosicionFirmaPersonalizada = ObtenerBooleanoFormulario(form, "UsaPosicionFirmaPersonalizada", hydrated.UsaPosicionFirmaPersonalizada);
            hydrated.NumeroPaginaFirma = ObtenerEnteroFormulario(form, "NumeroPaginaFirma", hydrated.NumeroPaginaFirma > 0 ? hydrated.NumeroPaginaFirma : 1);
            hydrated.PosicionFirmaX = ObtenerTextoFormulario(form, "PosicionFirmaX", hydrated.PosicionFirmaX);
            hydrated.PosicionFirmaY = ObtenerTextoFormulario(form, "PosicionFirmaY", hydrated.PosicionFirmaY);
            hydrated.AnchoFirma = ObtenerTextoFormulario(form, "AnchoFirma", hydrated.AnchoFirma);
            hydrated.AltoFirma = ObtenerTextoFormulario(form, "AltoFirma", hydrated.AltoFirma);
            hydrated.FechaEmisionDocumento = ObtenerFechaFormulario(form, "FechaEmisionDocumento", hydrated.FechaEmisionDocumento);
            hydrated.FechaExpedicion = ObtenerFechaNullableFormulario(form, "FechaExpedicion", hydrated.FechaExpedicion);
            hydrated.FechaRenovacion = ObtenerFechaNullableFormulario(form, "FechaRenovacion", hydrated.FechaRenovacion);
            hydrated.FechaVencimiento = ObtenerFechaNullableFormulario(form, "FechaVencimiento", hydrated.FechaVencimiento);

            var aeronaves = ObtenerAeronavesCondicionesFormulario(form);
            if (aeronaves.Count > 0)
            {
                hydrated.AeronavesCondiciones = aeronaves;
            }

            return hydrated;
        }

        private static List<AocrCondicionAeronaveFilaViewModel> ObtenerAeronavesCondicionesFormulario(NameValueCollection form)
        {
            var filas = new List<AocrCondicionAeronaveFilaViewModel>();
            if (form == null || form.Count == 0)
            {
                return filas;
            }

            for (var index = 0; index < 500; index++)
            {
                var prefix = "AeronavesCondiciones[" + index + "].";
                var tieneDatos = form.AllKeys.Any(key => key != null && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (!tieneDatos)
                {
                    if (index > 0)
                    {
                        break;
                    }

                    continue;
                }

                filas.Add(new AocrCondicionAeronaveFilaViewModel
                {
                    ModeloTipo = ObtenerTextoFormulario(form, prefix + "ModeloTipo", null),
                    Matricula = ObtenerTextoFormulario(form, prefix + "Matricula", null),
                    Serie = ObtenerTextoFormulario(form, prefix + "Serie", null),
                    Uio = ObtenerTextoFormulario(form, prefix + "Uio", null),
                    Gye = ObtenerTextoFormulario(form, prefix + "Gye", null),
                    Mec = ObtenerTextoFormulario(form, prefix + "Mec", null),
                    Ltx = ObtenerTextoFormulario(form, prefix + "Ltx", null)
                });
            }

            return filas;
        }

        private static string ObtenerTextoFormulario(NameValueCollection form, string key, string fallback)
        {
            if (form == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            var value = form[key];
            return value ?? fallback;
        }

        private static int ObtenerEnteroFormulario(NameValueCollection form, string key, int fallback)
        {
            if (form == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            int value;
            return int.TryParse(form[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(form[key], NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                ? value
                : fallback;
        }

        private static int? ObtenerEnteroNullableFormulario(NameValueCollection form, string key, int? fallback)
        {
            if (form == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            int value;
            return int.TryParse(form[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(form[key], NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                ? (int?)value
                : fallback;
        }

        private static bool ObtenerBooleanoFormulario(NameValueCollection form, string key, bool fallback)
        {
            if (form == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            var value = form[key];
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static DateTime ObtenerFechaFormulario(NameValueCollection form, string key, DateTime fallback)
        {
            DateTime value;
            if (TryParseFechaFormulario(form != null ? form[key] : null, out value))
            {
                return value;
            }

            return fallback != default(DateTime) ? fallback : DateTime.Now;
        }

        private static DateTime? ObtenerFechaNullableFormulario(NameValueCollection form, string key, DateTime? fallback)
        {
            DateTime value;
            if (TryParseFechaFormulario(form != null ? form[key] : null, out value))
            {
                return value;
            }

            return fallback;
        }

        private static bool TryParseFechaFormulario(string value, out DateTime result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = default(DateTime);
                return false;
            }

            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
                || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result)
                || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }

        private string ConstruirSwitchesPdfValidacionAocr()
        {
            return PdfBrandingHelper.StandardRotativaSwitches
                + " --disable-smart-shrinking --margin-top 8mm --margin-bottom 8mm --margin-left 8mm --margin-right 8mm --header-spacing 0 --footer-spacing 0";
        }

        private string CrearArchivoBrandingTemporal(bool esHeader)
        {
            if (Server == null)
            {
                return null;
            }

            var carpetaTemporal = Server.MapPath("~/App_Data/Temp/PdfBranding");
            if (!Directory.Exists(carpetaTemporal))
            {
                Directory.CreateDirectory(carpetaTemporal);
            }

            var fileName = esHeader ? "aocr_header.html" : "aocr_footer.html";
            var htmlPath = Path.Combine(carpetaTemporal, fileName);
            var html = esHeader ? ConstruirHtmlHeaderHoja() : ConstruirHtmlFooterHoja();

            if (string.IsNullOrWhiteSpace(html))
            {
                html = ConstruirHtmlBrandingFallback(esHeader);
            }

            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            System.IO.File.WriteAllText(htmlPath, html, Encoding.UTF8);
            return htmlPath;
        }

        private string ConstruirHtmlHeaderHoja()
        {
            var barra = ObtenerFuenteBrandingHoja("barra.png");
            var escudo = ObtenerFuenteBrandingHoja("escudo.png");
            var dgca = ObtenerFuenteBrandingHoja("DGCA.png");

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(escudo) || string.IsNullOrWhiteSpace(dgca))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}"
                + ".header{{position:relative;width:194mm;height:26mm;}}"
                + ".barra{{position:absolute;top:0;right:0;width:129mm;height:3.2mm;}}"
                + ".escudo{{position:absolute;left:0;top:6.2mm;width:34mm;height:auto;}}"
                + ".dgca{{position:absolute;right:0;top:8.2mm;width:82mm;height:auto;}}</style>"
                + "</head><body><div class=\"header\">"
                + "<img class=\"barra\" src=\"{0}\" alt=\"\" />"
                + "<img class=\"escudo\" src=\"{1}\" alt=\"Escudo Republica del Ecuador\" />"
                + "<img class=\"dgca\" src=\"{2}\" alt=\"Direccion General de Aviacion Civil\" />"
                + "</div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(escudo),
                HttpUtility.HtmlAttributeEncode(dgca));
        }

        private string ConstruirHtmlFooterHoja()
        {
            var barra = ObtenerFuenteBrandingHoja("barra.png");
            var direccion = ObtenerFuenteBrandingHoja("direccion.png");
            var nuevo = ObtenerFuenteBrandingHoja("nuevo.png");

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(direccion) || string.IsNullOrWhiteSpace(nuevo))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}"
                + ".footer{{position:relative;width:194mm;height:26mm;}}"
                + ".barra{{position:absolute;left:0;top:0;width:72mm;height:3.2mm;}}"
                + ".direccion{{position:absolute;left:0mm;top:7.2mm;width:64mm;height:auto;}}"
                + ".nuevo{{position:absolute;right:0;top:6.2mm;width:44mm;height:auto;}}</style>"
                + "</head><body><div class=\"footer\">"
                + "<img class=\"barra\" src=\"{0}\" alt=\"\" />"
                + "<img class=\"direccion\" src=\"{1}\" alt=\"Direccion DGAC\" />"
                + "<img class=\"nuevo\" src=\"{2}\" alt=\"El Nuevo Ecuador\" />"
                + "</div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(direccion),
                HttpUtility.HtmlAttributeEncode(nuevo));
        }

        private string ConstruirHtmlBrandingFallback(bool esHeader)
        {
            var assets = PdfBrandingHelper.ResolveAssets(Server, "CoordinacionJefaturaController.CrearArchivoBrandingTemporal");
            var imageSrc = esHeader
                ? ObtenerFuenteBranding(assets != null ? assets.HeaderPhysicalPath : null, assets != null ? assets.HeaderDataUri : null)
                : ObtenerFuenteBranding(assets != null ? assets.FooterPhysicalPath : null, assets != null ? assets.FooterDataUri : null);

            if (string.IsNullOrWhiteSpace(imageSrc))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;background:transparent;}}"
                + ".wrap{{width:100%;text-align:center;line-height:0;}}"
                + ".wrap img{{display:block;width:194mm;max-width:194mm;height:auto;margin:0 auto;}}</style>"
                + "</head><body><div class=\"wrap\"><img src=\"{0}\" alt=\"{1}\" /></div></body></html>",
                HttpUtility.HtmlAttributeEncode(imageSrc),
                esHeader ? "Header institucional DGAC" : "Footer institucional DGAC");
        }

        private string ObtenerFuenteBrandingHoja(string fileName)
        {
            var physicalPath = Server.MapPath("~/Content/assets/imganes/hoja/" + fileName);
            if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
            {
                return null;
            }

            return ConvertirRutaFisicaAUrlArchivo(physicalPath);
        }

        private static string ObtenerFuenteBranding(string physicalPath, string dataUri)
        {
            if (!string.IsNullOrWhiteSpace(physicalPath) && System.IO.File.Exists(physicalPath))
            {
                return ConvertirRutaFisicaAUrlArchivo(physicalPath);
            }

            return dataUri;
        }

        private static string ConvertirRutaFisicaAUrlArchivo(string physicalPath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                return null;
            }

            return "file:///" + physicalPath.Replace('\\', '/');
        }

        private static string ConstruirPuntosContactoOperacionales(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            var bloques = new List<string>();
            if (!string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre) || !string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico))
            {
                bloques.Add("Representante Tecnico: " + (solicitud.TecnicoResponsableNombre ?? "Pendiente") + Environment.NewLine
                    + "Correo: " + (solicitud.CorreoRepresentanteTecnico ?? solicitud.Email ?? "Pendiente") + Environment.NewLine
                    + "Telefono: " + (solicitud.Telefono ?? "Pendiente"));
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal))
            {
                bloques.Add("Representante Legal: " + solicitud.RepresentanteLegal + Environment.NewLine
                    + "Direccion: " + (solicitud.Direccion ?? "Pendiente") + Environment.NewLine
                    + "Telefono: " + (solicitud.Telefono ?? "Pendiente") + Environment.NewLine
                    + "Correo: " + (solicitud.Email ?? "Pendiente"));
            }

            return bloques.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, bloques) : null;
        }

        private bool EsCertificadoDigitalValido(HttpPostedFileBase archivo, out string mensaje)
        {
            mensaje = string.Empty;
            if (archivo == null || archivo.ContentLength <= 0)
            {
                mensaje = "Debe cargar un certificado digital en formato .p12 o .pfx.";
                return false;
            }

            var extension = Path.GetExtension(archivo.FileName ?? string.Empty);
            if (!string.Equals(extension, ".p12", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".pfx", StringComparison.OrdinalIgnoreCase))
            {
                mensaje = "Solo se admiten certificados digitales .p12 o .pfx.";
                return false;
            }

            if (archivo.ContentLength > 5 * 1024 * 1024)
            {
                mensaje = "El certificado digital supera el tamaño máximo permitido.";
                return false;
            }

            return true;
        }

        private bool TryCargarCertificadoInstitucional(out byte[] certificadoBytes, out string password, out string mensajeError)
        {
            certificadoBytes = null;
            password = null;
            mensajeError = null;

            var rutaConfigurada = System.Configuration.ConfigurationManager.AppSettings["Aocr:CertificadoInstitucionalRuta"];
            var passwordConfigurado = System.Configuration.ConfigurationManager.AppSettings["Aocr:CertificadoInstitucionalPassword"];

            if (string.IsNullOrWhiteSpace(rutaConfigurada))
            {
                mensajeError = "Debe cargar un certificado digital .p12/.pfx. (No hay certificado institucional configurado en el servidor.)";
                return false;
            }

            string rutaAbsoluta;
            try
            {
                rutaAbsoluta = rutaConfigurada.StartsWith("~", StringComparison.Ordinal)
                    ? Server.MapPath(rutaConfigurada)
                    : rutaConfigurada;
            }
            catch (Exception ex)
            {
                mensajeError = "Ruta del certificado institucional no válida: " + ex.Message;
                return false;
            }

            if (!System.IO.File.Exists(rutaAbsoluta))
            {
                mensajeError = "No se encontró el archivo del certificado institucional configurado (" + rutaAbsoluta + ").";
                return false;
            }

            try
            {
                certificadoBytes = System.IO.File.ReadAllBytes(rutaAbsoluta);
                password = passwordConfigurado ?? string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                mensajeError = "No se pudo leer el certificado institucional: " + ex.Message;
                return false;
            }
        }

        private static string ConstruirContenidoQrFirmaAocr(ValidarAocrSolicitudItemViewModel item, AocrDocumentoEdicionViewModel model, string tipoDocumento, InformacionCertificadoDigital infoCertificado, string nombreFirmante)
        {
            var solicitudId = model != null ? model.SolicitudId : 0;
            var numeroSolicitud = item != null && item.Solicitud != null
                ? (item.Solicitud.NumeroSolicitud ?? item.Solicitud.CodigoSolicitud.ToString())
                : solicitudId.ToString();
            var numeroAocr = model != null ? model.NumeroAocr : item != null ? item.NumeroAocr : null;
            var fechaFirma = DateTime.Now;

            var partes = new List<string>
            {
                "Sistema=AOCR DGAC",
                "Documento=" + (tipoDocumento ?? string.Empty),
                "SolicitudId=" + solicitudId,
                "NumeroSolicitud=" + (numeroSolicitud ?? string.Empty),
                "NumeroAOCR=" + (numeroAocr ?? string.Empty),
                "Firmante=" + (nombreFirmante ?? string.Empty),
                "Cargo=" + (model != null ? model.FirmanteCargo : string.Empty),
                "FechaFirma=" + fechaFirma.ToString("yyyy-MM-dd HH:mm:ss"),
                "Certificado=" + (infoCertificado != null ? infoCertificado.SujetoCertificado : string.Empty),
                "VigenciaHasta=" + ((infoCertificado != null && infoCertificado.VigenteHasta.HasValue) ? infoCertificado.VigenteHasta.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty)
            };

            return string.Join(" | ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private string GuardarDocumentoFirmadoAocr(int solicitudId, string tipoDocumento, string nombreArchivo, byte[] contenido)
        {
            var carpetaRelativa = "~/App_Data/Uploads/AOCR/Firmados/" + solicitudId;
            var carpetaAbsoluta = Server.MapPath(carpetaRelativa);
            if (!Directory.Exists(carpetaAbsoluta))
            {
                Directory.CreateDirectory(carpetaAbsoluta);
            }

            var prefijo = string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase)
                ? "reconocimiento"
                : "condiciones_limitaciones";
            var nombreSeguro = prefijo + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + solicitudId + ".pdf";
            var rutaAbsoluta = Path.Combine(carpetaAbsoluta, nombreSeguro);
            System.IO.File.WriteAllBytes(rutaAbsoluta, contenido ?? new byte[0]);

            return VirtualPathUtility.ToAbsolute(carpetaRelativa.TrimStart('~') + "/" + nombreSeguro);
        }

        private void AplicarPosicionFirmaAocr(AocrDocumentoEdicionViewModel model, string tipoDocumento)
        {
            if (model == null)
            {
                return;
            }

            var posicion = _aocrFirmaPosicionDocumentoDao.Obtener(model.SolicitudId, tipoDocumento, "AOCR_FIRMANTE");
            if (EsPosicionFirmaAocrLegada(posicion))
            {
                posicion = null;
            }

            if (posicion != null)
            {
                model.UsaPosicionFirmaPersonalizada = true;
                model.NumeroPaginaFirma = posicion.NumeroPagina > 0 ? posicion.NumeroPagina : 1;
                model.PosicionFirmaX = FormatearDecimalInvariante(posicion.PosicionXRatio);
                model.PosicionFirmaY = FormatearDecimalInvariante(posicion.PosicionYRatio);
                model.AnchoFirma = FormatearDecimalInvariante(posicion.AnchoRatio);
                model.AltoFirma = FormatearDecimalInvariante(posicion.AltoRatio);
                return;
            }

            model.UsaPosicionFirmaPersonalizada = false;
            model.NumeroPaginaFirma = 2;
            model.PosicionFirmaX = "0.020000";
            model.PosicionFirmaY = "0.060000";
            model.AnchoFirma = "0.940000";
            model.AltoFirma = "0.820000";
        }

        private PosicionFirmaVisualPdf ConstruirPosicionFirmaVisualPdf(AocrDocumentoEdicionViewModel model)
        {
            if (model == null || !model.UsaPosicionFirmaPersonalizada)
            {
                return null;
            }

            return ConstruirPosicionFirmaVisualPdf(new AocrFirmaPosicionEdicionViewModel
            {
                SolicitudId = model.SolicitudId,
                InspeccionId = model.InspeccionId,
                TipoDocumento = model.TipoDocumento,
                RolFirmante = "AOCR_FIRMANTE",
                NumeroPaginaFirma = model.NumeroPaginaFirma,
                PosicionFirmaX = model.PosicionFirmaX,
                PosicionFirmaY = model.PosicionFirmaY,
                AnchoFirma = model.AnchoFirma,
                AltoFirma = model.AltoFirma
            });
        }

        private static PosicionFirmaVisualPdf ConstruirPosicionFirmaVisualPdf(AocrFirmaPosicionEdicionViewModel model)
        {
            if (model == null)
            {
                return null;
            }

            decimal posicionX;
            decimal posicionY;
            decimal ancho;
            decimal alto;

            if (!TryParseDecimalInvariant(model.PosicionFirmaX, out posicionX)
                || !TryParseDecimalInvariant(model.PosicionFirmaY, out posicionY)
                || !TryParseDecimalInvariant(model.AnchoFirma, out ancho)
                || !TryParseDecimalInvariant(model.AltoFirma, out alto))
            {
                return null;
            }

            return new PosicionFirmaVisualPdf
            {
                NumeroPagina = model.NumeroPaginaFirma > 0 ? model.NumeroPaginaFirma : 1,
                PosicionXRatio = (float)posicionX,
                PosicionYRatio = (float)posicionY,
                AnchoRatio = (float)ancho,
                AltoRatio = (float)alto
            };
        }

        private void GuardarPosicionFirmaAocr(ValidarAocrSolicitudItemViewModel item, AocrDocumentoEdicionViewModel model, string tipoDocumento, PosicionFirmaVisualPdf posicion, string origenPosicion)
        {
            GuardarPosicionFirmaAocr(
                item,
                new AocrFirmaPosicionEdicionViewModel
                {
                    SolicitudId = model != null ? model.SolicitudId : 0,
                    InspeccionId = model != null ? model.InspeccionId : null,
                    TipoDocumento = tipoDocumento,
                    RolFirmante = "AOCR_FIRMANTE",
                    NumeroPaginaFirma = posicion != null ? posicion.NumeroPagina : 1,
                    PosicionFirmaX = posicion != null ? FormatearDecimalInvariante((decimal)posicion.PosicionXRatio) : null,
                    PosicionFirmaY = posicion != null ? FormatearDecimalInvariante((decimal)posicion.PosicionYRatio) : null,
                    AnchoFirma = posicion != null ? FormatearDecimalInvariante((decimal)posicion.AnchoRatio) : null,
                    AltoFirma = posicion != null ? FormatearDecimalInvariante((decimal)posicion.AltoRatio) : null
                },
                tipoDocumento,
                posicion,
                origenPosicion);
        }

        private void GuardarPosicionFirmaAocr(ValidarAocrSolicitudItemViewModel item, AocrFirmaPosicionEdicionViewModel model, string tipoDocumento, PosicionFirmaVisualPdf posicion, string origenPosicion)
        {
            if (item == null || item.Solicitud == null || posicion == null || !posicion.EsValida)
            {
                return;
            }

            _aocrFirmaPosicionDocumentoDao.Guardar(new AocrFirmaPosicionDocumento
            {
                CodigoSolicitud = item.Solicitud.CodigoSolicitud,
                CodigoInspeccion = model != null ? model.InspeccionId : item.Inspeccion != null ? (int?)item.Inspeccion.CodigoInspeccion : null,
                TipoDocumento = tipoDocumento,
                RolFirmante = "AOCR_FIRMANTE",
                OrigenPosicion = string.IsNullOrWhiteSpace(origenPosicion) ? "PUNTERO" : origenPosicion,
                NumeroPagina = posicion.NumeroPagina,
                PosicionXRatio = (decimal)posicion.PosicionXRatio,
                PosicionYRatio = (decimal)posicion.PosicionYRatio,
                AnchoRatio = (decimal)posicion.AnchoRatio,
                AltoRatio = (decimal)posicion.AltoRatio,
                CodigoUsuario = ObtenerUsuarioActualIdSeguro() > 0 ? (int?)ObtenerUsuarioActualIdSeguro() : null,
                UsuarioNombre = User != null && User.Identity != null ? User.Identity.Name : null
            });
        }

        private static bool TryParseDecimalInvariant(string value, out decimal result)
        {
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool EsPosicionFirmaAocrLegada(AocrFirmaPosicionDocumento posicion)
        {
            if (posicion == null)
            {
                return false;
            }

            return (SonDecimalesCercanos(posicion.PosicionXRatio, 0.642017m)
                && SonDecimalesCercanos(posicion.PosicionYRatio, 0.161520m)
                && SonDecimalesCercanos(posicion.AnchoRatio, 0.258824m)
                && SonDecimalesCercanos(posicion.AltoRatio, 0.073634m))
                || (posicion.NumeroPagina >= 1
                    && posicion.AnchoRatio <= 0.40m
                    && posicion.AltoRatio <= 0.20m);
        }

        private static bool SonDecimalesCercanos(decimal valor, decimal esperado)
        {
            return Math.Abs(valor - esperado) <= 0.0005m;
        }

        private static string FormatearDecimalInvariante(decimal value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private void RegistrarFirmaDigitalAocr(
            ValidarAocrSolicitudItemViewModel item,
            AocrDocumentoEdicionViewModel model,
            string tipoDocumento,
            string nombreArchivo,
            string rutaDocumento,
            string hashDocumento,
            string codigoQr,
            InformacionCertificadoDigital infoCertificado,
            string nombreFirmante,
            bool sincronizarCertificadoOficial,
            long tamanioPdfFirmado = 0,
            string firmadoPorRol = null)
        {
            var firma = new AocrFirmaDocumento
            {
                CodigoSolicitud = model != null ? model.SolicitudId : 0,
                CodigoInspeccion = model != null ? model.InspeccionId : null,
                TipoDocumento = tipoDocumento,
                NumeroAocr = model != null ? model.NumeroAocr : item != null ? item.NumeroAocr : null,
                NombreArchivo = nombreArchivo,
                RutaDocumento = rutaDocumento,
                HashDocumento = hashDocumento,
                TamanioPdfFirmado = tamanioPdfFirmado > 0 ? (long?)tamanioPdfFirmado : null,
                FirmadoPorRol = !string.IsNullOrWhiteSpace(firmadoPorRol) ? firmadoPorRol : null,
                CodigoQr = codigoQr,
                SujetoCertificado = infoCertificado != null ? infoCertificado.SujetoCertificado : null,
                NombreFirmante = nombreFirmante,
                CargoFirmante = model != null ? model.FirmanteCargo : null,
                FechaFirma = DateTime.Now,
                CodigoUsuario = ObtenerUsuarioActualIdSeguro() > 0 ? (int?)ObtenerUsuarioActualIdSeguro() : null,
                UsuarioNombre = User != null && User.Identity != null ? User.Identity.Name : null
            };

            _aocrFirmaDocumentoDao.Registrar(firma);

            if (sincronizarCertificadoOficial && item != null && item.Solicitud != null)
            {
                var certificado = item.Certificado ?? _certificadoDao.ObtenerPorSolicitud(item.Solicitud.CodigoSolicitud);
                if (certificado != null && certificado.CodigoCertificado > 0)
                {
                    certificado.RutaDocumento = rutaDocumento;
                    certificado.RutaPdf = rutaDocumento;
                    certificado.NumeroCertificado = model != null ? model.NumeroAocr : item.NumeroAocr;
                    certificado.Estado = "APROBADO";
                    certificado.AprobadoPor = nombreFirmante;
                    certificado.EmitidoPor = User != null && User.Identity != null ? User.Identity.Name : certificado.EmitidoPor;
                    certificado.FechaEmision = certificado.FechaEmision ?? DateTime.Now;
                    certificado.FechaVencimiento = model != null ? model.FechaVencimiento : certificado.FechaVencimiento;
                    certificado.UpdatedAt = DateTime.Now;
                    _certificadoDao.Actualizar(certificado);
                }
            }

            if (item != null && item.Solicitud != null)
            {
                var estadoActual = EstadoSolicitud.Normalizar(item.Solicitud.Estado);
                var observacion = "Firma digital AOCR registrada. Documento=" + tipoDocumento
                    + "; Archivo=" + (nombreArchivo ?? "N/D")
                    + "; Hash=" + (hashDocumento ?? "N/D")
                    + "; Ruta=" + (rutaDocumento ?? "N/D");
                var usuarioId = ObtenerUsuarioActualIdSeguro();
                if (usuarioId > 0)
                {
                    _historialEstadoDao.RegistrarCambio(item.Solicitud.CodigoSolicitud, estadoActual, estadoActual, usuarioId, observacion);
                }
            }
        }

        private static List<AocrCondicionAeronaveFilaViewModel> ConstruirFilasAeronavesCondiciones(IEnumerable<AeronaveSolicitud> aeronaves)
        {
            var filas = (aeronaves ?? new List<AeronaveSolicitud>())
                .Where(a => a != null)
                .Select(a => new AocrCondicionAeronaveFilaViewModel
                {
                    ModeloTipo = ((a.Marca ?? string.Empty) + " " + (a.Modelo ?? string.Empty)).Trim(),
                    Matricula = a.Matricula,
                    Serie = a.Serie,
                    Uio = string.Empty,
                    Gye = string.Empty,
                    Mec = string.Empty,
                    Ltx = string.Empty
                })
                .ToList();

            while (filas.Count < 4)
            {
                filas.Add(new AocrCondicionAeronaveFilaViewModel());
            }

            return filas;
        }

        private static string ConstruirFirmantes(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return "Pendiente";
            }

            var firmantes = new List<string>();
            if (!string.IsNullOrWhiteSpace(informe.UsuarioFirma1))
            {
                firmantes.Add("Inspector: " + informe.UsuarioFirma1);
            }

            if (!string.IsNullOrWhiteSpace(informe.UsuarioFirma2))
            {
                firmantes.Add("Direccion/Jefatura: " + informe.UsuarioFirma2);
            }

            return firmantes.Count > 0 ? string.Join(" | ", firmantes) : "Pendiente";
        }

        private static string GenerarNumeroAocr(int codigoSolicitud, DateTime fechaBase)
        {
            return string.Format("AOCR-{0}-{1:D4}", fechaBase.Year, codigoSolicitud);
        }

        private string ResolverRutaDocumento(string rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa))
            {
                return null;
            }

            var ruta = rutaRelativa.Trim();
            if (Path.IsPathRooted(ruta))
            {
                return ruta;
            }

            if (ruta.StartsWith("~"))
            {
                return Server.MapPath(ruta);
            }

            return Server.MapPath("~" + (ruta.StartsWith("/") ? ruta : "/" + ruta));
        }

        private void RegistrarTrazabilidadDocumento(SolicitudAOCR solicitud, string tipoDocumento, string accion)
        {
            if (solicitud == null)
            {
                return;
            }

            var usuarioId = ObtenerUsuarioActualIdSeguro();
            if (usuarioId <= 0)
            {
                return;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            var observacion = accion + " documento AOCR [" + tipoDocumento + "] desde Firma institucional AOCR.";
            _historialEstadoDao.RegistrarCambio(solicitud.CodigoSolicitud, estadoActual, estadoActual, usuarioId, observacion);
        }

  private int ObtenerUsuarioActualIdSeguro()
        {
            var ctx = _usuarioContexto.ObtenerContextoActual();
            return ctx.UsuarioId;
        }

        private string RegistrarErrorValidacionAocr(string operacion, Exception ex, int? solicitudId = null, int? inspeccionId = null, string tipoDocumento = null)
        {
            var referencia = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
            var detalle = ConstruirDetalleExcepcion(ex);
            var mensaje = string.Format(
                "Firma institucional AOCR fallo. Ref={0}; Operacion={1}; SolicitudId={2}; InspeccionId={3}; TipoDocumento={4}",
                referencia,
                operacion ?? "N/A",
                solicitudId.HasValue ? solicitudId.Value.ToString() : "N/A",
                inspeccionId.HasValue ? inspeccionId.Value.ToString() : "N/A",
                string.IsNullOrWhiteSpace(tipoDocumento) ? "N/A" : tipoDocumento);

            LogBL.RegistrarError(mensaje, detalle, "CoordinacionJefaturaController", ObtenerUsuarioActualIdSeguro());

            try
            {
                var logDir = Server.MapPath("~/App_Data/Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var logPath = Path.Combine(logDir, "ValidarAocrRuntime.log");
                System.IO.File.AppendAllText(
                    logPath,
                    string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}{2}{2}",
                        DateTime.Now,
                        mensaje + Environment.NewLine + detalle,
                        Environment.NewLine));
            }
            catch
            {
                // No bloquear el flujo si el log local falla.
            }

            return referencia;
        }

        private static string ConstruirDetalleExcepcion(Exception ex)
        {
            if (ex == null)
            {
                return "Excepcion nula.";
            }

            var builder = new StringBuilder();
            var actual = ex;
            var profundidad = 0;

            while (actual != null)
            {
                builder.AppendLine(string.Format("Nivel {0}: {1}", profundidad, actual.GetType().FullName));
                builder.AppendLine("Mensaje: " + actual.Message);

                var exPg = actual as PostgresException;
                if (exPg != null)
                {
                    builder.AppendLine("SqlState: " + exPg.SqlState);
                    builder.AppendLine("MessageText: " + exPg.MessageText);
                    builder.AppendLine("Detail: " + exPg.Detail);
                    builder.AppendLine("Hint: " + exPg.Hint);
                    builder.AppendLine("Where: " + exPg.Where);
                    builder.AppendLine("SchemaName: " + exPg.SchemaName);
                    builder.AppendLine("TableName: " + exPg.TableName);
                    builder.AppendLine("ColumnName: " + exPg.ColumnName);
                    builder.AppendLine("ConstraintName: " + exPg.ConstraintName);
                    builder.AppendLine("Position: " + exPg.Position);
                    builder.AppendLine("Routine: " + exPg.Routine);
                    builder.AppendLine("File: " + exPg.File);
                    builder.AppendLine("Line: " + exPg.Line);
                }

                builder.AppendLine("StackTrace:");
                builder.AppendLine(actual.StackTrace ?? "(sin stacktrace)");
                builder.AppendLine();

                actual = actual.InnerException;
                profundidad++;
            }

            return builder.ToString();
        }
    }
}
