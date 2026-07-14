using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio institucional para la generación automática del documento AOCR.
    /// Reemplaza la carga manual del "Borrador AOCR" por generación controlada
    /// a partir de los datos del trámite y el informe técnico que completa la fase tecnica.
    ///
    /// Este servicio evalúa las reglas de habilitación y persiste el documento
    /// generado. La creación física del PDF se realiza en la capa de presentación
    /// (que tiene acceso a Rotativa / Razor ViewEngine).
    /// </summary>
    public class GeneracionAOCRService
    {
        /// <summary>Tipo de documento institucional para el AOCR generado por el sistema.</summary>
        public const string TIPO_DOCUMENTO_AOCR_GENERADO = "AOCR_GENERADO";

        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly DocumentoDAO _documentoDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly InspeccionInformeDAO _informeDao;
        private readonly HallazgoDAO _hallazgoDao;
        private readonly HistorialEstadoDAO _historialDao;
        private readonly ListaVerificacionOperacionalEaeDAO _listaVerificacionDao;
        private readonly RevisionDocumentalService _revisionDocumentalService;
        private readonly SolicitudEstadoTransitionBL _solicitudEstadoTransitionBl;

        public GeneracionAOCRService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _documentoDao = new DocumentoDAO();
            _inspeccionDao = new InspeccionDAO();
            _informeDao = new InspeccionInformeDAO();
            _hallazgoDao = new HallazgoDAO();
            _historialDao = new HistorialEstadoDAO();
            _listaVerificacionDao = new ListaVerificacionOperacionalEaeDAO();
            _revisionDocumentalService = new RevisionDocumentalService();
            _solicitudEstadoTransitionBl = new SolicitudEstadoTransitionBL();
        }

        /// <summary>Resultado de la evaluación de disponibilidad para generar AOCR.</summary>
        public class Disponibilidad
        {
            public bool Habilitado { get; set; }
            public string Motivo { get; set; }
            public bool YaGenerado { get; set; }
            public Documento DocumentoGenerado { get; set; }
            public SolicitudAOCR Solicitud { get; set; }
            public InspeccionInformeTecnico InformeAprobado { get; set; }
            public Inspeccion InspeccionAprobada { get; set; }
            public ListaVerificacionOperacionalEae ListaVerificacionAprobada { get; set; }
            public string EstadoSolicitud { get; set; }
            public string EstadoInspeccion { get; set; }
            public string EstadoInforme { get; set; }
            public string ResultadoTecnicoFinal { get; set; }
            public bool InformeTecnicoExiste { get; set; }
            public bool InformeTecnicoFirmadoInspector { get; set; }
            public bool AprobadoDireccion { get; set; }
            public bool AprobadoDirdac { get; set; }
            public bool TieneObservacionesPendientes { get; set; }
            public bool TieneNoConformidadActiva { get; set; }
        }

        public class LegacyAocrResyncResult
        {
            public int LimiteAplicado { get; set; }
            public int Candidatas { get; set; }
            public int Sincronizadas { get; set; }
            public int YaGeneradas { get; set; }
            public int SinInformeAprobado { get; set; }
            public int Errores { get; set; }
            public List<int> SolicitudesSincronizadas { get; set; }
            public List<string> MensajesError { get; set; }

            public LegacyAocrResyncResult()
            {
                SolicitudesSincronizadas = new List<int>();
                MensajesError = new List<string>();
            }
        }

        public class LegacyAocrCandidateDetail
        {
            public int CodigoSolicitud { get; set; }
            public string NumeroSolicitud { get; set; }
            public string EstadoSolicitud { get; set; }
            public int CodigoInspeccion { get; set; }
            public int CodigoInforme { get; set; }
            public string EstadoInforme { get; set; }
        }

        public class LegacyAocrInventoryResult
        {
            public int LimiteAplicado { get; set; }
            public int LegacyPendientes { get; set; }
            public int ListasParaResync { get; set; }
            public int YaGeneradas { get; set; }
            public int SinInformeAprobado { get; set; }
            public List<LegacyAocrCandidateDetail> Candidatas { get; set; }

            public LegacyAocrInventoryResult()
            {
                Candidatas = new List<LegacyAocrCandidateDetail>();
            }
        }

        /// <summary>Formato institucional del número AOCR: AOCR-YYYY-#### .</summary>
        public static string GenerarNumeroAOCR(int idSolicitud, DateTime? fecha = null)
        {
            var f = fecha ?? DateTime.Now;
            return "AOCR-" + f.Year.ToString("0000") + "-" + idSolicitud.ToString("0000");
        }

        /// <summary>Evalúa si un trámite puede generar su AOCR automáticamente.</summary>
        public Disponibilidad Evaluar(int codigoSolicitud)
        {
            return Evaluar(codigoSolicitud, 0, null);
        }

        public Disponibilidad Evaluar(int codigoSolicitud, int codigoUsuario, IEnumerable<string> rolesUsuario)
        {
            var resultado = new Disponibilidad { Habilitado = false };

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            resultado.Solicitud = solicitud;
            if (solicitud == null)
            {
                resultado.Motivo = "La solicitud no existe.";
                return resultado;
            }

            resultado.EstadoSolicitud = solicitud.Estado ?? string.Empty;

            string motivoTipoTramite;
            if (!new AocrCierrePorTipoTramiteService().PuedeGenerarDocumento(
                solicitud, AocrCierrePorTipoTramiteService.Reconocimiento, out motivoTipoTramite))
            {
                resultado.Motivo = motivoTipoTramite;
                return resultado;
            }

            Documento existente = ObtenerAocrGeneradoVigente(codigoSolicitud);
            if (existente != null)
            {
                resultado.YaGenerado = true;
                resultado.DocumentoGenerado = existente;
            }

            if (!UsuarioPuedeGenerarAocr(codigoUsuario, rolesUsuario))
            {
                resultado.Motivo = "No tiene permisos para generar AOCR.";
                return resultado;
            }

            var estadoDocumental = _revisionDocumentalService.ObtenerEstadoFaseDocumental(codigoSolicitud);
            if (estadoDocumental == null || !estadoDocumental.DocumentacionAprobada)
            {
                resultado.Motivo = "Pendiente de aprobación documental.";
                return resultado;
            }

            if (estadoDocumental.TieneDocumentosObservados || estadoDocumental.TieneDocumentosSubsanadosPendientes || estadoDocumental.TienePendientes || estadoDocumental.DocumentosPendientesRevision > 0)
            {
                resultado.Motivo = "Existen documentos pendientes de revisión u observaciones activas.";
                return resultado;
            }

            // Regla 1: el trámite debe estar en fase AOCR o, al menos, en una etapa técnica aprobada sincronizable.
            var estado = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            bool estadoValido = EstadoSolicitudPermiteGeneracion(estado);

            if (!estadoValido)
            {
                resultado.Motivo = "El Informe Técnico ya debe estar aprobado por Dirección/DIRDAC y la solicitud debe encontrarse en fase AOCR para generar la AOCR.";
                return resultado;
            }

            // Regla 2: informe técnico finalizado y con aprobación institucional real.
            var informeRelacionado = ObtenerUltimoInformeRelacionado(codigoSolicitud);
            PoblarDiagnosticoInforme(resultado, informeRelacionado);

            InspeccionInformeTecnico informe = ObtenerInformeAprobado(codigoSolicitud);
            resultado.InformeAprobado = informe;

            if (informe == null)
            {
                resultado.Motivo = ConstruirMotivoInformeNoDisponible(informeRelacionado);
                return resultado;
            }
            if (!informe.Finalizado)
            {
                resultado.Motivo = "El informe técnico aún no ha sido finalizado por el inspector.";
                return resultado;
            }
            if (!informe.FirmadoInspector)
            {
                resultado.Motivo = "El informe técnico no ha sido firmado por el inspector.";
                return resultado;
            }
            if (!InformeCompletaFaseTecnicaAocr(informe))
            {
                resultado.Motivo = "Pendiente de aprobación del Informe Técnico por Dirección/DIRDAC.";
                return resultado;
            }

            if (InformeTieneObservacionesPendientes(informe))
            {
                resultado.Motivo = "El informe técnico tiene observaciones o rechazo vigentes.";
                return resultado;
            }

            if (!InformeResultadoPermiteGeneracionAocr(informe))
            {
                resultado.Motivo = "El informe técnico aprobado no tiene resultado satisfactorio para habilitar AOCR.";
                return resultado;
            }

            var inspeccion = _inspeccionDao.ObtenerPorId(informe.CodigoInspeccion);
            resultado.InspeccionAprobada = inspeccion;
            resultado.EstadoInspeccion = inspeccion != null ? (inspeccion.Estado ?? string.Empty) : string.Empty;
            if (inspeccion == null || inspeccion.CodigoInspeccion <= 0)
            {
                resultado.Motivo = "No existe una inspección asociada a la solicitud para habilitar AOCR.";
                return resultado;
            }

            var noConformidadesActivas = ContarNoConformidadesActivas(inspeccion.CodigoInspeccion);
            resultado.TieneNoConformidadActiva = noConformidadesActivas > 0;
            if (resultado.TieneNoConformidadActiva)
            {
                resultado.Motivo = "Existen no conformidades pendientes que bloquean la emisión de la AOCR.";
                return resultado;
            }

            var listaVerificacion = inspeccion != null && inspeccion.CodigoInspeccion > 0
                ? _listaVerificacionDao.ObtenerUltimaPorInspeccion(inspeccion.CodigoInspeccion)
                : null;
            resultado.ListaVerificacionAprobada = listaVerificacion;
            if (listaVerificacion == null || !listaVerificacion.Finalizado)
            {
                resultado.Motivo = "Pendiente de finalizar LV/EAE.";
                return resultado;
            }

            if (!listaVerificacion.FirmadoTecnico)
            {
                resultado.Motivo = "Pendiente de firma de la LV/EAE por el técnico responsable.";
                return resultado;
            }

            // Regla 3: no regenerar si ya existe uno vigente
            if (resultado.YaGenerado)
            {
                resultado.Motivo = "La AOCR ya fue generada para esta solicitud.";
                resultado.Habilitado = false;
                return resultado;
            }

            resultado.Habilitado = true;
            resultado.Motivo = "El Informe Técnico fue aprobado por Dirección/DIRDAC. Puede generar la AOCR.";
            return resultado;
        }

        public bool PuedeGenerarAocr(int codigoSolicitud, int codigoUsuario, IEnumerable<string> rolesUsuario, out string motivo)
        {
            var resultado = Evaluar(codigoSolicitud, codigoUsuario, rolesUsuario);
            motivo = resultado != null ? resultado.Motivo : "No se pudo evaluar la generación de la AOCR.";
            return resultado != null && resultado.Habilitado;
        }

        public string ObtenerMotivoBloqueoGeneracionAocr(int codigoSolicitud, int codigoUsuario, IEnumerable<string> rolesUsuario)
        {
            var resultado = Evaluar(codigoSolicitud, codigoUsuario, rolesUsuario);
            return resultado != null ? (resultado.Motivo ?? string.Empty) : string.Empty;
        }

        public bool ExisteAocrGenerada(int codigoSolicitud)
        {
            return ObtenerAocrGeneradoVigente(codigoSolicitud) != null;
        }

        public bool MarcarPendienteGeneracionAocr(int codigoSolicitud, int codigoInforme, int codigoUsuario, string usuarioNombre, out string mensaje)
        {
            mensaje = string.Empty;

            if (codigoSolicitud <= 0 || codigoUsuario <= 0)
            {
                mensaje = "No existe contexto suficiente para habilitar la generación de AOCR.";
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            if (solicitud == null)
            {
                mensaje = "La solicitud AOCR no existe.";
                return false;
            }

            var informe = codigoInforme > 0
                ? _informeDao.ObtenerPorId(codigoInforme)
                : ObtenerInformeAprobado(codigoSolicitud);

            if (informe == null || !InformeCompletaFaseTecnicaAocr(informe))
            {
                mensaje = "El Informe Técnico aún no cuenta con aprobación institucional final.";
                return false;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            if (string.Equals(estadoActual, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoActual, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoActual, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoActual, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoActual, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase))
            {
                mensaje = "La solicitud ya se encuentra habilitada para el flujo AOCR.";
                return true;
            }

            var observacion = "El Informe Técnico fue aprobado por Dirección/DIRDAC. Se habilita la generación de la AOCR.";
            string mensajeCambio;
            var actualizado = _solicitudEstadoTransitionBl.CambiarEstadoConReglasAocr(
                codigoSolicitud,
                EstadoSolicitud.AOCR_EnElaboracion,
                observacion,
                codigoUsuario,
                destino => string.Equals(destino, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase),
                out mensajeCambio);

            if (!actualizado)
            {
                actualizado = _solicitudDao.CambiarEstado(codigoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, codigoUsuario, observacion);
                if (!actualizado)
                {
                    mensaje = string.IsNullOrWhiteSpace(mensajeCambio)
                        ? "No fue posible marcar la solicitud como pendiente de generación AOCR."
                        : mensajeCambio;
                    return false;
                }
            }

            try
            {
                _historialDao.RegistrarCambio(
                    codigoSolicitud,
                    estadoActual,
                    EstadoSolicitud.AOCR_EnElaboracion,
                    codigoUsuario,
                    observacion + " Evento=AOCR_HABILITADA_PARA_GENERACION. Usuario=" + (usuarioNombre ?? "sistema"));
            }
            catch
            {
                // No romper el flujo si el historial no se pudo registrar por drift de esquema.
            }

            mensaje = "La solicitud quedó en AOCR En Elaboración y la generación ya está habilitada.";
            return true;
        }

        public LegacyAocrResyncResult ResincronizarCasosLegacyPendientesAocr(int codigoUsuario, string usuarioNombre, int maxSolicitudes = 200)
        {
            var resultado = new LegacyAocrResyncResult();

            if (maxSolicitudes <= 0 || maxSolicitudes > 500)
            {
                maxSolicitudes = 200;
            }

            resultado.LimiteAplicado = maxSolicitudes;
            var inventario = InventariarCasosLegacyPendientesAocr(maxSolicitudes);
            resultado.Candidatas = inventario.ListasParaResync;

            if (codigoUsuario <= 0)
            {
                resultado.Errores = 1;
                resultado.MensajesError.Add("No existe contexto suficiente para ejecutar la resincronización AOCR legacy.");
                return resultado;
            }

            foreach (var candidata in inventario.Candidatas)
            {
                try
                {
                    string mensaje;
                    if (MarcarPendienteGeneracionAocr(candidata.CodigoSolicitud, candidata.CodigoInforme, codigoUsuario, usuarioNombre, out mensaje))
                    {
                        resultado.Sincronizadas++;
                        resultado.SolicitudesSincronizadas.Add(candidata.CodigoSolicitud);
                        continue;
                    }

                    resultado.Errores++;
                    if (resultado.MensajesError.Count < 10)
                    {
                        resultado.MensajesError.Add("Solicitud " + candidata.CodigoSolicitud + ": " + (mensaje ?? "No se pudo resincronizar."));
                    }
                }
                catch (Exception ex)
                {
                    resultado.Errores++;
                    if (resultado.MensajesError.Count < 10)
                    {
                        resultado.MensajesError.Add("Solicitud " + candidata.CodigoSolicitud + ": " + ex.Message);
                    }
                }
            }

            resultado.YaGeneradas = inventario.YaGeneradas;
            resultado.SinInformeAprobado = inventario.SinInformeAprobado;

            return resultado;
        }

        public LegacyAocrInventoryResult InventariarCasosLegacyPendientesAocr(int maxSolicitudes = 200)
        {
            var resultado = new LegacyAocrInventoryResult();

            if (maxSolicitudes <= 0 || maxSolicitudes > 500)
            {
                maxSolicitudes = 200;
            }

            resultado.LimiteAplicado = maxSolicitudes;

            var legacyPendientes = (_solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>())
                .Where(s => s != null && s.CodigoSolicitud > 0)
                .Where(s => EstadoSolicitudEsLegacyPendienteAocr(EstadoSolicitud.Normalizar(s.Estado ?? string.Empty)))
                .OrderBy(s => s.CodigoSolicitud)
                .Take(maxSolicitudes)
                .ToList();

            resultado.LegacyPendientes = legacyPendientes.Count;

            foreach (var solicitud in legacyPendientes)
            {
                if (ObtenerAocrGeneradoVigente(solicitud.CodigoSolicitud) != null)
                {
                    resultado.YaGeneradas++;
                    continue;
                }

                var informe = ObtenerInformeAprobado(solicitud.CodigoSolicitud);
                if (informe == null)
                {
                    resultado.SinInformeAprobado++;
                    continue;
                }

                resultado.ListasParaResync++;
                resultado.Candidatas.Add(new LegacyAocrCandidateDetail
                {
                    CodigoSolicitud = solicitud.CodigoSolicitud,
                    NumeroSolicitud = solicitud.NumeroSolicitud,
                    EstadoSolicitud = solicitud.Estado,
                    CodigoInspeccion = informe.CodigoInspeccion,
                    CodigoInforme = informe.CodigoInforme,
                    EstadoInforme = informe.EstadoInforme
                });
            }

            return resultado;
        }

        /// <summary>Obtiene el documento AOCR generado vigente (si existe).</summary>
        public Documento ObtenerAocrGeneradoVigente(int codigoSolicitud)
        {
            try
            {
                var docs = _documentoDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>();
                return docs
                    .Where(d => d != null && !string.IsNullOrEmpty(d.TipoDocumento))
                    .Where(d => string.Equals(d.TipoDocumento, TIPO_DOCUMENTO_AOCR_GENERADO, StringComparison.OrdinalIgnoreCase))
                    .Where(d => !string.Equals((d.Estado ?? string.Empty).Trim(), "RECHAZADO", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals((d.Estado ?? string.Empty).Trim(), "ANULADO", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private InspeccionInformeTecnico ObtenerInformeAprobado(int codigoSolicitud)
        {
            try
            {
                var inspecciones = _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
                InspeccionInformeTecnico mejor = null;
                foreach (var ins in inspecciones)
                {
                    if (ins == null) continue;
                    var inf = _informeDao.ObtenerUltimoPorInspeccion(ins.CodigoInspeccion);
                    if (inf == null) continue;

                    if (!InformeCompletaFaseTecnicaAocr(inf) || InformeTieneObservacionesPendientes(inf))
                    {
                        continue;
                    }

                    if (!InformeResultadoPermiteGeneracionAocr(inf))
                    {
                        continue;
                    }

                    if (mejor == null) { mejor = inf; continue; }
                    int scoreActual = (inf.Finalizado ? 1 : 0) + (inf.FirmadoInspector ? 1 : 0) + (InformeCompletaFaseTecnicaAocr(inf) ? 1 : 0);
                    int scoreMejor = (mejor.Finalizado ? 1 : 0) + (mejor.FirmadoInspector ? 1 : 0) + (InformeCompletaFaseTecnicaAocr(mejor) ? 1 : 0);
                    if (scoreActual > scoreMejor) mejor = inf;
                }
                return mejor;
            }
            catch
            {
                return null;
            }
        }

        private InspeccionInformeTecnico ObtenerUltimoInformeRelacionado(int codigoSolicitud)
        {
            try
            {
                var inspecciones = _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
                InspeccionInformeTecnico mejor = null;
                foreach (var ins in inspecciones)
                {
                    if (ins == null)
                    {
                        continue;
                    }

                    var informe = _informeDao.ObtenerUltimoPorInspeccion(ins.CodigoInspeccion);
                    if (informe == null)
                    {
                        continue;
                    }

                    if (mejor == null || ObtenerFechaOrdenInforme(informe) > ObtenerFechaOrdenInforme(mejor))
                    {
                        mejor = informe;
                    }
                }

                return mejor;
            }
            catch
            {
                return null;
            }
        }

        private static bool InformeCompletaFaseTecnicaAocr(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return false;
            }

            if (!informe.Finalizado || !informe.FirmadoInspector)
            {
                return false;
            }

            if (informe.FirmadoDirdac)
            {
                return true;
            }

            if (informe.FechaFirma2.HasValue && !string.IsNullOrWhiteSpace(informe.UsuarioFirma2))
            {
                return true;
            }

            var estadoInforme = (informe.EstadoInforme ?? string.Empty).Trim().ToUpperInvariant();
            return estadoInforme == "APROBADO_DIRECCION"
                || estadoInforme == "APROBADO_DIRDAC"
                || estadoInforme == "FIRMADO_FINAL";
        }

        private static bool InformeTieneObservacionesPendientes(InspeccionInformeTecnico informe)
        {
            var estadoInforme = (informe != null ? informe.EstadoInforme : null ?? string.Empty).Trim();
            return string.Equals(estadoInforme, "OBSERVADO_DIRDAC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInforme, "RECHAZADO_DIRDAC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInforme, "DEVUELTO_DIRECCION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInforme, "DEVUELTO_RT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoInforme, "OBSERVADO", StringComparison.OrdinalIgnoreCase);
        }

        private static bool InformeResultadoPermiteGeneracionAocr(InspeccionInformeTecnico informe)
        {
            return string.Equals(NormalizarResultadoInformeTecnico(informe != null ? informe.Resultado : null), "SATISFACTORIO", StringComparison.OrdinalIgnoreCase);
        }

        private static string ConstruirMotivoInformeNoDisponible(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return "No existe Informe Técnico asociado a la inspección.";
            }

            if (!informe.Finalizado)
            {
                return "El Informe Técnico aún no ha sido finalizado por el inspector.";
            }

            if (!informe.FirmadoInspector)
            {
                return "Debe firmar el Informe Técnico antes de continuar.";
            }

            if (InformeTieneObservacionesPendientes(informe))
            {
                return "El Informe Técnico tiene observaciones pendientes. Debe subsanar antes de generar AOCR.";
            }

            var resultadoNormalizado = NormalizarResultadoInformeTecnico(informe.Resultado);
            if (string.IsNullOrWhiteSpace(resultadoNormalizado))
            {
                return "Debe seleccionar el resultado técnico final.";
            }

            if (string.Equals(resultadoNormalizado, "INSATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                return "El resultado técnico es no satisfactorio. No se habilita AOCR. Debe continuar el flujo de No Conformidad.";
            }

            if (!InformeCompletaFaseTecnicaAocr(informe))
            {
                return "El Informe Técnico satisfactorio está pendiente de aprobación por Dirección/DIRDAC.";
            }

            return "Pendiente de aprobación del Informe Técnico por Dirección/DIRDAC.";
        }

        private static void PoblarDiagnosticoInforme(Disponibilidad resultado, InspeccionInformeTecnico informe)
        {
            if (resultado == null || informe == null)
            {
                return;
            }

            resultado.InformeTecnicoExiste = true;
            resultado.EstadoInforme = informe.EstadoInforme ?? string.Empty;
            resultado.ResultadoTecnicoFinal = informe.Resultado ?? string.Empty;
            resultado.InformeTecnicoFirmadoInspector = informe.FirmadoInspector;
            resultado.AprobadoDireccion = InformeCompletaFaseTecnicaAocr(informe);
            resultado.AprobadoDirdac = informe.FirmadoDirdac
                || string.Equals((informe.EstadoInforme ?? string.Empty).Trim(), "APROBADO_DIRDAC", StringComparison.OrdinalIgnoreCase)
                || string.Equals((informe.EstadoInforme ?? string.Empty).Trim(), "FIRMADO_FINAL", StringComparison.OrdinalIgnoreCase);
            resultado.TieneObservacionesPendientes = InformeTieneObservacionesPendientes(informe);
        }

        private int ContarNoConformidadesActivas(int codigoInspeccion)
        {
            if (codigoInspeccion <= 0)
            {
                return 0;
            }

            try
            {
                var noConformidades = new NoConformidadDAO().ContarAbiertasRelacionadasConInspeccion(codigoInspeccion);
                var hallazgos = _hallazgoDao.ObtenerPorInspeccion(codigoInspeccion) ?? new List<Hallazgo>();
                return noConformidades + hallazgos.Count(hallazgo => hallazgo != null
                    && !string.Equals((hallazgo.Estado ?? string.Empty).Trim(), "CERRADO", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals((hallazgo.Estado ?? string.Empty).Trim(), "RESUELTO", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                // Ante una falla de persistencia se bloquea la emisión: nunca se debe
                // interpretar un error de lectura como ausencia de NC.
                return 1;
            }
        }

        private static DateTime ObtenerFechaOrdenInforme(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return DateTime.MinValue;
            }

            return informe.UpdatedAt
                ?? informe.FechaFirma2
                ?? informe.FechaFinalizacion
                ?? informe.FechaFirma1
                ?? informe.CreatedAt
                ?? DateTime.MinValue;
        }

        private static string NormalizarResultadoInformeTecnico(string resultado)
        {
            var normalizado = (resultado ?? string.Empty).Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');

            switch (normalizado)
            {
                case "APROBADO":
                case EstadosInspeccion.RESULTADO_SATISFACTORIO:
                    return "SATISFACTORIO";
                case "NO_SATISFACTORIO":
                case "RECHAZADO":
                case EstadosInspeccion.RESULTADO_NO_SATISFACTORIO:
                    return "INSATISFACTORIO";
                default:
                    return normalizado;
            }
        }

        private static bool EstadoSolicitudPermiteGeneracion(string estadoSolicitud)
        {
            return string.Equals(estadoSolicitud, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.DocumentacionCompleta, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.Aprobada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.InspeccionRealizada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase);
        }

            private static bool EstadoSolicitudEsLegacyPendienteAocr(string estadoSolicitud)
            {
                return string.Equals(estadoSolicitud, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoSolicitud, EstadoSolicitud.DocumentacionCompleta, StringComparison.OrdinalIgnoreCase);
            }

        private static bool UsuarioPuedeGenerarAocr(int codigoUsuario, IEnumerable<string> rolesUsuario)
        {
            if (rolesUsuario == null)
            {
                return true;
            }

            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rol in rolesUsuario)
            {
                if (!string.IsNullOrWhiteSpace(rol))
                {
                    roles.Add(rol.Trim());
                }
            }

            if (codigoUsuario <= 0 || roles.Count == 0)
            {
                return false;
            }

            return roles.Contains("Administrador")
                || roles.Contains("DIRDAC")
                || roles.Contains("Direccion")
                || roles.Contains("Director")
                || roles.Contains("DirectorGeneral")
                || roles.Contains("JefaturaTecnica")
                || roles.Contains("Jefe")
                || roles.Contains("DireccionJefaturaTecnica");
        }

        /// <summary>
        /// Persiste el documento AOCR generado (el archivo PDF ya debe existir en disco)
        /// y registra el evento en el historial institucional del trámite.
        /// </summary>
        public Documento RegistrarDocumentoGenerado(
            int codigoSolicitud,
            string rutaArchivo,
            string nombreArchivo,
            string numeroAOCR,
            int usuarioId,
            string usuarioNombre,
            out string mensaje)
        {
            mensaje = null;

            if (string.IsNullOrWhiteSpace(rutaArchivo) || !File.Exists(rutaArchivo))
            {
                mensaje = "No se encontró el archivo PDF generado.";
                return null;
            }

            long? tamano = null;
            try { tamano = new FileInfo(rutaArchivo).Length; } catch { /* opcional */ }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            string estadoAnterior = solicitud != null ? solicitud.Estado : null;

            var documento = new Documento
            {
                CodigoSolicitud = codigoSolicitud,
                TipoDocumento = TIPO_DOCUMENTO_AOCR_GENERADO,
                NombreArchivo = string.IsNullOrWhiteSpace(nombreArchivo) ? Path.GetFileName(rutaArchivo) : nombreArchivo,
                RutaArchivo = rutaArchivo,
                TamanioArchivo = tamano,
                Observaciones = "AOCR generada automáticamente por el sistema. N° " + (numeroAOCR ?? ""),
                UsuarioRegistro = string.IsNullOrEmpty(usuarioNombre) ? "sistema" : usuarioNombre,
                Estado = "APROBADO",
                Validado = true,
                Version = 1,
                FechaCarga = DateTime.Now
            };

            try
            {
                int idGenerado = _documentoDao.Crear(documento);
                if (idGenerado > 0)
                {
                    documento.CodigoDocumento = idGenerado;
                }
            }
            catch (Exception ex)
            {
                mensaje = "El PDF se generó pero no se pudo registrar en el expediente: " + ex.Message;
                return null;
            }

            try
            {
                _historialDao.RegistrarCambio(
                    codigoSolicitud,
                    estadoAnterior,
                    estadoAnterior,
                    usuarioId,
                    "Generación automática de AOCR (" + (numeroAOCR ?? "S/N") + "). Archivo: " + documento.NombreArchivo + ". Evento=AOCR_GENERADA.");
            }
            catch { /* no romper si el historial falla */ }

            mensaje = "AOCR generada correctamente" + (string.IsNullOrEmpty(numeroAOCR) ? "." : " (" + numeroAOCR + ").");
            return documento;
        }
    }
}
