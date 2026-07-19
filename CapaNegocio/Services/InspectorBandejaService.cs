using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class InspectorBandejaService
    {
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly RevisionDocumentalBandejaService _revisionDocumentalBandejaService = new RevisionDocumentalBandejaService();

        /// <summary>
        /// Fuente unica de la bandeja y del contador de documentos finales del Inspector.
        /// Las escrituras usan DIRDAC; los alias anteriores se conservan solo para lectura.
        /// </summary>
        public IList<InspectorDocumentoFinalPendiente> ObtenerPendientesDocumentosFinales(AocrBandejaRoleContext context)
        {
            if (context == null || !context.EsInspectorTecnico)
            {
                return new List<InspectorDocumentoFinalPendiente>();
            }

            var estadosCentrales = new HashSet<int>();
            var procesoDao = new AocrProcesoEstadoDAO();
            foreach (var estado in new[]
            {
                AocrEstadosProceso.DocumentosFinalesPorGenerar,
                AocrEstadosProceso.InformeTecnicoAprobadoDirdac,
                // Compatibilidad de lectura; nunca se utiliza para nuevas escrituras.
                AocrEstadosProceso.InformeTecnicoAprobadoDcav,
                "EMISION_AOCR_CONDICIONES"
            })
            {
                foreach (var id in procesoDao.ListarInspeccionesActivas(estado) ?? new List<int>())
                {
                    estadosCentrales.Add(id);
                }
            }

            var informeDao = new InspeccionInformeDAO();
            var solicitudDao = new SolicitudAOCRDAO();
            var ncDao = new NoConformidadDAO();
            var documentoDao = new AocrDocumentoGeneradoDAO();
            var cierreService = new AocrCierrePorTipoTramiteService();
            var resultado = new List<InspectorDocumentoFinalPendiente>();

            foreach (var inspeccion in ObtenerInspeccionesAsignadas(context))
            {
                var estadoInspeccion = InformeTecnicoEstadosInstitucionales.NormalizarToken(inspeccion.Estado);
                var tieneEstadoCentral = estadosCentrales.Contains(inspeccion.CodigoInspeccion);
                var tieneEstadoCompatible = estadoInspeccion == "EMISION_AOCR_CONDICIONES"
                    || estadoInspeccion == AocrEstadosProceso.DocumentosFinalesPorGenerar
                    || estadoInspeccion == AocrEstadosProceso.InformeTecnicoAprobadoDirdac
                    || estadoInspeccion == AocrEstadosProceso.InformeTecnicoAprobadoDcav;
                if (!tieneEstadoCentral && !tieneEstadoCompatible)
                {
                    continue;
                }

                var informe = informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion);
                if (!EsInformeAprobadoDirdac(informe))
                {
                    continue;
                }

                if (ncDao.ContarAbiertasRelacionadasConInspeccion(inspeccion.CodigoInspeccion) > 0)
                {
                    continue;
                }

                var solicitud = solicitudDao.ObtenerPorCodigo(inspeccion.CodigoSolicitud);
                if (solicitud == null || EsSolicitudFinal(solicitud.Estado))
                {
                    continue;
                }

                var plan = cierreService.Resolver(solicitud);
                if (!plan.EsValido)
                {
                    continue;
                }

                var aocr = plan.GenerarAocr
                    ? documentoDao.ObtenerUltimoPorSolicitudTipo(solicitud.CodigoSolicitud, AocrCierrePorTipoTramiteService.Reconocimiento)
                    : null;
                var condiciones = plan.GenerarCondiciones
                    ? documentoDao.ObtenerUltimoPorSolicitudTipo(solicitud.CodigoSolicitud, AocrCierrePorTipoTramiteService.Condiciones)
                    : null;

                resultado.Add(new InspectorDocumentoFinalPendiente
                {
                    InspeccionId = inspeccion.CodigoInspeccion,
                    SolicitudId = solicitud.CodigoSolicitud,
                    NumeroSolicitud = solicitud.NumeroSolicitud,
                    CompaniaRuc = solicitud.Ruc,
                    CompaniaNombre = PrimerValor(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial),
                    NumeroInspeccion = PrimerValor(inspeccion.NumeroInspeccion, inspeccion.CodigoInspeccion.ToString()),
                    TipoTramite = plan.TipoTramite,
                    InspectorAsignado = PrimerValor(inspeccion.InspectorPrincipalNombre, context.UserName, context.CodigoUsuario),
                    FechaAprobacionDirdac = informe.FechaFirma2 ?? informe.FechaFinalizacion ?? informe.UpdatedAt,
                    EstadoAocr = plan.GenerarAocr ? EstadoDocumento(aocr, "PENDIENTE_GENERAR") : "NO_APLICA",
                    EstadoCondiciones = plan.GenerarCondiciones ? EstadoDocumento(condiciones, "PENDIENTE_GENERAR") : "NO_APLICA",
                    GenerarAocr = plan.GenerarAocr,
                    GenerarCondiciones = plan.GenerarCondiciones
                });
            }

            return resultado
                .OrderBy(x => x.FechaAprobacionDirdac ?? DateTime.MinValue)
                .ThenBy(x => x.NumeroSolicitud)
                .ToList();
        }

        public int ContarPendientesDocumentosFinales(AocrBandejaRoleContext context)
        {
            // Intencionalmente usa exactamente la misma consulta/reglas que la bandeja.
            return ObtenerPendientesDocumentosFinales(context).Count;
        }

        public int ContarRevisionDocumentalPendiente(AocrBandejaRoleContext context)
        {
            return _revisionDocumentalBandejaService.ContarBandejaInspector(context);
        }

        public IList<Inspeccion> ObtenerInspeccionesAsignadas(AocrBandejaRoleContext context)
        {
            var inspectorIds = ResolverInspectorFilterIds(context);
            var inspectorTextIds = ResolverInspectorFilterTextIds(context, inspectorIds);
            var inspeccionesBase = _inspeccionDao.ListarTodas() ?? new List<Inspeccion>();

            return inspectorIds
                .SelectMany(id => _inspeccionDao.ListarPorInspector(id) ?? new List<Inspeccion>())
                .Concat(inspeccionesBase.Where(ins => InspectorMatchesTextIdentifiers(ins, inspectorTextIds)))
                .Where(ins => ins != null && ins.CodigoInspeccion > 0)
                .GroupBy(ins => ins.CodigoInspeccion)
                .Select(group => group.OrderByDescending(ins => ins.UpdatedAt ?? DateTime.MinValue).First())
                .ToList();
        }

        public InspectorBandejaContadores ObtenerContadores(AocrBandejaRoleContext context)
        {
            var inspecciones = ObtenerInspeccionesAsignadas(context);
            return new InspectorBandejaContadores
            {
                Total = inspecciones.Count,
                Asignadas = inspecciones.Count(ins => EsEstadoAsignada(ins.Estado)),
                EnFase = inspecciones.Count(ins => EsEstadoEnFase(ins.Estado)),
                Observadas = inspecciones.Count(ins => EsEstadoObservada(ins.Estado)),
                Finalizadas = inspecciones.Count(ins => EsEstadoFinalizada(ins.Estado)),
                RevisionDocumental = ContarRevisionDocumentalPendiente(context),
                EmisionAocrCondiciones = ContarPendientesDocumentosFinales(context)
            };
        }

        private static bool EsInformeAprobadoDirdac(InspeccionInformeTecnico informe)
        {
            if (informe == null || !informe.Finalizado || !informe.FirmadoInspector)
            {
                return false;
            }

            var estado = InformeTecnicoEstadosInstitucionales.NormalizarToken(informe.EstadoInforme);
            return estado == AocrEstadosProceso.InformeTecnicoAprobadoDirdac
                || estado == "APROBADO_DIRDAC"
                || estado == "APROBADO_DIRECCION"
                || estado == "INFORME_TECNICO_APROBADO_DIRECCION"
                || estado == AocrEstadosProceso.InformeTecnicoAprobadoDcav;
        }

        private static bool EsSolicitudFinal(string estado)
        {
            var normalizado = EstadoSolicitud.Normalizar(estado);
            return string.Equals(normalizado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadoSolicitud.Anulada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(InformeTecnicoEstadosInstitucionales.NormalizarToken(estado), AocrEstadosProceso.DocumentosFinalesEnFirma, StringComparison.OrdinalIgnoreCase);
        }

        private static string EstadoDocumento(AocrDocumentoGenerado documento, string estadoVacio)
        {
            return documento == null || string.IsNullOrWhiteSpace(documento.Estado)
                ? estadoVacio
                : InformeTecnicoEstadosInstitucionales.NormalizarToken(documento.Estado);
        }

        private static string PrimerValor(params string[] valores)
        {
            return (valores ?? new string[0]).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        }

        private static HashSet<int> ResolverInspectorFilterIds(AocrBandejaRoleContext context)
        {
            var ids = new HashSet<int>();
            if (context != null && context.UserId > 0)
            {
                ids.Add(context.UserId);
            }

            int codigoNumerico;
            if (context != null
                && int.TryParse(context.CodigoUsuario, out codigoNumerico)
                && codigoNumerico > 0)
            {
                ids.Add(codigoNumerico);
            }

            try
            {
                UsuarioInternoRTRegistro inspectorActual = null;
                if (context != null && context.UserId > 0)
                {
                    inspectorActual = new UsuarioInternoRTDAO().ObtenerInspectorActivoPorTecnicoIdOUsuarioId(context.UserId);
                }

                if (inspectorActual == null && context != null && !string.IsNullOrWhiteSpace(context.CodigoUsuario))
                {
                    inspectorActual = new UsuarioInternoRTDAO().ObtenerActivoPorCodigoUsuario(context.CodigoUsuario)
                        ?? new UsuarioInternoRTDAO().ObtenerInspectorAsignableActivo(context.CodigoUsuario);
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
            }

            return ids;
        }

        private static HashSet<string> ResolverInspectorFilterTextIds(AocrBandejaRoleContext context, IEnumerable<int> inspectorIds)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (context != null)
            {
                AddTextId(ids, context.CodigoUsuario);
                AddTextId(ids, context.UserName);
            }

            foreach (var id in inspectorIds ?? Enumerable.Empty<int>())
            {
                if (id > 0)
                {
                    AddTextId(ids, id.ToString());
                }
            }

            return ids;
        }

        private static void AddTextId(HashSet<string> ids, string value)
        {
            if (ids == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            ids.Add(value.Trim());
        }

        private static bool InspectorMatchesTextIdentifiers(Inspeccion inspeccion, HashSet<string> inspectorTextIds)
        {
            if (inspeccion == null || inspectorTextIds == null || inspectorTextIds.Count == 0)
            {
                return false;
            }

            return TextIdentifierMatches(inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : null, inspectorTextIds)
                || TextIdentifierMatches(inspeccion.InspectorPrincipalCedula, inspectorTextIds)
                || TextIdentifierMatches(inspeccion.InspectorPrincipalNombre, inspectorTextIds);
        }

        private static bool TextIdentifierMatches(string value, HashSet<string> identifiers)
        {
            if (string.IsNullOrWhiteSpace(value) || identifiers == null || identifiers.Count == 0)
            {
                return false;
            }

            var normalized = value.Trim();
            return identifiers.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        private static bool EsEstadoAsignada(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.PAGO_VALIDADO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.VIATICOS_REQUERIDOS, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoEnFase(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoObservada(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.OBSERVADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoFinalizada(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoEmisionAocr(string estado)
        {
            var normalized = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalized, AocrEstadosProceso.InformeTecnicoAprobadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "EMISION_AOCR_CONDICIONES", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class InspectorBandejaContadores
    {
        public int Total { get; set; }
        public int Asignadas { get; set; }
        public int EnFase { get; set; }
        public int Observadas { get; set; }
        public int Finalizadas { get; set; }
        public int RevisionDocumental { get; set; }
        public int EmisionAocrCondiciones { get; set; }
    }

    public sealed class InspectorDocumentoFinalPendiente
    {
        public int InspeccionId { get; set; }
        public int SolicitudId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string CompaniaRuc { get; set; }
        public string CompaniaNombre { get; set; }
        public string NumeroInspeccion { get; set; }
        public string TipoTramite { get; set; }
        public string InspectorAsignado { get; set; }
        public DateTime? FechaAprobacionDirdac { get; set; }
        public string EstadoAocr { get; set; }
        public string EstadoCondiciones { get; set; }
        public bool GenerarAocr { get; set; }
        public bool GenerarCondiciones { get; set; }
    }
}
