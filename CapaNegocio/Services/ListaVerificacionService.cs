using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// AC-07: Servicio de orquestación de reglas de negocio para Listas de Verificación (LV)
    /// independientes por inspección o estación solicitada.
    /// Garantiza la segregación institucional estricta (DIRDAC y Admin excluidos de firma/edición),
    /// la inmutabilidad tras la firma y el bloqueo preventivo del Informe Técnico si hay LVs pendientes.
    /// </summary>
    public class ListaVerificacionService
    {
        private readonly ListaVerificacionOperacionalEaeDAO _lvDao;
        private readonly SolicitudEstacionDAO _estacionDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AuditoriaDAO _auditoriaDao;
        private readonly ListaVerificacionCatalogService _catalogService;
        private readonly Func<int, InspectorIdentityInfo> _resolverIdentidad;

        public ListaVerificacionService()
        {
            _lvDao = new ListaVerificacionOperacionalEaeDAO();
            _estacionDao = new SolicitudEstacionDAO();
            _inspeccionDao = new InspeccionDAO();
            _solicitudDao = new SolicitudAOCRDAO();
            _auditoriaDao = new AuditoriaDAO();
            _catalogService = new ListaVerificacionCatalogService();
            _resolverIdentidad = id => new InspectorIdentityService().ObtenerIdentidadInspector(id, null, null);
        }

        public ListaVerificacionService(
            ListaVerificacionOperacionalEaeDAO lvDao,
            SolicitudEstacionDAO estacionDao,
            InspeccionDAO inspeccionDao,
            SolicitudAOCRDAO solicitudDao,
            AuditoriaDAO auditoriaDao = null,
            ListaVerificacionCatalogService catalogService = null,
            Func<int, InspectorIdentityInfo> resolverIdentidad = null)
        {
            _lvDao = lvDao ?? new ListaVerificacionOperacionalEaeDAO();
            _estacionDao = estacionDao ?? new SolicitudEstacionDAO();
            _inspeccionDao = inspeccionDao ?? new InspeccionDAO();
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _auditoriaDao = auditoriaDao ?? new AuditoriaDAO();
            _catalogService = catalogService ?? new ListaVerificacionCatalogService();
            _resolverIdentidad = resolverIdentidad ?? (id => new InspectorIdentityService().ObtenerIdentidadInspector(id, null, null));
        }

        #region Validaciones de Autorización Institucional (Segregación de Roles)

        public bool EsRolAutorizadoLectura(string rol)
        {
            if (string.IsNullOrWhiteSpace(rol)) return false;
            // RT y Financiero no tienen acceso
            if (string.Equals(rol, "RT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rol, "Financiero", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return AocrRolesInstitucionales.EsInspector(rol)
                || AocrRolesInstitucionales.EsCoordinador(rol)
                || AocrRolesInstitucionales.EsDircav(rol)
                || AocrRolesInstitucionales.EsDirdac(rol)
                || AocrRolesInstitucionales.EsAdministrador(rol);
        }

        public bool EsRolAutorizadoOperacion(string rol)
        {
            if (string.IsNullOrWhiteSpace(rol)) return false;

            // REGLA 7: El Administrador no puede crear, responder, finalizar ni firmar LV
            if (AocrRolesInstitucionales.EsAdministrador(rol)) return false;

            // DIRDAC no crea, responde ni firma LV
            if (AocrRolesInstitucionales.EsDirdac(rol)) return false;

            // COORDINADOR y DIRCAV solo consultan
            if (AocrRolesInstitucionales.EsCoordinador(rol) || AocrRolesInstitucionales.EsDircav(rol)) return false;

            // Solo el Inspector asignado
            return AocrRolesInstitucionales.EsInspector(rol);
        }

        #endregion

        private Inspeccion ValidarContexto(int? solicitudId, int inspeccionId, int? estacionId, int usuarioId, string rol)
        {
            var inspeccion = _inspeccionDao.ObtenerPorId(inspeccionId);
            if (inspeccion == null || (solicitudId.HasValue && solicitudId != inspeccion.CodigoSolicitud))
                throw new InvalidOperationException("La inspección no pertenece al trámite indicado.");
            var estaciones = _estacionDao.ListarPorSolicitud(inspeccion.CodigoSolicitud);
            var estacion = estaciones.FirstOrDefault(e => e.Id == estacionId && e.Activo);
            if ((estacionId.HasValue && (estacion == null || estacionId <= 0
                    || (estacion.InspeccionId.HasValue && estacion.InspeccionId != inspeccionId)))
                || (!estacionId.HasValue && estaciones.Any(e => e.Activo)))
                throw new InvalidOperationException("Seleccione una estación válida de esta inspección.");
            if (AocrRolesInstitucionales.EsInspector(rol))
            {
                var identidad = _resolverIdentidad(usuarioId);
                var asignado = inspeccion.CodigoInspector.HasValue && identidad.Ids.Contains(inspeccion.CodigoInspector.Value);
                asignado = asignado || identidad.Identificadores.Contains(inspeccion.InspectorPrincipalCedula ?? "")
                    || identidad.Identificadores.Contains(inspeccion.InspectorApoyoCedula ?? "");
                if (!asignado || (estacion?.InspectorId > 0 && !identidad.Ids.Contains(estacion.InspectorId.Value)))
                    throw new UnauthorizedAccessException("El inspector no está asignado a esta inspección o estación.");
            }
            return inspeccion;
        }

        #region Operaciones de Consulta e Inicio Idempotente por Estación

        /// <summary>
        /// Obtiene o inicia la Lista de Verificación independiente para una inspección y estación específica.
        /// La operación es idempotente: no duplica la cabecera si ya existe.
        /// </summary>
        public ListaVerificacionOperacionalEae ObtenerOIniciarListaParaEstacion(
            int solicitudId,
            int inspeccionId,
            int? estacionId,
            int usuarioId,
            string rol,
            string usuarioNombre)
        {
            if (!EsRolAutorizadoLectura(rol))
            {
                throw new UnauthorizedAccessException("Acceso denegado: rol no autorizado para consultar listas de verificación.");
            }

            ValidarContexto(solicitudId, inspeccionId, estacionId, usuarioId, rol);

            // 1. Buscar si ya existe una LV registrada para esta inspección y estación
            var lvExistente = _lvDao.ObtenerUltimaPorInspeccion(inspeccionId, estacionId);
            if (lvExistente != null)
            {
                AsegurarItemsDeserializados(lvExistente);
                return lvExistente;
            }

            // 2. Si no existe y el usuario tiene rol de Inspector, crear el borrador inicial de forma idempotente con el catálogo común
            if (EsRolAutorizadoOperacion(rol))
            {
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                var inspeccion = _inspeccionDao.ObtenerPorId(inspeccionId);

                // Obtener metadatos de la estación si aplica
                string codigoEstacion = string.Empty;
                string nombreEstacion = string.Empty;
                DateTime? fechaEstacion = null;

                if (estacionId.HasValue && estacionId.Value > 0)
                {
                    var estaciones = _estacionDao.ListarPorSolicitud(solicitudId);
                    var est = estaciones.FirstOrDefault(e => e.Id == estacionId.Value);
                    if (est != null)
                    {
                        codigoEstacion = est.EstacionCodigo;
                        nombreEstacion = est.EstacionNombre;
                        fechaEstacion = est.FechaInicio;
                    }
                }

                var catalogo = _catalogService.ObtenerCatalogoPreguntas();
                var nueva = new ListaVerificacionOperacionalEae
                {
                    CodigoInspeccion = inspeccionId,
                    SolicitudId = solicitudId,
                    EstacionId = estacionId,
                    EstacionCodigo = codigoEstacion,
                    EstacionNombre = nombreEstacion,
                    TipoLista = "EAE",
                    Version = 1,
                    Vigente = true,
                    EstadoLista = AocrEstadosListaVerificacion.Borrador,
                    NombreEae = solicitud?.NombreOperador ?? solicitud?.RazonSocial ?? string.Empty,
                    NumeroAocFechaValidez = solicitud?.NumeroAOC ?? string.Empty,
                    DireccionEstadoExplotador = solicitud?.Direccion ?? string.Empty,
                    DireccionEstadoReconocimiento = solicitud?.Pais ?? "ECUADOR",
                    TiposAeronaves = string.Empty,
                    TipoOperacion = solicitud?.TipoOperacion ?? "TRANSPORTE AÉREO",
                    FechaLista = fechaEstacion ?? inspeccion?.FechaProgramada ?? DateTime.Now,
                    InspectorResponsable = usuarioNombre ?? inspeccion?.InspectorPrincipalNombre ?? "Inspector Asignado",
                    CargoInspector = "Inspector de Operaciones / Aeronavegabilidad",
                    ResumenVerificacion = string.Empty,
                    ObservacionesGenerales = string.Empty,
                    ResultadoGeneral = "SATISFACTORIO",
                    ItemsJson = ListaVerificacionCatalogService.SerializarRespuestas(catalogo),
                    Items = catalogo,
                    Finalizado = false,
                    FirmadoTecnico = false
                };

                var guardada = _lvDao.GuardarBorrador(nueva, usuarioId);
                AsegurarItemsDeserializados(guardada);

                // Auditoría
                try
                {
                    _auditoriaDao.Registrar(new Auditoria
                    {
                        Entidad = "LISTA_VERIFICACION",
                        Accion = "CREAR_LV_ESTACION",
                        Usuario = usuarioNombre ?? "INSPECTOR",
                        Fecha = DateTime.Now,
                        DatosPrevios = AocrEstadosListaVerificacion.NoCreada,
                        DatosNuevos = $"LV #{guardada.CodigoListaVerificacion} Estacion: {codigoEstacion} v1"
                    });
                }
                catch { }

                return guardada;
            }

            return null;
        }

        #endregion

        #region Guardado, Finalización y Firma

        /// <summary>
        /// AC-08: Valida la completitud exhaustiva de la LV (cabecera e ítems obligatorios).
        /// Garantiza que no pueda ser guardada como completa, finalizada o firmada con ítems pendientes.
        /// </summary>
        public bool ValidarCompletitudParaFinalizar(ListaVerificacionOperacionalEae lista, out string mensaje)
        {
            var resultado = EvaluarCompletitud(lista);
            mensaje = resultado.EsValida ? string.Empty : resultado.Mensaje;
            return resultado.EsValida;
        }

        public ValidacionListaVerificacionResultado EvaluarCompletitud(ListaVerificacionOperacionalEae lista)
        {
            if (lista == null) return ValidadorListaVerificacion.Evaluar(null);
            // Nunca confiar en la cantidad enviada ni en EsNotaOrientacion del cliente.
            lista.Items = _catalogService.HidratarRespuestas(lista.ItemsJson, lista.Items);
            return lista.EvaluarCompletitud();
        }

        private void AsegurarItemsDeserializados(ListaVerificacionOperacionalEae lista)
        {
            if (lista == null) return;
            if (lista.Items != null && lista.Items.Count > 0) return;

            // Reutilizar catálogo común oficial para hidratar, garantizando que nunca quede en blanco
            lista.Items = _catalogService.HidratarRespuestas(lista.ItemsJson, lista.Items);
        }

        /// <summary>
        /// Guarda el borrador o avance de respuestas de la LV.
        /// Si la LV ya está firmada, arroja InvalidOperationException (inmutabilidad estricta).
        /// AC-08: Asigna LV_COMPLETA solo si la validación exhaustiva de completitud es exitosa; de lo contrario queda en LV_EN_PROCESO.
        /// </summary>
        public ListaVerificacionOperacionalEae GuardarRespuestas(ListaVerificacionOperacionalEae lista, int usuarioId, string rol)
        {
            if (lista == null) throw new ArgumentNullException(nameof(lista));

            if (!EsRolAutorizadoOperacion(rol))
            {
                throw new UnauthorizedAccessException("Acceso denegado: solo el Inspector asignado puede registrar respuestas en la Lista de Verificación.");
            }

            ValidarContexto(lista.SolicitudId, lista.CodigoInspeccion, lista.EstacionId, usuarioId, rol);

            // Comprobar estado previo para evitar sobreescritura de LV firmada
            var previa = _lvDao.ObtenerUltimaPorInspeccion(lista.CodigoInspeccion, lista.EstacionId);
            if (previa != null && (previa.FirmadoTecnico || previa.Finalizado || !previa.Vigente))
            {
                throw new InvalidOperationException("Conflicto (409): La lista de verificación ya se encuentra firmada oficialmente y es inmutable.");
            }

            string mensajeComp;
            lista.EstadoLista = ValidarCompletitudParaFinalizar(lista, out mensajeComp)
                ? AocrEstadosListaVerificacion.Completa
                : AocrEstadosListaVerificacion.EnProceso;
            lista.Vigente = true;
            lista.ItemsJson = ListaVerificacionCatalogService.SerializarRespuestas(lista.Items);

            return _lvDao.GuardarBorrador(lista, usuarioId);
        }

        /// <summary>
        /// Finaliza formalmente la LV antes de la firma digital.
        /// AC-08: Bloquea si existe algún ítem aplicable o campo de cabecera incompleto.
        /// </summary>
        public void FinalizarLista(int codigoLv, int usuarioId, string rol)
        {
            if (!EsRolAutorizadoOperacion(rol))
            {
                throw new UnauthorizedAccessException("Solo el Inspector asignado puede finalizar la Lista de Verificación.");
            }

            var lv = _lvDao.ObtenerPorId(codigoLv);
            if (lv == null) throw new KeyNotFoundException("Lista de verificación no encontrada.");
            ValidarContexto(lv.SolicitudId, lv.CodigoInspeccion, lv.EstacionId, usuarioId, rol);

            if (lv.FirmadoTecnico)
            {
                throw new InvalidOperationException("La lista de verificación ya está firmada.");
            }

            string mensaje;
            if (!ValidarCompletitudParaFinalizar(lv, out mensaje))
            {
                throw new InvalidOperationException(mensaje);
            }

            _lvDao.MarcarFinalizada(codigoLv, lv.RutaPdf, AocrEstadosListaVerificacion.Completa, usuarioId);
        }

        /// <summary>
        /// Firma digital o institucionalmente la LV correspondiente a la estación.
        /// Garantiza inmutabilidad y sella los metadatos de autoría y fecha.
        /// AC-08: Bloquea si la LV no está finalizada o contiene ítems incompletos.
        /// </summary>
        public void FirmarLista(
            int codigoLv,
            string usuarioFirma,
            string hashDocumento,
            string rutaDocumentoFirmado,
            int usuarioId,
            string rol)
        {
            if (!EsRolAutorizadoOperacion(rol))
            {
                throw new UnauthorizedAccessException("Acceso denegado: solo el Inspector asignado puede firmar la Lista de Verificación.");
            }

            var lv = _lvDao.ObtenerPorId(codigoLv);
            if (lv == null) throw new KeyNotFoundException("Lista de verificación no encontrada.");
            ValidarContexto(lv.SolicitudId, lv.CodigoInspeccion, lv.EstacionId, usuarioId, rol);

            if (lv.FirmadoTecnico)
            {
                throw new InvalidOperationException("La lista de verificación ya se encuentra firmada y es inmutable.");
            }

            if (!lv.Finalizado)
            {
                throw new InvalidOperationException("Debe finalizar la lista de verificación operacional EAE antes de firmarla.");
            }

            string mensaje;
            if (!ValidarCompletitudParaFinalizar(lv, out mensaje))
            {
                throw new InvalidOperationException(mensaje);
            }

            _lvDao.MarcarFirmada(
                codigoLv,
                rutaDocumentoFirmado,
                hashDocumento,
                usuarioFirma,
                DateTime.Now,
                AocrEstadosListaVerificacion.Firmada,
                usuarioId
            );

            // Auditoría
            try
            {
                _auditoriaDao.Registrar(new Auditoria
                {
                    Entidad = "LISTA_VERIFICACION",
                    Accion = "FIRMAR_LV_ESTACION",
                    Usuario = usuarioFirma ?? "INSPECTOR",
                    Fecha = DateTime.Now,
                    DatosPrevios = lv.EstadoLista,
                    DatosNuevos = $"{AocrEstadosListaVerificacion.Firmada} Hash:{hashDocumento}"
                });
            }
            catch { }
        }

        #endregion

        #region Regla Crucial: Bloqueo de Informe Técnico si hay LVs Incompletas

        /// <summary>
        /// Valida que todas las estaciones de la solicitud cuenten con su Lista de Verificación
        /// completamente respondida y firmada. Impide avanzar al Informe Técnico si falta alguna.
        /// </summary>
        public bool ValidarTodasLasListasFirmadasParaInforme(int solicitudId, int inspeccionId, out List<string> pendientes)
        {
            return _lvDao.TodasLasListasEstacionesFirmadas(solicitudId, inspeccionId, out pendientes);
        }

        #endregion
    }
}
