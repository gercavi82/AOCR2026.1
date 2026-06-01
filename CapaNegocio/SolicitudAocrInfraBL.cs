using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio.Integraciones.As400Sync;
using CapaModelo;

namespace CapaNegocio
{
    /// <summary>
    /// Fachada de infraestructura para desacoplar el controlador AOCR de instanciaciones DAO ad-hoc.
    /// </summary>
    public class SolicitudAocrInfraBL
    {
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly DocumentoDAO _documentoDao = new DocumentoDAO();
        private readonly HistorialEstadoDAO _historialEstadoDao = new HistorialEstadoDAO();
        private readonly RevisionDocumentalDAO _revisionDocumentalDao = new RevisionDocumentalDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao = new UsuarioInternoRTDAO();
        private readonly UsuarioAS400DAO _usuarioAs400Dao = new UsuarioAS400DAO(new SecureConfigurationService());
        private readonly EmpresaAS400DAO _empresaAs400Dao = new EmpresaAS400DAO(new SecureConfigurationService());
        private readonly MirrorReadService _mirrorReadService = new MirrorReadService();
        private readonly TrazabilidadDAO _trazabilidadDao = new TrazabilidadDAO();

        // =========================================================
        // TRAZABILIDAD COMPLETA DEL EXPEDIENTE
        // =========================================================
        public List<EventoTrazabilidad> ObtenerTrazabilidadCompleta(int codigoSolicitud)
        {
            try
            {
                return _trazabilidadDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<EventoTrazabilidad>();
            }
            catch
            {
                return new List<EventoTrazabilidad>();
            }
        }

        public List<DocumentoSubsanacionRegistro> ObtenerDocumentosSubsanacionPorSolicitud(int codigoSolicitud)
        {
            try
            {
                return _trazabilidadDao.ObtenerDocumentosSubsanacionPorSolicitud(codigoSolicitud)
                       ?? new List<DocumentoSubsanacionRegistro>();
            }
            catch
            {
                return new List<DocumentoSubsanacionRegistro>();
            }
        }

        public List<Inspeccion> ListarInspeccionesPorSolicitud(int codigoSolicitud)
        {
            return _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
        }

        public List<HistorialEstado> ObtenerHistorialEstadosPorSolicitud(int codigoSolicitud)
        {
            return _historialEstadoDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<HistorialEstado>();
        }

        public Dictionary<int, Tuple<string, string>> ObtenerUltimasRevisionesPorSolicitud(int codigoSolicitud)
        {
            return _revisionDocumentalDao.ObtenerUltimasRevisionesPorSolicitud(codigoSolicitud)
                   ?? new Dictionary<int, Tuple<string, string>>();
        }

        public Dictionary<int, RevisionDocumentalDetalle> ObtenerUltimosDetallesRevisionPorSolicitud(int codigoSolicitud)
        {
            return _revisionDocumentalDao.ObtenerUltimosDetallesPorSolicitud(codigoSolicitud)
                   ?? new Dictionary<int, RevisionDocumentalDetalle>();
        }

