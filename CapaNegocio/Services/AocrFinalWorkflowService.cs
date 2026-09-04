using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Interfaces;
using CapaModelo;
using CapaModelo.Common;
using CapaNegocio.Interfaces;

namespace CapaNegocio.Services
{
    public sealed class AocrFinalWorkflowDecision
    {
        public bool EsValida { get; set; }
        public string EstadoDestino { get; set; }
        public string ObservacionEstado { get; set; }
        public string MensajeValidacion { get; set; }
    }

    public class AocrFinalWorkflowValidationResult
    {
        public bool PuedeContinuar { get; set; }
        public string ClaveTempData { get; set; }
        public string Mensaje { get; set; }
    }

    public sealed class AocrFinalWorkflowLegalizacionPlan : AocrFinalWorkflowValidationResult
    {
        public AocrFinalWorkflowDecision Decision { get; set; }
        public string EventoNotificacion { get; set; }
        public string ObservacionNotificacion { get; set; }
    }

    public sealed class AocrFinalWorkflowEmisionPlan : AocrFinalWorkflowValidationResult
    {
        public AocrFinalWorkflowDecision Decision { get; set; }
        public string EventoNotificacion { get; set; }
        public string ObservacionNotificacion { get; set; }
    }

    public sealed class AocrFinalWorkflowElaboracionPlan : AocrFinalWorkflowValidationResult
    {
        public AocrFinalWorkflowDecision Decision { get; set; }
    }

    public sealed class AocrFinalWorkflowRevisionPlan : AocrFinalWorkflowValidationResult
    {
        public AocrFinalWorkflowDecision Decision { get; set; }
    }

    public class AocrFinalWorkflowService : IAocrFinalWorkflowService
    {
        private readonly SolicitudAocrCorreoService _solicitudAocrCorreoService;
        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBL;
        private readonly InspeccionInformeDAO _inspeccionInformeDao;
        private readonly HallazgoDAO _hallazgoDao;
        private readonly IAocrFinalWorkflowRepository _workflowRepository;

        public const string PermisoRemitirDirdac = "DIRCAV_REMITIR_DIRDAC";
        public const string PermisoBandejaDirdac = "DIRDAC_VER_BANDEJA";
        public const string PermisoDevolverDircav = "DIRDAC_DEVOLVER_DIRCAV";
        public const string PermisoFirmarAocr = "DIRDAC_FIRMAR_AOCR";

        public AocrFinalWorkflowService()
        {
            _solicitudAocrCorreoService = new SolicitudAocrCorreoService();
            _solicitudAocrInfraBL = new SolicitudAocrInfraBL();
            _inspeccionInformeDao = new InspeccionInformeDAO();
            _hallazgoDao = new HallazgoDAO();
            _workflowRepository = new AocrFinalWorkflowDAO();
        }

        public AocrFinalWorkflowService(IAocrFinalWorkflowRepository workflowRepository)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException("workflowRepository");
        }

        public AocrWorkflowResult RemitirAocrDirdac(RemitirAocrDirdacRequest request)
        {
            var error = ValidarActor(request != null ? request.Actor : null, AocrRolesInstitucionales.EsDircav, "DIRCAV", PermisoRemitirDirdac);
            if (error != null) return error;
            if (request == null || request.SolicitudId <= 0 || request.DocumentoId <= 0 || request.VersionEsperada <= 0 || request.VersionAocrEsperada <= 0)
                return AocrWorkflowResult.Error(400, "REQUEST_INVALIDO", "Solicitud, documento y versiones esperadas son obligatorios.");
            return EjecutarSeguro(() => _workflowRepository.RemitirAocrDirdac(request));
        }

        public BandejaAocrDirdacViewModel ObtenerBandejaDirdac()
        {
            return new BandejaAocrDirdacViewModel { Expedientes = _workflowRepository.ListarBandejaDirdac() };
        }

        public DetalleAocrDirdacViewModel ObtenerDetalleDirdac(int solicitudId)
        {
            return solicitudId <= 0 ? null : _workflowRepository.ObtenerDetalleDirdac(solicitudId);
        }

        public BandejaAocrDirdacItemViewModel ObtenerContextoRemisionDircav(int solicitudId)
        {
            return solicitudId <= 0 ? null : _workflowRepository.ObtenerContextoRemisionDircav(solicitudId);
        }

