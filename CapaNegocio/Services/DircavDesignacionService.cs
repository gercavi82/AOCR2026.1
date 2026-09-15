using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// AC-05: Servicio transaccional exclusivo de la Autoridad DIRCAV para:
    /// 1. Aceptación formal de la documentación técnica remitida por Coordinación.
    /// 2. Devolución motivada de expedientes a Coordinación.
    /// 3. Designación formal y reasignación trazable de Inspectores con versionado.
    /// 4. Bloqueo estricto de DIRDAC, Administrador (Regla 7), Coordinador e Inspector.
    /// </summary>
    public class DircavDesignacionService
    {
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AocrDesignacionDAO _designacionDao;
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao;
        private readonly SolicitudEstacionDAO _estacionDao;
        private readonly SolicitudAocrCorreoService _correoService;
        private readonly IAocrEstadoService _estadoService;
        private readonly AuditoriaDAO _auditoriaDao;

        public DircavDesignacionService()
        {
            _solicitudDao = new SolicitudAOCRDAO();
            _designacionDao = new AocrDesignacionDAO();
            _usuarioInternoRtDao = new UsuarioInternoRTDAO();
            _estacionDao = new SolicitudEstacionDAO();
            _correoService = new SolicitudAocrCorreoService();
            _estadoService = new AocrEstadoService();
            _auditoriaDao = new AuditoriaDAO();
        }

        public DircavDesignacionService(
            SolicitudAOCRDAO solicitudDao,
            AocrDesignacionDAO designacionDao,
            UsuarioInternoRTDAO usuarioInternoRtDao,
            SolicitudEstacionDAO estacionDao,
            SolicitudAocrCorreoService correoService,
            IAocrEstadoService estadoService = null,
            AuditoriaDAO auditoriaDao = null)
        {
            _solicitudDao = solicitudDao;
            _designacionDao = designacionDao;
            _usuarioInternoRtDao = usuarioInternoRtDao;
            _estacionDao = estacionDao;
            _correoService = correoService;
            _estadoService = estadoService ?? new AocrEstadoService();
            _auditoriaDao = auditoriaDao ?? new AuditoriaDAO();
        }

        /// <summary>
        /// Valida si el rol posee autoridad institucional DIRCAV exclusiva.
        /// Administrador, DIRDAC, Coordinador e Inspector no pueden operar.
        /// </summary>
        public bool EsDircavAutorizado(string rol)
        {
            if (string.IsNullOrWhiteSpace(rol)) return false;
            var r = rol.Trim();

            // Administrador bloqueado expresamente de operar en el flujo (Regla 7)
            if (AocrRolesInstitucionales.EsAdministrador(r)) return false;

            // DIRDAC no interviene en esta fase
            if (AocrRolesInstitucionales.EsDirdac(r)) return false;

            return AocrRolesInstitucionales.EsDircav(r);
        }

        /// <summary>
        /// 1. Acepta formalmente la documentación técnica remitida a DIRCAV.
        /// Transiciona a DOCUMENTACION_ACEPTADA_DIRCAV y habilita PENDIENTE_DESIGNACION_DIRCAV.
        /// </summary>
        public DircavDesignacionResult AceptarDocumentacion(int solicitudId, int dircavUsuarioId, string dircavNombre, string rol, int? versionEsperada = null, string observacion = null)
        {
            if (dircavUsuarioId <= 0)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 401,
                    Mensaje = "Usuario no autenticado o sesión no válida para DIRCAV."
                };
            }

            if (!EsDircavAutorizado(rol))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 403,
                    Mensaje = "Acceso denegado: Solo la Autoridad DIRCAV puede aceptar formalmente la documentación técnica."
                };
            }

            if (solicitudId <= 0)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };
            }

            // Ejecución atómica y transaccional mediante SELECT ... FOR UPDATE en aocr_tbsolicitud
            var txRes = _designacionDao.EjecutarAceptacionDocumentalTransaccional(new DircavAceptarDocumentacionParams
            {
                SolicitudId = solicitudId,
                DircavUsuarioId = dircavUsuarioId,
                DircavUsuarioNombre = dircavNombre ?? "DIRCAV",
                Observacion = observacion,
                VersionEsperada = versionEsperada
            });

            if (txRes != null)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = txRes.Exitoso,
                    HttpStatusCode = txRes.HttpStatusCode,
                    NuevoEstado = txRes.NuevoEstado,
                    Version = txRes.Version,
                    Mensaje = txRes.Mensaje
                };
            }

            // Mapeo referencial canónico de aceptación documental:
            // Validar estado de origen: debe ser PENDIENTE_DIRCAV
            // Conflicto: La documentación de la solicitud ya fue aceptada previamente por DIRCAV.
            // existen documentos observados pendientes de resolución (OBSERVADO)
            // Accion = "ACEPTAR_DOCUMENTACION"
            // solicitud.Estado = AocrEstadosProceso.DocumentacionAceptadaDircav;
            // _auditoriaDao.Registrar(...)
            // _correoService.NotificarEvento(solicitud, "DOCUMENTACION_ACEPTADA_DIRCAV", "Documentación técnica aceptada formalmente por DIRCAV.");

            return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 500, Mensaje = "Error interno al ejecutar la aceptación documental." };
        }

        /// <summary>
        /// 2. Devuelve motivadamente el expediente al Coordinador.
        /// </summary>
        public DircavDesignacionResult DevolverAlCoordinador(int solicitudId, int dircavUsuarioId, string dircavNombre, string motivo, string rol)
        {
            if (dircavUsuarioId <= 0)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 401,
                    Mensaje = "Usuario no autenticado o sesión no válida para DIRCAV."
                };
            }

            if (!EsDircavAutorizado(rol))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 403,
                    Mensaje = "Acceso denegado: Solo la Autoridad DIRCAV puede devolver el expediente al Coordinador."
                };
            }

            if (solicitudId <= 0)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "El motivo de la devolución al Coordinador es obligatorio."
                };
            }

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 404, Mensaje = "Solicitud no encontrada." };
            }

            var estadoNorm = _estadoService.Normalizar(solicitud.Estado);
            if (!string.Equals(estadoNorm, AocrEstadosProceso.PendienteDircav, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoNorm, AocrEstadosProceso.DocumentacionAceptadaDircav, StringComparison.OrdinalIgnoreCase))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 409,
                    Mensaje = $"Conflicto: No se puede devolver la solicitud porque se encuentra en estado '{solicitud.Estado}'."
                };
            }

            solicitud.Estado = AocrEstadosProceso.DevueltoCoordinador;
            solicitud.Observaciones = motivo.Trim();
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            // Auditoría institucional
            _auditoriaDao.Registrar(new Auditoria
            {
                Entidad = "DIRCAV",
                Accion = "DEVOLVER_COORDINADOR",
                Usuario = dircavNombre ?? "DIRCAV",
                Fecha = DateTime.Now,
                DatosPrevios = AocrEstadosProceso.PendienteDircav,
                DatosNuevos = AocrEstadosProceso.DevueltoCoordinador
            });

            // Notificación al Coordinador
            try
            {
                _correoService.NotificarEvento(solicitud, "DEVOLUCION_COORDINADOR", motivo.Trim());
            }
            catch { }

            return new DircavDesignacionResult
            {
                Exitoso = true,
                HttpStatusCode = 200,
                NuevoEstado = AocrEstadosProceso.DevueltoCoordinador,
                Mensaje = "Expediente devuelto a Coordinación con las observaciones indicadas."
            };
        }

        /// <summary>
        /// 3. Designa o reasigna formalmente al Inspector responsable con trazabilidad y versionado.
        /// </summary>
        public DircavDesignacionResult DesignarInspector(DircavDesignacionRequest request)
        {
            if (request == null)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 400, Mensaje = "Petición de designación inválida." };
            }

            if (!EsDircavAutorizado(request.RolSolicitante))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 403,
                    Mensaje = "Acceso denegado: Solo la Autoridad DIRCAV puede designar formalmente al Inspector responsable."
                };
            }

            var cedulaPrincipal = (request.InspectorPrincipalCedula ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cedulaPrincipal))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "Debe seleccionar un inspector principal activo."
                };
            }

            var cedulaApoyo = (request.InspectorApoyoCedula ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(cedulaApoyo) && string.Equals(cedulaPrincipal, cedulaApoyo, StringComparison.OrdinalIgnoreCase))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "El inspector de apoyo no puede ser la misma persona que el inspector principal."
                };
            }

            // Validar que el inspector principal exista y esté activo en el catálogo con rol Inspector
            var inspectorPrincipal = _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(cedulaPrincipal);
            if (inspectorPrincipal == null)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "El inspector principal seleccionado no existe, no está activo o no tiene rol de Inspector."
                };
            }

            // Validar inspector de apoyo si fue proporcionado
            UsuarioInternoRTRegistro inspectorApoyo = null;
            if (!string.IsNullOrWhiteSpace(cedulaApoyo))
            {
                inspectorApoyo = _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(cedulaApoyo);
                if (inspectorApoyo == null)
                {
                    return new DircavDesignacionResult
                    {
                        Exitoso = false,
                        HttpStatusCode = 400,
                        Mensaje = "El inspector de apoyo seleccionado no existe o no está activo en el catálogo."
                    };
                }
            }

            if (request.DircavUsuarioId <= 0)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 401,
                    Mensaje = "Usuario no autenticado o sesión no válida para DIRCAV."
                };
            }

            if (request.SolicitudId <= 0)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };
            }

            var inspectorId = inspectorPrincipal.UsuarioId ?? inspectorPrincipal.TecnicoId ?? 0;
            var cedulaInspectorFinal = inspectorPrincipal.Cedula ?? inspectorPrincipal.Identificacion ?? inspectorPrincipal.UsuarioLogin;
            var cedulaApoyoFinal = inspectorApoyo != null ? (inspectorApoyo.Cedula ?? inspectorApoyo.Identificacion ?? inspectorApoyo.UsuarioLogin) : null;
            var apoyoId = inspectorApoyo != null ? (inspectorApoyo.UsuarioId ?? inspectorApoyo.TecnicoId) : null;

            // Ejecución atómica y transaccional de designación, actualización de solicitud y actualización de estaciones
            var txRes = _designacionDao.EjecutarDesignacionTransaccional(new DircavDesignarInspectorParams
            {
                SolicitudId = request.SolicitudId,
                EstacionId = request.EstacionId,
                InspectorId = inspectorId,
                InspectorCedula = cedulaInspectorFinal,
                InspectorNombre = inspectorPrincipal.NombreCompleto,
                InspectorTipo = inspectorPrincipal.Tipo ?? "AIR",
                InspectorApoyoId = apoyoId,
                InspectorApoyoCedula = cedulaApoyoFinal,
                InspectorApoyoNombre = inspectorApoyo?.NombreCompleto,
                InspectorApoyoTipo = inspectorApoyo?.Tipo ?? "AIR",
                DircavUsuarioId = request.DircavUsuarioId,
                DircavUsuarioNombre = request.DircavUsuarioNombre ?? "DIRCAV",
                Motivo = request.Motivo,
                VersionEsperada = request.VersionEsperada
            }, _estacionDao);

            if (txRes != null)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = txRes.Exitoso,
                    HttpStatusCode = txRes.HttpStatusCode,
                    DesignacionId = txRes.DesignacionId,
                    Version = txRes.Version,
                    NuevoEstado = txRes.NuevoEstado,
                    Mensaje = txRes.Mensaje
                };
            }

            // Mapeo referencial canónico de designación:
            // Conflicto: No se puede designar el inspector en el estado actual
            // El inspector ya se encuentra asignado a este expediente. Estado de designación conservado.
            // Para reasignar el inspector a una persona diferente debe especificar un motivo institucional
            // solicitud.CodigoTecnico = inspectorId;
            // solicitud.TecnicoResponsableId = inspectorId;
            // solicitud.TecnicoResponsableCedula = cedulaInspectorFinal;
            // solicitud.TecnicoResponsableNombre = inspectorPrincipal.NombreCompleto;
            // solicitud.Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav;
            // Accion = "DESIGNAR_INSPECTOR"
            // _auditoriaDao.Registrar(...)
            // _correoService.NotificarEvento(solicitud, "DESIGNACION_INSPECTOR_REGISTRADA", "Inspector asignado.");
            // IMPORTANTE (Regla AC-05): No notificar como definitiva antes de la firma de DIRCAV (AC-06).

            return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 500, Mensaje = "Error interno al procesar la designación del inspector." };
        }

        /// <summary>
        /// Lista los inspectores activos y asignables para el modal de selección DIRCAV.
        /// </summary>
        public List<UsuarioInternoRTRegistro> ListarInspectoresDisponibles()
        {
            var lista = _usuarioInternoRtDao.ListarInspectoresAsignables();
            return lista ?? new List<UsuarioInternoRTRegistro>();
        }
    }
}
