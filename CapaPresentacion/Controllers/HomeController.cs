using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaNegocio;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Models;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly OrdenRecaudacionDAO _ordenDao = new OrdenRecaudacionDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly InspectorDashboardService _inspectorDashboardService = new InspectorDashboardService();
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao = new UsuarioInternoRTDAO();
        private readonly IUserContextAccessor _userContext = new UserContextAccessor();

        public ActionResult Index()
        {
            // Verificación de seguridad de sesión
            if (Session["NombreUsuario"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var rolActual = RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"]?.ToString());
            var rolesRaw = RoleGroupingHelper.ExtractRoles(Session["RolesRaw"]);
            var sinRolesRaw = rolesRaw.Count == 0;

            var esAdministrador = RoleGroupingHelper.IsAdministrador(rolActual);
            var esSolicitanteRol = RoleGroupingHelper.IsSolicitante(rolActual);
            var esTecnicaRol = RoleGroupingHelper.IsInspectorTecnico(rolActual)
                && (sinRolesRaw || RoleGroupingHelper.HasAnyRawRole(rolesRaw, "Inspector", "Tecnico", "EvaluadorTecnico"));
            var esFinancieroRol = RoleGroupingHelper.IsFinanciero(rolActual)
                && (sinRolesRaw || RoleGroupingHelper.HasAnyRawRole(rolesRaw, "Financiero", "CoordinadorFinanciero", "DirectorFinanciero"));
            var esLegalRol = RoleGroupingHelper.IsCoordinacion(rolActual)
                && (sinRolesRaw || RoleGroupingHelper.HasAnyRawRole(rolesRaw, "CoordinacionLegal", "CoordinadorLegal"));
            var esDireccionRol = RoleGroupingHelper.IsDireccionJefaturaTecnica(rolActual);
            var puedeAdministracion = esAdministrador;
            var puedeAprobarUsuarios = esAdministrador || esLegalRol || esDireccionRol;
            var rolVisible = RoleGroupingHelper.ToDisplayName(rolActual);

            ViewBag.Usuario = Session["NombreUsuario"];
            ViewBag.Rol = rolVisible;

            var model = new DashboardViewModel
            {
                NombreUsuario = Session["NombreUsuario"]?.ToString() ?? "Usuario",
                RolUsuario = rolVisible,

                // Inicialización de contadores en cero para nuevos usuarios
                SolicitudesPendientes = 0,
                TramitesEnCurso = 0,
                NotificacionesNuevas = 0,
                ResumenPrimarioTitulo = "Pendientes",
                ResumenPrimarioCaption = "Solicitudes por revisar",
                ResumenSecundarioTitulo = "En proceso",
                ResumenSecundarioCaption = "Trámites en curso",
                ResumenTerciarioTitulo = "Avisos",
                ResumenTerciarioCaption = "Notificaciones nuevas",

                // Permisos de visibilidad de módulos
                MostrarModuloOperador = esSolicitanteRol || esAdministrador,
                MostrarModuloFinanciero = esAdministrador || esFinancieroRol,
                MostrarModuloCertificacion = esAdministrador || esLegalRol || esDireccionRol,
                MostrarModuloInspector = esAdministrador || esTecnicaRol || esDireccionRol,
                MostrarDashboardOrdenes = true,
                MostrarDashboardFinanciero = esAdministrador || esFinancieroRol,
                MostrarDashboardInspector = esAdministrador || esTecnicaRol || esDireccionRol,
                MostrarDashboardGerencial = esAdministrador || esDireccionRol || esLegalRol,
                MostrarDashboardAdministracion = puedeAdministracion,
                MostrarSyncRt = esAdministrador,
                MostrarAprobacionRt = puedeAprobarUsuarios
            };

            AplicarResumenOperativo(model, esAdministrador, esSolicitanteRol, esTecnicaRol, esFinancieroRol, esLegalRol, esDireccionRol);

            model.AccesosDashboards = ConstruirDashboards(model);
            model.AccionesInstitucionales = ConstruirAcciones(model, esAdministrador);

            return View(model);
        }

        private void AplicarResumenOperativo(
            DashboardViewModel model,
            bool esAdministrador,
            bool esSolicitanteRol,
            bool esTecnicaRol,
            bool esFinancieroRol,
            bool esLegalRol,
            bool esDireccionRol)
        {
            var idUsuario = ObtenerIdUsuario();
            model.NotificacionesNuevas = idUsuario > 0 ? NotificacionBL.ContarNoLeidas(idUsuario) : 0;

            try
            {
                if (esAdministrador || esDireccionRol || esLegalRol)
                {
                    AplicarResumenInstitucional(model);
                    return;
                }

                if (esFinancieroRol)
                {
                    AplicarResumenFinanciero(model);
                    return;
                }

                if (esTecnicaRol)
                {
                    AplicarResumenTecnico(model);
                    return;
                }

                if (esSolicitanteRol)
                {
                    AplicarResumenSolicitante(model, idUsuario);
                }
            }
            catch
            {
                model.ResumenTerciarioCaption = "No se pudo actualizar el resumen en este momento.";
            }
        }

        private void AplicarResumenInstitucional(DashboardViewModel model)
        {
            var pendientes = ObtenerSolicitudesPendientesRevision();
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            var inspecciones = _inspeccionDao.ListarTodas() ?? new List<Inspeccion>();

            model.SolicitudesPendientes = pendientes.Count;
            model.TramitesEnCurso = solicitudes.Count(s => !EsSolicitudFinalizada(s))
                + inspecciones.Count(i => !EsInspeccionFinalizada(i));
            model.ResumenPrimarioTitulo = "Solicitudes por revisar";
            model.ResumenPrimarioCaption = "Bandeja institucional de revisión documental y coordinación.";
            model.ResumenSecundarioTitulo = "Trámites activos";
            model.ResumenSecundarioCaption = "Solicitudes e inspecciones globales aún no cerradas.";
            model.ResumenTerciarioTitulo = "Avisos";
            model.ResumenTerciarioCaption = "Notificaciones nuevas del usuario activo.";
        }

        private void AplicarResumenFinanciero(DashboardViewModel model)
        {
            var ordenes = _ordenDao.ObtenerTodasLasOrdenes(null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();

            model.SolicitudesPendientes = ordenes.Count(o => EsOrdenPendiente(o.Estado));
            model.TramitesEnCurso = ordenes.Count(o => !EsOrdenFinalizada(o.Estado));
            model.ResumenPrimarioTitulo = "Órdenes pendientes";
            model.ResumenPrimarioCaption = "Órdenes en BORRADOR, GENERADA o ENVIADA.";
            model.ResumenSecundarioTitulo = "Órdenes en gestión";
            model.ResumenSecundarioCaption = "Órdenes aún no completadas, anuladas o rechazadas.";
            model.ResumenTerciarioTitulo = "Avisos";
            model.ResumenTerciarioCaption = "Notificaciones nuevas del usuario activo.";
        }

        private void AplicarResumenTecnico(DashboardViewModel model)
        {
            var codigosInspector = ObtenerCodigosInspector().Where(id => id > 0).ToList();
            if (codigosInspector.Count == 0)
            {
                return;
            }

            var dashboard = _inspectorDashboardService.ObtenerDashboard(codigosInspector, null, null, null, null, null);

            model.SolicitudesPendientes = dashboard.InspeccionesPendientes;
            model.TramitesEnCurso = Math.Max(0, dashboard.InspeccionesAsignadas - dashboard.InspeccionesCerradas);
            model.NotificacionesNuevas = Math.Max(model.NotificacionesNuevas, dashboard.AlertasUrgentes.Count);
            model.ResumenPrimarioTitulo = "Inspecciones pendientes";
            model.ResumenPrimarioCaption = "Bandeja técnica del inspector actual.";
            model.ResumenSecundarioTitulo = "Expedientes activos";
            model.ResumenSecundarioCaption = "Inspecciones asignadas que aún no están cerradas.";
            model.ResumenTerciarioTitulo = "Alertas";
            model.ResumenTerciarioCaption = "Alertas del tablero técnico y avisos pendientes.";
        }

        private void AplicarResumenSolicitante(DashboardViewModel model, int idUsuario)
        {
            if (idUsuario <= 0)
            {
                return;
            }

            var solicitudes = _solicitudDao.ObtenerPorUsuario(idUsuario) ?? new List<SolicitudAOCR>();
            var companiaActivaCodigo = CompaniaActivaSessionHelper.ObtenerCodigo(Session);

            if (!string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                solicitudes = solicitudes
                    .Where(s => SolicitudCoincideConCompaniaActiva(s, companiaActivaCodigo))
                    .ToList();
            }

            var ordenes = _ordenDao.ListarPorUsuario(idUsuario, null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();

            model.SolicitudesPendientes = solicitudes.Count(s => !EsSolicitudFinalizada(s));
            model.TramitesEnCurso = ordenes.Count(o => !EsOrdenFinalizada(o.Estado));
            model.ResumenPrimarioTitulo = "Solicitudes activas";
            model.ResumenPrimarioCaption = "Expedientes visibles para la compañía activa.";
            model.ResumenSecundarioTitulo = "Órdenes en gestión";
            model.ResumenSecundarioCaption = "Órdenes de recaudación aún no finalizadas.";
            model.ResumenTerciarioTitulo = "Avisos";
            model.ResumenTerciarioCaption = "Notificaciones nuevas del usuario activo.";
        }

        private List<SolicitudAOCR> ObtenerSolicitudesPendientesRevision()
        {
            var pendientes = _solicitudDao.ObtenerPendientesRevision();
            if (pendientes == null || pendientes.Count == 0)
            {
                pendientes = _solicitudDao.ObtenerPorEstados(
                    "PENDIENTE",
                    "EN_REVISION",
                    "ENVIADO_A_INSPECTOR",
                    "ENVIADO_A_JEFATURA");
            }

            return pendientes ?? new List<SolicitudAOCR>();
        }

        private static bool EsSolicitudFinalizada(SolicitudAOCR solicitud)
        {
            var estado = EstadoSolicitud.Normalizar(solicitud != null ? solicitud.Estado : null);
            return string.Equals(estado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoSolicitud.Rechazada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoSolicitud.Anulada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoSolicitud.CertificadoEmitido, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsInspeccionFinalizada(Inspeccion inspeccion)
        {
            var estado = (inspeccion != null ? inspeccion.Estado : string.Empty) ?? string.Empty;
            var resultado = (inspeccion != null ? inspeccion.Resultado : string.Empty) ?? string.Empty;

            return estado.Equals("CERRADA", StringComparison.OrdinalIgnoreCase)
                || estado.Equals("APROBADA", StringComparison.OrdinalIgnoreCase)
                || resultado.Equals("SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                || resultado.Equals("APROBADO", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsOrdenPendiente(string estado)
        {
            var estadoNormalizado = (estado ?? string.Empty).Trim().ToUpperInvariant();
            return estadoNormalizado == "BORRADOR"
                || estadoNormalizado == "GENERADA"
                || estadoNormalizado == "ENVIADA";
        }

        private static bool EsOrdenFinalizada(string estado)
        {
            var estadoNormalizado = (estado ?? string.Empty).Trim().ToUpperInvariant();
            return estadoNormalizado == "FACTURADA"
                || estadoNormalizado == "COMPLETADA"
                || estadoNormalizado == "ANULADA"
                || estadoNormalizado == "RECHAZADA";
        }

        private static bool SolicitudCoincideConCompaniaActiva(SolicitudAOCR solicitud, string companiaActivaCodigo)
        {
            if (solicitud == null || string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(solicitud.CompaniasSeleccionadas))
            {
                return true;
            }

            return solicitud.CompaniasSeleccionadas
                .Split(',')
                .Select(x => (x ?? string.Empty).Trim())
                .Any(x => x.Equals(companiaActivaCodigo.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private int ObtenerIdUsuario()
        {
            int idUsuario;
            return _userContext.TryGetUserId(Session, out idUsuario) ? idUsuario : 0;
        }

        private HashSet<int> ObtenerCodigosInspector()
        {
            var ids = new HashSet<int>();
            var idUsuario = ObtenerIdUsuario();
            var codigoUsuarioTexto = (Session["CodigoUsuario"] ?? string.Empty).ToString().Trim();
            var codigoUsuarioNumerico = 0;

            if (idUsuario > 0)
            {
                ids.Add(idUsuario);
            }

            if (_userContext.TryGetCodigoUsuario(Session, out codigoUsuarioNumerico) && codigoUsuarioNumerico > 0)
            {
                ids.Add(codigoUsuarioNumerico);
            }

            try
            {
                UsuarioInternoRTRegistro inspectorActual = null;

                if (idUsuario > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(idUsuario);
                }

                if (inspectorActual == null && !string.IsNullOrWhiteSpace(codigoUsuarioTexto))
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerActivoPorCodigoUsuario(codigoUsuarioTexto)
                        ?? _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(codigoUsuarioTexto);
                }

                if (inspectorActual == null && codigoUsuarioNumerico > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(codigoUsuarioNumerico);
                }

                if (inspectorActual != null)
                {
                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                    }

                    if (inspectorActual.UsuarioId.HasValue && inspectorActual.UsuarioId.Value > 0)
                    {
                        ids.Add(inspectorActual.UsuarioId.Value);
                    }
                }
            }
            catch
            {
            }

            return ids;
        }

        private static IList<DashboardShortcutViewModel> ConstruirDashboards(DashboardViewModel model)
        {
            var items = new List<DashboardShortcutViewModel>();

            if (model.MostrarDashboardOrdenes)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Dashboard de Órdenes",
                    Descripcion = "Seguimiento operativo de órdenes de recaudación y estado financiero general.",
                    Icono = "fas fa-receipt",
                    Controlador = "OrdenRecaudacionDashboard",
                    Accion = "Index",
                    Estilo = "primary"
                });
            }

            if (model.MostrarDashboardFinanciero)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Dashboard Financiero",
                    Descripcion = "Control de validación, facturación, pagos y estado FR3.",
                    Icono = "fas fa-chart-line",
                    Controlador = "Financiero",
                    Accion = "Dashboard",
                    Estilo = "success"
                });
            }

            if (model.MostrarDashboardInspector)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Dashboard Técnico / Inspector",
                    Descripcion = "Carga operativa, inspecciones pendientes, alertas y documentación.",
                    Icono = "fas fa-clipboard-check",
                    Controlador = "Dashboard",
                    Accion = "Inspector",
                    Estilo = "info"
                });
            }

            if (model.MostrarDashboardGerencial)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Dashboard Gerencial",
                    Descripcion = "Lectura ejecutiva de solicitudes, inspecciones y cuellos de botella institucionales.",
                    Icono = "fas fa-sitemap",
                    Controlador = "CoordinacionJefatura",
                    Accion = "DashboardGerencial",
                    Estilo = "warning"
                });
            }

            if (model.MostrarDashboardAdministracion)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Dashboard Administración",
                    Descripcion = "Usuarios, roles, aprobaciones RT y accesos operativos de administración.",
                    Icono = "fas fa-user-shield",
                    Controlador = "AdminUsuarios",
                    Accion = "Index",
                    Estilo = "secondary"
                });
            }

            return items;
        }

        private static IList<DashboardShortcutViewModel> ConstruirAcciones(DashboardViewModel model, bool esAdministrador)
        {
            var items = new List<DashboardShortcutViewModel>();

            if (model.MostrarModuloOperador)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Crear Solicitud",
                    Descripcion = "Nueva solicitud AOCR desde la pantalla inicial.",
                    Icono = "fas fa-plus-circle",
                    Controlador = "SolicitudAOCR",
                    Accion = "Index",
                    Estilo = "primary"
                });

                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Mis Solicitudes",
                    Descripcion = "Gestión de trámites, observaciones y documentación remitida.",
                    Icono = "fas fa-file-alt",
                    Controlador = "SolicitudAOCR",
                    Accion = "Index",
                    Estilo = "info"
                });
            }

            if (model.MostrarModuloFinanciero)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Reportes Financieros",
                    Descripcion = "Consulta de reportes y métricas del flujo financiero.",
                    Icono = "fas fa-chart-column",
                    Controlador = "ReportesFinancieros",
                    Accion = "Index",
                    Estilo = "success"
                });
            }

            if (model.MostrarModuloOperador || model.MostrarModuloInspector || model.MostrarModuloCertificacion || model.MostrarDashboardAdministracion)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "AOCR Generadas y Firmadas",
                    Descripcion = "Consulta consolidada de AOCR, condiciones, firmas y PDFs finales por rol.",
                    Icono = "fas fa-file-circle-check",
                    Controlador = "SolicitudAOCR",
                    Accion = "GeneradasFirmadas",
                    Estilo = "warning"
                });
            }

            if (model.MostrarSyncRt)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Sync RT DB2",
                    Descripcion = "Ejecución y monitoreo del espejo AS400 hacia PostgreSQL.",
                    Icono = "fas fa-database",
                    Controlador = "SyncAdmin",
                    Accion = "Index",
                    Estilo = "warning"
                });
            }

            if (model.MostrarAprobacionRt)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Aprobar Usuarios RT",
                    Descripcion = "Revisión documental y aprobación de designaciones RT pendientes.",
                    Icono = "fas fa-user-check",
                    Controlador = "Usuario",
                    Accion = "RevisarDesignaciones",
                    Estilo = "secondary"
                });
            }

            if (esAdministrador)
            {
                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Crear Usuario AOCR",
                    Descripcion = "Alta local de usuarios y asignacion inicial de roles, aun sin vinculacion externa.",
                    Icono = "fas fa-user-plus",
                    Controlador = "AdminUsuarios",
                    Accion = "Create",
                    Estilo = "primary"
                });

                items.Add(new DashboardShortcutViewModel
                {
                    Titulo = "Configuración Sistema",
                    Descripcion = "Parámetros globales y opciones institucionales del sistema.",
                    Icono = "fas fa-cogs",
                    Controlador = "Direccion",
                    Accion = "ConfiguracionSistema",
                    Estilo = "dark"
                });
            }

            return items;
        }

        public ActionResult Salir()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            if (Request.Cookies[FormsAuthentication.FormsCookieName] != null)
            {
                var cookie = new System.Web.HttpCookie(FormsAuthentication.FormsCookieName)
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(cookie);
            }

            return RedirectToAction("Login", "Account");
        }
    }
}