        public EstadoRevisionDocumental ObtenerEstadoRevisionDocumental(int codigoSolicitud)
        {
            var estado = new EstadoRevisionDocumental
            {
                CodigoSolicitud = codigoSolicitud
            };

            if (codigoSolicitud <= 0)
            {
                estado.TienePendientes = true;
                estado.MensajeBloqueoDocumental = "Fase documental pendiente. No se puede continuar porque la solicitud documental no es válida.";
                AsignarFlujoDocumental(estado, "PENDIENTE_CARGA_DOCUMENTAL", "Pendiente de carga documental", "RT");
                return estado;
            }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            var inspecciones = ListarInspeccionesPorSolicitud(codigoSolicitud);

            var documentos = (_documentoDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .GroupBy(ObtenerClaveDocumentoRevision, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(d => d.Version ?? 0)
                    .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                    .ThenByDescending(d => d.CodigoDocumento)
                    .First())
                .ToList();

            var revisiones = ObtenerUltimasRevisionesPorSolicitud(codigoSolicitud);
            estado.TotalDocumentosVigentes = documentos.Count;

            foreach (var documento in documentos)
            {
                var decision = ObtenerDecisionRevisionDocumental(documento, revisiones);
                var estadoDocumento = NormalizarEstadoDocumento(documento.Estado);

                if (decision == "ACEPTADO")
                {
                    estado.DocumentosAceptados++;
                    continue;
                }

                if (decision == "OBSERVADO" || decision == "DEVUELTO")
                {
                    estado.DocumentosObservadosDevueltos++;
                    continue;
                }

                if (decision == "SUBSANADO" || decision == "PENDIENTE_REVISION_SUBSANACION"
                    || estadoDocumento == "SUBSANADO" || estadoDocumento == "PENDIENTE_REVISION_SUBSANACION")
                {
                    estado.DocumentosSubsanadosPendientes++;
                    continue;
                }

                estado.DocumentosPendientesRevision++;
            }

            estado.TieneDocumentosObservados = estado.DocumentosObservadosDevueltos > 0;
            estado.TieneDocumentosSubsanadosPendientes = estado.DocumentosSubsanadosPendientes > 0;
            estado.TienePendientes = estado.DocumentosPendientesRevision > 0
                || estado.TieneDocumentosObservados
                || estado.TieneDocumentosSubsanadosPendientes;
            estado.DocumentacionAprobada = estado.TotalDocumentosVigentes > 0 && !estado.TienePendientes;
            estado.MensajeBloqueoDocumental = ConstruirMensajeBloqueoDocumental(estado);
            ConfigurarFlujoDocumental(estado, solicitud, inspecciones);

            return estado;
        }

        public bool TieneDocumentacionPendienteOSubsanacion(int codigoSolicitud)
        {
            return ObtenerEstadoRevisionDocumental(codigoSolicitud).TienePendientes;
        }

        public bool TodosDocumentosAceptados(int codigoSolicitud)
        {
            return ObtenerEstadoRevisionDocumental(codigoSolicitud).DocumentacionAprobada;
        }

        public bool RegistrarRevisionDocumental(int codigoSolicitud, int codigoDocumento, string decision, string observacion, int usuarioId, string usuarioRegistro)
        {
            return _revisionDocumentalDao.RegistrarRevision(codigoSolicitud, codigoDocumento, decision, observacion, usuarioId, usuarioRegistro);
        }

        public bool RegistrarEventoHistorialRevision(int codigoSolicitud, int? codigoDocumento, string tipoEvento, string observacion, int usuarioId, string usuarioRegistro)
        {
            return _revisionDocumentalDao.RegistrarEventoHistorial(codigoSolicitud, codigoDocumento, tipoEvento, observacion, usuarioId, usuarioRegistro);
        }

        public HashSet<int> ObtenerDocumentosConEventoHistorial(int codigoSolicitud, string tipoEvento)
        {
            return _revisionDocumentalDao.ObtenerDocumentosConEventoHistorial(codigoSolicitud, tipoEvento)
                   ?? new HashSet<int>();
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

        private static void ConfigurarFlujoDocumental(EstadoRevisionDocumental estado, SolicitudAOCR solicitud, IEnumerable<Inspeccion> inspecciones)
        {
            if (estado == null)
            {
                return;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud != null ? solicitud.Estado : null);
            var tieneInspectorAsignado = TieneInspectorAsignado(solicitud, inspecciones);

            if (estado.TotalDocumentosVigentes <= 0)
            {
                AsignarFlujoDocumental(estado, "PENDIENTE_CARGA_DOCUMENTAL", "Pendiente de carga documental", "RT");
                return;
            }

            if (string.Equals(estadoSolicitud, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.DocumentacionCompleta, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.RequiereInspeccion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.GeneradoCondicionesLimitaciones, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase))
            {
                AsignarFlujoDocumental(estado, "REVISADA_POR_INSPECTOR", "Revision completada por inspector", "COORDINADOR");
                return;
            }

            if (string.Equals(estadoSolicitud, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                AsignarFlujoDocumental(estado, "OBSERVADA_POR_INSPECTOR", "Documentacion observada", "RT");
                return;
            }

            if (string.Equals(estadoSolicitud, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase))
            {
                AsignarFlujoDocumental(
                    estado,
                    tieneInspectorAsignado ? "SUBSANADA_POR_RT" : "PENDIENTE_COORDINADOR",
                    tieneInspectorAsignado ? "Documentacion subsanada" : "Pendiente de coordinador",
                    tieneInspectorAsignado ? "INSPECTOR" : "COORDINADOR");
                return;
            }

            if (string.Equals(estadoSolicitud, EstadoSolicitud.EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.DocumentacionPendiente, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase))
            {
                AsignarFlujoDocumental(
                    estado,
                    tieneInspectorAsignado ? (estado.TieneDocumentosSubsanadosPendientes ? "SUBSANADA_POR_RT" : "EN_REVISION_INSPECTOR") : "PENDIENTE_COORDINADOR",
                    tieneInspectorAsignado ? (estado.TieneDocumentosSubsanadosPendientes ? "Documentacion subsanada" : "En revision documental") : "Pendiente de coordinador",
                    tieneInspectorAsignado ? "INSPECTOR" : "COORDINADOR");
                return;
            }

            if (!tieneInspectorAsignado)
            {
                AsignarFlujoDocumental(estado, "PENDIENTE_COORDINADOR", "Pendiente de coordinador", "COORDINADOR");
                return;
            }

            if (estado.DocumentacionAprobada)
            {
                AsignarFlujoDocumental(estado, "REVISADA_POR_INSPECTOR", "Revision completada por inspector", "COORDINADOR");
                return;
            }

            if (estado.TieneDocumentosSubsanadosPendientes)
            {
                AsignarFlujoDocumental(estado, "SUBSANADA_POR_RT", "Documentacion subsanada", "INSPECTOR");
                return;
            }

            AsignarFlujoDocumental(estado, "EN_REVISION_INSPECTOR", "En revision documental", "INSPECTOR");
        }

        private static void AsignarFlujoDocumental(EstadoRevisionDocumental estado, string codigo, string nombre, string responsable)
        {
            estado.FlujoDocumentalCodigo = codigo ?? string.Empty;
            estado.FlujoDocumentalNombre = nombre ?? string.Empty;
            estado.ResponsableActual = responsable ?? string.Empty;
            estado.VisibleEnBandejaInspector = string.Equals(estado.ResponsableActual, "INSPECTOR", StringComparison.OrdinalIgnoreCase);
            estado.VisibleEnBandejaCoordinador = string.Equals(estado.ResponsableActual, "COORDINADOR", StringComparison.OrdinalIgnoreCase);
            estado.VisibleEnBandejaRt = string.Equals(estado.ResponsableActual, "RT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TieneInspectorAsignado(SolicitudAOCR solicitud, IEnumerable<Inspeccion> inspecciones)
        {
            if (solicitud != null)
            {
                if (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableCedula)
                    || !string.IsNullOrWhiteSpace(solicitud.InspectorApoyoCedula))
                {
                    return true;
                }
            }

            return (inspecciones ?? Enumerable.Empty<Inspeccion>())
                .Any(inspeccion => inspeccion != null
                    && ((inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
                        || !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula)
                        || !string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoCedula)));
        }

        private static string ConstruirMensajeBloqueoDocumental(EstadoRevisionDocumental estado)
        {
            if (estado == null)
            {
                return "Fase documental pendiente. La inspección no puede iniciar porque la fase documental aún no ha sido finalizada.";
            }

            if (estado.TotalDocumentosVigentes <= 0)
            {
                return "Fase documental pendiente. La inspección no puede iniciar porque el RT aún no ha cargado o completado los documentos habilitantes para revisión.";
            }

            if (estado.TieneDocumentosObservados)
            {
                return "Fase documental pendiente. La inspección no puede iniciar porque existen documentos observados pendientes de subsanación y nueva revisión.";
            }

            if (estado.TieneDocumentosSubsanadosPendientes)
            {
                return "Fase documental pendiente. La inspección no puede iniciar porque existen documentos subsanados pendientes de revisión por parte del Inspector.";
            }

            if (estado.DocumentosPendientesRevision > 0)
            {
                return "Fase documental pendiente. La inspección no puede iniciar porque todavía hay documentos habilitantes pendientes de revisión documental.";
            }

            return string.Empty;
        }

        private static string ObtenerDecisionRevisionDocumental(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            Tuple<string, string> revisionActual;
            if (revisiones != null &&
                revisiones.TryGetValue(documento.CodigoDocumento, out revisionActual) &&
                revisionActual != null &&
                !string.IsNullOrWhiteSpace(revisionActual.Item1))
            {
                return NormalizarDecisionRevisionDocumental(revisionActual.Item1);
            }

            var estadoDocumento = NormalizarEstadoDocumento(documento.Estado);
            if (estadoDocumento == "APROBADO" || estadoDocumento == "VALIDADO" || estadoDocumento == "ACEPTADO")
            {
                return "ACEPTADO";
            }

            if (estadoDocumento == "OBSERVADO")
            {
                return "OBSERVADO";
            }

            if (estadoDocumento == "RECHAZADO" || estadoDocumento == "DEVUELTO")
            {
                return "DEVUELTO";
            }

            if (estadoDocumento == "SUBSANADO" || estadoDocumento == "PENDIENTE_REVISION_SUBSANACION")
            {
                return estadoDocumento;
            }

            return string.Empty;
        }

        private static string NormalizarDecisionRevisionDocumental(string decision)
        {
            var normalized = (decision ?? string.Empty).Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "ACEPTADO":
                case "APROBADO":
                case "VALIDADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                case "DEVUELTO":
                    return "DEVUELTO";
                case "OBSERVADO":
                case "MODIFICACION_SOLICITADA":
                case "MODIFICACION SOLICITADA":
                case "SOLICITAR_MODIFICACION":
                    return "OBSERVADO";
                case "SUBSANADO":
                case "PENDIENTE_REVISION_SUBSANACION":
                    return normalized;
                default:
                    return normalized;
            }
        }

        private static string NormalizarEstadoDocumento(string estado)
        {
            var normalized = (estado ?? string.Empty).Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "APROBADO":
                case "VALIDADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                    return "DEVUELTO";
                default:
                    return normalized;
            }
        }

        public AsignacionRTRegistro ObtenerAsignacionActiva(int codigoSolicitud)
        {
            return _usuarioInternoRtDao.ObtenerAsignacionActiva(codigoSolicitud);
        }

        public List<AsignacionRTRegistro> ObtenerHistorialAsignacion(int codigoSolicitud)
        {
            return _usuarioInternoRtDao.ObtenerHistorialAsignacion(codigoSolicitud) ?? new List<AsignacionRTRegistro>();
        }

        public string ObtenerCedulaPorCodigoUsuario(string codigoUsuario)
        {
            var clave = (codigoUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clave))
            {
                return null;
            }

            try
            {
                var mirror = _mirrorReadService.ObtenerIdentificacionPorClavesUsuario(new[] { clave });
                if (mirror != null && !string.IsNullOrWhiteSpace(mirror.Cedula))
                {
                    return mirror.Cedula.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerCedulaPorCodigoUsuario mirror error: " + ex.Message);
            }

            return _usuarioAs400Dao.ObtenerCedulaPorCodigoUsuario(clave);
        }

        public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario)
        {
            var clave = (codigoUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clave))
            {
                return null;
            }

