using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public interface IAocrAuthorizationService
    {
        bool TieneAccesoModulo(string modulo, AocrAuthorizationContext usuario);
        AocrAuthorizationResult PuedeEjecutarAccion(string accion, AocrAuthorizationContext usuario, int? codigoSolicitud = null, int? codigoInspeccion = null, int? codigoOrden = null, int? codigoInforme = null, string modulo = null);
        bool PuedeRtEditarSolicitud(int codigoSolicitud, int codigoUsuario);
        bool PuedeFinancieroAprobarPago(int codigoOrden, int codigoUsuario);
        bool PuedeCoordinadorAsignarInspector(int codigoSolicitud, int codigoUsuario);
        bool PuedeInspectorRevisarDocumentos(int codigoSolicitud, int codigoUsuario);
        bool PuedeInspectorAbrirInspeccion(int codigoInspeccion, int codigoUsuario);
        bool PuedeInspectorAbrirLv(int codigoInspeccion, int codigoUsuario);
        bool PuedeInspectorGenerarInforme(int codigoInspeccion, int codigoUsuario);
        bool PuedeDirectorRevisarInforme(int codigoInforme, int codigoUsuario);
        bool PuedeAdministradorConfigurarCorreos(int codigoUsuario);
    }

    public sealed class AocrAuthorizationContext
    {
        public bool IsAuthenticated { get; set; }
        public int UserId { get; set; }
        public string CodigoUsuario { get; set; }
        public string UserName { get; set; }
        public string SelectedRole { get; set; }
        public IList<string> RawRoles { get; set; }
        public IList<string> Roles { get; set; }
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }

        public AocrAuthorizationContext()
        {
            RawRoles = new List<string>();
            Roles = new List<string>();
        }
    }

    public sealed class AocrAuthorizationResult
    {
        public bool Permitido { get; set; }
        public string Modulo { get; set; }
        public string Accion { get; set; }
        public string Motivo { get; set; }
        public bool RequiereSeleccionCompania { get; set; }
        public int? CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public int? CodigoOrden { get; set; }
        public int? CodigoInforme { get; set; }

        public static AocrAuthorizationResult Allowed(string modulo, string accion)
        {
            return new AocrAuthorizationResult
            {
                Permitido = true,
                Modulo = modulo ?? string.Empty,
                Accion = accion ?? string.Empty,
                Motivo = string.Empty
            };
        }

        public static AocrAuthorizationResult Denied(string modulo, string accion, string motivo, bool requiereSeleccionCompania = false)
        {
            return new AocrAuthorizationResult
            {
                Permitido = false,
                Modulo = modulo ?? string.Empty,
                Accion = accion ?? string.Empty,
                Motivo = string.IsNullOrWhiteSpace(motivo) ? "No tiene permisos para acceder a este módulo." : motivo.Trim(),
                RequiereSeleccionCompania = requiereSeleccionCompania
            };
        }
    }

    public class AocrAuthorizationService : IAocrAuthorizationService
    {
        private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

        private static readonly IDictionary<string, string[]> ModuleMatrix = new Dictionary<string, string[]>(Comparer)
        {
            { "OrdenRecaudacion", new[] { "Solicitante", "Administrador" } },
            { "SolicitudAOCR", new[] { "Solicitante", "Administrador" } },
            { "Financiero", new[] { "Financiero", "Administrador" } },
            { "Coordinador", new[] { "Coordinacion", "Administrador" } },
            { "Documento", new[] { "Solicitante", "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Tecnico", new[] { "Coordinacion", "Administrador" } },
            { "CoordinacionJefatura", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "RevisionDocumental", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "InformeTecnico", new[] { "InspectorTecnico", "DireccionJefaturaTecnica", "Administrador" } },
            { "CorreoInstitucional", new[] { "Administrador" } },
            { "Administrador", new[] { "Administrador" } }
        };

        private static readonly IDictionary<string, string[]> ActionMatrix = new Dictionary<string, string[]>(Comparer)
        {
            { "OrdenRecaudacion/Nueva", new[] { "Solicitante", "Administrador" } },
            { "OrdenRecaudacion/Generar", new[] { "Solicitante", "Administrador" } },
            { "OrdenRecaudacion/SubirComprobante", new[] { "Solicitante", "Administrador" } },
            { "OrdenRecaudacion/Descargar", new[] { "Solicitante", "Financiero", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Financiero/Index", new[] { "Financiero", "Administrador" } },
            { "Financiero/AprobarOrden", new[] { "Financiero", "Administrador" } },
            { "Financiero/AprobarPago", new[] { "Financiero", "Administrador" } },
            { "Financiero/RechazarOrden", new[] { "Financiero", "Administrador" } },
            { "Financiero/RechazarPago", new[] { "Financiero", "Administrador" } },
            { "Financiero/AprobarYEnviarAS400", new[] { "Financiero", "Administrador" } },
            { "SolicitudAOCR/AbrirFormularioRT", new[] { "Solicitante", "Administrador" } },
            { "SolicitudAOCR/EditarRT", new[] { "Solicitante", "Administrador" } },
            { "SolicitudAOCR/GuardarProgresoRT", new[] { "Solicitante", "Administrador" } },
            { "SolicitudAOCR/FinalizarRT", new[] { "Solicitante", "Administrador" } },
            { "SolicitudAOCR/Detalle", new[] { "Solicitante", "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/Generar", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/DescargarGenerada", new[] { "Solicitante", "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/Aprobar", new[] { "Coordinacion", "Administrador" } },
            { "SolicitudAOCR/AprobarJefatura", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            // Legalizar/Emitir: los roles legales (CoordinacionLegal/CoordinadorLegal) se normalizan a "Coordinacion";
            // el filtro [Authorize(Roles=...)] del controlador ya restringe a los roles legales raw.
            { "SolicitudAOCR/Legalizar", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/Emitir", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/DecisionInstitucional", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "Coordinador/AsignarInspector", new[] { "Coordinacion", "Administrador" } },
            { "Tecnico/Index", new[] { "Coordinacion", "Administrador" } },
            { "Tecnico/AsignarInspector", new[] { "Coordinacion", "Administrador" } },
            { "Documento/Lista", new[] { "Solicitante", "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Documento/Subir", new[] { "Solicitante", "Administrador" } },
            { "Documento/Descargar", new[] { "Solicitante", "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "CoordinacionJefatura/DashboardInspeccion", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "CoordinacionJefatura/ValidarAocr", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "CoordinacionJefatura/RevisionVerificacion", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "CoordinacionJefatura/DocumentoValidacionAocr", new[] { "Coordinacion", "DireccionJefaturaTecnica", "InspectorTecnico", "Administrador" } },
            { "CoordinacionJefatura/GenerarDocumentoValidacionAocr", new[] { "Coordinacion", "DireccionJefaturaTecnica", "InspectorTecnico", "Administrador" } },
            { "CoordinacionJefatura/FirmarAceptacionDocumental", new[] { "Coordinacion", "Administrador" } },
            { "RevisionDocumental/Index", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "RevisionDocumental/Revisar", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/Detalle", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Solicitante", "Administrador" } },
            { "Inspeccion/Index", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Solicitante", "Administrador" } },
            { "Inspeccion/Abrir", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/LV", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/ConfirmarRevisionDocumentalInspector", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/GuardarInformeTecnico", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/FinalizarInformeTecnico", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/FirmarInformeInspector", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/GuardarListaVerificacionOperacionalEae", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/FinalizarListaVerificacionOperacionalEae", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/FirmarListaVerificacionOperacionalEae", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/VerInforme", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Solicitante", "Administrador" } },
            { "Inspeccion/DescargarInforme", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Solicitante", "Administrador" } },
            { "Inspeccion/VerListaVerificacionOperacionalEae", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/DescargarListaVerificacionOperacionalEae", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/VerAdjuntoInformeTecnico", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/DescargarAdjuntoInformeTecnico", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/VerLvEaeOficial", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Solicitante", "Administrador" } },
            { "Inspeccion/DescargarLvEaeOficial", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Solicitante", "Administrador" } },
            { "Inspeccion/CambiarEstado", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/SubirInforme", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/SubirDocumentoSolicitante", new[] { "Solicitante", "Administrador" } },
            { "Inspeccion/RegistrarNoConforme", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/GuardarPosicionFirmaInformeTecnico", new[] { "InspectorTecnico", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/AprobarNcSubsanacionDocumental", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/SolicitarNueva", new[] { "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Documento/RevisarDocumentos", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/ModalInformeTecnico", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/RevisionDireccion", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/AprobarDecisionFinalDireccion", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/DevolverDecisionFinalDireccion", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/FirmarDireccion", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/FirmarInformeDirdac", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/RechazarInformeDirdac", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "InformeTecnico/Inspector", new[] { "InspectorTecnico", "Administrador" } },
            { "InformeTecnico/RevisionDireccion", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "CorreoInstitucional/Index", new[] { "Administrador" } }
        };

        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly SolicitudAocrService _solicitudRtService = new SolicitudAocrService();
        private readonly OrdenRecaudacionDAO _ordenDao = new OrdenRecaudacionDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly RevisionDocumentalService _revisionDocumentalService;
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao = new UsuarioInternoRTDAO();
        private readonly InspectorIdentityService _inspectorIdentityService = new InspectorIdentityService();

        public AocrAuthorizationService(CapaDatos.Interfaces.IUsuarioAS400DAO usuarioAs400Dao = null, CapaDatos.Interfaces.IEmpresaAS400DAO empresaAs400Dao = null)
        {
            _revisionDocumentalService = new RevisionDocumentalService(usuarioAs400Dao, empresaAs400Dao);
        }

        public bool TieneAccesoModulo(string modulo, AocrAuthorizationContext usuario)
        {
            if (!EsContextoAutenticado(usuario))
            {
                return false;
            }

            var rolesNormalizados = NormalizarRoles(usuario);
            string[] rolesPermitidos;
            if (!ModuleMatrix.TryGetValue(NormalizarClave(modulo), out rolesPermitidos))
            {
                return rolesNormalizados.Contains("Administrador", Comparer);
            }

            return rolesPermitidos.Any(rol => rolesNormalizados.Contains(rol, Comparer));
        }

        public AocrAuthorizationResult PuedeEjecutarAccion(string accion, AocrAuthorizationContext usuario, int? codigoSolicitud = null, int? codigoInspeccion = null, int? codigoOrden = null, int? codigoInforme = null, string modulo = null)
        {
            var moduloNormalizado = NormalizarModulo(modulo, accion);
            var accionNormalizada = NormalizarAccion(accion);

            if (!EsContextoAutenticado(usuario))
            {
                RegistrarDecisionAutorizacion("[AUTH][DENY]", moduloNormalizado, accionNormalizada, usuario, codigoSolicitud, codigoInspeccion, codigoInforme, false, "SesionNoAutenticada");
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, "La sesión expiró o no ha iniciado sesión.");
            }

            var requiereCompaniaActiva = usuario != null && RolRequiereCompaniaActiva(usuario.SelectedRole);

            if (requiereCompaniaActiva && RequiereCompaniaSeleccionada(usuario, moduloNormalizado))
            {
                RegistrarDecisionAutorizacion("[AUTH][DENY]", moduloNormalizado, accionNormalizada, usuario, codigoSolicitud, codigoInspeccion, codigoInforme, true, "Debe seleccionar una compania activa antes de continuar.");
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, "Debe seleccionar una compañía activa antes de continuar.", true);
            }

            var rolesNormalizados = NormalizarRoles(usuario);
            if (!TieneAccesoPorMatriz(moduloNormalizado, accionNormalizada, rolesNormalizados))
            {
                RegistrarDecisionAutorizacion("[AUTH][DENY]", moduloNormalizado, accionNormalizada, usuario, codigoSolicitud, codigoInspeccion, codigoInforme, requiereCompaniaActiva, "No tiene permisos para acceder a este modulo.");
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, "No tiene permisos para acceder a este módulo.");
            }

            string motivo;
            if (!ValidarRecurso(moduloNormalizado, accionNormalizada, usuario, codigoSolicitud, codigoInspeccion, codigoOrden, codigoInforme, out motivo))
            {
                RegistrarDecisionAutorizacion("[AUTH][DENY]", moduloNormalizado, accionNormalizada, usuario, codigoSolicitud, codigoInspeccion, codigoInforme, requiereCompaniaActiva, motivo);
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, motivo);
            }

            RegistrarDecisionAutorizacion("[AUTH][ALLOW]", moduloNormalizado, accionNormalizada, usuario, codigoSolicitud, codigoInspeccion, codigoInforme, requiereCompaniaActiva, EsRolInstitucional(usuario.SelectedRole) ? "RolInstitucionalAutorizado" : "RolAutorizado");
            return AocrAuthorizationResult.Allowed(moduloNormalizado, accionNormalizada);
        }

        public bool PuedeRtEditarSolicitud(int codigoSolicitud, int codigoUsuario)
        {
            string mensaje;
            return _solicitudRtService.PuedeRtEditarSolicitud(codigoSolicitud, codigoUsuario, out mensaje);
        }

        public bool PuedeFinancieroAprobarPago(int codigoOrden, int codigoUsuario)
        {
            if (codigoOrden <= 0 || codigoUsuario <= 0)
            {
                return false;
            }

            var orden = _ordenDao.ObtenerOrdenPorIdModel(codigoOrden);
            if (orden == null)
            {
                return false;
            }

            var estado = EstadoOrden.NormalizarEstado(orden.Estado);
            if (!Comparer.Equals(estado, EstadoOrden.EnRevisionFinanciera)
                && !Comparer.Equals(estado, EstadoOrden.Pendiente)
                && !Comparer.Equals(estado, EstadoOrden.Generada)
                && !Comparer.Equals(estado, EstadoOrden.Devuelta))
            {
                return false;
            }

            var pagos = _ordenDao.ObtenerPagosPorOrden(codigoOrden) ?? new List<CapaDatos.Models.PagoModel>();
            return pagos.Any(p => p != null && Comparer.Equals((p.Estado ?? string.Empty).Trim(), EstadoPago.Pendiente));
        }

        public bool PuedeCoordinadorAsignarInspector(int codigoSolicitud, int codigoUsuario)
        {
            if (codigoSolicitud <= 0 || codigoUsuario <= 0)
            {
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            if (solicitud == null)
            {
                return false;
            }

            var tieneAprobacionFinanciera = _ordenDao.TieneAprobacionFinancieraSolicitud(codigoSolicitud);
            if (!new AocrFlujoService().PuedeCoordinadorAsignarInspector(solicitud, tieneAprobacionFinanciera))
            {
                return false;
            }

            var inspecciones = _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
            return !inspecciones.Any(i => i != null && i.CodigoInspector.HasValue && i.CodigoInspector.Value > 0);
        }

        public bool PuedeInspectorRevisarDocumentos(int codigoSolicitud, int codigoUsuario)
        {
            if (codigoSolicitud <= 0 || codigoUsuario <= 0)
            {
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            if (solicitud == null)
            {
                return false;
            }

            var inspecciones = _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
            var login = codigoUsuario.ToString(CultureInfo.InvariantCulture);
            var codigoUsuarioInstitucional = login;

            try
            {
                var usuario = UsuarioDAO.ObtenerPorId(codigoUsuario);
                if (usuario != null)
                {
                    if (!string.IsNullOrWhiteSpace(usuario.NombreUsuario))
                    {
                        login = usuario.NombreUsuario.Trim();
                    }

                    if (!string.IsNullOrWhiteSpace(usuario.CodigoUsuario))
                    {
                        codigoUsuarioInstitucional = usuario.CodigoUsuario.Trim();
                    }
                }
            }
            catch
            {
                // Mantener fallback por id de usuario.
            }

            var identidad = _inspectorIdentityService.ObtenerIdentidadInspector(
                codigoUsuario,
                login,
                codigoUsuarioInstitucional);
            var evaluacion = _inspectorIdentityService.EvaluarInspectorAsignado(
                codigoSolicitud,
                solicitud,
                inspecciones,
                identidad);

            if (evaluacion != null && evaluacion.EsInspectorAsignado)
            {
                return true;
            }

            var inspectorIds = ResolverIdsInspector(codigoUsuario, login);
            return inspecciones.Any(i => i != null && i.CodigoInspector.HasValue && inspectorIds.Contains(i.CodigoInspector.Value));
        }

        public bool PuedeInspectorAbrirInspeccion(int codigoInspeccion, int codigoUsuario)
        {
            if (codigoInspeccion <= 0 || codigoUsuario <= 0)
            {
                return false;
            }

            var inspeccion = _inspeccionDao.ObtenerPorId(codigoInspeccion);
            if (inspeccion == null)
            {
                return false;
            }

            var inspectorIds = ResolverIdsInspector(codigoUsuario, codigoUsuario.ToString(CultureInfo.InvariantCulture));
            if (!inspeccion.CodigoInspector.HasValue || !inspectorIds.Contains(inspeccion.CodigoInspector.Value))
            {
                return false;
            }

            return _revisionDocumentalService.PuedeInspectorAbrirFaseOperativaLv(inspeccion);
        }

        public bool PuedeInspectorAbrirLv(int codigoInspeccion, int codigoUsuario)
        {
            return PuedeInspectorAbrirInspeccion(codigoInspeccion, codigoUsuario);
        }

        public bool PuedeInspectorGenerarInforme(int codigoInspeccion, int codigoUsuario)
        {
            if (!PuedeInspectorAbrirInspeccion(codigoInspeccion, codigoUsuario))
            {
                return false;
            }

            string motivoLv;
            if (!new AocrFlujoValidacionService().PuedeGenerarInformeTecnico(codigoInspeccion, out motivoLv))
            {
                return false;
            }

            var informe = _informeDao.ObtenerUltimoPorInspeccion(codigoInspeccion);
            return informe == null
                || string.IsNullOrWhiteSpace(informe.EstadoInforme)
                || Comparer.Equals((informe.EstadoInforme ?? string.Empty).Trim(), "BORRADOR_INFORME")
                || !informe.FirmadoInspector;
        }

        public bool PuedeDirectorRevisarInforme(int codigoInforme, int codigoUsuario)
        {
            if (codigoInforme <= 0 || codigoUsuario <= 0)
            {
                Trace.TraceWarning("[INFTEC_DIR][AUTH_DENY] InformeId=" + codigoInforme + "; UsuarioId=" + codigoUsuario + "; Estado=; Motivo=Parametros invalidos;");
                return false;
            }

            Trace.TraceInformation("[INFTEC_DIR][AUTH_IN] InformeId=" + codigoInforme + "; UsuarioId=" + codigoUsuario + ";");

            var informe = _informeDao.ObtenerPorId(codigoInforme);
            if (informe == null)
            {
                Trace.TraceWarning("[INFTEC_DIR][AUTH_DENY] InformeId=" + codigoInforme + "; Estado=; Motivo=Informe no existe;");
                return false;
            }

            var estadoNormalizado = InformeTecnicoEstadosInstitucionales.NormalizarToken(informe.EstadoInforme);
            var puedeRevisar = InformeTecnicoEstadosInstitucionales.PuedeRevisarDireccion(informe.EstadoInforme);
            Trace.TraceInformation("[INFTEC_DIR][AUTH_ESTADO] InformeId=" + codigoInforme + "; Estado=" + estadoNormalizado + "; PuedeRevisar=" + puedeRevisar + ";");

            if (!puedeRevisar)
            {
                Trace.TraceWarning("[INFTEC_DIR][AUTH_DENY] InformeId=" + codigoInforme + "; Estado=" + estadoNormalizado + "; Motivo=Estado no permitido para revision institucional;");
                return false;
            }

            if (!informe.FirmadoInspector)
            {
                Trace.TraceWarning("[INFTEC_DIR][AUTH_DENY] InformeId=" + codigoInforme + "; Estado=" + estadoNormalizado + "; Motivo=Informe sin firma de inspector;");
                return false;
            }

            if (informe.FirmadoDirdac)
            {
                Trace.TraceWarning("[INFTEC_DIR][AUTH_DENY] InformeId=" + codigoInforme + "; Estado=" + estadoNormalizado + "; Motivo=Informe ya firmado por Direccion/Jefatura;");
                return false;
            }

            if (informe.CodigoInspeccion <= 0)
            {
                Trace.TraceWarning("[INFTEC_DIR][AUTH_DENY] InformeId=" + codigoInforme + "; Estado=" + estadoNormalizado + "; Motivo=Informe sin inspeccion valida;");
                return false;
            }

            Trace.TraceInformation("[INFTEC_DIR][AUTH_OK] InformeId=" + codigoInforme + "; Estado=" + estadoNormalizado + ";");
            return true;
        }

        public bool PuedeInspectorFirmarInforme(int codigoInspeccion, int codigoUsuario)
        {
            if (!PuedeInspectorAbrirInspeccion(codigoInspeccion, codigoUsuario))
            {
                return false;
            }

            var informe = _informeDao.ObtenerUltimoPorInspeccion(codigoInspeccion);
            if (informe == null)
            {
                return false;
            }

            string motivo;
            return new AocrFlujoValidacionService().PuedeFirmarInformeTecnico(informe.CodigoInforme, out motivo);
        }

        public bool PuedeAdministradorConfigurarCorreos(int codigoUsuario)
        {
            return codigoUsuario > 0;
        }

        private bool ValidarRecurso(string modulo, string accion, AocrAuthorizationContext usuario, int? codigoSolicitud, int? codigoInspeccion, int? codigoOrden, int? codigoInforme, out string motivo)
        {
            motivo = string.Empty;

            if (Comparer.Equals(modulo, "SolicitudAOCR"))
            {
                if (Comparer.Equals(accion, "GuardarProgresoRT"))
                {
                    return ValidarGuardarProgresoRt(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "AbrirFormularioRT") || Comparer.Equals(accion, "EditarRT") || Comparer.Equals(accion, "FinalizarRT"))
                {
                    return ValidarRtSobreSolicitud(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "Aprobar"))
                {
                    return ValidarAprobacionDocumentalCoordinador(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "Detalle") || Comparer.Equals(accion, "DescargarGenerada"))
                {
                    return ValidarAccesoDetalleSolicitud(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "Generar"))
                {
                    if (!ValidarAccesoDetalleSolicitud(usuario, codigoSolicitud, out motivo))
                    {
                        return false;
                    }

                    var rolesGeneracion = NormalizarRoles(usuario);
                    string motivoGeneracion;
                    if (!new GeneracionAOCRService().PuedeGenerarAocr(codigoSolicitud.GetValueOrDefault(), usuario.UserId, rolesGeneracion, out motivoGeneracion))
                    {
                        motivo = string.IsNullOrWhiteSpace(motivoGeneracion)
                            ? "La solicitud no cumple las condiciones para generar AOCR."
                            : motivoGeneracion;
                        return false;
                    }

                    return true;
                }
            }

            if (Comparer.Equals(modulo, "OrdenRecaudacion"))
            {
                if (Comparer.Equals(accion, "Generar") || Comparer.Equals(accion, "SubirComprobante"))
                {
                    return ValidarAccesoOrden(usuario, codigoOrden, out motivo, Comparer.Equals(accion, "SubirComprobante"));
                }

                if (Comparer.Equals(accion, "Descargar"))
                {
                    return ValidarAccesoOrden(usuario, codigoOrden, out motivo, false);
                }
            }

            if (Comparer.Equals(modulo, "Financiero"))
            {
                if ((Comparer.Equals(accion, "AprobarOrden") || Comparer.Equals(accion, "AprobarPago") || Comparer.Equals(accion, "RechazarOrden") || Comparer.Equals(accion, "RechazarPago") || Comparer.Equals(accion, "AprobarYEnviarAS400"))
                    && !PuedeFinancieroAprobarPago(codigoOrden.GetValueOrDefault(), usuario.UserId))
                {
                    motivo = "El pago no está pendiente de validación o la orden no es válida para esta acción.";
                    return false;
                }
            }

            if (Comparer.Equals(modulo, "Coordinador") && Comparer.Equals(accion, "AsignarInspector")
                && !PuedeCoordinadorAsignarInspector(codigoSolicitud.GetValueOrDefault(), usuario.UserId))
            {
                motivo = "La solicitud no cumple las condiciones para asignar inspector.";
                return false;
            }

            if (Comparer.Equals(modulo, "Tecnico") && Comparer.Equals(accion, "AsignarInspector")
                && !PuedeCoordinadorAsignarInspector(codigoSolicitud.GetValueOrDefault(), usuario.UserId))
            {
                motivo = "La solicitud no cumple las condiciones para asignar inspector.";
                return false;
            }

            if (Comparer.Equals(modulo, "Documento"))
            {
                if (Comparer.Equals(accion, "Lista") || Comparer.Equals(accion, "Descargar"))
                {
                    return ValidarAccesoDetalleSolicitud(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "Subir"))
                {
                    return ValidarRtSobreSolicitud(usuario, codigoSolicitud, out motivo);
                }
            }

            if (Comparer.Equals(modulo, "RevisionDocumental") && Comparer.Equals(accion, "Revisar"))
            {
                var roles = NormalizarRoles(usuario);
                if (!roles.Contains("Administrador", Comparer)
                    && !roles.Contains("Coordinacion", Comparer)
                    && !roles.Contains("DireccionJefaturaTecnica", Comparer)
                    && !PuedeInspectorRevisarDocumentos(codigoSolicitud.GetValueOrDefault(), usuario.UserId))
                {
                    motivo = "No tiene una inspección asignada para revisar los documentos de esta solicitud.";
                    return false;
                }
            }

            if (Comparer.Equals(modulo, "Inspeccion"))
            {
                var rolesInspeccion = NormalizarRoles(usuario);
                var esAdministrador = rolesInspeccion.Contains("Administrador", Comparer);

                if (codigoInspeccion.GetValueOrDefault() > 0
                    && !ValidarAccesoInspeccion(usuario, codigoInspeccion.GetValueOrDefault(), out motivo))
                {
                    return false;
                }

                if (Comparer.Equals(accion, "ConfirmarRevisionDocumentalInspector"))
                {
                    if (!esAdministrador
                        && !PuedeInspectorRevisarDocumentos(ObtenerCodigoSolicitudInspeccion(codigoInspeccion.GetValueOrDefault()), usuario.UserId))
                    {
                        motivo = "No tiene una inspección asignada para confirmar la revisión documental.";
                        return false;
                    }
                }

                if (!esAdministrador
                    && (Comparer.Equals(accion, "GuardarListaVerificacionOperacionalEae")
                    || Comparer.Equals(accion, "FinalizarListaVerificacionOperacionalEae")
                    || Comparer.Equals(accion, "FirmarListaVerificacionOperacionalEae")
                    || Comparer.Equals(accion, "LV")))
                {
                    if (!PuedeInspectorAbrirLv(codigoInspeccion.GetValueOrDefault(), usuario.UserId))
                    {
                        motivo = "La inspección no está habilitada para gestionar la Lista de Verificación.";
                        return false;
                    }
                }

                if (!esAdministrador
                    && (Comparer.Equals(accion, "GuardarInformeTecnico")
                    || Comparer.Equals(accion, "FinalizarInformeTecnico")
                    || Comparer.Equals(accion, "ModalInformeTecnico")))
                {
                    if (!PuedeInspectorGenerarInforme(codigoInspeccion.GetValueOrDefault(), usuario.UserId))
                    {
                        motivo = "Debe finalizar y firmar la LV antes de gestionar el Informe Técnico.";
                        return false;
                    }
                }

                if (!esAdministrador
                    && Comparer.Equals(accion, "FirmarInformeInspector")
                    && !PuedeInspectorFirmarInforme(codigoInspeccion.GetValueOrDefault(), usuario.UserId))
                {
                    motivo = "El informe técnico no cumple las condiciones para firma del inspector.";
                    return false;
                }

                if (!esAdministrador
                    && (Comparer.Equals(accion, "RevisionDireccion")
                    || Comparer.Equals(accion, "AprobarDecisionFinalDireccion")
                    || Comparer.Equals(accion, "DevolverDecisionFinalDireccion")
                    || Comparer.Equals(accion, "FirmarDireccion")
                    || Comparer.Equals(accion, "FirmarInformeDirdac")
                    || Comparer.Equals(accion, "RechazarInformeDirdac")))
                {
                    var codigoInformeValidar = codigoInforme.GetValueOrDefault();
                    if (codigoInformeValidar <= 0 && codigoInspeccion.GetValueOrDefault() > 0)
                    {
                        var informeTmp = _informeDao.ObtenerUltimoPorInspeccion(codigoInspeccion.GetValueOrDefault());
                        if (informeTmp != null)
                        {
                            codigoInformeValidar = informeTmp.CodigoInforme;
                        }
                    }

                    if (!PuedeDirectorRevisarInforme(codigoInformeValidar, usuario.UserId))
                    {
                        motivo = "El informe técnico no está pendiente de revisión o firma institucional.";
                        return false;
                    }
                }

                if (!esAdministrador
                    && Comparer.Equals(accion, "Abrir")
                    && !PuedeInspectorAbrirInspeccion(codigoInspeccion.GetValueOrDefault(), usuario.UserId))
                {
                    motivo = "La inspección no está habilitada para el inspector actual o la fase documental aún no está aprobada.";
                    return false;
                }

                if (!esAdministrador
                    && rolesInspeccion.Contains("Solicitante", Comparer)
                    && (Comparer.Equals(accion, "CambiarEstado")
                        || Comparer.Equals(accion, "GuardarInformeTecnico")
                        || Comparer.Equals(accion, "FinalizarInformeTecnico")
                        || Comparer.Equals(accion, "FirmarInformeInspector")
                        || Comparer.Equals(accion, "GuardarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "FinalizarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "FirmarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "LV")
                        || Comparer.Equals(accion, "RegistrarNoConforme")
                        || Comparer.Equals(accion, "SubirInforme")))
                {
                    motivo = "No tiene permisos para ejecutar esta acción en la inspección.";
                    return false;
                }

                if (!esAdministrador
                    && rolesInspeccion.Contains("Coordinacion", Comparer)
                    && (Comparer.Equals(accion, "GuardarInformeTecnico")
                        || Comparer.Equals(accion, "FinalizarInformeTecnico")
                        || Comparer.Equals(accion, "FirmarInformeInspector")
                        || Comparer.Equals(accion, "GuardarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "FinalizarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "FirmarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "LV")
                        || Comparer.Equals(accion, "RegistrarNoConforme")
                        || Comparer.Equals(accion, "SubirInforme")))
                {
                    motivo = "La coordinación no puede modificar LV ni Informe Técnico del inspector.";
                    return false;
                }

                if (!esAdministrador
                    && (Comparer.Equals(accion, "VerLvEaeOficial")
                        || Comparer.Equals(accion, "DescargarLvEaeOficial")
                        || Comparer.Equals(accion, "GuardarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "FinalizarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "FirmarListaVerificacionOperacionalEae")
                        || Comparer.Equals(accion, "LV"))
                    && !PuedeInspectorAbrirLv(codigoInspeccion.GetValueOrDefault(), usuario.UserId)
                    && !rolesInspeccion.Contains("Coordinacion", Comparer)
                    && !rolesInspeccion.Contains("DireccionJefaturaTecnica", Comparer)
                    && !rolesInspeccion.Contains("Solicitante", Comparer))
                {
                    motivo = "La Lista de Verificación no está habilitada para su usuario.";
                    return false;
                }

                if (!esAdministrador
                    && (Comparer.Equals(accion, "RegistrarNoConforme")
                        || Comparer.Equals(accion, "SubirInforme"))
                    && !PuedeInspectorGenerarInforme(codigoInspeccion.GetValueOrDefault(), usuario.UserId))
                {
                    motivo = "Debe finalizar y firmar la LV antes de gestionar el informe o registrar no conformidades.";
                    return false;
                }
            }

            if (Comparer.Equals(modulo, "InformeTecnico"))
            {
                if (Comparer.Equals(accion, "Inspector") && !PuedeInspectorGenerarInforme(codigoInspeccion.GetValueOrDefault(), usuario.UserId))
                {
                    motivo = "El informe técnico no puede abrirse porque la inspección o la LV/EAE aún no están habilitadas.";
                    return false;
                }

                if (Comparer.Equals(accion, "RevisionDireccion") && !PuedeDirectorRevisarInforme(codigoInforme.GetValueOrDefault(), usuario.UserId))
                {
                    motivo = "El informe técnico todavía no está firmado por Inspector o no está pendiente de revisión institucional.";
                    return false;
                }
            }

            if (Comparer.Equals(modulo, "CoordinacionJefatura"))
            {
                if (Comparer.Equals(accion, "FirmarAceptacionDocumental"))
                {
                    return ValidarAprobacionDocumentalCoordinador(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "ValidarAocr"))
                {
                    if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0)
                    {
                        return true;
                    }

                    return ValidarAccesoDetalleSolicitud(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "GenerarDocumentoValidacionAocr")
                    || Comparer.Equals(accion, "DocumentoValidacionAocr"))
                {
                    return ValidarAccesoDetalleSolicitud(usuario, codigoSolicitud, out motivo);
                }
            }

            if (Comparer.Equals(modulo, "CorreoInstitucional") && !PuedeAdministradorConfigurarCorreos(usuario.UserId))
            {
                motivo = "No tiene permisos administrativos para configurar correos institucionales.";
                return false;
            }

            return true;
        }

        private bool ValidarAprobacionDocumentalCoordinador(AocrAuthorizationContext usuario, int? codigoSolicitud, out string motivo)
        {
            motivo = string.Empty;
            var roles = NormalizarRoles(usuario);
            if (!roles.Contains("Administrador", Comparer) && !roles.Contains("Coordinacion", Comparer))
            {
                motivo = "Solo Coordinación puede aceptar formalmente la documentación.";
                return false;
            }

            if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0)
            {
                motivo = "Solicitud inválida.";
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud.Value);
            if (solicitud == null)
            {
                motivo = "La solicitud no existe.";
                return false;
            }

            var estado = EstadoSolicitud.Normalizar(solicitud.Estado);
            if (Comparer.Equals(estado, EstadoSolicitud.Anulada) || Comparer.Equals(estado, EstadoSolicitud.Finalizado))
            {
                motivo = "La solicitud está cerrada o anulada.";
                return false;
            }

            var estadosPermitidos = new AocrFlujoService().EsTransicionPermitida(estado, EstadoSolicitud.AceptacionDocumental)
                || Comparer.Equals(estado, EstadoSolicitud.AceptacionDocumental)
                || Comparer.Equals(estado, EstadoSolicitud.EnRevision)
                || Comparer.Equals(estado, EstadoSolicitud.DocumentacionCompleta)
                || Comparer.Equals(estado, EstadoSolicitud.Subsanada)
                || Comparer.Equals(estado, EstadoSolicitud.DocumentacionPendiente);

            if (!estadosPermitidos)
            {
                motivo = "La solicitud no está en un estado válido para aceptación documental.";
                return false;
            }

            return true;
        }

        private bool ValidarGuardarProgresoRt(AocrAuthorizationContext usuario, int? codigoSolicitud, out string motivo)
        {
            motivo = string.Empty;
            var roles = NormalizarRoles(usuario);
            if (!roles.Contains("Administrador", Comparer) && !roles.Contains("Solicitante", Comparer))
            {
                motivo = "No tiene permisos para guardar esta sección.";
                return false;
            }

            if (codigoSolicitud.HasValue && codigoSolicitud.Value > 0)
            {
                return ValidarEdicionRtSolicitudExistente(usuario, codigoSolicitud.Value, out motivo);
            }

            if (PuedeIniciarOContinuarSolicitudRt(usuario, out motivo))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                motivo = "La Solicitud AOCR no está habilitada. Debe contar con Orden de Recaudación vigente y pago aprobado.";
            }

            return false;
        }

        private bool ValidarRtSobreSolicitud(AocrAuthorizationContext usuario, int? codigoSolicitud, out string motivo)
        {
            motivo = string.Empty;
            if (codigoSolicitud.HasValue && codigoSolicitud.Value > 0)
            {
                return ValidarEdicionRtSolicitudExistente(usuario, codigoSolicitud.Value, out motivo);
            }

            if (PuedeIniciarOContinuarSolicitudRt(usuario, out motivo))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                motivo = "La Solicitud AOCR no está habilitada para el RT hasta contar con Orden de Recaudación vigente y pago aprobado.";
            }

            return false;
        }

        private bool ValidarEdicionRtSolicitudExistente(AocrAuthorizationContext usuario, int codigoSolicitud, out string motivo)
        {
            motivo = string.Empty;
            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                motivo = "No se encontró la solicitud AOCR.";
                return false;
            }

            var roles = NormalizarRoles(usuario);
            if (!roles.Contains("Administrador", Comparer) && solicitud.CodigoUsuario != usuario.UserId)
            {
                motivo = "No tiene permisos para modificar esta solicitud.";
                return false;
            }

            if (!roles.Contains("Administrador", Comparer) && !CoincideCompania(usuario.CompanyCode, solicitud.CompaniasSeleccionadas))
            {
                motivo = "La solicitud no corresponde a la compañía activa.";
                return false;
            }

            if (!EstadoSolicitud.PermiteEdicionFormularioEmision(solicitud.Estado))
            {
                var estadoVisible = string.IsNullOrWhiteSpace(solicitud.Estado) ? "desconocido" : solicitud.Estado.Trim();
                motivo = "La solicitud ya no puede editarse porque se encuentra en estado: " + estadoVisible;
                return false;
            }

            if (!roles.Contains("Administrador", Comparer))
            {
                string mensajeRt;
                if (!_solicitudRtService.PuedeRtEditarSolicitud(codigoSolicitud, usuario.UserId, out mensajeRt))
                {
                    motivo = string.IsNullOrWhiteSpace(mensajeRt) ? "La solicitud no está habilitada para edición RT." : mensajeRt;
                    return false;
                }
            }

            return true;
        }

        private bool PuedeIniciarOContinuarSolicitudRt(AocrAuthorizationContext usuario, out string motivo)
        {
            motivo = string.Empty;
            if (usuario == null || usuario.UserId <= 0)
            {
                motivo = "La sesión expiró o no ha iniciado sesión.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(usuario.CompanyCode) && _ordenDao.TieneOrdenHabilitanteAOCR(usuario.UserId, usuario.CompanyCode))
            {
                return true;
            }

            var solicitudes = _solicitudDao.ObtenerPorUsuario(usuario.UserId) ?? new List<SolicitudAOCR>();
            foreach (var solicitud in solicitudes.Where(s => s != null && s.CodigoSolicitud > 0))
            {
                if (!CoincideCompania(usuario.CompanyCode, solicitud.CompaniasSeleccionadas))
                {
                    continue;
                }

                if (!EstadoSolicitud.PermiteEdicionFormularioEmision(solicitud.Estado))
                {
                    continue;
                }

                string mensajeRt;
                if (_solicitudRtService.PuedeRtEditarSolicitud(solicitud.CodigoSolicitud, usuario.UserId, out mensajeRt))
                {
                    return true;
                }
            }

            motivo = "La Solicitud AOCR se habilitará automáticamente cuando Financiero apruebe el pago de la Orden de Recaudación.";
            return false;
        }

        private bool ValidarAccesoDetalleSolicitud(AocrAuthorizationContext usuario, int? codigoSolicitud, out string motivo)
        {
            motivo = string.Empty;
            if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0)
            {
                motivo = "La solicitud no es válida.";
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud.Value);
            if (solicitud == null)
            {
                motivo = "La solicitud no existe.";
                return false;
            }

            var roles = NormalizarRoles(usuario);
            if (roles.Contains("Administrador", Comparer) || roles.Contains("Coordinacion", Comparer) || roles.Contains("DireccionJefaturaTecnica", Comparer))
            {
                return true;
            }

            if (roles.Contains("Solicitante", Comparer))
            {
                if (solicitud.CodigoUsuario != usuario.UserId)
                {
                    motivo = "No tiene permisos sobre esta solicitud.";
                    return false;
                }

                if (!CoincideCompania(usuario.CompanyCode, solicitud.CompaniasSeleccionadas))
                {
                    motivo = "La solicitud no corresponde a la compañía activa.";
                    return false;
                }

                return true;
            }

            if (roles.Contains("InspectorTecnico", Comparer))
            {
                if (PuedeInspectorRevisarDocumentos(codigoSolicitud.Value, usuario.UserId))
                {
                    return true;
                }

                motivo = "No tiene una inspección asignada para esta solicitud.";
                return false;
            }

            motivo = "No tiene permisos para acceder a esta solicitud.";
            return false;
        }

        private int ObtenerCodigoSolicitudInspeccion(int codigoInspeccion)
        {
            if (codigoInspeccion <= 0)
            {
                return 0;
            }

            var inspeccion = _inspeccionDao.ObtenerPorId(codigoInspeccion);
            return inspeccion != null ? inspeccion.CodigoSolicitud : 0;
        }

        private bool ValidarAccesoInspeccion(AocrAuthorizationContext usuario, int codigoInspeccion, out string motivo)
        {
            motivo = string.Empty;
            if (codigoInspeccion <= 0)
            {
                motivo = "Inspección inválida.";
                return false;
            }

            var roles = NormalizarRoles(usuario);
            if (roles.Contains("Administrador", Comparer))
            {
                return true;
            }

            Inspeccion inspeccion;
            try
            {
                inspeccion = _inspeccionDao.ObtenerPorId(codigoInspeccion);
            }
            catch (Exception)
            {
                motivo = "No fue posible validar el acceso a la inspección.";
                return false;
            }

            if (inspeccion == null)
            {
                motivo = "La inspección no existe.";
                return false;
            }

            if (roles.Contains("Coordinacion", Comparer)
                || roles.Contains("DireccionJefaturaTecnica", Comparer))
            {
                return true;
            }

            if (roles.Contains("Solicitante", Comparer))
            {
                var solicitud = _solicitudDao.ObtenerPorId(inspeccion.CodigoSolicitud);
                if (solicitud == null || solicitud.CodigoUsuario != usuario.UserId)
                {
                    motivo = "No tiene permisos sobre esta inspección.";
                    return false;
                }

                return true;
            }

            if (roles.Contains("InspectorTecnico", Comparer))
            {
                var inspectorIds = ResolverIdsInspector(usuario.UserId, usuario.CodigoUsuario);
                if (inspeccion.CodigoInspector.HasValue && inspectorIds.Contains(inspeccion.CodigoInspector.Value))
                {
                    return true;
                }

                motivo = "No tiene asignada esta inspección.";
                return false;
            }

            motivo = "No tiene permisos para acceder a esta inspección.";
            return false;
        }

        private bool ValidarAccesoOrden(AocrAuthorizationContext usuario, int? codigoOrden, out string motivo, bool requierePagoPendiente)
        {
            motivo = string.Empty;
            if (!codigoOrden.HasValue || codigoOrden.Value <= 0)
            {
                motivo = "La orden no es válida.";
                return false;
            }

            var orden = _ordenDao.ObtenerOrdenPorIdModel(codigoOrden.Value);
            if (orden == null)
            {
                motivo = "La orden no existe.";
                return false;
            }

            var roles = NormalizarRoles(usuario);
            if (roles.Contains("Administrador", Comparer) || roles.Contains("Financiero", Comparer) || roles.Contains("Coordinacion", Comparer) || roles.Contains("DireccionJefaturaTecnica", Comparer))
            {
                return true;
            }

            if (!roles.Contains("Solicitante", Comparer))
            {
                motivo = "No tiene permisos para acceder a esta orden.";
                return false;
            }

            if (orden.CodigoUsuario != usuario.UserId)
            {
                motivo = "No tiene permisos sobre esta orden.";
                return false;
            }

            if (requierePagoPendiente)
            {
                var estado = EstadoOrden.NormalizarEstado(orden.Estado);
                if (!Comparer.Equals(estado, EstadoOrden.Generada)
                    && !Comparer.Equals(estado, EstadoOrden.Pendiente)
                    && !Comparer.Equals(estado, EstadoOrden.Devuelta))
                {
                    motivo = "La orden no admite el registro de un nuevo comprobante en su estado actual.";
                    return false;
                }
            }

            return true;
        }

        private static bool CoincideCompania(string companiaActiva, string companiasSolicitud)
        {
            var activa = (companiaActiva ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(activa) || string.IsNullOrWhiteSpace(companiasSolicitud))
            {
                return true;
            }

            return companiasSolicitud
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim())
                .Any(x => x.Equals(activa, StringComparison.OrdinalIgnoreCase));
        }

        private bool TieneAccesoPorMatriz(string modulo, string accion, IList<string> rolesNormalizados)
        {
            if (rolesNormalizados == null || rolesNormalizados.Count == 0)
            {
                return false;
            }

            string[] rolesPermitidos;
            if (ActionMatrix.TryGetValue(modulo + "/" + accion, out rolesPermitidos))
            {
                return rolesPermitidos.Any(rol => rolesNormalizados.Contains(rol, Comparer));
            }

            if (ModuleMatrix.TryGetValue(modulo, out rolesPermitidos))
            {
                return rolesPermitidos.Any(rol => rolesNormalizados.Contains(rol, Comparer));
            }

            return rolesNormalizados.Contains("Administrador", Comparer);
        }

        private static bool RequiereCompaniaSeleccionada(AocrAuthorizationContext usuario, string modulo)
        {
            if (usuario == null || EsRolInstitucional(usuario.SelectedRole) || !RolRequiereCompaniaActiva(usuario.SelectedRole))
            {
                return false;
            }

            return (Comparer.Equals(modulo, "SolicitudAOCR") || Comparer.Equals(modulo, "OrdenRecaudacion"))
                && string.IsNullOrWhiteSpace((usuario.CompanyCode ?? string.Empty).Trim());
        }

        private static bool RolRequiereCompaniaActiva(string rol)
        {
            return Comparer.Equals(NormalizarRol(rol), "Solicitante");
        }

        private static bool EsRolInstitucional(string rol)
        {
            var normalizado = NormalizarRol(rol);
            return Comparer.Equals(normalizado, "Administrador")
                || Comparer.Equals(normalizado, "Coordinacion")
                || Comparer.Equals(normalizado, "DireccionJefaturaTecnica")
                || Comparer.Equals(normalizado, "Financiero")
                || Comparer.Equals(normalizado, "InspectorTecnico");
        }

        private static bool EsContextoAutenticado(AocrAuthorizationContext usuario)
        {
            return usuario != null && usuario.IsAuthenticated && usuario.UserId > 0;
        }

        private static bool EsRolActivoSolicitante(AocrAuthorizationContext usuario)
        {
            return usuario != null && Comparer.Equals(NormalizarRol(usuario.SelectedRole), "Solicitante");
        }

        private void RegistrarDecisionAutorizacion(
            string etiqueta,
            string modulo,
            string accion,
            AocrAuthorizationContext usuario,
            int? codigoSolicitud,
            int? codigoInspeccion,
            int? codigoInforme,
            bool requiereCompaniaActiva,
            string motivo)
        {
            try
            {
                var estadoInforme = ResolverEstadoInformeLog(codigoInforme);
                var solicitudId = codigoSolicitud;
                var inspeccionId = codigoInspeccion;

                if ((!solicitudId.HasValue || solicitudId.Value <= 0) && codigoInforme.HasValue && codigoInforme.Value > 0)
                {
                    var informe = _informeDao.ObtenerPorId(codigoInforme.Value);
                    if (informe != null && informe.CodigoInspeccion > 0)
                    {
                        inspeccionId = informe.CodigoInspeccion;
                    }
                }

                if ((!solicitudId.HasValue || solicitudId.Value <= 0) && inspeccionId.HasValue && inspeccionId.Value > 0)
                {
                    var inspeccion = _inspeccionDao.ObtenerPorId(inspeccionId.Value);
                    if (inspeccion != null && inspeccion.CodigoSolicitud > 0)
                    {
                        solicitudId = inspeccion.CodigoSolicitud;
                    }
                }

                var estadoSolicitud = ResolverEstadoSolicitudLog(solicitudId);
                Trace.TraceInformation(
                    etiqueta +
                    " UsuarioId=" + (usuario != null ? usuario.UserId.ToString(CultureInfo.InvariantCulture) : "0") +
                    "; Login=" + (usuario != null ? (usuario.UserName ?? string.Empty) : string.Empty) +
                    "; RolActivo=" + (usuario != null ? (usuario.SelectedRole ?? string.Empty) : string.Empty) +
                    "; Accion=" + (accion ?? string.Empty) +
                    "; Modulo=" + (modulo ?? string.Empty) +
                    "; SolicitudId=" + (solicitudId.HasValue ? solicitudId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                    "; InformeId=" + (codigoInforme.HasValue ? codigoInforme.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                    "; EstadoSolicitud=" + estadoSolicitud +
                    "; EstadoInforme=" + estadoInforme +
                    "; CompaniaActiva=" + (usuario != null ? (usuario.CompanyCode ?? string.Empty) : string.Empty) +
                    "; RequiereCompaniaActiva=" + requiereCompaniaActiva +
                    "; Motivo=" + (motivo ?? string.Empty));
            }
            catch
            {
            }
        }

        private string ResolverEstadoSolicitudLog(int? codigoSolicitud)
        {
            if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0)
            {
                return string.Empty;
            }

            try
            {
                var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud.Value);
                return solicitud != null ? (solicitud.Estado ?? string.Empty) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string ResolverEstadoInformeLog(int? codigoInforme)
        {
            if (!codigoInforme.HasValue || codigoInforme.Value <= 0)
            {
                return string.Empty;
            }

            try
            {
                var informe = _informeDao.ObtenerPorId(codigoInforme.Value);
                return informe != null ? (informe.EstadoInforme ?? string.Empty) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizarModulo(string modulo, string accion)
        {
            var moduloNormalizado = NormalizarClave(modulo);
            if (!string.IsNullOrWhiteSpace(moduloNormalizado))
            {
                return moduloNormalizado;
            }

            var accionNormalizada = NormalizarClave(accion);
            var separador = accionNormalizada.IndexOf('/');
            return separador > 0 ? accionNormalizada.Substring(0, separador) : accionNormalizada;
        }

        private static string NormalizarAccion(string accion)
        {
            var accionNormalizada = NormalizarClave(accion);
            var separador = accionNormalizada.IndexOf('/');
            return separador >= 0 ? accionNormalizada.Substring(separador + 1) : accionNormalizada;
        }

        private static string NormalizarClave(string valor)
        {
            return (valor ?? string.Empty).Trim();
        }

        private static IList<string> NormalizarRoles(AocrAuthorizationContext usuario)
        {
            var roles = new List<string>();
            if (usuario == null)
            {
                return roles;
            }

            if (!string.IsNullOrWhiteSpace(usuario.SelectedRole))
            {
                roles.Add(NormalizarRol(usuario.SelectedRole));
            }

            foreach (var rol in usuario.Roles ?? new List<string>())
            {
                var normalizado = NormalizarRol(rol);
                if (!string.IsNullOrWhiteSpace(normalizado))
                {
                    roles.Add(normalizado);
                }
            }

            return roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(Comparer).ToList();
        }

        private static string NormalizarRol(string rol)
        {
            var normalized = Simplify(rol);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (Matches(normalized, "ADMIN", "ADMINISTRADOR")) return "Administrador";
            if (Matches(normalized, "SOLICITANTE", "OPERADOR", "REPRESENTANTETECNICO", "REPRESENTANTELEGAL", "RT")) return "Solicitante";
            if (Matches(normalized, "INSPECTOR", "TECNICO", "EVALUADORTECNICO", "INSPECTORTECNICO")) return "InspectorTecnico";
            if (Matches(normalized, "FINANCIERO", "COORDINADORFINANCIERO", "DIRECTORFINANCIERO")) return "Financiero";
            if (Matches(normalized, "COORDINACION", "COORDINADOR", "COORDINADORINSPECCIONES", "COORDINACIONLEGAL", "COORDINADORLEGAL")) return "Coordinacion";
            if (Matches(normalized, "DIRECCION", "JEFATURATECNICA", "DIRDAC", "DCAV", "DIRECTORCERTIFICACIONESDCAV", "DIRECTORGENERAL", "DIRECCIONJEFATURA", "DIRECCIONJEFATURATECNICA")) return "DireccionJefaturaTecnica";
            return rol == null ? string.Empty : rol.Trim();
        }

        private static bool Matches(string normalizedRole, params string[] aliases)
        {
            return aliases.Any(alias => normalizedRole.Equals(alias, StringComparison.OrdinalIgnoreCase));
        }

        private static string Simplify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToUpperInvariant()
                .Replace('Á', 'A')
                .Replace('É', 'E')
                .Replace('Í', 'I')
                .Replace('Ó', 'O')
                .Replace('Ú', 'U')
                .Replace('Ü', 'U')
                .Replace('Ñ', 'N');

            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private HashSet<int> ResolverIdsInspector(int userId, string codigoUsuario)
        {
            var ids = new HashSet<int>();
            if (userId > 0)
            {
                ids.Add(userId);
            }

            try
            {
                CapaDatos.Models.UsuarioInternoRTRegistro inspectorActual = null;
                if (userId > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(userId);
                }

                if (inspectorActual == null && !string.IsNullOrWhiteSpace(codigoUsuario))
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerActivoPorCodigoUsuario(codigoUsuario)
                        ?? _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(codigoUsuario);
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
    }
}
