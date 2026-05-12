using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Security;
using CapaPresentacion.Helpers;
using CapaPresentacion.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
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
            var esFinancieroRol = RoleGroupingHelper.IsCoordinacion(rolActual)
                && (sinRolesRaw || RoleGroupingHelper.HasAnyRawRole(rolesRaw, "Financiero", "CoordinadorFinanciero"));
            var esLegalRol = RoleGroupingHelper.IsCoordinacion(rolActual)
                && (sinRolesRaw || RoleGroupingHelper.HasAnyRawRole(rolesRaw, "CoordinacionLegal", "CoordinadorLegal"));
            var esDireccionRol = RoleGroupingHelper.IsDireccionJefaturaTecnica(rolActual);
            var puedeAdministracion = esAdministrador || esDireccionRol;
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

            model.AccesosDashboards = ConstruirDashboards(model);
            model.AccionesInstitucionales = ConstruirAcciones(model, esAdministrador);

            return View(model);
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