            try
            {
                var mirror = _mirrorReadService.ObtenerIdentificacionPorClavesUsuario(new[] { clave });
                if (mirror != null && !string.IsNullOrWhiteSpace(mirror.Ruc))
                {
                    return mirror.Ruc.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerNumeroRucPorCodigoUsuario mirror error: " + ex.Message);
            }

            return _usuarioAs400Dao.ObtenerNumeroRucPorCodigoUsuario(clave);
        }

        public Empresa ObtenerEmpresaPorCodigo(string codigoOaci)
        {
            var codigo = (codigoOaci ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return null;
            }

            try
            {
                var mirror = _mirrorReadService.ObtenerCompaniaPorCodigo(codigo);
                if (mirror != null)
                {
                    return new Empresa
                    {
                        CodigoOaci = mirror.CodigoOaci,
                        CodigoIata = mirror.CodigoIata,
                        CodigoNumeroCia = mirror.CodigoNumeroCia,
                        Nombre = mirror.NombreCompania
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerEmpresaPorCodigo mirror error: " + ex.Message);
            }

            return _empresaAs400Dao.ObtenerEmpresaPorCodigo(codigo);
        }

        public List<Empresa> ObtenerEmpresas()
        {
            try
            {
                var mirror = _mirrorReadService.ListarCompaniasActivas(5000);
                if (mirror != null && mirror.Count > 0)
                {
                    return mirror
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CodigoOaci))
                        .Select(c => new Empresa
                        {
                            CodigoOaci = c.CodigoOaci,
                            CodigoIata = c.CodigoIata,
                            CodigoNumeroCia = c.CodigoNumeroCia,
                            Nombre = c.NombreCompania
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerEmpresas mirror error: " + ex.Message);
            }

            return _empresaAs400Dao.ObtenerEmpresas() ?? new List<Empresa>();
        }
    }
}
