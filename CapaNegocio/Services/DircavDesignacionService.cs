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
        public DircavDesignacionResult AceptarDocumentacion(int solicitudId, int dircavUsuarioId, string dircavNombre, string rol)
        {
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

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 404, Mensaje = "Solicitud no encontrada." };
            }

            var estadoNorm = _estadoService.Normalizar(solicitud.Estado);

            // Validar estado de origen: debe ser PENDIENTE_DIRCAV
            if (!string.Equals(estadoNorm, AocrEstadosProceso.PendienteDircav, StringComparison.OrdinalIgnoreCase))
            {
                // Si ya fue aceptada previamente -> 409 Conflict
                if (string.Equals(estadoNorm, AocrEstadosProceso.DocumentacionAceptadaDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoNorm, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoNorm, AocrEstadosProceso.DesignacionPendienteFirmaDircav, StringComparison.OrdinalIgnoreCase))
                {
                    return new DircavDesignacionResult
                    {
                        Exitoso = false,
                        HttpStatusCode = 409,
                        Mensaje = "Conflicto: La documentación de la solicitud ya fue aceptada previamente por DIRCAV."
                    };
                }

                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 409,
                    Mensaje = $"Conflicto: La solicitud se encuentra en estado '{solicitud.Estado}' y no puede ser aceptada directamente por DIRCAV."
                };
            }

            // Validar integridad documental: la solicitud no puede tener documentos con observaciones abiertas
            if (string.Equals(solicitud.EstadoDocumental, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitud.Estado, "OBSERVADA", StringComparison.OrdinalIgnoreCase))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 400,
                    Mensaje = "No se puede aceptar el expediente: existen documentos observados pendientes de resolución."
                };
            }

            // Transición a DOCUMENTACION_ACEPTADA_DIRCAV / PENDIENTE_DESIGNACION_DIRCAV
            solicitud.Estado = AocrEstadosProceso.DocumentacionAceptadaDircav;
            solicitud.EstadoDocumental = "ACEPTADO_DIRCAV";
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            // Registrar auditoría e historial institucional
            try
            {
                _auditoriaDao.Registrar(new Auditoria
                {
                    Entidad = "DIRCAV",
                    Accion = "ACEPTAR_DOCUMENTACION",
                    Usuario = dircavNombre ?? "DIRCAV",
                    Fecha = DateTime.Now,
                    DatosPrevios = AocrEstadosProceso.PendienteDircav,
                    DatosNuevos = AocrEstadosProceso.DocumentacionAceptadaDircav
                });
            }
            catch
            {
                // Tolerante en entornos sin tabla de auditoría
            }

            return new DircavDesignacionResult
            {
                Exitoso = true,
                HttpStatusCode = 200,
                NuevoEstado = AocrEstadosProceso.DocumentacionAceptadaDircav,
                Mensaje = "Documentación técnica aceptada formalmente por DIRCAV. Se habilita la designación del Inspector."
            };
        }

        /// <summary>
        /// 2. Devuelve motivadamente el expediente al Coordinador.
        /// </summary>
        public DircavDesignacionResult DevolverAlCoordinador(int solicitudId, int dircavUsuarioId, string dircavNombre, string motivo, string rol)
        {
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
            try
            {
                _auditoriaDao.Registrar(new Auditoria
                {
                    Entidad = "DIRCAV",
                    Accion = "DEVOLVER_COORDINADOR",
                    Usuario = dircavNombre ?? "DIRCAV",
                    Fecha = DateTime.Now,
                    DatosPrevios = AocrEstadosProceso.PendienteDircav,
                    DatosNuevos = AocrEstadosProceso.DevueltoCoordinador
                });
            }
            catch { }

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

            if (request.SolicitudId <= 0)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 400, Mensaje = "ID de solicitud inválido." };
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
            var cedulaApoyo = (request.InspectorApoyoCedula ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(cedulaApoyo))
            {
                if (string.Equals(cedulaPrincipal, cedulaApoyo, StringComparison.OrdinalIgnoreCase))
                {
                    return new DircavDesignacionResult
                    {
                        Exitoso = false,
                        HttpStatusCode = 400,
                        Mensaje = "El inspector de apoyo no puede ser la misma persona que el inspector principal."
                    };
                }

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

            var solicitud = _solicitudDao.ObtenerPorId(request.SolicitudId);
            if (solicitud == null)
            {
                return new DircavDesignacionResult { Exitoso = false, HttpStatusCode = 404, Mensaje = "Solicitud no encontrada." };
            }

            var estadoNorm = _estadoService.Normalizar(solicitud.Estado);

            // Validar estados habilitados para designar o reasignar
            var permiteDesignacion = string.Equals(estadoNorm, AocrEstadosProceso.DocumentacionAceptadaDircav, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(estadoNorm, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(estadoNorm, AocrEstadosProceso.DesignacionPendienteFirmaDircav, StringComparison.OrdinalIgnoreCase);

            if (!permiteDesignacion)
            {
                return new DircavDesignacionResult
                {
                    Exitoso = false,
                    HttpStatusCode = 409,
                    Mensaje = $"Conflicto: No se puede designar el inspector en el estado actual '{solicitud.Estado}'. Debe estar en Aceptación Documental DIRCAV."
                };
            }

            // Comprobar si ya existe una designación vigente idéntica para evitar duplicaciones innecesarias
            var designacionVigente = _designacionDao.ObtenerDesignacionVigente(request.SolicitudId, request.EstacionId);
            if (designacionVigente != null 
                && string.Equals(designacionVigente.InspectorCedula, cedulaPrincipal, StringComparison.OrdinalIgnoreCase)
                && string.Equals(designacionVigente.InspectorApoyoCedula ?? string.Empty, cedulaApoyo, StringComparison.OrdinalIgnoreCase))
            {
                return new DircavDesignacionResult
                {
                    Exitoso = true,
                    HttpStatusCode = 200,
                    DesignacionId = designacionVigente.Id,
                    Version = designacionVigente.Version,
                    NuevoEstado = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                    Mensaje = "El inspector ya se encuentra asignado a este expediente. Estado de designación conservado."
                };
            }

            // Si es reasignación de una persona distinta, se exige motivo
            if (designacionVigente != null && !string.Equals(designacionVigente.InspectorCedula, cedulaPrincipal, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.Motivo))
                {
                    return new DircavDesignacionResult
                    {
                        Exitoso = false,
                        HttpStatusCode = 400,
                        Mensaje = "Para reasignar el inspector a una persona diferente debe especificar un motivo institucional."
                    };
                }
            }

            var inspectorId = inspectorPrincipal.UsuarioId ?? inspectorPrincipal.TecnicoId ?? 0;
            var cedulaInspectorFinal = inspectorPrincipal.Cedula ?? inspectorPrincipal.Identificacion ?? inspectorPrincipal.UsuarioLogin;
            var cedulaApoyoFinal = inspectorApoyo != null ? (inspectorApoyo.Cedula ?? inspectorApoyo.Identificacion ?? inspectorApoyo.UsuarioLogin) : null;

            // 1. Registrar la designación en aocr_tbdesignacion_inspector (con inactivación de la anterior si existe)
            var nuevaDesignacion = _designacionDao.RegistrarDesignacion(
                solicitudId: request.SolicitudId,
                inspeccionId: null,
                estacionId: request.EstacionId,
                inspectorId: inspectorId,
                inspectorCedula: cedulaInspectorFinal,
                inspectorNombre: inspectorPrincipal.NombreCompleto,
                inspectorApoyoCedula: cedulaApoyoFinal,
                inspectorApoyoNombre: inspectorApoyo?.NombreCompleto,
                dircavUsuarioId: request.DircavUsuarioId,
                dircavUsuarioNombre: request.DircavUsuarioNombre ?? "DIRCAV",
                motivo: request.Motivo,
                estado: AocrEstadosProceso.DesignacionPendienteFirmaDircav
            );

            // 2. Actualizar las estaciones solicitadas de AC-02
            try
            {
                var estaciones = _estacionDao.ListarPorSolicitud(request.SolicitudId);
                if (estaciones != null && estaciones.Any())
                {
                    foreach (var est in estaciones)
                    {
                        if (!request.EstacionId.HasValue || est.Id == request.EstacionId.Value)
                        {
                            est.InspectorId = inspectorId;
                            est.InspectorNombre = inspectorPrincipal.NombreCompleto;
                            est.Estado = "DESIGNADO";
                            est.ActualizadoEn = DateTime.Now;
                            est.ActualizadoPor = request.DircavUsuarioId;
                        }
                    }
                    _estacionDao.GuardarEstaciones(request.SolicitudId, estaciones, request.DircavUsuarioId);
                }
            }
            catch
            {
                // Si la tabla de estaciones no está presente o falla en testing
            }

            // 3. Actualizar la solicitud principal
            solicitud.TecnicoResponsableId = inspectorId;
            solicitud.TecnicoResponsableCedula = cedulaInspectorFinal;
            solicitud.TecnicoResponsableNombre = inspectorPrincipal.NombreCompleto;
            if (inspectorApoyo != null)
            {
                solicitud.InspectorApoyoCedula = cedulaApoyoFinal;
                solicitud.InspectorApoyoNombre = inspectorApoyo.NombreCompleto;
            }
            solicitud.Estado = AocrEstadosProceso.DesignacionPendienteFirmaDircav;
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            // 4. Auditoría
            try
            {
                _auditoriaDao.Registrar(new Auditoria
                {
                    Entidad = "DIRCAV",
                    Accion = "DESIGNAR_INSPECTOR",
                    Usuario = request.DircavUsuarioNombre ?? "DIRCAV",
                    Fecha = DateTime.Now,
                    DatosPrevios = estadoNorm,
                    DatosNuevos = $"Designado {inspectorPrincipal.NombreCompleto} (v{nuevaDesignacion.Version})"
                });
            }
            catch { }

            // IMPORTANTE (Regla AC-05): No notificar como definitiva antes de la firma de DIRCAV (AC-06).

            return new DircavDesignacionResult
            {
                Exitoso = true,
                HttpStatusCode = 200,
                DesignacionId = nuevaDesignacion.Id,
                Version = nuevaDesignacion.Version,
                NuevoEstado = AocrEstadosProceso.DesignacionPendienteFirmaDircav,
                Mensaje = $"Inspector '{inspectorPrincipal.NombreCompleto}' designado formalmente (Versión {nuevaDesignacion.Version}). Proceda a la firma digital del oficio de designación."
            };
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
