using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class DashboardInspeccionDAO
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly HallazgoDAO _hallazgoDao = new HallazgoDAO();

        public List<DashboardInspeccionSeguimientoData> ObtenerInspeccionesEnSeguimiento(int maxRows = 120)
        {
            var solicitudes = (_solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>())
                .GroupBy(s => s.CodigoSolicitud)
                .Select(g => g.First())
                .ToDictionary(x => x.CodigoSolicitud, x => x);

            var inspecciones = (_inspeccionDao.ListarTodas() ?? new List<Inspeccion>())
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada ?? DateTime.MinValue)
                .ThenByDescending(i => i.CodigoInspeccion)
                .Take(maxRows)
                .ToList();

            var informes = ObtenerInformes(inspecciones);

            return inspecciones
                .Select(i =>
                {
                    SolicitudAOCR solicitud;
                    solicitudes.TryGetValue(i.CodigoSolicitud, out solicitud);

                    InspeccionInformeTecnico informe;
                    informes.TryGetValue(i.CodigoInspeccion, out informe);

                    var estadoNormalizado = EstadosInspeccion.NormalizarEstado(i.Estado);

                    return new DashboardInspeccionSeguimientoData
                    {
                        CodigoInspeccion = i.CodigoInspeccion,
                        CodigoSolicitud = i.CodigoSolicitud,
                        NumeroSolicitud = ObtenerNumeroSolicitud(solicitud, i.CodigoSolicitud),
                        Compania = ObtenerNombreCompania(solicitud),
                        TipoOperacion = ObtenerTipoOperacion(solicitud, i),
                        InspectorAsignado = ObtenerInspectorAsignado(i, solicitud),
                        Estado = i.Estado,
                        EstadoVisual = MapearEstadoInspeccionVisual(estadoNormalizado),
                        FechaAsignacion = i.CreatedAt ?? i.FechaProgramada,
                        UltimaActualizacion = informe != null
                            ? (informe.UpdatedAt ?? informe.FechaFinalizacion ?? informe.FechaEnvioDirdac ?? i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada)
                            : (i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada),
                        EtapaActual = EstadosInspeccion.ObtenerDescripcion(estadoNormalizado),
                        RequiereRevisionInstitucional = RequiereRevisionInstitucional(estadoNormalizado),
                        PuedeAsignarInspector = PuedeAsignarInspector(estadoNormalizado, i, solicitud),
                        RequiereFirmaDirdac = informe != null && informe.FirmadoInspector && !informe.FirmadoDirdac,
                        TieneInformePdf = informe != null && !string.IsNullOrWhiteSpace(FirstNonEmpty(informe.RutaDocumentoFirmado, informe.RutaPdf, i.RutaInforme))
                    };
                })
                .ToList();
        }

        public List<DashboardInspeccionDocumentoData> ObtenerControlDocumental(int maxRows = 120)
        {
            var solicitudes = (_solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>())
                .OrderByDescending(s => s.UpdatedAt ?? s.FechaSolicitud ?? DateTime.MinValue)
                .ThenByDescending(s => s.CodigoSolicitud)
                .ToList();

            var inspecciones = (_inspeccionDao.ListarTodas() ?? new List<Inspeccion>())
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada ?? DateTime.MinValue)
                .ThenByDescending(i => i.CodigoInspeccion)
                .ToList();

            var inspeccionPorSolicitud = inspecciones
                .GroupBy(i => i.CodigoSolicitud)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? x.FechaProgramada ?? DateTime.MinValue)
                        .ThenByDescending(x => x.CodigoInspeccion)
                        .First());

            var informes = ObtenerInformes(inspeccionPorSolicitud.Values);

            return solicitudes
                .Where(s => inspeccionPorSolicitud.ContainsKey(s.CodigoSolicitud) || EsSolicitudControlDocumental(s.Estado))
                .Take(maxRows)
                .Select(s =>
                {
                    Inspeccion inspeccion;
                    inspeccionPorSolicitud.TryGetValue(s.CodigoSolicitud, out inspeccion);

                    InspeccionInformeTecnico informe = null;
                    if (inspeccion != null)
                    {
                        informes.TryGetValue(inspeccion.CodigoInspeccion, out informe);
                    }

                    var estadoDocumento = MapearEstadoDocumentoVisual(s, inspeccion, informe);

                    return new DashboardInspeccionDocumentoData
                    {
                        CodigoSolicitud = s.CodigoSolicitud,
                        CodigoInspeccion = inspeccion != null ? (int?)inspeccion.CodigoInspeccion : null,
                        NumeroSolicitud = ObtenerNumeroSolicitud(s, s.CodigoSolicitud),
                        Compania = ObtenerNombreCompania(s),
                        TipoOperacion = ObtenerTipoOperacion(s, inspeccion),
                        Documento = informe != null
                            ? (!string.IsNullOrWhiteSpace(informe.Titulo) ? informe.Titulo : "Informe técnico de inspección")
                            : (inspeccion != null ? "Expediente de inspección" : "Expediente AOCR"),
                        TipoDocumento = informe != null ? "Informe técnico" : "Control documental",
                        EstadoDocumento = estadoDocumento,
                        FirmadoInspector = informe != null && informe.FirmadoInspector,
                        FirmadoDirdac = informe != null && informe.FirmadoDirdac,
                        FechaUltimaActualizacion = informe != null
                            ? (informe.UpdatedAt ?? informe.FechaEnvioDirdac ?? informe.FechaFinalizacion ?? (inspeccion != null ? inspeccion.UpdatedAt : null))
                            : (inspeccion != null ? (inspeccion.UpdatedAt ?? inspeccion.CreatedAt ?? inspeccion.FechaProgramada) : (s.UpdatedAt ?? s.FechaSolicitud)),
                        TienePdf = informe != null && !string.IsNullOrWhiteSpace(FirstNonEmpty(informe.RutaDocumentoFirmado, informe.RutaPdf, inspeccion != null ? inspeccion.RutaInforme : null)),
                        RequiereFirmaDirdac = informe != null && informe.FirmadoInspector && !informe.FirmadoDirdac,
                        RequiereRevision = informe == null || !informe.FirmadoDirdac,
                        InspectorAsignado = ObtenerInspectorAsignado(inspeccion, s)
                    };
                })
                .OrderByDescending(x => x.FechaUltimaActualizacion ?? DateTime.MinValue)
                .ThenByDescending(x => x.CodigoSolicitud)
                .ToList();
        }

        public List<DashboardInspeccionFirmaData> ObtenerPendientesFirma(int maxRows = 120)
        {
            var solicitudes = (_solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>())
                .GroupBy(s => s.CodigoSolicitud)
                .Select(g => g.First())
                .ToDictionary(x => x.CodigoSolicitud, x => x);

            var inspecciones = (_inspeccionDao.ListarTodas() ?? new List<Inspeccion>())
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada ?? DateTime.MinValue)
                .ThenByDescending(i => i.CodigoInspeccion)
                .Take(maxRows)
                .ToList();

            var informes = ObtenerInformes(inspecciones);
            var items = new List<DashboardInspeccionFirmaData>();

            foreach (var inspeccion in inspecciones)
            {
                InspeccionInformeTecnico informe;
                if (!informes.TryGetValue(inspeccion.CodigoInspeccion, out informe) || informe == null || !informe.Finalizado)
                {
                    continue;
                }

                var estadoInforme = NormalizarTexto(informe.EstadoInforme);

                SolicitudAOCR solicitud;
                solicitudes.TryGetValue(inspeccion.CodigoSolicitud, out solicitud);

                if (!informe.FirmadoInspector && string.Equals(estadoInforme, "APROBADO_COORDINADOR", StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new DashboardInspeccionFirmaData
                    {
                        CodigoInspeccion = inspeccion.CodigoInspeccion,
                        CodigoSolicitud = inspeccion.CodigoSolicitud,
                        NumeroSolicitud = ObtenerNumeroSolicitud(solicitud, inspeccion.CodigoSolicitud),
                        Compania = ObtenerNombreCompania(solicitud),
                        Documento = !string.IsNullOrWhiteSpace(informe.Titulo) ? informe.Titulo : "Informe técnico de inspección",
                        FirmanteRequerido = "Inspector",
                        Estado = "PENDIENTE_FIRMA_INSPECTOR",
                        FechaEnvio = informe.FechaFinalizacion ?? informe.UpdatedAt ?? inspeccion.UpdatedAt ?? inspeccion.CreatedAt,
                        InspectorAsignado = ObtenerInspectorAsignado(inspeccion, solicitud)
                    });
                }

                if (string.Equals(estadoInforme, "ENVIADO_A_DIRDAC", StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new DashboardInspeccionFirmaData
                    {
                        CodigoInspeccion = inspeccion.CodigoInspeccion,
                        CodigoSolicitud = inspeccion.CodigoSolicitud,
                        NumeroSolicitud = ObtenerNumeroSolicitud(solicitud, inspeccion.CodigoSolicitud),
                        Compania = ObtenerNombreCompania(solicitud),
                        Documento = !string.IsNullOrWhiteSpace(informe.Titulo) ? informe.Titulo : "Informe técnico de inspección",
                        FirmanteRequerido = "DIRDAC",
                        Estado = "PENDIENTE_REVISION_DIRDAC",
                        FechaEnvio = informe.FechaEnvioDirdac ?? informe.UpdatedAt ?? informe.FechaFinalizacion ?? inspeccion.UpdatedAt ?? inspeccion.CreatedAt,
                        InspectorAsignado = ObtenerInspectorAsignado(inspeccion, solicitud)
                    });
                }
            }

            return items
                .OrderByDescending(x => x.FechaEnvio ?? DateTime.MinValue)
                .ThenByDescending(x => x.CodigoInspeccion)
                .Take(maxRows)
                .ToList();
        }

        public List<DashboardInspeccionNcData> ObtenerNoConformidades(int maxRows = 120)
        {
            var solicitudes = (_solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>())
                .GroupBy(s => s.CodigoSolicitud)
                .Select(g => g.First())
                .ToDictionary(x => x.CodigoSolicitud, x => x);

            var inspecciones = (_inspeccionDao.ListarTodas() ?? new List<Inspeccion>())
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt ?? i.FechaProgramada ?? DateTime.MinValue)
                .ThenByDescending(i => i.CodigoInspeccion)
                .Take(maxRows)
                .ToList();

            var items = new List<DashboardInspeccionNcData>();

            foreach (var inspeccion in inspecciones)
            {
                var hallazgos = _hallazgoDao.ObtenerPorInspeccion(inspeccion.CodigoInspeccion) ?? new List<Hallazgo>();
                if (hallazgos.Count == 0)
                {
                    continue;
                }

                SolicitudAOCR solicitud;
                solicitudes.TryGetValue(inspeccion.CodigoSolicitud, out solicitud);

                items.AddRange(hallazgos.Select(h => new DashboardInspeccionNcData
                {
                    CodigoInspeccion = inspeccion.CodigoInspeccion,
                    CodigoSolicitud = inspeccion.CodigoSolicitud,
                    NumeroSolicitud = ObtenerNumeroSolicitud(solicitud, inspeccion.CodigoSolicitud),
                    Compania = ObtenerNombreCompania(solicitud),
                    TipoNc = string.IsNullOrWhiteSpace(h.Criticidad) ? "NC" : h.Criticidad.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(h.Descripcion) ? "Sin descripción registrada." : h.Descripcion.Trim(),
                    Estado = NormalizarEstadoNc(h.Estado),
                    Responsable = string.IsNullOrWhiteSpace(h.Responsable) ? ObtenerInspectorAsignado(inspeccion, solicitud) : h.Responsable.Trim(),
                    Fecha = h.FechaDeteccion ?? h.CreatedAt ?? h.UpdatedAt,
                    InspectorAsignado = ObtenerInspectorAsignado(inspeccion, solicitud)
                }));
            }

            if (items.Count == 0)
            {
                var observaciones = new List<Observacion>();
                try
                {
                    observaciones.AddRange(ObservacionDAO.ObtenerPorEstado("Pendiente") ?? new List<Observacion>());
                    observaciones.AddRange(ObservacionDAO.ObtenerPorEstado("En Proceso") ?? new List<Observacion>());
                    observaciones.AddRange(ObservacionDAO.ObtenerPorEstado("Cerrada") ?? new List<Observacion>());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[DashboardInspeccionDAO] Fallback de observaciones no disponible: " + ex.Message);
                    observaciones = new List<Observacion>();
                }

                foreach (var observacion in observaciones)
                {
                    if (observacion == null || !observacion.CodigoInspeccion.HasValue)
                    {
                        continue;
                    }

                    var inspeccion = inspecciones.FirstOrDefault(x => x.CodigoInspeccion == observacion.CodigoInspeccion.Value);
                    if (inspeccion == null)
                    {
                        continue;
                    }

                    SolicitudAOCR solicitud;
                    solicitudes.TryGetValue(inspeccion.CodigoSolicitud, out solicitud);

                    items.Add(new DashboardInspeccionNcData
                    {
                        CodigoInspeccion = inspeccion.CodigoInspeccion,
                        CodigoSolicitud = inspeccion.CodigoSolicitud,
                        NumeroSolicitud = ObtenerNumeroSolicitud(solicitud, inspeccion.CodigoSolicitud),
                        Compania = ObtenerNombreCompania(solicitud),
                        TipoNc = string.IsNullOrWhiteSpace(observacion.Gravedad) ? "NC" : observacion.Gravedad.Trim(),
                        Descripcion = string.IsNullOrWhiteSpace(observacion.Descripcion) ? "Sin descripción registrada." : observacion.Descripcion.Trim(),
                        Estado = NormalizarEstadoNc(observacion.Estado),
                        Responsable = ObtenerInspectorAsignado(inspeccion, solicitud),
                        Fecha = observacion.FechaObservacion,
                        InspectorAsignado = ObtenerInspectorAsignado(inspeccion, solicitud)
                    });
                }
            }

            return items
                .OrderByDescending(x => x.Fecha ?? DateTime.MinValue)
                .ThenByDescending(x => x.CodigoInspeccion)
                .Take(maxRows)
                .ToList();
        }

        private Dictionary<int, InspeccionInformeTecnico> ObtenerInformes(IEnumerable<Inspeccion> inspecciones)
        {
            var dict = new Dictionary<int, InspeccionInformeTecnico>();

            foreach (var inspeccion in inspecciones ?? Enumerable.Empty<Inspeccion>())
            {
                if (inspeccion == null || dict.ContainsKey(inspeccion.CodigoInspeccion))
                {
                    continue;
                }

                dict[inspeccion.CodigoInspeccion] = _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion);
            }

            return dict;
        }

        private static string ObtenerNumeroSolicitud(SolicitudAOCR solicitud, int codigoSolicitud)
        {
            if (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud))
            {
                return solicitud.NumeroSolicitud.Trim();
            }

            return codigoSolicitud.ToString();
        }

        private static string ObtenerNombreCompania(SolicitudAOCR solicitud)
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

        private static string ObtenerInspectorAsignado(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (inspeccion != null && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre))
            {
                return inspeccion.InspectorPrincipalNombre.Trim();
            }

            if (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                return solicitud.TecnicoResponsableNombre.Trim();
            }

            return "No asignado";
        }

        private static string ObtenerTipoOperacion(SolicitudAOCR solicitud, Inspeccion inspeccion)
        {
            var valor = FirstNonEmpty(
                inspeccion != null ? inspeccion.Tipo : null,
                solicitud != null ? solicitud.TipoOperacion : null,
                solicitud != null ? solicitud.DescripcionOperacion : null);

            if (string.IsNullOrWhiteSpace(valor))
            {
                return "OPS";
            }

            var normalizado = NormalizarTexto(valor);
            if (normalizado.Contains("AIR") || normalizado.Contains("AERONAVEG"))
            {
                return "AIR";
            }

            if (normalizado.Contains("OPS") || normalizado.Contains("OPERAC"))
            {
                return "OPS";
            }

            return valor.Trim().ToUpperInvariant();
        }

        private static string MapearEstadoInspeccionVisual(string estadoNormalizado)
        {
            switch (EstadosInspeccion.NormalizarEstado(estadoNormalizado))
            {
                case EstadosInspeccion.ACEPTADA:
                case EstadosInspeccion.PAGO_VALIDADO:
                case EstadosInspeccion.VERIFICACION_SOLICITUD:
                case EstadosInspeccion.SOLICITUD_INSPECCION_CREADA:
                    return "ASIGNADA";
                case EstadosInspeccion.EN_INSPECCION:
                case EstadosInspeccion.VIATICOS_REQUERIDOS:
                    return "EN_PROCESO";
                case EstadosInspeccion.OBSERVADA:
                case EstadosInspeccion.RESULTADO_NO_SATISFACTORIO:
                case EstadosInspeccion.OBSERVACION_DOCUMENTAL:
                    return "OBSERVADA";
                case EstadosInspeccion.SUBSANADA:
                    return "SUBSANADA";
                case EstadosInspeccion.INFORME_ELABORADO:
                case EstadosInspeccion.RESULTADO_SATISFACTORIO:
                case EstadosInspeccion.CERRADA:
                    return "FINALIZADA";
                default:
                    return string.IsNullOrWhiteSpace(estadoNormalizado) ? "PENDIENTE" : estadoNormalizado.Trim().ToUpperInvariant();
            }
        }

        private static string MapearEstadoDocumentoVisual(SolicitudAOCR solicitud, Inspeccion inspeccion, InspeccionInformeTecnico informe)
        {
            if (informe != null)
            {
                if (informe.FirmadoDirdac)
                {
                    return "FIRMADO";
                }

                if (informe.FirmadoInspector)
                {
                    return "APROBADO";
                }

                if (EstadoEsObservado(inspeccion != null ? inspeccion.Estado : null) || EstadoEsObservado(solicitud != null ? solicitud.Estado : null))
                {
                    return "OBSERVADO";
                }

                if (informe.Finalizado)
                {
                    return "EN_REVISION";
                }
            }

            if (EstadoEsObservado(inspeccion != null ? inspeccion.Estado : null) || EstadoEsObservado(solicitud != null ? solicitud.Estado : null))
            {
                return "OBSERVADO";
            }

            if (EstadoEsAprobado(solicitud != null ? solicitud.Estado : null))
            {
                return "APROBADO";
            }

            if (EstadoEsRevision(solicitud != null ? solicitud.Estado : null) || inspeccion != null)
            {
                return "EN_REVISION";
            }

            return "PENDIENTE";
        }

        private static bool EsSolicitudControlDocumental(string estado)
        {
            var value = NormalizarTexto(estado);
            return value.Contains("PEND") || value.Contains("REVISION") || value.Contains("OBSERV") || value.Contains("ACEPTACION") || value.Contains("VALIDA");
        }

        private static bool RequiereRevisionInstitucional(string estadoNormalizado)
        {
            var estado = EstadosInspeccion.NormalizarEstado(estadoNormalizado);
            return string.Equals(estado, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.OBSERVADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PuedeAsignarInspector(string estadoNormalizado, Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (TieneInspectorAsignado(inspeccion, solicitud))
            {
                return false;
            }

            if (inspeccion == null)
            {
                return solicitud != null
                    && EstadoSolicitudSql.EstadoPermiteAsignacionInicial(solicitud.Estado);
            }

            var estado = EstadosInspeccion.NormalizarEstado(estadoNormalizado);
            return string.Equals(estado, EstadosInspeccion.SOLICITUD_INSPECCION_CREADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.PAGO_VALIDADO, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TieneInspectorAsignado(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            return (inspeccion != null && inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
                || (inspeccion != null && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre))
                || (inspeccion != null && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula))
                || (solicitud != null && solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
                || (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
                || (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableCedula));
        }

        private static bool EstadoEsObservado(string estado)
        {
            var value = NormalizarTexto(estado);
            return value.Contains("OBSERV") || value.Contains("NO_SATISFACTORIO") || value.Contains("RECHAZ");
        }

        private static bool EstadoEsRevision(string estado)
        {
            var value = NormalizarTexto(estado);
            return value.Contains("REVISION") || value.Contains("PEND") || value.Contains("VERIFICACION");
        }

        private static bool EstadoEsAprobado(string estado)
        {
            var value = NormalizarTexto(estado);
            return value.Contains("APROBAD") || value.Contains("VALIDAD") || value.Contains("LEGALIZ") || value.Contains("ACEPTAD");
        }

        private static string NormalizarEstadoNc(string estado)
        {
            var value = NormalizarTexto(estado);
            if (value.Contains("CERRAD") || value.Contains("RESUELT"))
            {
                return "CERRADA";
            }

            if (value.Contains("PROCESO") || value.Contains("SUBSAN") || value.Contains("ATENCION"))
            {
                return "EN_PROCESO";
            }

            return "NC_ABIERTA";
        }

        private static string NormalizarTexto(string valor)
        {
            return (valor ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return (values ?? new string[0]).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }
    }

    public class DashboardInspeccionSeguimientoData
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string TipoOperacion { get; set; }
        public string InspectorAsignado { get; set; }
        public string Estado { get; set; }
        public string EstadoVisual { get; set; }
        public DateTime? FechaAsignacion { get; set; }
        public DateTime? UltimaActualizacion { get; set; }
        public string EtapaActual { get; set; }
        public bool RequiereRevisionInstitucional { get; set; }
        public bool PuedeAsignarInspector { get; set; }
        public bool RequiereFirmaDirdac { get; set; }
        public bool TieneInformePdf { get; set; }
    }

    public class DashboardInspeccionDocumentoData
    {
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string TipoOperacion { get; set; }
        public string Documento { get; set; }
        public string TipoDocumento { get; set; }
        public string EstadoDocumento { get; set; }
        public bool FirmadoInspector { get; set; }
        public bool FirmadoDirdac { get; set; }
        public DateTime? FechaUltimaActualizacion { get; set; }
        public bool TienePdf { get; set; }
        public bool RequiereFirmaDirdac { get; set; }
        public bool RequiereRevision { get; set; }
        public string InspectorAsignado { get; set; }
    }

    public class DashboardInspeccionFirmaData
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string Documento { get; set; }
        public string FirmanteRequerido { get; set; }
        public string Estado { get; set; }
        public DateTime? FechaEnvio { get; set; }
        public string InspectorAsignado { get; set; }
    }

    public class DashboardInspeccionNcData
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public string Compania { get; set; }
        public string TipoNc { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; }
        public string Responsable { get; set; }
        public DateTime? Fecha { get; set; }
        public string InspectorAsignado { get; set; }
    }
}
