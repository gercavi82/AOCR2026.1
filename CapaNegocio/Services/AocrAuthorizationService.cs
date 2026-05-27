using System;
using System.Collections.Generic;
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
        public IList<string> Roles { get; set; }
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }

        public AocrAuthorizationContext()
        {
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
            { "SolicitudAOCR/FinalizarRT", new[] { "Solicitante", "Administrador" } },
            { "SolicitudAOCR/Detalle", new[] { "Solicitante", "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/Generar", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/DescargarGenerada", new[] { "Solicitante", "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/Aprobar", new[] { "Coordinacion", "Administrador" } },
            { "SolicitudAOCR/AprobarJefatura", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/Legalizar", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "SolicitudAOCR/DecisionInstitucional", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "Coordinador/AsignarInspector", new[] { "Coordinacion", "Administrador" } },
            { "RevisionDocumental/Index", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "RevisionDocumental/Revisar", new[] { "InspectorTecnico", "Administrador" } },
            { "Inspeccion/Detalle", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Solicitante", "Administrador" } },
            { "Inspeccion/Abrir", new[] { "InspectorTecnico", "Coordinacion", "DireccionJefaturaTecnica", "Administrador" } },
            { "Inspeccion/LV", new[] { "InspectorTecnico", "Administrador" } },
            { "InformeTecnico/Inspector", new[] { "InspectorTecnico", "Administrador" } },
            { "InformeTecnico/RevisionDireccion", new[] { "DireccionJefaturaTecnica", "Administrador" } },
            { "CorreoInstitucional/Index", new[] { "Administrador" } }
        };

        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly SolicitudAocrService _solicitudRtService = new SolicitudAocrService();
        private readonly OrdenRecaudacionDAO _ordenDao = new OrdenRecaudacionDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly RevisionDocumentalService _revisionDocumentalService = new RevisionDocumentalService();
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao = new UsuarioInternoRTDAO();

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
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, "La sesión expiró o no ha iniciado sesión.");
            }

            if (RequiereCompaniaSeleccionada(usuario, moduloNormalizado))
            {
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, "Debe seleccionar una compañía activa antes de continuar.", true);
            }

            var rolesNormalizados = NormalizarRoles(usuario);
            if (!TieneAccesoPorMatriz(moduloNormalizado, accionNormalizada, rolesNormalizados))
            {
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, "No tiene permisos para acceder a este módulo.");
            }

            string motivo;
            if (!ValidarRecurso(moduloNormalizado, accionNormalizada, usuario, codigoSolicitud, codigoInspeccion, codigoOrden, codigoInforme, out motivo))
            {
                return AocrAuthorizationResult.Denied(moduloNormalizado, accionNormalizada, motivo);
            }

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

            if (!_ordenDao.TieneAprobacionFinancieraSolicitud(codigoSolicitud))
            {
                return false;
            }

            var estado = EstadoSolicitud.Normalizar(solicitud.Estado);
            if (Comparer.Equals(estado, EstadoSolicitud.Anulada)
                || Comparer.Equals(estado, EstadoSolicitud.Finalizado))
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

            var inspecciones = _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
            var inspectorIds = ResolverIdsInspector(codigoUsuario, codigoUsuario.ToString(CultureInfo.InvariantCulture));
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

            return _revisionDocumentalService.EstaInspeccionHabilitadaParaEjecucion(inspeccion);
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
                return false;
            }

            var pendientes = _informeDao.ListarPendientesFirmaDirdac() ?? new List<InspeccionInformeTecnico>();
            return pendientes.Any(i => i != null && i.CodigoInforme == codigoInforme && i.FirmadoInspector && !i.FirmadoDirdac);
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
                if (Comparer.Equals(accion, "AbrirFormularioRT") || Comparer.Equals(accion, "EditarRT") || Comparer.Equals(accion, "FinalizarRT"))
                {
                    return ValidarRtSobreSolicitud(usuario, codigoSolicitud, out motivo);
                }

                if (Comparer.Equals(accion, "Aprobar"))
                {
                    if (!PuedeCoordinadorAsignarInspector(codigoSolicitud.GetValueOrDefault(), usuario.UserId))
                    {
                        motivo = "La solicitud no está lista para asignación o no tiene pago aprobado.";
                        return false;
                    }

                    return true;
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

            if (Comparer.Equals(modulo, "Inspeccion") && (Comparer.Equals(accion, "Abrir") || Comparer.Equals(accion, "LV")))
            {
                var permitido = Comparer.Equals(accion, "LV")
                    ? PuedeInspectorAbrirLv(codigoInspeccion.GetValueOrDefault(), usuario.UserId)
                    : PuedeInspectorAbrirInspeccion(codigoInspeccion.GetValueOrDefault(), usuario.UserId);
                if (!permitido)
                {
                    motivo = "La inspección no está habilitada para el inspector actual o la fase documental aún no está aprobada.";
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

            if (Comparer.Equals(modulo, "CorreoInstitucional") && !PuedeAdministradorConfigurarCorreos(usuario.UserId))
            {
                motivo = "No tiene permisos administrativos para configurar correos institucionales.";
                return false;
            }

            return true;
        }

        private bool ValidarRtSobreSolicitud(AocrAuthorizationContext usuario, int? codigoSolicitud, out string motivo)
        {
            motivo = string.Empty;
            if (codigoSolicitud.HasValue && codigoSolicitud.Value > 0)
            {
                string mensajeRt;
                if (!_solicitudRtService.PuedeRtEditarSolicitud(codigoSolicitud.Value, usuario.UserId, out mensajeRt))
                {
                    motivo = string.IsNullOrWhiteSpace(mensajeRt) ? "La solicitud no está habilitada para edición RT." : mensajeRt;
                    return false;
                }

                return true;
            }

            var solicitudes = _solicitudDao.ObtenerPorUsuario(usuario.UserId) ?? new List<SolicitudAOCR>();
            foreach (var solicitud in solicitudes.Where(s => s != null && s.CodigoSolicitud > 0))
            {
                if (!CoincideCompania(usuario.CompanyCode, solicitud.CompaniasSeleccionadas))
                {
                    continue;
                }

                string mensajeRt;
                if (_solicitudRtService.PuedeRtEditarSolicitud(solicitud.CodigoSolicitud, usuario.UserId, out mensajeRt))
                {
                    return true;
                }
            }

            motivo = "La Solicitud AOCR no está habilitada para el RT hasta contar con Orden de Recaudación vigente y pago aprobado.";
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
                var inspectorIds = ResolverIdsInspector(usuario.UserId, usuario.CodigoUsuario);
                var inspecciones = _inspeccionDao.ListarPorSolicitud(codigoSolicitud.Value) ?? new List<Inspeccion>();
                if (inspecciones.Any(i => i != null && i.CodigoInspector.HasValue && inspectorIds.Contains(i.CodigoInspector.Value)))
                {
                    return true;
                }

                motivo = "No tiene una inspección asignada para esta solicitud.";
                return false;
            }

            motivo = "No tiene permisos para acceder a esta solicitud.";
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
            if (!EsRolActivoSolicitante(usuario))
            {
                return false;
            }

            return (Comparer.Equals(modulo, "SolicitudAOCR") || Comparer.Equals(modulo, "OrdenRecaudacion"))
                && string.IsNullOrWhiteSpace((usuario.CompanyCode ?? string.Empty).Trim());
        }

        private static bool EsContextoAutenticado(AocrAuthorizationContext usuario)
        {
            return usuario != null && usuario.IsAuthenticated && usuario.UserId > 0;
        }

        private static bool EsRolActivoSolicitante(AocrAuthorizationContext usuario)
        {
            return usuario != null && Comparer.Equals(NormalizarRol(usuario.SelectedRole), "Solicitante");
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
            if (Matches(normalized, "DIRECCION", "JEFATURATECNICA", "DIRDAC", "DIRECTORGENERAL", "DIRECCIONJEFATURATECNICA")) return "DireccionJefaturaTecnica";
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