        public AocrWorkflowResult DevolverAocrDircav(DevolverAocrDircavRequest request)
        {
            var error = ValidarActor(request != null ? request.Actor : null, AocrRolesInstitucionales.EsDirdac, "DIRDAC", PermisoDevolverDircav);
            if (error != null) return error;
            if (request == null || request.SolicitudId <= 0 || request.VersionEsperada <= 0)
                return AocrWorkflowResult.Error(400, "REQUEST_INVALIDO", "Solicitud y versión esperada son obligatorias.");
            var observacion = (request.Observacion ?? string.Empty).Trim();
            if (observacion.Length < 10 || observacion.Length > 2000 || observacion.Any(char.IsControl))
                return AocrWorkflowResult.Error(400, "OBSERVACION_INVALIDA", "La observación debe contener entre 10 y 2000 caracteres válidos.");
            request.Observacion = observacion;
            return EjecutarSeguro(() => _workflowRepository.DevolverAocrDircav(request));
        }

        public AocrWorkflowResult FirmarLegalizarAocr(FirmarLegalizarAocrRequest request)
        {
            var error = ValidarActor(request != null ? request.Actor : null, AocrRolesInstitucionales.EsDirdac, "DIRDAC", PermisoFirmarAocr);
            if (error != null) return error;
            if (request == null || request.SolicitudId <= 0 || request.DocumentoId <= 0 || request.VersionEsperada <= 0 || request.VersionAocrEsperada <= 0)
                return AocrWorkflowResult.Error(400, "REQUEST_INVALIDO", "Solicitud, documento y versiones esperadas son obligatorios.");
            if (string.IsNullOrWhiteSpace(request.RutaPdfFirmado) || request.TamanioPdfFirmado <= 4 || !EsSha256(request.HashPdfFirmado))
                return AocrWorkflowResult.Error(400, "FIRMA_INVALIDA", "La evidencia PDF firmada y su hash SHA-256 son obligatorios.");
            return EjecutarSeguro(() => _workflowRepository.FirmarLegalizarAocr(request));
        }

        public AocrWorkflowResult EvaluarFirmasCompletas(int solicitudId, long versionEsperada, AocrWorkflowActor actor)
        {
            var error = ValidarActor(actor, AocrRolesInstitucionales.EsDirdac, "DIRDAC", PermisoFirmarAocr);
            if (error != null) return error;
            if (solicitudId <= 0 || versionEsperada <= 0) return AocrWorkflowResult.Error(400, "REQUEST_INVALIDO", "Solicitud y versión son obligatorias.");
            return EjecutarSeguro(() => _workflowRepository.EvaluarFirmasCompletas(solicitudId, versionEsperada, actor));
        }

        private static AocrWorkflowResult ValidarActor(AocrWorkflowActor actor, Func<string, bool> rolValido, string rolEsperado, string permiso)
        {
            if (actor == null || actor.UsuarioId <= 0 || string.IsNullOrWhiteSpace(actor.UsuarioNombre))
                return AocrWorkflowResult.Error(401, "NO_AUTENTICADO", "No existe una identidad autenticada válida.");
            if (!rolValido(actor.RolActivo) || AocrRolesInstitucionales.EsAdministrador(actor.RolActivo))
                return AocrWorkflowResult.Error(403, "ROL_NO_AUTORIZADO", "La operación es exclusiva del rol activo " + rolEsperado + ".");
            if (!actor.TienePermiso)
                return AocrWorkflowResult.Error(403, "PERMISO_REQUERIDO", "Falta el permiso granular " + permiso + ".");
            return null;
        }

        private static AocrWorkflowResult EjecutarSeguro(Func<AocrWorkflowResult> operacion)
        {
            try { return operacion(); }
            catch (UnauthorizedAccessException ex) { return AocrWorkflowResult.Error(403, "ACCESO_DENEGADO", ex.Message); }
            catch (InvalidOperationException ex) { return AocrWorkflowResult.Error(409, "CONFLICTO", ex.Message); }
            catch (Exception) { return AocrWorkflowResult.Error(500, "ERROR_INTERNO", "No fue posible completar la operación; la transacción fue revertida."); }
        }

