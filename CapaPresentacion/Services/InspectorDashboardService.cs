using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaPresentacion.Models.ViewModels;

namespace CapaPresentacion.Services
{
    public class InspectorDashboardService
    {
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly HallazgoDAO _hallazgoDao = new HallazgoDAO();
        private readonly DocumentoDAO _documentoDao = new DocumentoDAO();

        public InspectorDashboardViewModel ObtenerDashboard(
            int codigoInspector,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string estado,
            string compania,
            int? codigoSolicitud,
            bool puedeVerGlobal = false)
        {
            return ObtenerDashboard(new[] { codigoInspector }, fechaDesde, fechaHasta, estado, compania, codigoSolicitud, puedeVerGlobal);
        }

        public InspectorDashboardViewModel ObtenerDashboard(
            IEnumerable<int> codigosInspector,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string estado,
            string compania,
            int? codigoSolicitud,
            bool puedeVerGlobal = false)
        {
            var stopwatch = Stopwatch.StartNew();
            var inspectores = (codigosInspector ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (!puedeVerGlobal && inspectores.Length == 0)
            {
                throw new ArgumentException("Código de inspector inválido.");
            }

            var vm = new InspectorDashboardViewModel
            {
                CodigoInspector = inspectores.FirstOrDefault(),
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Estado = estado,
                Compania = compania,
                CodigoSolicitud = codigoSolicitud,
                PuedeVerGlobal = puedeVerGlobal,
                TieneFiltrosActivos = fechaDesde.HasValue
                    || fechaHasta.HasValue
                    || !string.IsNullOrWhiteSpace(estado)
                    || !string.IsNullOrWhiteSpace(compania)
                    || codigoSolicitud.HasValue
            };

            try
            {
                var universo = ObtenerUniversoInspecciones(inspectores, puedeVerGlobal);
                vm.EstadosDisponibles = universo
                    .Select(x => EstadosInspeccion.NormalizarEstado(x.Estado))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var solicitudes = CargarSolicitudes(universo);
                var inspeccionesFiltradas = universo
                    .Where(inspeccion => CumpleFiltros(inspeccion, solicitudes, fechaDesde, fechaHasta, estado, compania, codigoSolicitud))
                    .ToList();

                vm.SinResultadosFiltro = vm.TieneFiltrosActivos && inspeccionesFiltradas.Count == 0;
                vm.InspeccionesAsignadas = inspeccionesFiltradas.Count;

                if (inspeccionesFiltradas.Count == 0)
                {
                    return vm;
                }

                var informes = CargarInformes(inspeccionesFiltradas);
                var hallazgos = CargarHallazgos(inspeccionesFiltradas);
                var documentos = CargarDocumentosPorSolicitud(inspeccionesFiltradas);
                var filas = ConstruirFilas(inspeccionesFiltradas, solicitudes, informes, hallazgos, documentos);

                vm.InspeccionesPendientes = filas.Count(EsPendiente);
                vm.InspeccionesEnEjecucion = filas.Count(EsEjecucionActiva);
                vm.InspeccionesConNc = filas.Count(TieneNcAbierta);
                vm.InspeccionesCerradas = filas.Count(EsCerrada);
                vm.InspeccionesRequierenNueva = filas.Count(RequiereNuevaInspeccion);
                vm.DocumentosPendientesRevision = filas.Count(TieneDocumentacionPendiente);
                vm.DocumentacionSubsanadaRt = filas.Count(TieneDocumentacionSubsanadaRt);
                vm.InformesTecnicosPendientes = filas.Count(TieneInformeTecnicoPendiente);
                vm.TiempoPromedioAtencionHoras = CalcularTiempoPromedioHoras(filas);

                var tendencia = ConstruirTendencia(filas);
                vm.TendenciaAtencionEtiquetas = tendencia.Select(x => x.Label).ToList();
                vm.TendenciaAtencionValores = tendencia.Select(x => x.Value).ToList();

                vm.UltimasInspecciones = filas
                    .OrderByDescending(x => x.UltimaActividad ?? DateTime.MinValue)
                    .ThenByDescending(x => x.Inspeccion.CodigoInspeccion)
                    .Take(10)
                    .Select(x => new InspectorInspeccionItemViewModel
                    {
                        CodigoInspeccion = x.Inspeccion.CodigoInspeccion,
                        CodigoSolicitud = x.Inspeccion.CodigoSolicitud,
                        NumeroInspeccion = ObtenerNumeroInspeccionVisible(x.Inspeccion, x.Solicitud),
                        Estado = x.EstadoNormalizado,
                        Resultado = (x.Inspeccion.Resultado ?? string.Empty).Trim(),
                        Operador = ObtenerOperadorVisible(x.Solicitud),
                        FechaProgramada = x.Inspeccion.FechaProgramada,
                        UltimaActualizacion = x.UltimaActividad,
                        TieneNoConformidadAbierta = x.TieneHallazgosAbiertos
                    })
                    .ToList();

                vm.AlertasUrgentes = ConstruirAlertas(filas)
                    .OrderByDescending(x => x.Fecha)
                    .ThenByDescending(x => x.Severidad, StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();
            }
            catch (Exception ex)
            {
                Trace.TraceError("InspectorDashboardService.ObtenerDashboard error. Inspectores={0}. Error={1}", string.Join(",", inspectores), ex);
            }
            finally
            {
                stopwatch.Stop();
                Trace.TraceInformation("[DashboardInspector] Servicio completado en {0} ms. Inspectores={1}", stopwatch.ElapsedMilliseconds, inspectores.Length == 0 ? "GLOBAL" : string.Join(",", inspectores));
            }

            return vm;
        }

        private List<Inspeccion> ObtenerUniversoInspecciones(IEnumerable<int> inspectores, bool puedeVerGlobal)
        {
            if (puedeVerGlobal)
            {
                return (_inspeccionDao.ListarTodas() ?? new List<Inspeccion>())
                    .Where(x => x != null && x.CodigoInspeccion > 0)
                    .GroupBy(x => x.CodigoInspeccion)
                    .Select(g => g.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada ?? DateTime.MinValue).First())
                    .ToList();
            }

            return (inspectores ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .SelectMany(id => _inspeccionDao.ListarPorInspector(id) ?? new List<Inspeccion>())
                .Where(x => x != null && x.CodigoInspeccion > 0)
                .GroupBy(x => x.CodigoInspeccion)
                .Select(g => g.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada ?? DateTime.MinValue).First())
                .ToList();
        }

        private Dictionary<int, SolicitudAOCR> CargarSolicitudes(IEnumerable<Inspeccion> inspecciones)
        {
            var idsSolicitud = new HashSet<int>((inspecciones ?? Enumerable.Empty<Inspeccion>())
                .Where(x => x != null && x.CodigoSolicitud > 0)
                .Select(x => x.CodigoSolicitud));

            if (idsSolicitud.Count == 0)
            {
                return new Dictionary<int, SolicitudAOCR>();
            }

            return (_solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>())
                .Where(x => x != null && idsSolicitud.Contains(x.CodigoSolicitud))
                .GroupBy(x => x.CodigoSolicitud)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.UpdatedAt ?? x.FechaSolicitud ?? DateTime.MinValue).First());
        }

