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
using CapaPresentacion.Models;
using Npgsql;
using Rotativa;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
    public class CoordinacionJefaturaController : Controller
    {
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

        [Authorize(Roles = "Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DashboardGerencial()
        {
            return RedirectToAction("DashboardGerencial", "Direccion");
        }

        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones")]
        public ActionResult DashboardInspeccion(string compania = null, string inspector = null, string estado = null, string quickFilter = null)
        {
            var urlHelper = new UrlHelper(ControllerContext.RequestContext);
            var puedeGestionarAsignacion = User.IsInRole("Administrador")
                || User.IsInRole("Coordinador")
                || User.IsInRole("CoordinadorInspecciones");
            var puedeVerPendientesDirdac = User.IsInRole("Administrador") || User.IsInRole("DIRDAC") || User.IsInRole("Direccion") || User.IsInRole("Director") || User.IsInRole("JefaturaTecnica") || User.IsInRole("Jefe");
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

            var solicitudIds = documentosPorSolicitud.Keys
                .Concat(inspeccionesPorSolicitud.Keys)
                .Distinct()
                .ToList();

            var items = new List<DashboardGestionIntegralItemViewModel>();

            foreach (var codigoSolicitud in solicitudIds)
            {
                DashboardInspeccionSeguimientoData inspeccion;
                DashboardInspeccionDocumentoData documento;
                inspeccionesPorSolicitud.TryGetValue(codigoSolicitud, out inspeccion);
                documentosPorSolicitud.TryGetValue(codigoSolicitud, out documento);

                var numeroSolicitud = FirstNonEmpty(documento != null ? documento.NumeroSolicitud : null, inspeccion != null ? inspeccion.NumeroSolicitud : null, codigoSolicitud.ToString());
                var compania = FirstNonEmpty(documento != null ? documento.Compania : null, inspeccion != null ? inspeccion.Compania : null, "No especificada");
                var tipo = ResolverTipoGestion(FirstNonEmpty(documento != null ? documento.TipoOperacion : null, inspeccion != null ? inspeccion.TipoOperacion : null));
                var estadoDocumental = string.IsNullOrWhiteSpace(documento != null ? documento.EstadoDocumento : null) ? "PENDIENTE" : documento.EstadoDocumento;
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
                    inspeccion != null ? inspeccion.FechaAsignacion : null);

                var urlDetalle = inspeccion != null
                    ? urlHelper.Action("Detalle", "Inspeccion", new { id = inspeccion.CodigoInspeccion })
                    : urlHelper.Action("Detalle", "SolicitudAOCR", new { id = codigoSolicitud });
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
                    ? urlHelper.Action("ValidarAocr", "CoordinacionJefatura", new { solicitudId = codigoSolicitud })
                    : null;
                var puedeAsignarInspector = !tieneInspector && (inspeccion == null || inspeccion.PuedeAsignarInspector);
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
                    textoAccionPrincipal = "Validar AOCR";
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

        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult ValidarAocr(int? solicitudId = null)
        {
            // Evitar fuga de TempData["Error"] establecido por otras acciones.
            TempData.Remove("Error");

            try
            {
                var items = ConstruirItemsValidacionAocr();
                if (solicitudId.HasValue && solicitudId.Value > 0)
                {
                    items = items.Where(item => item != null && item.Solicitud != null && item.Solicitud.CodigoSolicitud == solicitudId.Value).ToList();
                }

                var model = new ValidarAocrViewModel
                {
                    Items = items
                };

                return View("~/Views/CoordinacionJefatura/ValidarAocr.cshtml", model);
            }
            catch (PostgresException exPg)
            {
                var referencia = RegistrarErrorValidacionAocr("ValidarAocr.CargarBandeja", exPg);
                TempData["Error"] = "No se pudo cargar la bandeja de Validar AOCR por un error de base de datos. Ref: " + referencia;
                return RedirectToAction("DashboardInspeccion");
            }
            catch (Exception ex)
            {
                var referencia = RegistrarErrorValidacionAocr("ValidarAocr.CargarBandeja", ex);
                TempData["Error"] = "No se pudo cargar la bandeja de Validar AOCR. Ref: " + referencia;
                return RedirectToAction("DashboardInspeccion");
            }
        }

        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult DocumentoValidacionAocr(int solicitudId, string tipo, bool descargar = false)
        {
            try
            {
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                if (solicitud == null)
                {
                    return HttpNotFound("La solicitud AOCR indicada no existe.");
                }

                var inspeccionesSolicitud = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
                var item = ConstruirItemValidacionAocr(solicitud, inspeccionesSolicitud);
                if (item == null)
                {
                    return HttpNotFound("No existe contexto disponible para el documento AOCR solicitado.");
                }

                var tipoNormalizado = NormalizarTipoDocumento(tipo);
                if (tipoNormalizado == null)
                {
                    return new HttpStatusCodeResult(400, "Tipo de documento AOCR no válido.");
                }

                var habilitadoPorModificacion = PuedeEditarCondicionesLimitacionesModificacion(item, tipoNormalizado);
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
                        return File(rutaFisicaCertificado, "application/pdf");
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
                        return File(rutaFisica, "application/pdf");
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
                        return File(rutaFirmada, "application/pdf");
                    }
                }

                var viewName = usarPlantillaOficial
                    ? "~/Views/Certificado/CertificadoAOCR.cshtml"
                    : (tipoNormalizado == "RECONOCIMIENTO"
                        ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                        : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml");
                var pdfModel = usarPlantillaOficial
                    ? (object)ConstruirCertificadoAocrViewModelOficial(documentoModel)
                    : documentoModel;
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
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult EditarDocumentoValidacionAocr(int solicitudId, string tipo)
        {
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

        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
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

                if (usarPlantillaOficial)
                {
                    return View("~/Views/Certificado/CertificadoAOCR.cshtml", ConstruirCertificadoAocrViewModelOficial(documentoModel));
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
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public JsonResult CargarDatosFirmaDigitalAocr(HttpPostedFileBase certificadoDigital, string passwordCertificado)
        {
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
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult GenerarDocumentoValidacionAocr(AocrDocumentoEdicionViewModel model, string accion = null, HttpPostedFileBase certificadoDigital = null, string passwordCertificado = null)
        {
            try
            {
                model = CompletarDocumentoEdicionDesdeFormulario(model);
                var tipoNormalizado = NormalizarTipoDocumento(model != null ? model.TipoDocumento : null);
                if (model == null || model.SolicitudId <= 0 || tipoNormalizado == null)
                {
                    return new HttpStatusCodeResult(400, "No se recibieron datos validos para generar el documento AOCR.");
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
                    return new HttpStatusCodeResult(400, "No se puede generar el documento AOCR porque faltan campos obligatorios: " + string.Join(", ", camposFaltantes) + ".");
                }

                var viewName = usarPlantillaOficial
                    ? "~/Views/Certificado/CertificadoAOCR.cshtml"
                    : (tipoNormalizado == "RECONOCIMIENTO"
                        ? "~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml"
                        : "~/Views/CoordinacionJefatura/AocrCondicionesLimitacionesPdf.cshtml");
                var pdfModel = usarPlantillaOficial
                    ? (object)ConstruirCertificadoAocrViewModelOficial(documentoModel)
                    : documentoModel;
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
        [Authorize(Roles = "Inspector,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public JsonResult GuardarPosicionFirmaAocr(AocrFirmaPosicionEdicionViewModel model)
        {
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
                .FirstOrDefault(x => x.Informe.Finalizado && x.Informe.FirmadoInspector && x.Informe.FirmadoDirdac);

            var firmaCompleta = !esModificacionDirecta && informeFirmado != null;

            // Incluir tambien solicitudes con informe tecnico firmado que aun no
            // han transicionado al estado AOCR En Revision (flujo incompleto en datos).
            var estadoPermitidoConFirma = firmaCompleta
                && (string.Equals(estadoSolicitud, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase));

            var estadoIncluido = string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, "ENVIADO_A_JEFATURA", StringComparison.OrdinalIgnoreCase)
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

            return new List<ValidarAocrDocumentoItemViewModel>
            {
                new ValidarAocrDocumentoItemViewModel
                {
                    TipoDocumento = "RECONOCIMIENTO",
                    NombreVisible = item.FirmaCompleta
                        ? "AOCR oficial unificada (paginas 1 y 2)"
                        : "Reconocimiento de Certificado de Explotador de Servicios Aereos",
                    Estado = item.FirmaCompleta ? "Disponible" : (esModificacionDirecta ? "No aplica" : "Pendiente"),
                    Observacion = item.FirmaCompleta
                        ? "La salida oficial AOCR1 integra reconocimiento y condiciones/limitaciones en un solo PDF institucional."
                        : (esModificacionDirecta
                            ? "La modificación directa de Condiciones y Limitaciones no genera un reconocimiento adicional."
                            : "Falta firma final del informe tecnico para habilitar este documento."),
                    UrlEditar = item.FirmaCompleta ? urlHelper.Action("EditarDocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "RECONOCIMIENTO" }) : null,
                    UrlVer = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "RECONOCIMIENTO", descargar = false }) : null,
                    UrlDescargar = item.FirmaCompleta ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "RECONOCIMIENTO", descargar = true }) : null,
                    FechaDocumento = item.Certificado != null ? (item.Certificado.UpdatedAt ?? item.Certificado.FechaEmision ?? fechaBase) : fechaBase,
                    Disponible = item.FirmaCompleta
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
                    UrlEditar = (item.FirmaCompleta || esModificacionDirecta) ? urlHelper.Action("EditarDocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES" }) : null,
                    UrlVer = (item.FirmaCompleta || esModificacionDirecta) ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES", descargar = false }) : null,
                    UrlDescargar = item.FirmaCompleta
                        ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES", descargar = true })
                        : (condicionesFirmadas ? urlHelper.Action("DocumentoValidacionAocr", "CoordinacionJefatura", new { solicitudId = item.Solicitud.CodigoSolicitud, tipo = "CONDICIONES_LIMITACIONES", descargar = true }) : null),
                    FechaDocumento = fechaBase,
                    Disponible = item.FirmaCompleta || condicionesFirmadas
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
                    || User.IsInRole("DirectorGeneral")
                    || User.IsInRole("JefaturaTecnica"));
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
            if (tipoNormalizado == "RECONOCIMIENTO")
            {
                return tipoNormalizado;
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
            var switches = PdfBrandingHelper.StandardRotativaSwitches
                + " --disable-smart-shrinking --margin-top 30mm --margin-bottom 26mm --margin-left 8mm --margin-right 8mm --header-spacing 0 --footer-spacing 0";

            var headerHtmlPath = CrearArchivoBrandingTemporal(true);
            var footerHtmlPath = CrearArchivoBrandingTemporal(false);

            if (!string.IsNullOrWhiteSpace(headerHtmlPath))
            {
                switches += " --header-html \"" + ConvertirRutaFisicaAUrlArchivo(headerHtmlPath) + "\"";
            }

            if (!string.IsNullOrWhiteSpace(footerHtmlPath))
            {
                switches += " --footer-html \"" + ConvertirRutaFisicaAUrlArchivo(footerHtmlPath) + "\"";
            }

            return switches;
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
            bool sincronizarCertificadoOficial)
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
            var observacion = accion + " documento AOCR [" + tipoDocumento + "] desde Validar AOCR.";
            _historialEstadoDao.RegistrarCambio(solicitud.CodigoSolicitud, estadoActual, estadoActual, usuarioId, observacion);
        }

        private int ObtenerUsuarioActualIdSeguro()
        {
            int usuarioId;
            if (Session != null && Session["IdUsuario"] != null && int.TryParse(Session["IdUsuario"].ToString(), out usuarioId))
            {
                return usuarioId;
            }

            if (Session != null && Session["CodigoUsuario"] != null && int.TryParse(Session["CodigoUsuario"].ToString(), out usuarioId))
            {
                return usuarioId;
            }

            return 0;
        }

        private string RegistrarErrorValidacionAocr(string operacion, Exception ex, int? solicitudId = null, int? inspeccionId = null, string tipoDocumento = null)
        {
            var referencia = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
            var detalle = ConstruirDetalleExcepcion(ex);
            var mensaje = string.Format(
                "Validar AOCR fallo. Ref={0}; Operacion={1}; SolicitudId={2}; InspeccionId={3}; TipoDocumento={4}",
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