        private static bool EsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            return value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        }

        public AocrFinalWorkflowValidationResult ValidarInspeccionSatisfactoriaParaAocr(int codigoSolicitud)
        {
            var inspecciones = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();

            if (inspecciones.Count == 0)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede avanzar porque la solicitud no tiene inspecciones registradas."
                };
            }

            Inspeccion inspeccionSatisfactoria = null;
            InspeccionInformeTecnico informeSatisfactorio = null;

            foreach (var inspeccion in inspecciones
                .Where(i => i != null)
                .OrderByDescending(i => i.CodigoInspeccion))
            {
                if (!EsInspeccionSatisfactoria(inspeccion))
                {
                    continue;
                }

                var informeCandidato = _inspeccionInformeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion);
                if (informeCandidato == null)
                {
                    continue;
                }

                if (!informeCandidato.Finalizado || !informeCandidato.FirmadoInspector || !InformeCompletaFaseTecnicaAocr(informeCandidato))
                {
                    continue;
                }

                if (!InformeResultadoSatisfactorio(informeCandidato.Resultado))
                {
                    continue;
                }

                inspeccionSatisfactoria = inspeccion;
                informeSatisfactorio = informeCandidato;
                break;
            }

            if (inspeccionSatisfactoria == null)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede avanzar a AOCR final sin una inspección satisfactoria con Informe Técnico aprobado y resultado satisfactorio."
                };
            }

            foreach (var inspeccion in inspecciones.Where(i => i != null && i.CodigoInspeccion > 0))
            {
                var hallazgos = _hallazgoDao.ObtenerPorInspeccion(inspeccion.CodigoInspeccion) ?? new List<Hallazgo>();
                var tieneNcAbiertas = hallazgos.Any(h =>
                    h != null &&
                    !string.Equals((h.Estado ?? string.Empty).Trim(), "CERRADO", StringComparison.OrdinalIgnoreCase));

                if (tieneNcAbiertas)
                {
                    return new AocrFinalWorkflowValidationResult
                    {
                        PuedeContinuar = false,
                        ClaveTempData = "Error",
                        Mensaje = "No se puede avanzar porque existen no conformidades abiertas en la inspección #" + inspeccion.CodigoInspeccion + "."
                    };
                }
            }

            var informe = informeSatisfactorio ?? _inspeccionInformeDao.ObtenerUltimoPorInspeccion(inspeccionSatisfactoria.CodigoInspeccion);
            if (informe == null)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede avanzar porque la inspección satisfactoria no tiene informe técnico registrado."
                };
            }

            if (!informe.Finalizado)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede avanzar porque el informe técnico aún no está finalizado."
                };
            }

            if (!informe.FirmadoInspector)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede avanzar porque el informe técnico aún no cuenta con firma del inspector."
                };
            }

            if (!InformeCompletaFaseTecnicaAocr(informe))
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede avanzar porque el informe tecnico todavia no completa la firma final del flujo tecnico AOCR."
                };
            }

            if (!InformeResultadoSatisfactorio(informe.Resultado))
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede avanzar porque el Informe Técnico aprobado de la inspección no tiene resultado satisfactorio."
                };
            }

            return new AocrFinalWorkflowValidationResult
            {
                PuedeContinuar = true
            };
        }

        public AocrFinalWorkflowValidationResult ValidarEnvioRevisionInstitucional(bool tieneAocrGenerada, string estadoActual)
        {
            if (!tieneAocrGenerada)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "Debe generar primero el documento AOCR antes de enviarlo a revisión."
                };
            }

            var estadoNormalizado = EstadoSolicitud.Normalizar(estadoActual);
            if (string.Equals(estadoNormalizado, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase))
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Exito",
                    Mensaje = "La AOCR ya fue enviada a DIRDAC y permanece pendiente de revisión institucional."
                };
            }

            if (!string.Equals(estadoNormalizado, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoNormalizado, EstadoSolicitud.Aprobada, StringComparison.OrdinalIgnoreCase))
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "La AOCR solo puede enviarse a DIRDAC cuando el documento se encuentra en elaboración y listo para revisión."
                };
            }

            return new AocrFinalWorkflowValidationResult
            {
                PuedeContinuar = true
            };
        }

        public AocrFinalWorkflowValidationResult ValidarLegalizacion(bool tieneAocrGenerada)
        {
            if (!tieneAocrGenerada)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede legalizar sin documento AOCR generado en el expediente."
                };
            }

            return new AocrFinalWorkflowValidationResult
            {
                PuedeContinuar = true
            };
        }

        public AocrFinalWorkflowValidationResult ValidarEmision(bool tieneAocrGenerada)
        {
            if (!tieneAocrGenerada)
            {
                return new AocrFinalWorkflowValidationResult
                {
                    PuedeContinuar = false,
                    ClaveTempData = "Error",
                    Mensaje = "No se puede emitir AOCR sin documento AOCR generado y vigente."
                };
            }

            return new AocrFinalWorkflowValidationResult
            {
                PuedeContinuar = true
            };
        }

        public AocrFinalWorkflowLegalizacionPlan PrepararLegalizacion(bool tieneAocrGenerada, string observacionLegal)
        {
            var validacion = ValidarLegalizacion(tieneAocrGenerada);
            if (!validacion.PuedeContinuar)
            {
                return new AocrFinalWorkflowLegalizacionPlan
                {
                    PuedeContinuar = false,
                    ClaveTempData = validacion.ClaveTempData,
                    Mensaje = validacion.Mensaje
                };
            }

            return new AocrFinalWorkflowLegalizacionPlan
            {
                PuedeContinuar = true,
                Decision = CrearDecisionLegalizacion(observacionLegal),
                EventoNotificacion = "AOCR_LEGALIZADO",
                ObservacionNotificacion = observacionLegal
            };
        }

        public AocrFinalWorkflowEmisionPlan PrepararEmision(bool tieneAocrGenerada, string observacion)
        {
            var validacion = ValidarEmision(tieneAocrGenerada);
            if (!validacion.PuedeContinuar)
            {
                return new AocrFinalWorkflowEmisionPlan
                {
                    PuedeContinuar = false,
                    ClaveTempData = validacion.ClaveTempData,
                    Mensaje = validacion.Mensaje
                };
            }

            return new AocrFinalWorkflowEmisionPlan
            {
                PuedeContinuar = true,
                Decision = CrearDecisionEmision(observacion),
                EventoNotificacion = "AOCR_EMITIDO_RECIBIDO",
                ObservacionNotificacion = observacion
            };
        }

        public AocrFinalWorkflowElaboracionPlan PrepararElaboracion(int codigoSolicitud, string observacion)
        {
            var validacion = ValidarInspeccionSatisfactoriaParaAocr(codigoSolicitud);
            if (!validacion.PuedeContinuar)
            {
                return new AocrFinalWorkflowElaboracionPlan
                {
                    PuedeContinuar = false,
                    ClaveTempData = validacion.ClaveTempData,
                    Mensaje = validacion.Mensaje
                };
            }

            return new AocrFinalWorkflowElaboracionPlan
            {
                PuedeContinuar = true,
                Decision = CrearDecisionElaboracion(observacion)
            };
        }

        public AocrFinalWorkflowRevisionPlan PrepararEnvioRevisionInstitucional(bool tieneAocrGenerada, string estadoActual, string observacion)
        {
            var validacion = ValidarEnvioRevisionInstitucional(tieneAocrGenerada, estadoActual);
            if (!validacion.PuedeContinuar)
            {
                return new AocrFinalWorkflowRevisionPlan
                {
                    PuedeContinuar = false,
                    ClaveTempData = validacion.ClaveTempData,
                    Mensaje = validacion.Mensaje
                };
            }

            return new AocrFinalWorkflowRevisionPlan
            {
                PuedeContinuar = true,
                Decision = CrearDecisionEnvioRevisionInstitucional(observacion)
            };
        }

        public AocrFinalWorkflowDecision CrearDecisionEnvioRevisionInstitucional(string observacion)
        {
            var observacionLimpia = (observacion ?? string.Empty).Trim();
            return new AocrFinalWorkflowDecision
            {
                EsValida = true,
                EstadoDestino = EstadoSolicitud.AOCR_EnRevision,
                ObservacionEstado = string.IsNullOrWhiteSpace(observacionLimpia)
                    ? "AOCR en revisión"
                    : observacionLimpia
            };
        }

        public AocrFinalWorkflowDecision CrearDecisionAprobacionJefatura()
        {
            return new AocrFinalWorkflowDecision
            {
                EsValida = true,
                EstadoDestino = EstadoSolicitud.AOCR_Validado,
                ObservacionEstado = "Validado por Dirección / Jefatura"
            };
        }

        public AocrFinalWorkflowDecision CrearDecisionObservacionJefatura(string observaciones)
        {
            var observacionLimpia = (observaciones ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(observacionLimpia))
            {
                return new AocrFinalWorkflowDecision
                {
                    EsValida = false,
                    MensajeValidacion = "Debe registrar una observación obligatoria para solicitar modificación al Inspector."
                };
            }

            return new AocrFinalWorkflowDecision
            {
                EsValida = true,
                EstadoDestino = EstadoSolicitud.Observada,
                ObservacionEstado = observacionLimpia
            };
        }

        public AocrFinalWorkflowDecision CrearDecisionLegalizacion(string observacionLegal)
        {
            var observacionLimpia = (observacionLegal ?? string.Empty).Trim();
            return new AocrFinalWorkflowDecision
            {
                EsValida = true,
                EstadoDestino = EstadoSolicitud.AOCR_Legalizado,
                ObservacionEstado = string.IsNullOrWhiteSpace(observacionLimpia)
                    ? "Legalizado por Coordinación Legal"
                    : observacionLimpia
            };
        }

        public AocrFinalWorkflowDecision CrearDecisionEmision(string observacion)
        {
            var observacionLimpia = (observacion ?? string.Empty).Trim();
            return new AocrFinalWorkflowDecision
            {
                EsValida = true,
                EstadoDestino = EstadoSolicitud.AOCR_EmitidoRecibido,
                ObservacionEstado = string.IsNullOrWhiteSpace(observacionLimpia)
                    ? "AOCR emitido/recibido"
                    : observacionLimpia
            };
        }

        public AocrFinalWorkflowDecision CrearDecisionElaboracion(string observacion)
        {
            var observacionLimpia = (observacion ?? string.Empty).Trim();
            return new AocrFinalWorkflowDecision
            {
                EsValida = true,
                EstadoDestino = EstadoSolicitud.AOCR_EnElaboracion,
                ObservacionEstado = string.IsNullOrWhiteSpace(observacionLimpia)
                    ? "AOCR en elaboración"
                    : observacionLimpia
            };
        }

        public ResultadoOperacion NotificarLegalizacion(SolicitudAOCR solicitudActualizada, AocrFinalWorkflowLegalizacionPlan legalizacionPlan)
        {
            if (legalizacionPlan == null)
            {
                return ResultadoOperacion.Error("No existe un plan de legalización para notificar.");
            }

            return _solicitudAocrCorreoService.NotificarEvento(
                solicitudActualizada,
                legalizacionPlan.EventoNotificacion,
                legalizacionPlan.ObservacionNotificacion);
        }

        public ResultadoOperacion NotificarEmision(SolicitudAOCR solicitudActualizada, AocrFinalWorkflowEmisionPlan emisionPlan)
        {
            if (emisionPlan == null)
            {
                return ResultadoOperacion.Error("No existe un plan de emisión para notificar.");
            }

            return _solicitudAocrCorreoService.NotificarEvento(
                solicitudActualizada,
                emisionPlan.EventoNotificacion,
                emisionPlan.ObservacionNotificacion);
        }

        public bool UsuarioPuedeTransicionarEstadoAocr(string estadoDestino, IEnumerable<string> rolesActuales, bool usuarioAutenticado)
        {
            var destino = EstadoSolicitud.Normalizar(estadoDestino);
            var roles = new HashSet<string>(rolesActuales ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            if (roles.Contains("Administrador"))
            {
                return true;
            }

            if (destino == EstadoSolicitud.Observada)
            {
                return TieneAlgunRol(
                    roles,
                    "Inspector",
                    "Coordinador",
                    "CoordinadorInspecciones",
                    "Coordinacion",
                    "JefaturaTecnica",
                    "DIRDAC",
                    "Direccion",
                    "CoordinacionLegal",
                    "CoordinadorLegal");
            }

            if (destino == EstadoSolicitud.AceptacionDocumental)
            {
                return TieneAlgunRol(roles, "Inspector", "Coordinador", "CoordinadorInspecciones", "Coordinacion");
            }

            if (destino == EstadoSolicitud.Subsanada)
            {
                return TieneAlgunRol(
                    roles,
                    "Solicitante",
                    "Operador",
                    "RepresentanteTecnico",
                    "Representante Técnico",
                    "RepresentanteLegal",
                    "RT");
            }

            if (destino == EstadoSolicitud.RequiereInspeccion || destino == EstadoSolicitud.GeneradoCondicionesLimitaciones)
            {
                return TieneAlgunRol(roles, "Inspector");
            }

            if (destino == EstadoSolicitud.EnRevisionCoordinadorFinal || destino == EstadoSolicitud.EnviadoDcav)
            {
                return TieneAlgunRol(roles, "Coordinador", "CoordinadorInspecciones", "Coordinacion");
            }

            if (destino == EstadoSolicitud.FirmadoDcav)
            {
                return TieneAlgunRol(roles, "DIRDAC", "Direccion", "JefaturaTecnica", "DirectorGeneral");
            }

            if (destino == EstadoSolicitud.PendienteAsignacionRT)
            {
                return TieneAlgunRol(roles, "Coordinador", "CoordinadorInspecciones", "Coordinacion");
            }

            if (destino == EstadoSolicitud.FirmadoCoordinador)
            {
                return TieneAlgunRol(roles, "Coordinador", "CoordinadorInspecciones", "Coordinacion");
            }

            if (destino == EstadoSolicitud.Finalizado)
            {
                return usuarioAutenticado;
            }

            if (destino == EstadoSolicitud.EnInspeccion || destino == EstadoSolicitud.AOCR_EnElaboracion)
            {
                return TieneAlgunRol(roles, "Inspector", "Coordinador", "CoordinadorInspecciones", "Coordinacion");
            }

            if (destino == EstadoSolicitud.AOCR_EnRevision || destino == EstadoSolicitud.AOCR_Validado)
            {
                return TieneAlgunRol(roles, "DIRDAC", "Direccion", "JefaturaTecnica", "DirectorGeneral");
            }

            if (destino == EstadoSolicitud.AOCR_Legalizado || destino == EstadoSolicitud.AOCR_EmitidoRecibido)
            {
                return TieneAlgunRol(roles, "CoordinacionLegal", "CoordinadorLegal", "DirectorGeneral");
            }

            return false;
        }

        private static bool EsInspeccionSatisfactoria(Inspeccion inspeccion)
        {
            if (inspeccion == null)
            {
                return false;
            }

            var estado = (inspeccion.Estado ?? string.Empty).Trim().ToUpperInvariant();
            var resultado = (inspeccion.Resultado ?? string.Empty).Trim().ToUpperInvariant();
            var resultadoEvaluacion = (inspeccion.ResultadoEvaluacion ?? string.Empty).Trim().ToUpperInvariant();

            return estado == "APROBADA"
                   || estado == "RESULTADO_SATISFACTORIO"
                   || estado == "CERRADA"
                   || resultado == "APROBADO"
                   || resultado == "SATISFACTORIO"
                   || resultadoEvaluacion == "RESULTADO_SATISFACTORIO"
                   || resultadoEvaluacion == "SATISFACTORIO";
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
                || estadoInforme == "FIRMADO_FINAL";
        }

        private static bool InformeResultadoSatisfactorio(string resultado)
        {
            return NormalizarResultadoInformeTecnico(resultado) == "SATISFACTORIO";
        }

        private static string NormalizarResultadoInformeTecnico(string resultado)
        {
            var normalized = NormalizarToken(resultado);
            switch (normalized)
            {
                case "NO_SATISFACTORIO":
                    return "INSATISFACTORIO";
                case "OBSERVACION_DOCUMENTAL":
                    return "OBSERVADO";
                case "NO_APLICABLE":
                case "N/A":
                    return "NO_APLICA";
                default:
                    return normalized;
            }
        }

        private static string NormalizarToken(string valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? string.Empty
                : valor.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
        }

        private static bool TieneAlgunRol(ISet<string> rolesActuales, params string[] rolesPermitidos)
        {
            if (rolesActuales == null || rolesActuales.Count == 0 || rolesPermitidos == null)
            {
                return false;
            }

            foreach (var rolPermitido in rolesPermitidos)
            {
                if (rolesActuales.Contains(rolPermitido))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