        private Dictionary<int, InspeccionInformeTecnico> CargarInformes(IEnumerable<Inspeccion> inspecciones)
        {
            var dict = new Dictionary<int, InspeccionInformeTecnico>();

            foreach (var inspeccion in inspecciones ?? Enumerable.Empty<Inspeccion>())
            {
                if (inspeccion == null || inspeccion.CodigoInspeccion <= 0 || dict.ContainsKey(inspeccion.CodigoInspeccion))
                {
                    continue;
                }

                dict[inspeccion.CodigoInspeccion] = _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion);
            }

            return dict;
        }

        private Dictionary<int, List<Hallazgo>> CargarHallazgos(IEnumerable<Inspeccion> inspecciones)
        {
            var dict = new Dictionary<int, List<Hallazgo>>();

            foreach (var inspeccion in inspecciones ?? Enumerable.Empty<Inspeccion>())
            {
                if (inspeccion == null || inspeccion.CodigoInspeccion <= 0 || dict.ContainsKey(inspeccion.CodigoInspeccion))
                {
                    continue;
                }

                dict[inspeccion.CodigoInspeccion] = _hallazgoDao.ObtenerPorInspeccion(inspeccion.CodigoInspeccion) ?? new List<Hallazgo>();
            }

            return dict;
        }

        private Dictionary<int, List<Documento>> CargarDocumentosPorSolicitud(IEnumerable<Inspeccion> inspecciones)
        {
            var dict = new Dictionary<int, List<Documento>>();

            foreach (var codigoSolicitud in (inspecciones ?? Enumerable.Empty<Inspeccion>())
                .Where(x => x != null && x.CodigoSolicitud > 0)
                .Select(x => x.CodigoSolicitud)
                .Distinct())
            {
                dict[codigoSolicitud] = _documentoDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>();
            }

            return dict;
        }

        private List<DashboardFilaInspector> ConstruirFilas(
            IEnumerable<Inspeccion> inspecciones,
            IDictionary<int, SolicitudAOCR> solicitudes,
            IDictionary<int, InspeccionInformeTecnico> informes,
            IDictionary<int, List<Hallazgo>> hallazgos,
            IDictionary<int, List<Documento>> documentos)
        {
            return (inspecciones ?? Enumerable.Empty<Inspeccion>())
                .Where(x => x != null)
                .Select(inspeccion =>
                {
                    SolicitudAOCR solicitud;
                    solicitudes.TryGetValue(inspeccion.CodigoSolicitud, out solicitud);

                    InspeccionInformeTecnico informe;
                    informes.TryGetValue(inspeccion.CodigoInspeccion, out informe);

                    List<Hallazgo> hallazgosInspeccion;
                    hallazgos.TryGetValue(inspeccion.CodigoInspeccion, out hallazgosInspeccion);

                    List<Documento> documentosSolicitud;
                    documentos.TryGetValue(inspeccion.CodigoSolicitud, out documentosSolicitud);

                    var fila = new DashboardFilaInspector
                    {
                        Inspeccion = inspeccion,
                        Solicitud = solicitud,
                        Informe = informe,
                        Hallazgos = hallazgosInspeccion ?? new List<Hallazgo>(),
                        Documentos = documentosSolicitud ?? new List<Documento>(),
                        EstadoNormalizado = EstadosInspeccion.NormalizarEstado(inspeccion.Estado),
                        EstadoSolicitudNormalizado = EstadoSolicitud.Normalizar(solicitud != null ? solicitud.Estado : null)
                    };

                    fila.TieneHallazgosAbiertos = fila.Hallazgos.Any(h => !EstaHallazgoCerrado(h));
                    fila.UltimaActividad = ObtenerUltimaActividad(inspeccion, informe, fila.Hallazgos, solicitud);
                    return fila;
                })
                .ToList();
        }

        private bool CumpleFiltros(
            Inspeccion inspeccion,
            IDictionary<int, SolicitudAOCR> solicitudes,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string estado,
            string compania,
            int? codigoSolicitud)
        {
            if (inspeccion == null)
            {
                return false;
            }

            SolicitudAOCR solicitud;
            solicitudes.TryGetValue(inspeccion.CodigoSolicitud, out solicitud);

            var fechaReferencia = inspeccion.FechaProgramada ?? inspeccion.UpdatedAt ?? inspeccion.CreatedAt ?? DateTime.MinValue;
            if (fechaDesde.HasValue && fechaReferencia != DateTime.MinValue && fechaReferencia.Date < fechaDesde.Value.Date)
            {
                return false;
            }

            if (fechaHasta.HasValue && fechaReferencia != DateTime.MinValue && fechaReferencia.Date > fechaHasta.Value.Date)
            {
                return false;
            }

            if (codigoSolicitud.HasValue && inspeccion.CodigoSolicitud != codigoSolicitud.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(compania))
            {
                var filtroCompania = compania.Trim();
                if (!ContieneTexto(ObtenerOperadorVisible(solicitud), filtroCompania)
                    && !ContieneTexto(solicitud != null ? solicitud.CodigoOaci : null, filtroCompania)
                    && !ContieneTexto(solicitud != null ? solicitud.RazonSocial : null, filtroCompania)
                    && !ContieneTexto(solicitud != null ? solicitud.NombreOperador : null, filtroCompania))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(estado) && !CoincideEstadoFiltro(estado, inspeccion, solicitud))
            {
                return false;
            }

            return true;
        }

        private static bool CoincideEstadoFiltro(string estadoFiltro, Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            var filtro = NormalizarToken(estadoFiltro);
            if (string.IsNullOrWhiteSpace(filtro))
            {
                return true;
            }

            var candidatos = new[]
            {
                NormalizarToken(inspeccion != null ? inspeccion.Estado : null),
                NormalizarToken(EstadosInspeccion.NormalizarEstado(inspeccion != null ? inspeccion.Estado : null)),
                NormalizarToken(inspeccion != null ? inspeccion.Resultado : null),
                NormalizarToken(EstadoSolicitud.Normalizar(solicitud != null ? solicitud.Estado : null)),
                NormalizarToken(solicitud != null ? solicitud.Estado : null)
            };

            return candidatos.Any(candidato =>
                !string.IsNullOrWhiteSpace(candidato)
                && (string.Equals(candidato, filtro, StringComparison.OrdinalIgnoreCase)
                    || candidato.Contains(filtro)));
        }

        private static DateTime? ObtenerUltimaActividad(Inspeccion inspeccion, InspeccionInformeTecnico informe, IEnumerable<Hallazgo> hallazgos, SolicitudAOCR solicitud)
        {
            var fechas = new List<DateTime?>
            {
                inspeccion != null ? inspeccion.UpdatedAt : null,
                inspeccion != null ? inspeccion.CreatedAt : null,
                inspeccion != null ? inspeccion.FechaProgramada : null,
                informe != null ? informe.UpdatedAt : null,
                informe != null ? informe.FechaFinalizacion : null,
                informe != null ? informe.FechaEnvioDirdac : null,
                solicitud != null ? solicitud.UpdatedAt : null,
                solicitud != null ? solicitud.FechaSolicitud : null
            };

            if (hallazgos != null)
            {
                fechas.AddRange(hallazgos.Select(h => h != null ? (h.UpdatedAt ?? h.CreatedAt ?? h.FechaDeteccion) : (DateTime?)null));
            }

            return fechas.Where(f => f.HasValue).Select(f => f.Value).DefaultIfEmpty().Max();
        }

        private static decimal CalcularTiempoPromedioHoras(IEnumerable<DashboardFilaInspector> filas)
        {
            var horas = (filas ?? Enumerable.Empty<DashboardFilaInspector>())
                .Select(fila =>
                {
                    var inicio = fila != null && fila.Inspeccion != null ? fila.Inspeccion.CreatedAt : null;
                    var fin = fila != null ? fila.UltimaActividad : null;
                    if (!inicio.HasValue || !fin.HasValue || fin.Value < inicio.Value)
                    {
                        return (decimal?)null;
                    }

                    return (decimal)(fin.Value - inicio.Value).TotalHours;
                })
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToList();

            if (horas.Count == 0)
            {
                return 0m;
            }

            return Math.Round(horas.Average(), 2);
        }

        private static List<TendenciaPunto> ConstruirTendencia(IEnumerable<DashboardFilaInspector> filas)
        {
            var lista = (filas ?? Enumerable.Empty<DashboardFilaInspector>())
                .Where(x => x != null && x.UltimaActividad.HasValue)
                .ToList();

            if (lista.Count == 0)
            {
                return new List<TendenciaPunto>();
            }

            var fechaMaxima = lista.Max(x => x.UltimaActividad.Value.Date);
            var fechas = Enumerable.Range(0, 7)
                .Select(offset => fechaMaxima.AddDays(offset - 6))
                .ToList();

            return fechas
                .Select(fecha => new TendenciaPunto
                {
                    Label = fecha.ToString("dd/MM"),
                    Value = lista.Count(x => x.UltimaActividad.HasValue && x.UltimaActividad.Value.Date == fecha)
                })
                .ToList();
        }

        private static List<InspectorAlertaViewModel> ConstruirAlertas(IEnumerable<DashboardFilaInspector> filas)
        {
            var alertas = new List<InspectorAlertaViewModel>();

            foreach (var fila in filas ?? Enumerable.Empty<DashboardFilaInspector>())
            {
                if (fila == null || fila.Inspeccion == null)
                {
                    continue;
                }

                var fecha = fila.UltimaActividad ?? DateTime.Now;
                var urlInspeccion = "/Inspeccion/Detalle/" + fila.Inspeccion.CodigoInspeccion;
                var urlSolicitud = "/SolicitudAOCR/Detalle/" + fila.Inspeccion.CodigoSolicitud;
                var numeroInspeccion = ObtenerNumeroInspeccionVisible(fila.Inspeccion, fila.Solicitud);

                if (TieneDocumentacionSubsanadaRt(fila))
                {
                    alertas.Add(new InspectorAlertaViewModel
                    {
                        Tipo = "DOCUMENTACION_SUBSANADA",
                        Titulo = "Documentación subsanada por RT",
                        Mensaje = "La solicitud " + fila.Inspeccion.CodigoSolicitud + " fue subsanada y requiere nueva revisión documental.",
                        UrlDestino = urlSolicitud,
                        Severidad = "ALTA",
                        Fecha = fecha
                    });
                }
                else if (TieneDocumentacionPendiente(fila))
                {
                    alertas.Add(new InspectorAlertaViewModel
                    {
                        Tipo = "DOCUMENTACION_PENDIENTE",
                        Titulo = "Documentación pendiente",
                        Mensaje = "La inspección " + numeroInspeccion + " mantiene documentación pendiente de revisión.",
                        UrlDestino = urlSolicitud,
                        Severidad = "MEDIA",
                        Fecha = fecha
                    });
                }

                if (TieneInformeTecnicoPendiente(fila))
                {
                    alertas.Add(new InspectorAlertaViewModel
                    {
                        Tipo = "INFORME_PENDIENTE",
                        Titulo = "Informe técnico pendiente",
                        Mensaje = "La inspección " + numeroInspeccion + " requiere completar o firmar el informe técnico.",
                        UrlDestino = urlInspeccion,
                        Severidad = "ALTA",
                        Fecha = fecha
                    });
                }

                if (TieneNcAbierta(fila))
                {
                    alertas.Add(new InspectorAlertaViewModel
                    {
                        Tipo = "NC_PENDIENTE",
                        Titulo = "No conformidades pendientes",
                        Mensaje = "La inspección " + numeroInspeccion + " mantiene no conformidades abiertas.",
                        UrlDestino = urlInspeccion,
                        Severidad = "ALTA",
                        Fecha = fecha
                    });
                }

                if (RequiereNuevaInspeccion(fila))
                {
                    alertas.Add(new InspectorAlertaViewModel
                    {
                        Tipo = "NUEVA_INSPECCION",
                        Titulo = "Requiere nueva inspección",
                        Mensaje = "La inspección " + numeroInspeccion + " requiere un nuevo ciclo técnico.",
                        UrlDestino = urlInspeccion,
                        Severidad = "MEDIA",
                        Fecha = fecha
                    });
                }

                if (InformeFueDevuelto(fila.Informe))
                {
                    alertas.Add(new InspectorAlertaViewModel
                    {
                        Tipo = "INFORME_DEVUELTO",
                        Titulo = "Informe devuelto para corrección",
                        Mensaje = "El informe técnico de la inspección " + numeroInspeccion + " fue devuelto para ajuste.",
                        UrlDestino = urlInspeccion,
                        Severidad = "MEDIA",
                        Fecha = fecha
                    });
                }
            }

            return alertas
                .GroupBy(a => a.Tipo + "|" + a.UrlDestino + "|" + a.Mensaje, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(a => a.Fecha).First())
                .ToList();
        }

        private static bool EsPendiente(DashboardFilaInspector fila)
        {
            return fila != null && (
                string.Equals(fila.EstadoNormalizado, EstadosInspeccion.SOLICITUD_INSPECCION_CREADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.PAGO_VALIDADO, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EsEjecucionActiva(DashboardFilaInspector fila)
        {
            return fila != null && (
                string.Equals(fila.EstadoNormalizado, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EsCerrada(DashboardFilaInspector fila)
        {
            return fila != null && (
                string.Equals(fila.EstadoNormalizado, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TieneNcAbierta(DashboardFilaInspector fila)
        {
            if (fila == null)
            {
                return false;
            }

            return fila.TieneHallazgosAbiertos
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizarToken(fila.Inspeccion != null ? fila.Inspeccion.Resultado : null), "CON_NC", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiereNuevaInspeccion(DashboardFilaInspector fila)
        {
            if (fila == null)
            {
                return false;
            }

            return string.Equals(fila.EstadoNormalizado, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.OBSERVADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TieneDocumentacionPendiente(DashboardFilaInspector fila)
        {
            if (fila == null)
            {
                return false;
            }

            return string.Equals(fila.EstadoSolicitudNormalizado, EstadoSolicitud.EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoSolicitudNormalizado, EstadoSolicitud.DocumentacionPendiente, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoSolicitudNormalizado, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoSolicitudNormalizado, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoSolicitudNormalizado, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TieneDocumentacionSubsanadaRt(DashboardFilaInspector fila)
        {
            if (fila == null)
            {
                return false;
            }

            return string.Equals(fila.EstadoSolicitudNormalizado, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fila.EstadoNormalizado, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TieneInformeTecnicoPendiente(DashboardFilaInspector fila)
        {
            if (fila == null || fila.Inspeccion == null)
            {
                return false;
            }

            if (!EsEjecucionActiva(fila)
                && !string.Equals(fila.EstadoNormalizado, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (fila.Informe == null)
            {
                return true;
            }

            return !fila.Informe.Finalizado
                || !fila.Informe.FirmadoInspector
                || InformeFueDevuelto(fila.Informe);
        }

        private static bool InformeFueDevuelto(InspeccionInformeTecnico informe)
        {
            var estado = NormalizarToken(informe != null ? informe.EstadoInforme : null);
            return string.Equals(estado, "DEVUELTO_DIRECCION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, "RECHAZADO_DIRDAC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, "DEVUELTO_COORDINADOR", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EstaHallazgoCerrado(Hallazgo hallazgo)
        {
            return hallazgo != null
                && string.Equals(NormalizarToken(hallazgo.Estado), "CERRADO", StringComparison.OrdinalIgnoreCase);
        }

        private static string ObtenerNumeroInspeccionVisible(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (inspeccion != null && !string.IsNullOrWhiteSpace(inspeccion.NumeroInspeccion))
            {
                return inspeccion.NumeroInspeccion.Trim();
            }

            if (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud))
            {
                return solicitud.NumeroSolicitud.Trim();
            }

            return inspeccion != null ? ("INS-" + inspeccion.CodigoInspeccion) : "INS";
        }

        private static string ObtenerOperadorVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "No especificada";
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RazonSocial))
            {
                return solicitud.RazonSocial.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.NombreOperador))
            {
                return solicitud.NombreOperador.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.CodigoOaci))
            {
                return solicitud.CodigoOaci.Trim();
            }

            return "No especificada";
        }

        private static bool ContieneTexto(string valor, string filtro)
        {
            return !string.IsNullOrWhiteSpace(valor)
                && !string.IsNullOrWhiteSpace(filtro)
                && valor.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizarToken(string valor)
        {
            return (valor ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace("Ñ", "N")
                .Replace(" ", "_")
                .Replace("-", "_");
        }

        private sealed class DashboardFilaInspector
        {
            public Inspeccion Inspeccion { get; set; }
            public SolicitudAOCR Solicitud { get; set; }
            public InspeccionInformeTecnico Informe { get; set; }
            public List<Hallazgo> Hallazgos { get; set; }
            public List<Documento> Documentos { get; set; }
            public string EstadoNormalizado { get; set; }
            public string EstadoSolicitudNormalizado { get; set; }
            public bool TieneHallazgosAbiertos { get; set; }
            public DateTime? UltimaActividad { get; set; }
        }

        private sealed class TendenciaPunto
        {
            public string Label { get; set; }
            public int Value { get; set; }
        }
    }
}