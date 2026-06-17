using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Models.ViewModels;
using CapaPresentacion.Services;

namespace CapaPresentacion.Helpers
{
    public static class SidebarMenuBuilder
    {
        private sealed class SidebarBadgeSnapshot
        {
            public int UnreadNotifications { get; set; }
            public int RtActiveRequests { get; set; }
            public int RtObservedRequests { get; set; }
            public int RtPendingSubsanations { get; set; }
            public int RtFinalDocuments { get; set; }
            public int RtAocrFirmadas { get; set; }
            public int RtCondicionesFirmadas { get; set; }
            public int RtDescargasFinales { get; set; }
            public int InspectorPendingRevision { get; set; }
            public int InspectorTotalInspections { get; set; }
            public int InspectorAssignedInspections { get; set; }
            public int InspectorInProgressInspections { get; set; }
            public int InspectorObservedInspections { get; set; }
            public int InspectorFinishedInspections { get; set; }
            public int CoordinatorPendingAssignment { get; set; }
            public int CoordinatorDocumentalQueue { get; set; }
            public int CoordinatorAocrFormalReview { get; set; }
            public int CoordinatorFinalDocuments { get; set; }
            public int FinancialPendingOrders { get; set; }
            public int FinancialPendingReviewOrders { get; set; }
            public int FinancialUploadedPaymentOrders { get; set; }
            public int FinancialObservedOrders { get; set; }
            public int FinancialApprovedOrders { get; set; }
            public int FinancialFacturedOrders { get; set; }
            public int FinancialHistoryOrders { get; set; }
            public int DirdacPendingSignatures { get; set; }
            public int ExecutiveApprovalQueue { get; set; }
            public int AdminApprovalUsers { get; set; }
        }

        private sealed class SidebarBuildContext
        {
            public HttpContextBase HttpContext { get; set; }
            public UrlHelper Url { get; set; }
            public ViewDataDictionary ViewData { get; set; }
            public object Model { get; set; }
            public string CurrentController { get; set; }
            public string CurrentAction { get; set; }
            public string CurrentFragment { get; set; }
            public int UserId { get; set; }
            public string CodigoUsuarioSesion { get; set; }
            public string UserName { get; set; }
            public string UserEmail { get; set; }
            public string RolActual { get; set; }
            public string RolDisplay { get; set; }
            public IList<string> RolesRaw { get; set; }
            public IList<string> RolesDisponibles { get; set; }
            public bool SinRolesRaw { get; set; }
            public bool EsAdministrador { get; set; }
            public bool EsSolicitanteRol { get; set; }
            public bool EsRepresentanteRtRol { get; set; }
            public bool EsSolicitanteORT { get; set; }
            public bool EsInspectorRol { get; set; }
            public bool EsCoordinadorRol { get; set; }
            public bool EsFinancieroRol { get; set; }
            public bool EsLegalRol { get; set; }
            public bool EsDirdacRol { get; set; }
            public bool EsDirectorGeneralRol { get; set; }
            public bool PuedeAdministracion { get; set; }
            public bool PuedeAprobarUsuarios { get; set; }
            public bool TieneNavegacionRol { get; set; }
            public bool RequiereOrden { get; set; }
            public bool TieneOrdenGenerada { get; set; }
            public bool TieneOrdenBorrador { get; set; }
            public bool TieneOrdenPendienteProceso { get; set; }
            public bool TieneOrdenPendienteComprobante { get; set; }
            public bool TieneSolicitudRtHabilitada { get; set; }
            public bool TieneAccesoSolicitudRt { get; set; }
            public int? CodigoSolicitudRtSeguimiento { get; set; }
            public int? CodigoSolicitudRtEditable { get; set; }
            public bool SolicitudRtFormularioEditable { get; set; } = true;
            public bool PuedeAbrirFormularioEmisionRt { get; set; }
            public string MensajeBloqueoRtSidebar { get; set; }
            public bool ExisteProcesoActivoCompania { get; set; }
            public string ProcesoActivoReferencia { get; set; }
            public string ProcesoActivoEstado { get; set; }
            public string ProcesoActivoMensaje { get; set; }
            public string CodigoCompaniaActiva { get; set; }
            public string NombreCompaniaActiva { get; set; }
            public bool MostrarSelectorCompaniaRt { get; set; }
            public IList<UsuarioCompaniaRT> CompaniasRtAsignadas { get; set; }
            public SidebarBadgeSnapshot Badges { get; set; }
        }

        private sealed class FinancialSidebarCounterRow
        {
            public int OrdenId { get; set; }
            public string EstadoOrden { get; set; }
            public string EstadoPago { get; set; }
            public bool TieneFactura { get; set; }
        }

        public static SidebarMenuViewModel Build(ViewContext viewContext, ViewDataDictionary viewData, object model)
        {
            var context = BuildContext(viewContext, viewData, model);
            var vm = new SidebarMenuViewModel
            {
                UserName = !string.IsNullOrWhiteSpace(context.UserName) ? context.UserName : "Usuario AOCR",
                UserRoleDisplay = !string.IsNullOrWhiteSpace(context.RolDisplay) ? context.RolDisplay : "Perfil institucional",
                UserEmail = !string.IsNullOrWhiteSpace(context.UserEmail) ? context.UserEmail : "Sin correo registrado",
                AvailableRoleCount = context.RolesDisponibles.Count,
                ActiveRoleSummary = context.RolesDisponibles.Count > 1
                    ? "Tiene " + context.RolesDisponibles.Count + " perfil(es) disponible(s). Use el selector de rol para cambiar de módulo sin saturar el menú lateral."
                    : string.Empty,
                ActiveCompanyCode = context.CodigoCompaniaActiva,
                ActiveCompanyName = !string.IsNullOrWhiteSpace(context.NombreCompaniaActiva)
                    ? context.NombreCompaniaActiva
                    : (!string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva) ? context.CodigoCompaniaActiva : "No definida"),
                ShowCompanySelector = context.MostrarSelectorCompaniaRt,
                CompanyChangeUrl = context.Url.Action("CambiarCompaniaActiva", "Account"),
                ReturnUrl = context.HttpContext != null && context.HttpContext.Request != null
                    ? context.HttpContext.Request.RawUrl
                    : string.Empty,
                HasNavigation = context.TieneNavegacionRol,
                EmptyStateMessage = "Cambie de rol o vuelva a iniciar sesión para ver las opciones correspondientes.",
                OrderStatusCard = BuildOrderStatusCard(context)
            };

            foreach (var compania in context.CompaniasRtAsignadas ?? new List<UsuarioCompaniaRT>())
            {
                if (compania == null || string.IsNullOrWhiteSpace(compania.CompaniaCodigo))
                {
                    continue;
                }

                var code = (compania.CompaniaCodigo ?? string.Empty).Trim();
                var name = (compania.CompaniaNombre ?? string.Empty).Trim();
                vm.Companias.Add(new SidebarCompanyOptionViewModel
                {
                    Code = code,
                    Name = !string.IsNullOrWhiteSpace(name) ? name : code,
                    Selected = string.Equals(code, context.CodigoCompaniaActiva, StringComparison.OrdinalIgnoreCase)
                });
            }

            AddGroup(vm, BuildInicioMenuGroup(context));
            AddGroup(vm, BuildOrdenesPagosMenuGroup(context));
            AddGroup(vm, BuildSolicitudMenuGroup(context));
            AddGroup(vm, BuildDocumentosMenuGroup(context));
            AddGroup(vm, BuildInspeccionesMenuGroup(context));
            AddGroup(vm, BuildInformeTecnicoMenuGroup(context));
            AddGroup(vm, BuildAocrCondicionesMenuGroup(context));
            AddGroup(vm, BuildFirmasAprobacionesMenuGroup(context));
            AddGroup(vm, BuildHistorialMenuGroup(context));
            AddGroup(vm, BuildAdministracionMenuGroup(context));

            foreach (var quickAction in BuildQuickActions(context).Where(item => item.Visible))
            {
                vm.QuickActions.Add(quickAction);
            }

            vm.FooterItems.Add(CreateItem(
                context,
                "logout",
                "Cerrar sesión",
                "Salir del sistema AOCR de forma segura.",
                "fas fa-sign-out-alt",
                "Account",
                "Logout",
                null,
                null,
                "danger",
                true,
                true,
                string.Empty,
                new[] { "Logout" },
                null,
                null,
                string.Empty,
                "aocr-sidebar-item--danger"));

            return vm;
        }

        private static SidebarBuildContext BuildContext(ViewContext viewContext, ViewDataDictionary viewData, object model)
        {
            var httpContext = viewContext.HttpContext;
            AuthenticatedSessionBootstrapper.EnsureSession(httpContext);
            var userContext = AocrUserContextService.FromHttpContext(httpContext);
            var session = httpContext.Session;
            var permission = SidebarPermissionHelper.Resolve(
                userContext.RolActivo,
                userContext.RolesRaw != null && userContext.RolesRaw.Count > 0
                    ? userContext.RolesRaw
                    : userContext.RolesUnificados);

            var context = new SidebarBuildContext
            {
                HttpContext = httpContext,
                Url = new UrlHelper(viewContext.RequestContext),
                ViewData = viewData,
                Model = model,
                CurrentController = Convert.ToString(viewContext.RouteData.Values["controller"] ?? string.Empty),
                CurrentAction = Convert.ToString(viewContext.RouteData.Values["action"] ?? string.Empty),
                CurrentFragment = httpContext.Request != null && httpContext.Request.Url != null
                    ? Convert.ToString(httpContext.Request.Url.Fragment ?? string.Empty)
                    : string.Empty,
                RolActual = permission.RolActual,
                RolesRaw = permission.RolesRaw,
                CodigoUsuarioSesion = !string.IsNullOrWhiteSpace(userContext.Login)
                    ? userContext.Login.Trim()
                    : (session["CodigoUsuario"] as string ?? string.Empty).Trim(),
                UserName = !string.IsNullOrWhiteSpace(userContext.Nombre)
                    ? userContext.Nombre.Trim()
                    : (session["NombreUsuario"] as string ?? string.Empty).Trim(),
                UserEmail = (session["Correo"] as string ?? string.Empty).Trim(),
                UserId = userContext.UsuarioId > 0
                    ? userContext.UsuarioId
                    : ParseUserId(session["IdUsuario"] ?? session["UserId"])
            };

            context.RolesDisponibles = permission.RolesDisponibles;
            context.SinRolesRaw = permission.SinRolesRaw;
            context.RolDisplay = permission.RolDisplay;

            LoadOrdenState(context);

            context.EsAdministrador = permission.EsAdministrador;
            context.EsSolicitanteRol = permission.EsSolicitanteRol;
            context.EsRepresentanteRtRol = permission.EsRepresentanteRtRol;
            context.EsSolicitanteORT = permission.EsSolicitanteORT;
            context.EsInspectorRol = permission.EsInspectorRol;
            context.EsCoordinadorRol = permission.EsCoordinadorRol;
            context.EsFinancieroRol = permission.EsFinancieroRol;
            context.EsLegalRol = permission.EsLegalRol;
            context.EsDirectorGeneralRol = permission.EsDirectorGeneralRol;
            context.EsDirdacRol = permission.EsDirdacRol;

            context.RequiereOrden = context.EsSolicitanteRol || context.EsAdministrador;
            context.MensajeBloqueoRtSidebar = (!context.TieneOrdenBorrador && !context.TieneOrdenPendienteProceso && !context.TieneOrdenPendienteComprobante)
                ? "Debe generar la Orden de Recaudación para continuar con el proceso AOCR."
                : "El módulo de Solicitud AOCR se habilitará cuando Financiero apruebe el pago correspondiente.";

            LoadCompanyState(context);

            context.PuedeAdministracion = permission.PuedeAdministracion;
            context.PuedeAprobarUsuarios = permission.PuedeAprobarUsuarios;
            context.TieneAccesoSolicitudRt = context.TieneOrdenGenerada
                || (context.EsSolicitanteORT && context.TieneSolicitudRtHabilitada)
                || (context.EsSolicitanteORT && context.PuedeAbrirFormularioEmisionRt);
            context.TieneNavegacionRol = permission.TieneNavegacionRol;

            context.Badges = LoadBadgeSnapshot(context);
            return context;
        }

        private static void AddGroup(SidebarMenuViewModel vm, SidebarMenuGroupViewModel group)
        {
            if (group == null)
            {
                return;
            }

            group.BadgeCount = group.Items.Where(item => item.Visible && item.ShowBadge).Sum(item => item.BadgeCount);
            group.ShowBadge = group.Items.Any(item => item.Visible && item.ShowBadge);
            group.Expanded = group.Expanded || group.Items.Any(item => item.Visible && item.IsActive);

            if (group.Visible && group.Items.Any(item => item.Visible))
            {
                vm.Groups.Add(group);
            }
        }

        private static void LoadOrdenState(SidebarBuildContext context)
        {
            var session = context.HttpContext.Session;
            try
            {
                if (context.UserId <= 0)
                {
                    return;
                }

                var cacheKey = "_Sidebar_OrdenStatus_" + context.UserId + "_" + (context.CodigoCompaniaActiva ?? "ALL");
                var cached = session[cacheKey] as int[];
                var cacheTimeKey = cacheKey + "_t";
                var cachedTime = session[cacheTimeKey] as long?;
                var now = DateTime.UtcNow.Ticks;

                if (cached != null && cached.Length >= 4 && cachedTime.HasValue && (now - cachedTime.Value) < TimeSpan.FromSeconds(30).Ticks)
                {
                    context.TieneOrdenGenerada = cached[0] == 1;
                    context.TieneOrdenBorrador = cached[1] == 1;
                    context.TieneOrdenPendienteProceso = cached[2] == 1;
                    context.TieneOrdenPendienteComprobante = cached[3] == 1;
                    return;
                }

                var procesoService = new AocrProcesoActivoService();
                var companiaCodigo = context.CodigoCompaniaActiva;
                var companiaNombre = context.NombreCompaniaActiva;

                if (!string.IsNullOrWhiteSpace(companiaCodigo))
                {
                    context.TieneOrdenBorrador = procesoService.TieneOrdenBorradorPorCompania(
                        context.UserId,
                        companiaCodigo,
                        companiaNombre);
                    context.TieneOrdenPendienteProceso = procesoService.TieneOrdenActivaEnProcesoPorCompania(
                        context.UserId,
                        companiaCodigo,
                        companiaNombre);
                    context.TieneOrdenPendienteComprobante = procesoService.TieneOrdenPendienteComprobantePorCompania(
                        context.UserId,
                        companiaCodigo,
                        companiaNombre);
                    context.TieneOrdenGenerada = context.TieneOrdenPendienteProceso || context.TieneOrdenPendienteComprobante;
                }
                else
                {
                    var dao = new OrdenRecaudacionDAO();
                    context.TieneOrdenGenerada = dao.TieneOrdenHabilitanteAOCR(context.UserId);
                    context.TieneOrdenBorrador = dao.ExisteORMinima(context.UserId);
                    context.TieneOrdenPendienteProceso = dao.TieneOrdenActivaEnProceso(context.UserId);
                    context.TieneOrdenPendienteComprobante = dao.TieneOrdenPendienteComprobante(context.UserId);
                }

                var procesoActivo = !string.IsNullOrWhiteSpace(companiaCodigo)
                    ? procesoService.ObtenerProcesoActivoPorCompania(context.UserId, companiaCodigo, companiaNombre)
                    : null;
                context.ExisteProcesoActivoCompania = procesoActivo != null && procesoActivo.ExisteProcesoActivo;
                context.ProcesoActivoReferencia = procesoActivo != null
                    ? (procesoActivo.NumeroSolicitudActiva ?? procesoActivo.NumeroOrdenActiva)
                    : null;
                context.ProcesoActivoEstado = procesoActivo != null ? procesoActivo.EstadoProcesoActivo : null;
                context.ProcesoActivoMensaje = procesoActivo != null
                    ? (procesoActivo.ExisteProcesoActivo ? procesoActivo.MensajeBloqueo : procesoActivo.MensajeInformativo)
                    : null;

                session[cacheKey] = new[]
                {
                    context.TieneOrdenGenerada ? 1 : 0,
                    context.TieneOrdenBorrador ? 1 : 0,
                    context.TieneOrdenPendienteProceso ? 1 : 0,
                    context.TieneOrdenPendienteComprobante ? 1 : 0
                };
                session[cacheTimeKey] = now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SidebarMenuBuilder: Error consultando estado de orden: " + ex.Message);
            }
        }

        private static void LoadCompanyState(SidebarBuildContext context)
        {
            var session = context.HttpContext.Session;
            context.CompaniasRtAsignadas = new List<UsuarioCompaniaRT>();
            context.CodigoCompaniaActiva = CompaniaActivaSessionHelper.ObtenerCodigo(session);
            if (string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva))
            {
                context.CodigoCompaniaActiva = ResolveCompanyText(context, new[] { "CompaniaActivaCodigo", "CodigoCompaniaActiva", "CompaniaCodigo", "CodigoCompania" });
            }

            context.NombreCompaniaActiva = CompaniaActivaSessionHelper.ObtenerNombre(session);
            if (string.IsNullOrWhiteSpace(context.NombreCompaniaActiva))
            {
                context.NombreCompaniaActiva = ResolveCompanyText(context, new[] { "CompaniaActivaNombre", "NombreCompaniaActiva", "CompaniaNombre", "NombreCompania", "Compania" });
            }

            try
            {
                if (context.UserId <= 0)
                {
                    return;
                }

                var daoCompaniasRt = new UsuarioCompaniaRTDAO();
                var companias = daoCompaniasRt.ObtenerCompaniasAsignadas(context.UserId) ?? new List<UsuarioCompaniaRT>();
                var usuarioActual = UsuarioDAO.ObtenerPorId(context.UserId);
                if (usuarioActual != null)
                {
                    foreach (var codigo in ParseLegacyCompanies(usuarioActual.EmpresaCodigo))
                    {
                        if (companias.Any(c => c != null && string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        companias.Add(new UsuarioCompaniaRT
                        {
                            UsuarioId = context.UserId,
                            CompaniaCodigo = codigo,
                            CompaniaNombre = codigo,
                            Activo = true
                        });
                    }
                }

                context.CompaniasRtAsignadas = companias
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CompaniaCodigo))
                    .GroupBy(c => (c.CompaniaCodigo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(c => (c.CompaniaCodigo ?? string.Empty).Trim())
                    .ToList();

                context.MostrarSelectorCompaniaRt = context.CompaniasRtAsignadas.Count > 1;

                var codigoActivoActual = (context.CodigoCompaniaActiva ?? string.Empty).Trim();
                var companiaActiva = !string.IsNullOrWhiteSpace(codigoActivoActual)
                    ? context.CompaniasRtAsignadas.FirstOrDefault(c =>
                        string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigoActivoActual, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (context.CompaniasRtAsignadas.Count == 1 && companiaActiva == null)
                {
                    companiaActiva = context.CompaniasRtAsignadas[0];
                    context.CodigoCompaniaActiva = (companiaActiva.CompaniaCodigo ?? string.Empty).Trim();
                    context.NombreCompaniaActiva = !string.IsNullOrWhiteSpace(companiaActiva.CompaniaNombre)
                        ? companiaActiva.CompaniaNombre.Trim()
                        : context.CodigoCompaniaActiva;

                    CompaniaActivaSessionHelper.Establecer(session, context.CodigoCompaniaActiva, context.NombreCompaniaActiva);
                    try
                    {
                        CapaNegocio.LogBL.RegistrarInfo(
                            string.Format(
                                "[SIDEBAR][COMPANIA_ACTIVA] UsuarioId={0}; Rol={1}; Motivo=UnicaCompaniaAsignada; CompaniaSeleccionada={2}",
                                context.UserId,
                                context.RolActual ?? string.Empty,
                                context.CodigoCompaniaActiva),
                            "SidebarMenuBuilder");
                    }
                    catch
                    {
                    }
                }
                else if (context.CompaniasRtAsignadas.Count > 1 && companiaActiva == null)
                {
                    try
                    {
                        CapaNegocio.LogBL.RegistrarAdvertencia(
                            string.Format(
                                "[SIDEBAR][COMPANIA_ACTIVA] UsuarioId={0}; Motivo=MultiCompaniaSinSeleccion; TotalCompanias={1}",
                                context.UserId,
                                context.CompaniasRtAsignadas.Count),
                            "SidebarMenuBuilder");
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrWhiteSpace(context.NombreCompaniaActiva) && !string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva))
                {
                    companiaActiva = context.CompaniasRtAsignadas.FirstOrDefault(c =>
                        string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), context.CodigoCompaniaActiva.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (companiaActiva != null)
                    {
                        context.NombreCompaniaActiva = (companiaActiva.CompaniaNombre ?? string.Empty).Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(context.NombreCompaniaActiva) && !string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva))
                {
                    context.NombreCompaniaActiva = context.CodigoCompaniaActiva;
                }

                if (!string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva)
                    && !string.IsNullOrWhiteSpace(context.NombreCompaniaActiva)
                    && (string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerCodigo(session))
                        || string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerNombre(session))))
                {
                    CompaniaActivaSessionHelper.Establecer(session, context.CodigoCompaniaActiva, context.NombreCompaniaActiva);
                }

                if (context.EsSolicitanteORT)
                {
                    var workflow = new AocrPostPagoWorkflowService();
                    var solicitudDao = new SolicitudAOCRDAO();
                    var solicitudesUsuario = (solicitudDao.ObtenerPorUsuario(context.UserId) ?? new List<SolicitudAOCR>())
                        .Where(s => s != null && s.CodigoSolicitud > 0)
                        .Where(s => string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva)
                            || string.IsNullOrWhiteSpace(s.CompaniasSeleccionadas)
                            || s.CompaniasSeleccionadas
                                .Split(',')
                                .Select(x => (x ?? string.Empty).Trim())
                                .Any(x => x.Equals(context.CodigoCompaniaActiva, StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(s => s.CodigoSolicitud)
                        .ToList();

                    foreach (var solicitud in solicitudesUsuario)
                    {
                        string mensajeBloqueo;
                        if (workflow.PuedeRtAccederModuloSolicitud(solicitud.CodigoSolicitud, context.UserId, out mensajeBloqueo))
                        {
                            context.TieneSolicitudRtHabilitada = true;
                            break;
                        }
                    }

                    CargarSolicitudRtSeguimientoSidebar(context, solicitudDao);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SidebarMenuBuilder: Error consultando companias RT: " + ex.Message);
            }
        }

        private static void CargarSolicitudRtSeguimientoSidebar(SidebarBuildContext context, SolicitudAOCRDAO solicitudDao)
        {
            if (context == null || solicitudDao == null || context.UserId <= 0)
            {
                return;
            }

            try
            {
                var ordenDao = new OrdenRecaudacionDAO();
                var solicitudesFiltradas = (solicitudDao.ObtenerPorUsuario(context.UserId) ?? new List<SolicitudAOCR>())
                    .Where(s => s != null && s.CodigoSolicitud > 0)
                    .Where(s => string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva)
                        || string.IsNullOrWhiteSpace(s.CompaniasSeleccionadas)
                        || s.CompaniasSeleccionadas
                            .Split(',')
                            .Select(x => (x ?? string.Empty).Trim())
                            .Any(x => x.Equals(context.CodigoCompaniaActiva, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(s => s.CodigoSolicitud)
                    .ToList();

                var solicitudEditable = solicitudesFiltradas
                    .FirstOrDefault(s => EstadoSolicitud.PermiteEdicionFormularioEmision(s.Estado));

                SolicitudAOCR solicitudOrden = null;
                var codigoSolicitudOrden = ordenDao.ObtenerCodigoSolicitudOrdenRecienteUsuario(context.UserId, soloOrdenGenerada: true);
                if (codigoSolicitudOrden.HasValue && codigoSolicitudOrden.Value > 0)
                {
                    solicitudOrden = solicitudDao.ObtenerPorId(codigoSolicitudOrden.Value);
                }

                if (solicitudEditable != null)
                {
                    context.CodigoSolicitudRtEditable = solicitudEditable.CodigoSolicitud;
                    context.PuedeAbrirFormularioEmisionRt = true;
                    context.SolicitudRtFormularioEditable = true;
                }
                else if (ordenDao.ExisteORGeneradaOPagada(context.UserId))
                {
                    if (solicitudOrden == null
                        || solicitudOrden.CodigoSolicitud <= 0
                        || EstadoSolicitud.PermiteEdicionFormularioEmision(solicitudOrden.Estado))
                    {
                        context.PuedeAbrirFormularioEmisionRt = true;
                        context.SolicitudRtFormularioEditable = solicitudOrden != null
                            && EstadoSolicitud.PermiteEdicionFormularioEmision(solicitudOrden.Estado);
                    }
                }

                var solicitudSeguimiento = solicitudOrden;
                if (solicitudSeguimiento == null || solicitudSeguimiento.CodigoSolicitud <= 0)
                {
                    solicitudSeguimiento = solicitudesFiltradas.FirstOrDefault();
                }

                if (solicitudSeguimiento != null
                    && solicitudSeguimiento.CodigoSolicitud > 0
                    && !EstadoSolicitud.PermiteEdicionFormularioEmision(solicitudSeguimiento.Estado))
                {
                    context.CodigoSolicitudRtSeguimiento = solicitudSeguimiento.CodigoSolicitud;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SidebarMenuBuilder: Error resolviendo solicitud RT seguimiento: " + ex.Message);
            }
        }

        private static bool DebeMostrarSeguimientoSolicitudRt(SidebarBuildContext context)
        {
            return context != null
                && context.CodigoSolicitudRtSeguimiento.HasValue
                && context.CodigoSolicitudRtSeguimiento.Value > 0
                && !context.PuedeAbrirFormularioEmisionRt;
        }

        private static string ResolverAccionSolicitudRtSidebar(SidebarBuildContext context, string accionEdicion)
        {
            if (context != null && context.PuedeAbrirFormularioEmisionRt)
            {
                return accionEdicion;
            }

            return DebeMostrarSeguimientoSolicitudRt(context) ? "Detalle" : accionEdicion;
        }

        private static object ResolverRouteValuesSolicitudRtSidebar(SidebarBuildContext context, int tipoSolicitud)
        {
            if (context != null && context.PuedeAbrirFormularioEmisionRt)
            {
                if (context.CodigoSolicitudRtEditable.HasValue && context.CodigoSolicitudRtEditable.Value > 0)
                {
                    return new { tipoSolicitud = tipoSolicitud, oid = context.CodigoSolicitudRtEditable.Value };
                }

                return new { tipoSolicitud = tipoSolicitud };
            }

            if (DebeMostrarSeguimientoSolicitudRt(context))
            {
                return new { id = context.CodigoSolicitudRtSeguimiento.Value };
            }

            return new { tipoSolicitud = tipoSolicitud };
        }

        private static string[] ResolverActiveActionsSolicitudRtSidebar(SidebarBuildContext context, params string[] accionesEdicion)
        {
            if (DebeMostrarSeguimientoSolicitudRt(context))
            {
                return new[] { "Detalle", "FormularioEmisionAOCR", "Index" };
            }

            return accionesEdicion ?? new[] { "FormularioEmisionAOCR" };
        }

        private static SidebarBadgeSnapshot LoadBadgeSnapshot(SidebarBuildContext context)
        {
            var session = context.HttpContext.Session;
            var cacheScope = context.EsFinancieroRol || context.EsAdministrador
                ? "GLOBAL"
                : (context.CodigoCompaniaActiva ?? string.Empty).Trim();
            var cacheKey = "_Sidebar_Badges_" + context.UserId + "_" + context.RolActual + "_" + cacheScope;
            var cacheTimeKey = cacheKey + "_t";
            var cached = session[cacheKey] as SidebarBadgeSnapshot;
            var cachedTime = session[cacheTimeKey] as long?;
            var now = DateTime.UtcNow.Ticks;
            var cacheSeconds = context.EsFinancieroRol || context.EsAdministrador ? 5 : 45;
            if (cached != null && cachedTime.HasValue && (now - cachedTime.Value) < TimeSpan.FromSeconds(cacheSeconds).Ticks)
            {
                return cached;
            }

            var snapshot = new SidebarBadgeSnapshot();

            try
            {
                if (context.UserId > 0)
                {
                    snapshot.UnreadNotifications = NotificacionDAO.ContarNoLeidas(context.UserId);
                }
            }
            catch (Exception ex)
            {
                snapshot.UnreadNotifications = 0;
                LogSidebarCounterError(context, "NOTIFICACIONES", ex);
            }

            try
            {
                if (DebeCargarContadorRol(context, "rt"))
                {
                    var counterService = new AocrSidebarCounterService();
                    var solicitudes = counterService.ObtenerSolicitudesRtBase(context.UserId, context.EsAdministrador);
                    solicitudes = FiltrarSolicitudesPorContextoSidebar(context, solicitudes);
                    var contadoresRt = counterService.ObtenerContadoresRt(context.UserId, context.EsAdministrador, solicitudes);
                    snapshot.RtActiveRequests = contadoresRt.Activas;
                    snapshot.RtObservedRequests = contadoresRt.Observadas;
                    snapshot.RtPendingSubsanations = contadoresRt.PendientesSubsanacion;
                    var finales = new AocrBandejaDAO().ListarGeneradasFirmadas();
                    CargarContadoresDocumentosFinalesSidebar(context, finales, snapshot);
                }
            }
            catch (Exception ex)
            {
                snapshot.RtActiveRequests = 0;
                snapshot.RtObservedRequests = 0;
                snapshot.RtPendingSubsanations = 0;
                snapshot.RtFinalDocuments = 0;
                snapshot.RtAocrFirmadas = 0;
                snapshot.RtCondicionesFirmadas = 0;
                snapshot.RtDescargasFinales = 0;
                LogSidebarCounterError(context, "RT_SOLICITANTE", ex);
            }

            try
            {
                if (DebeCargarContadorRol(context, "inspector"))
                {
                    var roleContext = AocrUserContextService.ToBandejaRoleContext(AocrUserContextService.FromHttpContext(context.HttpContext));
                    var contadoresInspector = new AocrSidebarCounterService().ObtenerContadoresInspector(roleContext);
                    snapshot.InspectorPendingRevision = contadoresInspector.RevisionDocumental;
                    snapshot.InspectorTotalInspections = contadoresInspector.Total;
                    snapshot.InspectorAssignedInspections = contadoresInspector.Asignadas;
                    snapshot.InspectorInProgressInspections = contadoresInspector.EnFase;
                    snapshot.InspectorObservedInspections = contadoresInspector.Observadas;
                    snapshot.InspectorFinishedInspections = contadoresInspector.Finalizadas;
                }
            }
            catch (Exception ex)
            {
                snapshot.InspectorPendingRevision = 0;
                snapshot.InspectorTotalInspections = 0;
                snapshot.InspectorAssignedInspections = 0;
                snapshot.InspectorInProgressInspections = 0;
                snapshot.InspectorObservedInspections = 0;
                snapshot.InspectorFinishedInspections = 0;
                LogSidebarCounterError(context, "INSPECTOR_REVISION_DOCUMENTAL", ex);
            }

            try
            {
                if (DebeCargarContadorRol(context, "coordinacion"))
                {
                    var contadoresCoordinacion = new AocrSidebarCounterService().ObtenerContadoresCoordinacion();
                    snapshot.CoordinatorPendingAssignment = contadoresCoordinacion.PendientesAsignacion;
                    snapshot.CoordinatorDocumentalQueue = contadoresCoordinacion.ColaDocumental;
                    snapshot.CoordinatorAocrFormalReview = contadoresCoordinacion.RevisionFormalAocr;
                    var finales = new AocrBandejaDAO().ListarGeneradasFirmadas();
                    CargarContadoresDocumentosFinalesSidebar(context, finales, snapshot);
                    snapshot.CoordinatorFinalDocuments = snapshot.RtFinalDocuments;
                }
            }
            catch (Exception ex)
            {
                snapshot.CoordinatorPendingAssignment = 0;
                snapshot.CoordinatorDocumentalQueue = 0;
                snapshot.CoordinatorAocrFormalReview = 0;
                snapshot.CoordinatorFinalDocuments = 0;
                LogSidebarCounterError(context, "COORDINACION", ex);
            }

            try
            {
                if (DebeCargarContadorRol(context, "direccion"))
                {
                    var contadoresDireccion = new AocrSidebarCounterService().ObtenerContadoresDireccion();
                    snapshot.ExecutiveApprovalQueue = contadoresDireccion.BandejaEjecutivaAprobacion;
                    snapshot.DirdacPendingSignatures = contadoresDireccion.FirmasPendientesDirdac;
                }
            }
            catch (Exception ex)
            {
                snapshot.ExecutiveApprovalQueue = 0;
                snapshot.DirdacPendingSignatures = 0;
                LogSidebarCounterError(context, "DIRECCION_EJECUTIVA", ex);
            }

            try
            {
                if (DebeCargarContadorRol(context, "financiero"))
                {
                    var contadoresFinanciero = new AocrSidebarCounterService().ObtenerContadoresFinanciero();
                    snapshot.FinancialPendingReviewOrders = contadoresFinanciero.PendientesValidacion;
                    snapshot.FinancialUploadedPaymentOrders = contadoresFinanciero.PagosCargados;
                    snapshot.FinancialObservedOrders = contadoresFinanciero.Observadas;
                    snapshot.FinancialApprovedOrders = contadoresFinanciero.Aprobadas;
                    snapshot.FinancialFacturedOrders = contadoresFinanciero.Facturadas;
                    snapshot.FinancialHistoryOrders = contadoresFinanciero.Historial;
                    snapshot.FinancialPendingOrders = snapshot.FinancialPendingReviewOrders;
                }
            }
            catch (Exception ex)
            {
                snapshot.FinancialPendingOrders = 0;
                snapshot.FinancialPendingReviewOrders = 0;
                snapshot.FinancialUploadedPaymentOrders = 0;
                snapshot.FinancialObservedOrders = 0;
                snapshot.FinancialApprovedOrders = 0;
                snapshot.FinancialFacturedOrders = 0;
                snapshot.FinancialHistoryOrders = 0;
                LogSidebarCounterError(context, "FINANCIERO_GLOBAL", ex);
            }

            try
            {
                if (context.PuedeAprobarUsuarios)
                {
                    var pendientes = UsuarioDAO.ObtenerUsuariosPendientesDesignacion();
                    snapshot.AdminApprovalUsers = pendientes != null ? pendientes.Count : 0;
                }
            }
            catch (Exception ex)
            {
                snapshot.AdminApprovalUsers = 0;
                LogSidebarCounterError(context, "ADMIN_USUARIOS", ex);
            }

            LogSidebarCounters(context, snapshot, cacheSeconds, cacheScope);
            session[cacheKey] = snapshot;
            session[cacheTimeKey] = now;
            return snapshot;
        }

        private static void LoadInspectorInspectionCounters(SidebarBuildContext context, SidebarBadgeSnapshot snapshot)
        {
            var inspectorIds = ResolveInspectorFilterIds(context);
            var inspectorTextIds = ResolveInspectorFilterTextIds(context, inspectorIds);
            var dao = new InspeccionDAO();
            var inspeccionesBase = dao.ListarTodas() ?? new List<Inspeccion>();
            var inspeccionesAsignadas = inspectorIds
                .SelectMany(id => dao.ListarPorInspector(id) ?? new List<Inspeccion>())
                .Concat(inspeccionesBase.Where(ins => InspectorMatchesTextIdentifiers(ins, inspectorTextIds)))
                .Where(ins => ins != null && ins.CodigoInspeccion > 0)
                .GroupBy(ins => ins.CodigoInspeccion)
                .Select(group => group.OrderByDescending(ins => ins.UpdatedAt ?? DateTime.MinValue).First())
                .ToList();

            snapshot.InspectorTotalInspections = inspeccionesAsignadas.Count;
            snapshot.InspectorAssignedInspections = inspeccionesAsignadas.Count(ins => EsEstadoInspeccionAsignada(ins.Estado));
            snapshot.InspectorInProgressInspections = inspeccionesAsignadas.Count(ins => EsEstadoInspeccionEnFase(ins.Estado));
            snapshot.InspectorObservedInspections = inspeccionesAsignadas.Count(ins => EsEstadoInspeccionObservada(ins.Estado));
            snapshot.InspectorFinishedInspections = inspeccionesAsignadas.Count(ins => EsEstadoInspeccionFinalizada(ins.Estado));

            LogSidebarInspecciones(context, inspectorIds, inspeccionesBase, inspeccionesAsignadas, snapshot);
        }

        private static HashSet<int> ResolveInspectorFilterIds(SidebarBuildContext context)
        {
            var ids = new HashSet<int>();
            if (context != null && context.UserId > 0)
            {
                ids.Add(context.UserId);
            }

            int codigoUsuarioNumerico = 0;
            if (context != null && int.TryParse(context.CodigoUsuarioSesion, out codigoUsuarioNumerico) && codigoUsuarioNumerico > 0)
            {
                ids.Add(codigoUsuarioNumerico);
            }

            try
            {
                var usuarioRtDao = new UsuarioInternoRTDAO();
                UsuarioInternoRTRegistro inspectorActual = null;

                if (context != null && context.UserId > 0)
                {
                    inspectorActual = usuarioRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(context.UserId);
                }

                if (inspectorActual == null && context != null && !string.IsNullOrWhiteSpace(context.CodigoUsuarioSesion))
                {
                    inspectorActual = usuarioRtDao.ObtenerActivoPorCodigoUsuario(context.CodigoUsuarioSesion)
                        ?? usuarioRtDao.ObtenerInspectorAsignableActivo(context.CodigoUsuarioSesion);
                }

                if (inspectorActual == null && codigoUsuarioNumerico > 0)
                {
                    inspectorActual = usuarioRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(codigoUsuarioNumerico);
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
            catch (Exception ex)
            {
                LogSidebarCounterError(context, "INSPECTOR_IDS", ex);
            }

            return ids;
        }

        private static HashSet<string> ResolveInspectorFilterTextIds(SidebarBuildContext context, IEnumerable<int> inspectorIds)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (context != null)
            {
                AddTextId(ids, context.CodigoUsuarioSesion);
                AddTextId(ids, context.UserName);
            }

            foreach (var id in inspectorIds ?? Enumerable.Empty<int>())
            {
                if (id > 0)
                {
                    AddTextId(ids, id.ToString());
                }
            }

            return ids;
        }

        private static void AddTextId(HashSet<string> ids, string value)
        {
            if (ids == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            ids.Add(value.Trim());
        }

        private static bool InspectorMatchesTextIdentifiers(Inspeccion inspeccion, HashSet<string> inspectorTextIds)
        {
            if (inspeccion == null || inspectorTextIds == null || inspectorTextIds.Count == 0)
            {
                return false;
            }

            return TextIdentifierMatches(inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : null, inspectorTextIds)
                || TextIdentifierMatches(inspeccion.InspectorPrincipalCedula, inspectorTextIds)
                || TextIdentifierMatches(inspeccion.InspectorApoyoCedula, inspectorTextIds);
        }

        private static bool TextIdentifierMatches(string value, HashSet<string> inspectorTextIds)
        {
            return !string.IsNullOrWhiteSpace(value) && inspectorTextIds.Contains(value.Trim());
        }

        private static bool EsEstadoInspeccionAsignada(string estado)
        {
            var normalizado = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalizado, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.VERIFICACION_SOLICITUD, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.PAGO_VALIDADO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.VIATICOS_REQUERIDOS, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoInspeccionEnFase(string estado)
        {
            var normalizado = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalizado, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoInspeccionObservada(string estado)
        {
            var normalizado = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalizado, EstadosInspeccion.OBSERVADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.OBSERVACION_DOCUMENTAL, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.RESULTADO_NO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoInspeccionFinalizada(string estado)
        {
            var normalizado = EstadosInspeccion.NormalizarEstado(estado);
            return string.Equals(normalizado, EstadosInspeccion.RESULTADO_SATISFACTORIO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadosInspeccion.CERRADA, StringComparison.OrdinalIgnoreCase);
        }

        private static void LogSidebarInspecciones(
            SidebarBuildContext context,
            IEnumerable<int> inspectorIds,
            IList<Inspeccion> inspeccionesBase,
            IList<Inspeccion> inspeccionesAsignadas,
            SidebarBadgeSnapshot snapshot)
        {
            try
            {
                var lista = inspeccionesAsignadas ?? new List<Inspeccion>();
                CapaNegocio.LogBL.RegistrarInfo(
                    string.Format(
                        "[SIDEBAR][INSPECCIONES] Usuario={0}; RolActivo={1}; UserIdSesion={2}; CodigoUsuarioSesion={3}; CompaniaActiva={4}; IdsFiltroInspector={5}; TotalBaseInspecciones={6}; TotalFiltradoInspector={7}; TotalMisInspecciones={8}; TotalAsignadas={9}; TotalFaseInspeccion={10}; TotalObservaciones={11}; TotalFinalizadas={12}; EstadosEncontrados={13}; IdsInspeccionesEncontradas={14}",
                        context != null ? (context.UserName ?? string.Empty) : string.Empty,
                        context != null ? (context.RolActual ?? string.Empty) : string.Empty,
                        context != null ? context.UserId : 0,
                        context != null ? (context.CodigoUsuarioSesion ?? string.Empty) : string.Empty,
                        context != null ? (string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva) ? "No definida" : context.CodigoCompaniaActiva) : string.Empty,
                        string.Join(",", (inspectorIds ?? Enumerable.Empty<int>()).OrderBy(id => id)),
                        inspeccionesBase != null ? inspeccionesBase.Count : 0,
                        lista.Count,
                        snapshot.InspectorTotalInspections,
                        snapshot.InspectorAssignedInspections,
                        snapshot.InspectorInProgressInspections,
                        snapshot.InspectorObservedInspections,
                        snapshot.InspectorFinishedInspections,
                        string.Join(", ", lista.GroupBy(ins => EstadosInspeccion.NormalizarEstado(ins.Estado)).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count())),
                        string.Join(",", lista.Select(ins => ins.CodigoInspeccion).OrderBy(id => id))),
                    "SidebarMenuBuilder",
                    context != null && context.UserId > 0 ? (int?)context.UserId : null);
            }
            catch
            {
            }
        }

        private static void LogSidebarCounters(SidebarBuildContext context, SidebarBadgeSnapshot snapshot, int cacheSeconds, string cacheScope)
        {
            try
            {
                var companiaActiva = string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva)
                    ? "No definida"
                    : context.CodigoCompaniaActiva;

                CapaNegocio.LogBL.RegistrarInfo(
                    string.Format(
                        "[SIDEBAR_COUNTERS] UsuarioId={0}; Usuario={1}; Rol={2}; RolesRaw={3}; CompaniaActiva={4}; CacheSeconds={5}; CacheScope={6}; Notificaciones={7}; RtActivas={8}; RtObservadas={9}; RtSubsanaciones={10}; RtFinales={11}; RtAocrFirmadas={12}; RtCondicionesFirmadas={13}; RtDescargasFinales={14}; InspectorRevision={15}; CoordAsignacion={16}; CoordDocumental={17}; CoordFinales={18}; FinPendientes={19}; FinCargados={20}; FinObservados={21}; FinAprobados={22}; FinFacturasFr3={23}; FinHistorial={24}; DirdacFirmas={25}; EjecutivoAprobacion={26}; AdminUsuarios={27}",
                        context.UserId,
                        context.UserName ?? string.Empty,
                        context.RolActual ?? string.Empty,
                        string.Join(",", context.RolesRaw ?? new List<string>()),
                        companiaActiva,
                        cacheSeconds,
                        cacheScope ?? string.Empty,
                        snapshot.UnreadNotifications,
                        snapshot.RtActiveRequests,
                        snapshot.RtObservedRequests,
                        snapshot.RtPendingSubsanations,
                        snapshot.RtFinalDocuments,
                        snapshot.RtAocrFirmadas,
                        snapshot.RtCondicionesFirmadas,
                        snapshot.RtDescargasFinales,
                        snapshot.InspectorPendingRevision,
                        snapshot.CoordinatorPendingAssignment,
                        snapshot.CoordinatorDocumentalQueue,
                        snapshot.CoordinatorFinalDocuments,
                        snapshot.FinancialPendingReviewOrders,
                        snapshot.FinancialUploadedPaymentOrders,
                        snapshot.FinancialObservedOrders,
                        snapshot.FinancialApprovedOrders,
                        snapshot.FinancialFacturedOrders,
                        snapshot.FinancialHistoryOrders,
                        snapshot.DirdacPendingSignatures,
                        snapshot.ExecutiveApprovalQueue,
                        snapshot.AdminApprovalUsers),
                    "SidebarMenuBuilder",
                    context.UserId > 0 ? (int?)context.UserId : null);
            }
            catch
            {
            }
        }

        private static void LogSidebarCounterError(SidebarBuildContext context, string contador, Exception ex)
        {
            try
            {
                CapaNegocio.LogBL.RegistrarError(
                    string.Format(
                        "[SIDEBAR_COUNTERS][ERROR] Contador={0}; UsuarioId={1}; Rol={2}; CompaniaActiva={3}",
                        contador ?? string.Empty,
                        context != null ? context.UserId : 0,
                        context != null ? (context.RolActual ?? string.Empty) : string.Empty,
                        context != null ? (context.CodigoCompaniaActiva ?? string.Empty) : string.Empty),
                    ex != null ? ex.ToString() : null,
                    "SidebarMenuBuilder",
                    context != null && context.UserId > 0 ? (int?)context.UserId : null);
            }
            catch
            {
            }
        }

        private static string ResumirEstadosFinancieros(IEnumerable<FinancialSidebarCounterRow> estados)
        {
            try
            {
                return string.Join(", ", (estados ?? Enumerable.Empty<FinancialSidebarCounterRow>())
                    .GroupBy(o => (FinancialOrderStateHelper.ResolverEstadoOperativo(o.EstadoOrden, o.EstadoPago, o.TieneFactura) ?? "SIN_ESTADO").Trim())
                    .OrderBy(g => g.Key)
                    .Select(g => g.Key + ":" + g.Count()));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<SolicitudAOCR> FiltrarSolicitudesPorContextoSidebar(SidebarBuildContext context, IEnumerable<SolicitudAOCR> solicitudes)
        {
            var lista = (solicitudes ?? Enumerable.Empty<SolicitudAOCR>())
                .Where(s => s != null)
                .ToList();

            if (context == null || context.EsAdministrador || !context.EsSolicitanteORT)
            {
                return lista;
            }

            if (!string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva))
            {
                lista = lista
                    .Where(s => string.IsNullOrWhiteSpace(s.CompaniasSeleccionadas)
                        || ContieneValorListaSidebar(s.CompaniasSeleccionadas, context.CodigoCompaniaActiva))
                    .ToList();
            }

            return lista;
        }

        private static void CargarContadoresDocumentosFinalesSidebar(
            SidebarBuildContext context,
            IEnumerable<CapaModelo.Common.AocrBandejaDocumentoRow> filas,
            SidebarBadgeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var visibles = (filas ?? Enumerable.Empty<CapaModelo.Common.AocrBandejaDocumentoRow>())
                .Where(fila => DebeMostrarDocumentoFinalSidebar(context, fila))
                .GroupBy(fila => fila.SolicitudId)
                .Select(group => group.First())
                .ToList();

            snapshot.RtAocrFirmadas = visibles.Count(fila =>
                !AocrBandejaEstadoHelper.UsaFlujoCondiciones(fila)
                && AocrBandejaEstadoHelper.TieneDocumentoFinalFirmado(fila));

            snapshot.RtCondicionesFirmadas = visibles.Count(fila =>
                AocrBandejaEstadoHelper.UsaFlujoCondiciones(fila)
                && AocrBandejaEstadoHelper.TieneDocumentoFinalFirmado(fila));

            snapshot.RtDescargasFinales = visibles.Count(fila =>
                AocrBandejaEstadoHelper.TieneDocumentoFinalFirmado(fila)
                || AocrBandejaEstadoHelper.TieneDocumentoPreliminar(fila));

            snapshot.RtFinalDocuments = snapshot.RtDescargasFinales;
        }

        private static bool DebeMostrarDocumentoFinalSidebar(SidebarBuildContext context, CapaModelo.Common.AocrBandejaDocumentoRow fila)
        {
            if (context == null || fila == null || fila.SolicitudId <= 0)
            {
                return false;
            }

            if (context.EsAdministrador || context.EsCoordinadorRol || context.EsLegalRol || context.EsDirdacRol || context.EsDirectorGeneralRol)
            {
                return true;
            }

            if (context.EsSolicitanteORT)
            {
                if (context.UserId > 0 && fila.CodigoUsuario != context.UserId)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(context.CodigoCompaniaActiva) || string.IsNullOrWhiteSpace(fila.CompaniasSeleccionadas))
                {
                    return true;
                }

                return ContieneValorListaSidebar(fila.CompaniasSeleccionadas, context.CodigoCompaniaActiva);
            }

            if (context.EsInspectorRol)
            {
                return (fila.CodigoInspectorInspeccion.HasValue && fila.CodigoInspectorInspeccion.Value == context.UserId)
                    || (fila.CodigoInspectorSolicitud.HasValue && fila.CodigoInspectorSolicitud.Value == context.UserId);
            }

            return false;
        }

        private static bool ContieneValorListaSidebar(string valores, string buscado)
        {
            var objetivo = (buscado ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(valores) || string.IsNullOrWhiteSpace(objetivo))
            {
                return false;
            }

            return valores
                .Split(new[] { ',', ';', '|', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Any(x => string.Equals(x, objetivo, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsFinancialPendingReviewState(string estado)
        {
            return FinancialOrderStateHelper.EsPendienteGestion(estado, null, false);
        }

        private static bool IsFinancialObservedState(string estado)
        {
            return FinancialOrderStateHelper.EsObservada(estado, null);
        }

        private static bool IsFinancialApprovedState(string estado)
        {
            return FinancialOrderStateHelper.EsAprobadaOFacturada(estado, null, false);
        }

        private static SidebarStatusCardViewModel BuildOrderStatusCard(SidebarBuildContext context)
        {
            var card = new SidebarStatusCardViewModel { Visible = context.RequiereOrden };
            if (!card.Visible)
            {
                return card;
            }

            if (context.ExisteProcesoActivoCompania
                && !context.TieneAccesoSolicitudRt
                && !string.IsNullOrWhiteSpace(context.ProcesoActivoMensaje))
            {
                card.ToneClass = "info";
                card.IconClass = "fas fa-building";
                card.Title = "Proceso AOCR activo";
                card.Message = context.ProcesoActivoMensaje;
                if (!string.IsNullOrWhiteSpace(context.ProcesoActivoReferencia))
                {
                    card.Message += " Referencia: " + context.ProcesoActivoReferencia + ".";
                }

                card.LinkText = context.PuedeAbrirFormularioEmisionRt
                    ? "Continuar solicitud actual"
                    : (DebeMostrarSeguimientoSolicitudRt(context) ? "Ver seguimiento AOCR" : "Continuar proceso");
                card.LinkUrl = context.PuedeAbrirFormularioEmisionRt
                    ? context.Url.Action("FormularioEmisionAOCR", "SolicitudAOCR", ResolverRouteValuesSolicitudRtSidebar(context, 1))
                    : (DebeMostrarSeguimientoSolicitudRt(context)
                        ? context.Url.Action("Detalle", "SolicitudAOCR", new { id = context.CodigoSolicitudRtSeguimiento.Value })
                        : context.Url.Action("Index", "OrdenRecaudacion"));
                return card;
            }

            if (context.TieneAccesoSolicitudRt)
            {
                card.ToneClass = "success";
                card.IconClass = "fas fa-circle-check";
                card.Title = "Pago aprobado";
                card.Message = context.PuedeAbrirFormularioEmisionRt
                    ? "La Solicitud AOCR y la carga documental ya están habilitadas."
                    : (DebeMostrarSeguimientoSolicitudRt(context)
                        ? "La solicitud ya avanzó en el flujo AOCR. Consulte el seguimiento del trámite."
                        : "La Solicitud AOCR y la carga documental ya están habilitadas.");
                card.LinkText = context.PuedeAbrirFormularioEmisionRt
                    ? "Ir al formulario AOCR"
                    : (DebeMostrarSeguimientoSolicitudRt(context) ? "Ver seguimiento AOCR" : "Ir al flujo AOCR");
                card.LinkUrl = context.PuedeAbrirFormularioEmisionRt
                    ? context.Url.Action("FormularioEmisionAOCR", "SolicitudAOCR", ResolverRouteValuesSolicitudRtSidebar(context, 1))
                    : (DebeMostrarSeguimientoSolicitudRt(context)
                        ? context.Url.Action("Detalle", "SolicitudAOCR", new { id = context.CodigoSolicitudRtSeguimiento.Value })
                        : context.Url.Action("FormularioEmisionAOCR", "SolicitudAOCR", new { tipoSolicitud = 1 }));
                return card;
            }

            if (context.TieneOrdenPendienteProceso)
            {
                card.ToneClass = "warning";
                card.IconClass = "fas fa-file-invoice-dollar";
                card.Title = "Orden generada / pago pendiente";
                card.Message = context.TieneOrdenPendienteComprobante
                    ? "Todavía debe cargar el comprobante financiero para continuar."
                    : "La orden está creada, pero el pago aún no habilita el trámite AOCR.";
                card.LinkText = context.TieneOrdenPendienteComprobante ? "Cargar comprobante" : "Revisar mis órdenes";
                card.LinkUrl = context.Url.Action("Index", "OrdenRecaudacion");
                return card;
            }

            if (context.TieneOrdenBorrador)
            {
                card.ToneClass = "warning";
                card.IconClass = "fas fa-file-pen";
                card.Title = "Orden pendiente de generación";
                card.Message = "Existe una orden iniciada que debe completarse antes de continuar con AOCR.";
                card.LinkText = "Completar orden";
                card.LinkUrl = context.Url.Action("Obligatoria", "OrdenRecaudacion");
                return card;
            }

            card.ToneClass = "danger";
            card.IconClass = "fas fa-triangle-exclamation";
            card.Title = "Orden requerida";
            card.Message = "Debe generar la Orden de Recaudación para habilitar la Solicitud AOCR.";
            card.LinkText = "Generar orden";
            card.LinkUrl = context.Url.Action("Obligatoria", "OrdenRecaudacion");
            return card;
        }

        private static SidebarMenuGroupViewModel BuildInicioGroup(SidebarBuildContext context)
        {
            var group = NewGroup("inicio", "Inicio", "fas fa-compass", "Accesos de entrada, notificaciones y paneles según el rol activo.", "info");
            group.Expanded = true;

            group.Items.Add(CreateItem(context, "home", "Panel principal", "Acceso general al portal institucional AOCR.", "fas fa-house", "Home", "Index", null, null, "info", true, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "dashboard-inspector", "Dashboard técnico", "Resumen de inspecciones asignadas y alertas operativas.", "fas fa-gauge-high", "Dashboard", "Inspector", null, context.Badges.InspectorPendingRevision, "warning", context.EsInspectorRol || context.EsAdministrador, true, string.Empty, new[] { "Inspector" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "dashboard-coordinacion", "Bandeja integral", "Seguimiento integral de revisión documental, AOCR, firmas e inspección.", "fas fa-table-columns", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorDocumentalQueue, "info", context.EsCoordinadorRol || context.EsLegalRol || context.EsAdministrador, true, string.Empty, new[] { "DashboardInspeccion", "RevisionVerificacion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "dashboard-financiero", "Dashboard financiero", "Control de órdenes, pagos y aprobaciones financieras.", "fas fa-chart-line", "Financiero", "Dashboard", null, context.Badges.FinancialPendingReviewOrders, "warning", context.EsFinancieroRol || context.EsAdministrador, true, string.Empty, new[] { "Dashboard", "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "dashboard-direccion", "Dashboard dirección", "Vista ejecutiva de firmas, pendientes y documentos finales.", "fas fa-chart-area", "CoordinacionJefatura", "DashboardGerencial", null, context.Badges.DirdacPendingSignatures, "danger", context.EsDirdacRol || context.EsDirectorGeneralRol || context.EsAdministrador, true, string.Empty, new[] { "DashboardGerencial" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "dashboard-admin", "Panel global y salud", "Monitoreo del sistema, mantenimiento e integraciones.", "fas fa-heart-pulse", "Health", "Dashboard", null, 0, "neutral", context.EsAdministrador, true, string.Empty, new[] { "Dashboard" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "notificaciones", "Notificaciones", "Avisos del sistema, alertas y novedades del flujo AOCR.", "fas fa-bell", "Notificacion", "Index", null, context.Badges.UnreadNotifications, "info", true, true, string.Empty, new[] { "Index" }, null, null, string.Empty));

            string pendientesController = "SolicitudAOCR";
            string pendientesAction = "MisSolicitudes";
            int pendientesBadge = context.Badges.RtActiveRequests;
            bool pendientesVisible = context.EsSolicitanteORT || context.EsAdministrador;
            string pendientesTitle = "Mis pendientes";
            string pendientesDescription = "Mis trámites AOCR activos, observados o en proceso institucional.";
            string pendientesIcon = "fas fa-inbox";

            if (context.EsInspectorRol)
            {
                pendientesController = "RevisionDocumental";
                pendientesAction = "Index";
                pendientesBadge = context.Badges.InspectorPendingRevision;
                pendientesVisible = true;
                pendientesTitle = "Pendientes de revisión";
                pendientesDescription = "Documentación asignada al inspector para revisión documental.";
                pendientesIcon = "fas fa-file-check";
            }
            else if (context.EsCoordinadorRol || context.EsLegalRol)
            {
                pendientesController = "Tecnico";
                pendientesAction = "Index";
                pendientesBadge = context.Badges.CoordinatorPendingAssignment;
                pendientesVisible = true;
                pendientesTitle = "Pendientes de asignación";
                pendientesDescription = "Solicitudes que requieren designación, revisión o seguimiento coordinado.";
                pendientesIcon = "fas fa-user-plus";
            }
            else if (context.EsFinancieroRol)
            {
                pendientesController = "Financiero";
                pendientesAction = "Index";
                pendientesBadge = context.Badges.FinancialPendingReviewOrders;
                pendientesVisible = true;
                pendientesTitle = "Pagos pendientes";
                pendientesDescription = "Órdenes y comprobantes que esperan validación financiera.";
                pendientesIcon = "fas fa-file-invoice-dollar";
            }
            else if (context.EsDirdacRol || context.EsDirectorGeneralRol)
            {
                pendientesController = "Inspeccion";
                pendientesAction = "PendientesDireccion";
                pendientesBadge = context.Badges.DirdacPendingSignatures;
                pendientesVisible = true;
                pendientesTitle = "Pendientes de firma";
                pendientesDescription = "Informes técnicos y documentos que requieren revisión y firma institucional.";
                pendientesIcon = "fas fa-signature";
            }

            group.Items.Add(CreateItem(context, "pendientes", pendientesTitle, pendientesDescription, pendientesIcon, pendientesController, pendientesAction, null, pendientesBadge, "warning", pendientesVisible, true, string.Empty, new[] { pendientesAction, "Detalle" }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildFlujoGroup(SidebarBuildContext context)
        {
            var group = NewGroup("flujo", "Flujo AOCR", "fas fa-route", "Aquí inicia, continúa y cierra cada etapa del trámite institucional.", "primary");
            var ordenAction = context.TieneOrdenPendienteProceso ? "Index" : (context.TieneOrdenBorrador ? "Obligatoria" : "Nueva");
            var ordenTitle = context.TieneOrdenPendienteProceso ? "Continuar orden de recaudación" : (context.TieneOrdenBorrador ? "Completar orden de recaudación" : "Nueva orden de recaudación");

            group.Items.Add(CreateItem(context, "ordenes-rt", ordenTitle, "Gestión económica habilitante del trámite AOCR.", "fas fa-file-invoice-dollar", "OrdenRecaudacion", ordenAction, null, context.TieneOrdenPendienteProceso || context.TieneOrdenBorrador ? 1 : 0, "warning", context.EsSolicitanteORT || context.EsAdministrador, true, string.Empty, new[] { "Nueva", "Obligatoria", "Index", "Detalles" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "ordenes-financiero", "Órdenes de recaudación", "Consulta integral de órdenes para revisión, historial y seguimiento.", "fas fa-receipt", "Financiero", "TodasOrdenes", null, context.Badges.FinancialPendingReviewOrders + context.Badges.FinancialObservedOrders, "warning", context.EsFinancieroRol || context.EsAdministrador, true, string.Empty, new[] { "TodasOrdenes" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "solicitud-emision", context.PuedeAbrirFormularioEmisionRt ? "Solicitud AOCR" : (DebeMostrarSeguimientoSolicitudRt(context) ? "Seguimiento AOCR" : "Solicitud AOCR"), context.PuedeAbrirFormularioEmisionRt ? "Inicia o continúa la emisión o renovación formal de AOCR." : (DebeMostrarSeguimientoSolicitudRt(context) ? "Consulte el estado del trámite y continúe el flujo desde el detalle." : "Inicia o continúa la emisión o renovación formal de AOCR."), "fas fa-file-signature", "SolicitudAOCR", ResolverAccionSolicitudRtSidebar(context, "FormularioEmisionAOCR"), ResolverRouteValuesSolicitudRtSidebar(context, 1), context.Badges.RtActiveRequests, "info", context.EsSolicitanteORT || context.EsAdministrador, context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt || context.EsAdministrador, context.EsAdministrador || context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt ? string.Empty : context.MensajeBloqueoRtSidebar, ResolverActiveActionsSolicitudRtSidebar(context, "FormularioEmisionAOCR", "Index"), "tipoSolicitud", "1", string.Empty));
            group.Items.Add(CreateItem(context, "solicitud-renovacion", "Renovación AOCR", "Gestiona renovaciones con el mismo flujo documental institucional.", "fas fa-rotate", "SolicitudAOCR", "FormularioEmisionAOCR", new { tipoSolicitud = 2 }, 0, "neutral", context.EsSolicitanteORT || context.EsAdministrador, context.TieneAccesoSolicitudRt || context.EsAdministrador, context.EsAdministrador || context.TieneAccesoSolicitudRt ? string.Empty : context.MensajeBloqueoRtSidebar, new[] { "FormularioEmisionAOCR" }, "tipoSolicitud", "2", string.Empty));
            group.Items.Add(CreateItem(context, "modificacion", "Condiciones y limitaciones", "Solicitud formal de modificación operativa y regulatoria.", "fas fa-sliders", "SolicitudAOCR", "FormularioEmisionAOCR", new { tipoSolicitud = 3 }, context.Badges.RtObservedRequests, "warning", context.EsSolicitanteORT || context.EsAdministrador, context.TieneAccesoSolicitudRt || context.EsAdministrador, context.EsAdministrador || context.TieneAccesoSolicitudRt ? string.Empty : context.MensajeBloqueoRtSidebar, new[] { "FormularioEmisionAOCR", "Index" }, "tipoSolicitud", "3", string.Empty));
            group.Items.Add(CreateItem(context, "documentos-expediente", "Documentos y expediente", "Carga y organización documental del expediente AOCR.", "fas fa-folder-open", "Documento", "Subir", null, 0, "neutral", context.EsSolicitanteORT || context.EsAdministrador, true, string.Empty, new[] { "Subir", "Lista" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "revision-documental", "Revisión documental", "Validación documental previa a la fase técnica o de emisión.", "fas fa-file-check", context.EsInspectorRol ? "RevisionDocumental" : "CoordinacionJefatura", context.EsInspectorRol ? "Index" : "RevisionVerificacion", null, context.EsInspectorRol ? context.Badges.InspectorPendingRevision : context.Badges.CoordinatorDocumentalQueue, context.EsInspectorRol ? "warning" : "info", context.EsInspectorRol || context.EsCoordinadorRol || context.EsLegalRol || context.EsAdministrador, true, string.Empty, new[] { context.EsInspectorRol ? "Index" : "RevisionVerificacion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inspecciones", "Inspecciones", "Bandeja principal de ejecución, seguimiento y control operativo.", "fas fa-plane-departure", "Inspeccion", "Index", null, context.EsInspectorRol ? context.Badges.InspectorTotalInspections : 0, "neutral", context.EsInspectorRol || context.EsCoordinadorRol || context.EsAdministrador, true, string.Empty, new[] { "Index", "Detalle" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "lv-eae", "Lista de Verificación LV/EAE", "Checklist y evidencias de verificación operacional en inspección.", "fas fa-list-check", "Inspeccion", "Index", new { vista = "operativa" }, 0, "neutral", context.EsInspectorRol || context.EsCoordinadorRol || context.EsAdministrador, true, string.Empty, new[] { "Index" }, "vista", "operativa", string.Empty));
            group.Items.Add(CreateItem(context, "informe-nc", "Informe técnico y no conformidades", "Resultados, hallazgos, subsanaciones y cierre técnico del trámite.", "fas fa-file-signature", "Inspeccion", context.EsDirdacRol ? "PendientesDireccion" : "Index", null, context.EsDirdacRol ? context.Badges.DirdacPendingSignatures : 0, context.EsDirdacRol ? "danger" : "neutral", context.EsInspectorRol || context.EsCoordinadorRol || context.EsDirdacRol || context.EsAdministrador, true, string.Empty, new[] { context.EsDirdacRol ? "PendientesDireccion" : "Index", "Detalle" }, null, null, string.Empty));
            var aocrCondicionesBadge = context.EsSolicitanteORT
                ? context.Badges.RtDescargasFinales
                : (context.EsDirdacRol || context.EsCoordinadorRol || context.EsLegalRol || context.EsAdministrador
                    ? context.Badges.ExecutiveApprovalQueue + context.Badges.CoordinatorFinalDocuments
                    : 0);
            group.Items.Add(CreateItem(context, "aocr-condiciones", "AOCR y condiciones", "Revisión, validación, legalización y emisión final de documentos AOCR.", "fas fa-certificate", context.EsDirdacRol ? "Inspeccion" : (context.EsLegalRol ? "SolicitudAOCR" : "SolicitudAOCR"), context.EsDirdacRol ? "PendientesDireccion" : (context.EsLegalRol ? "RevisarLegalizacion" : "GeneradasFirmadas"), null, aocrCondicionesBadge, "info", context.EsSolicitanteORT || context.EsInspectorRol || context.EsCoordinadorRol || context.EsLegalRol || context.EsDirdacRol || context.EsAdministrador, true, string.Empty, new[] { context.EsDirdacRol ? "PendientesDireccion" : (context.EsLegalRol ? "RevisarLegalizacion" : "GeneradasFirmadas") }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "documentos-finales", "Documentos finales", "AOCR generadas, firmadas, condiciones emitidas y PDFs institucionales.", "fas fa-file-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtFinalDocuments > 0 ? context.Badges.RtFinalDocuments : context.Badges.CoordinatorFinalDocuments, "success", true, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildBandejasGroup(SidebarBuildContext context)
        {
            var group = NewGroup("bandejas", "Bandejas de trabajo", "fas fa-inbox", "Aquí encontrará pendientes, observados, subsanaciones, finalizados e histórico operativo.", "secondary");
            group.Items.Add(CreateItem(context, "mis-tramites", "Mis trámites", "Seguimiento de solicitudes AOCR, estados y trazabilidad del trámite.", "fas fa-folder-tree", "SolicitudAOCR", "MisSolicitudes", null, context.Badges.RtActiveRequests, "info", context.EsSolicitanteORT || context.EsAdministrador, true, string.Empty, new[] { "MisSolicitudes", "Detalle" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "revision-pendiente", "Pendientes de revisión", "Documentación que espera decisión técnica, coordinadora o legal.", "fas fa-hourglass-half", context.EsInspectorRol ? "RevisionDocumental" : "CoordinacionJefatura", context.EsInspectorRol ? "Index" : "RevisionVerificacion", null, context.EsInspectorRol ? context.Badges.InspectorPendingRevision : context.Badges.CoordinatorDocumentalQueue, "warning", context.EsInspectorRol || context.EsCoordinadorRol || context.EsLegalRol || context.EsAdministrador, true, string.Empty, new[] { context.EsInspectorRol ? "Index" : "RevisionVerificacion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "observados", "Observados y devueltos", "Trámites con observaciones activas o pendientes de corrección.", "fas fa-triangle-exclamation", context.EsSolicitanteORT ? "SolicitudAOCR" : "CoordinacionJefatura", context.EsSolicitanteORT ? "MisSolicitudes" : "DashboardInspeccion", null, context.Badges.RtObservedRequests, "danger", context.EsSolicitanteORT || context.EsCoordinadorRol || context.EsAdministrador, true, string.Empty, new[] { context.EsSolicitanteORT ? "MisSolicitudes" : "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "subsanaciones", "Subsanaciones", "Documentación observada que requiere nueva carga o verificación posterior.", "fas fa-screwdriver-wrench", context.EsSolicitanteORT ? "SolicitudAOCR" : "RevisionDocumental", context.EsSolicitanteORT ? "MisSolicitudes" : "Index", null, context.EsSolicitanteORT ? context.Badges.RtPendingSubsanations : context.Badges.InspectorPendingRevision, "warning", context.EsSolicitanteORT || context.EsInspectorRol || context.EsAdministrador, true, string.Empty, new[] { context.EsSolicitanteORT ? "MisSolicitudes" : "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "listos-firma", "Listos para firma", "Documentos que llegaron a fase de validación o firma institucional.", "fas fa-signature", context.EsDirdacRol ? "Inspeccion" : (context.EsLegalRol ? "SolicitudAOCR" : "CoordinacionJefatura"), context.EsDirdacRol ? "PendientesDireccion" : (context.EsLegalRol ? "RevisarLegalizacion" : "DashboardInspeccion"), null, context.EsDirdacRol ? context.Badges.DirdacPendingSignatures : context.Badges.ExecutiveApprovalQueue, "danger", context.EsCoordinadorRol || context.EsLegalRol || context.EsDirdacRol || context.EsAdministrador, true, string.Empty, new[] { context.EsDirdacRol ? "PendientesDireccion" : (context.EsLegalRol ? "RevisarLegalizacion" : "DashboardInspeccion") }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "finalizados", "Finalizados", "Consulta consolidada de documentos emitidos, firmados y concluidos.", "fas fa-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtFinalDocuments > 0 ? context.Badges.RtFinalDocuments : context.Badges.CoordinatorFinalDocuments, "success", true, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "historial", "Historial del trámite", "Histórico funcional del expediente y sus hitos documentales y técnicos.", "fas fa-clock-rotate-left", context.EsFinancieroRol ? "Financiero" : "SolicitudAOCR", context.EsFinancieroRol ? "TodasOrdenes" : "MisSolicitudes", null, 0, "neutral", context.EsSolicitanteORT || context.EsFinancieroRol || context.EsAdministrador, true, string.Empty, new[] { context.EsFinancieroRol ? "TodasOrdenes" : "MisSolicitudes", "Detalle" }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildFirmasGroup(SidebarBuildContext context)
        {
            var group = NewGroup("firmas", "Firmas y aprobación", "fas fa-file-signature", "Aquí revise, apruebe y firme documentos finales del flujo AOCR.", "danger");
            group.Items.Add(CreateItem(context, "informes-firma", "Informes pendientes de aprobación", "Revisión final de informes técnicos e hitos previos a la firma institucional.", "fas fa-file-signature", "Inspeccion", "PendientesDireccion", null, context.Badges.DirdacPendingSignatures, "danger", context.EsDirdacRol || context.EsDirectorGeneralRol || context.EsAdministrador, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-firma", "Firma institucional AOCR", "Documentos AOCR listos para validacion y firma institucional.", "fas fa-stamp", "Inspeccion", "PendientesDireccion", null, context.Badges.ExecutiveApprovalQueue, "warning", context.EsDirdacRol || context.EsDirectorGeneralRol || context.EsAdministrador, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "condiciones-firma", "Condiciones dentro de firma AOCR", "Condiciones y limitaciones gestionadas desde la firma institucional AOCR.", "fas fa-file-contract", "Inspeccion", "PendientesDireccion", null, context.Badges.ExecutiveApprovalQueue, "warning", context.EsDirdacRol || context.EsDirectorGeneralRol || context.EsAdministrador, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "legalizacion", "Legalización y firma DIRDAC", "Revisión jurídica, emisión y cierre formal del documento final.", "fas fa-gavel", context.EsLegalRol ? "SolicitudAOCR" : "Inspeccion", context.EsLegalRol ? "RevisarLegalizacion" : "PendientesDireccion", null, context.Badges.ExecutiveApprovalQueue, "info", context.EsLegalRol || context.EsDirdacRol || context.EsAdministrador, true, string.Empty, new[] { context.EsLegalRol ? "RevisarLegalizacion" : "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "firmados", "Documentos firmados", "Consulta de AOCR, condiciones e informes ya firmados y emitidos.", "fas fa-file-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "success", context.EsLegalRol || context.EsDirdacRol || context.EsAdministrador, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildFinancieroGroup(SidebarBuildContext context)
        {
            var group = NewGroup("financiero", "Financiero", "fas fa-money-bill-wave", "Aquí gestiona pagos, órdenes, reportes y trazabilidad económica.", "success");
            group.Items.Add(CreateItem(context, "fin-validar", "Pagos pendientes", "Validación de comprobantes y cierre financiero del trámite.", "fas fa-check-circle", "Financiero", "Index", new { estado = "PENDIENTES_FINANCIERO" }, context.Badges.FinancialPendingReviewOrders, "warning", context.EsFinancieroRol || context.EsAdministrador, true, string.Empty, new[] { "Index", "Dashboard" }, "estado", "PENDIENTES_FINANCIERO", string.Empty));
            group.Items.Add(CreateItem(context, "fin-ordenes", "Órdenes generadas", "Bandeja completa de órdenes, pagos observados y aprobados.", "fas fa-receipt", "Financiero", "TodasOrdenes", null, context.Badges.FinancialPendingReviewOrders + context.Badges.FinancialObservedOrders + context.Badges.FinancialApprovedOrders, "info", context.EsFinancieroRol || context.EsAdministrador, true, string.Empty, new[] { "TodasOrdenes" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "fin-reportes", "Facturas, FR3 y reportes", "Consulta analítica, exportable y documental de la recaudación.", "fas fa-file-invoice", "ReportesFinancieros", "Index", null, context.Badges.FinancialFacturedOrders, "success", context.EsFinancieroRol || context.EsAdministrador, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "fin-historial", "Historial financiero", "Histórico de órdenes, pagos procesados y resultados de validación.", "fas fa-clock-rotate-left", "Financiero", "TodasOrdenes", null, context.Badges.FinancialHistoryOrders, "neutral", context.EsFinancieroRol || context.EsAdministrador, true, string.Empty, new[] { "TodasOrdenes" }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildAdministracionGroup(SidebarBuildContext context)
        {
            var group = NewGroup("administracion", "Administración", "fas fa-user-cog", "Usuarios, roles, parámetros, auditoría funcional y servicios de soporte institucional.", "warning");
            group.Items.Add(CreateItem(context, "admin-usuarios", "Usuarios", "Gestión institucional de usuarios, perfiles y estados AOCR.", "fas fa-users-cog", "AdminUsuarios", "Index", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "Index", "Edit", "Create" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "admin-roles", "Roles y permisos", "Matriz de accesos y perfiles institucionales.", "fas fa-key", "AdminUsuarios", "PermisosRol", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "PermisosRol" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "admin-rt", "Usuarios internos RT", "Catálogo, creación y mantenimiento de inspectores internos RT.", "fas fa-id-badge", "AdminUsuarios", "ListarUsuariosInternosRT", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "ListarUsuariosInternosRT", "CrearUsuarioInternoRT", "EditarUsuarioInternoRT" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "admin-designaciones", "Designaciones RT", "Aprobación institucional de designaciones y constancias RT.", "fas fa-clipboard-check", "Usuario", "RevisarDesignaciones", null, context.Badges.AdminApprovalUsers, "warning", context.PuedeAprobarUsuarios, true, string.Empty, new[] { "RevisarDesignaciones" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "admin-parametros", "Parámetros y catálogos", "Configuración funcional del sistema y catálogos institucionales.", "fas fa-cogs", "Direccion", "ConfiguracionSistema", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "ConfiguracionSistema" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "admin-correos", "Cola y correos institucionales", "Mantenimiento de destinatarios y seguimiento de comunicaciones oficiales.", "fas fa-envelope-open-text", "CorreoInstitucional", "Index", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "Index", "Editar", "Historial" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "admin-health", "Monitoreo y logs funcionales", "Salud del sistema, integraciones y herramientas de soporte AOCR.", "fas fa-heart-pulse", "Health", "Dashboard", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "Dashboard" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "admin-sync", "Integraciones y sincronización", "Herramientas de resincronización del registro RT y servicios conectados.", "fas fa-sync-alt", "SyncAdmin", "Index", null, 0, "neutral", context.EsAdministrador, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildInicioMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("inicio-main", "Inicio", "fas fa-house", "Resumen general, pendientes, notificaciones y bandeja diaria del rol activo.", "info");
            group.Expanded = true;

            string panelController = "Home";
            string panelAction = "Index";
            object panelRouteValues = null;
            string panelDescription = "Vista general del portal institucional AOCR.";
            int? panelBadge = null;

            string workController = "SolicitudAOCR";
            string workAction = "MisSolicitudes";
            object workRouteValues = null;
            string workDescription = "Acceso directo a la bandeja operativa del rol actual.";
            int? workBadge = context.Badges.RtActiveRequests;

            string pendingController = "SolicitudAOCR";
            string pendingAction = "MisSolicitudes";
            object pendingRouteValues = null;
            string pendingDescription = "Sus trámites AOCR activos, observados o en proceso institucional.";
            int? pendingBadge = context.Badges.RtActiveRequests;

            if (context.EsInspectorRol)
            {
                panelController = "Dashboard";
                panelAction = "Inspector";
                panelDescription = "Resumen técnico de inspecciones, documentación e hitos pendientes.";
                panelBadge = context.Badges.InspectorPendingRevision;
                workController = "RevisionDocumental";
                workAction = "Index";
                workDescription = "Documentación pendiente y bandeja técnica de revisión.";
                workBadge = context.Badges.InspectorPendingRevision;
                pendingController = "RevisionDocumental";
                pendingAction = "Index";
                pendingDescription = "Documentación asignada para revisión documental e inspecciones por atender.";
                pendingBadge = context.Badges.InspectorPendingRevision;
            }
            else if (context.EsCoordinadorRol || context.EsLegalRol)
            {
                panelController = "CoordinacionJefatura";
                panelAction = "DashboardInspeccion";
                panelDescription = "Bandeja integral de coordinación, revisión documental y AOCR.";
                panelBadge = context.Badges.CoordinatorDocumentalQueue;
                workController = "Tecnico";
                workAction = "Index";
                workDescription = "Asignación de inspectores y control del trabajo en curso.";
                workBadge = context.Badges.CoordinatorPendingAssignment;
                pendingController = "CoordinacionJefatura";
                pendingAction = "RevisionVerificacion";
                pendingDescription = "Solicitudes y documentos que requieren verificación o revisión.";
                pendingBadge = context.Badges.CoordinatorDocumentalQueue;
            }
            else if (context.EsFinancieroRol)
            {
                panelController = "Financiero";
                panelAction = "Dashboard";
                panelDescription = "Vista financiera con pagos, órdenes y aprobaciones pendientes.";
                panelBadge = null;
                workController = "Financiero";
                workAction = "Index";
                workRouteValues = new { estado = "PENDIENTES_FINANCIERO" };
                workDescription = "Bandeja diaria de pagos, comprobantes y validaciones.";
                workBadge = null;
                pendingController = "Financiero";
                pendingAction = "Index";
                pendingRouteValues = new { estado = "PENDIENTES_FINANCIERO" };
                pendingDescription = "Pagos, comprobantes y observaciones pendientes de gestión.";
                pendingBadge = context.Badges.FinancialPendingReviewOrders;
            }
            else if (context.EsDirdacRol || context.EsDirectorGeneralRol)
            {
                panelController = "CoordinacionJefatura";
                panelAction = "DashboardGerencial";
                panelDescription = "Vista ejecutiva con documentos listos para aprobación y firma.";
                panelBadge = context.Badges.DirdacPendingSignatures;
                workController = "Inspeccion";
                workAction = "PendientesDireccion";
                workDescription = "Bandeja de informes y documentos pendientes de decisión institucional.";
                workBadge = context.Badges.DirdacPendingSignatures;
                pendingController = "Inspeccion";
                pendingAction = "PendientesDireccion";
                pendingDescription = "Informes y documentos pendientes de firma o devolución.";
                pendingBadge = context.Badges.DirdacPendingSignatures;
            }
            else if (context.EsAdministrador)
            {
                panelController = "Health";
                panelAction = "Dashboard";
                panelDescription = "Panel global del sistema con monitoreo operativo y funcional.";
                workController = "SolicitudAOCR";
                workAction = "RevisarPorJefatura";
                workDescription = "Bandeja amplia de trabajo para seguimiento transversal del sistema.";
                pendingController = "SolicitudAOCR";
                pendingAction = "RevisarPorJefatura";
                pendingDescription = "Solicitudes, revisiones y pendientes institucionales del sistema.";
                pendingBadge = context.Badges.ExecutiveApprovalQueue;
            }

            group.Items.Add(CreateItem(context, "inicio-panel", "Panel principal", panelDescription, "fas fa-house", panelController, panelAction, panelRouteValues, panelBadge, "info", true, true, string.Empty, new[] { panelAction }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inicio-pendientes", "Mis pendientes", pendingDescription, "fas fa-inbox", pendingController, pendingAction, pendingRouteValues, pendingBadge, "warning", true, true, string.Empty, new[] { pendingAction }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inicio-notificaciones", "Notificaciones", "Avisos del sistema, alertas y novedades del flujo AOCR.", "fas fa-bell", "Notificacion", "Index", null, context.Badges.UnreadNotifications, "info", true, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inicio-bandeja", "Bandeja de trabajo", workDescription, "fas fa-briefcase", workController, workAction, workRouteValues, workBadge, "neutral", true, true, string.Empty, new[] { workAction }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildOrdenesPagosMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("ordenes-pagos", "Órdenes y pagos", "fas fa-file-invoice-dollar", "Genere órdenes, cargue pagos y revise facturas o comprobantes del proceso.", "success");
            var ordenAction = context.TieneOrdenPendienteProceso ? "Index" : (context.TieneOrdenBorrador ? "Obligatoria" : "Nueva");
            var ordenTitle = context.TieneOrdenPendienteProceso ? "Continuar orden" : (context.TieneOrdenBorrador ? "Completar orden" : "Nueva orden");

            group.Items.Add(CreateItem(context, "op-rt-nueva", ordenTitle, "Generación o continuación de la orden habilitante del trámite AOCR.", "fas fa-file-circle-plus", "OrdenRecaudacion", ordenAction, null, context.TieneOrdenPendienteProceso || context.TieneOrdenBorrador ? 1 : 0, "warning", context.EsSolicitanteORT, true, string.Empty, new[] { "Nueva", "Obligatoria", "Index", "Detalles" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "op-rt-mis", "Mis órdenes", "Consulte sus órdenes de recaudación y el estado actual de pago.", "fas fa-receipt", "OrdenRecaudacion", "Index", null, 0, "neutral", context.EsSolicitanteORT, true, string.Empty, new[] { "Index", "Detalles" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "op-rt-comprobante", "Subir comprobante", "Carga o revisión del comprobante de pago asociado a su orden.", "fas fa-upload", "OrdenRecaudacion", "Index", null, context.TieneOrdenPendienteComprobante ? 1 : 0, "warning", context.EsSolicitanteORT, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "op-rt-observados", "Pagos observados", "Órdenes con observaciones o pendientes de corrección financiera.", "fas fa-triangle-exclamation", "OrdenRecaudacion", "Index", null, context.TieneOrdenPendienteComprobante ? 1 : 0, "danger", context.EsSolicitanteORT, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateDisabledItem(context, "op-rt-facturas", "Facturas y comprobantes", "Consulta documental de facturas y soportes económicos del trámite.", "fas fa-file-invoice", "Disponible desde el detalle de la orden o la gestión financiera asociada.", context.EsSolicitanteORT));
            group.Items.Add(CreateItem(context, "op-rt-historial", "Historial de pagos", "Histórico de órdenes, pagos procesados y revisiones realizadas.", "fas fa-clock-rotate-left", "OrdenRecaudacion", "Index", null, 0, "neutral", context.EsSolicitanteORT, true, string.Empty, new[] { "Index" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "op-fin-pendientes", "Pagos pendientes", "Pagos y comprobantes pendientes de validación financiera.", "fas fa-hourglass-half", "Financiero", "Index", new { estado = "PENDIENTES_FINANCIERO" }, context.Badges.FinancialPendingReviewOrders, "warning", context.EsFinancieroRol, true, string.Empty, new[] { "Index", "Dashboard" }, "estado", "PENDIENTES_FINANCIERO", string.Empty));
            group.Items.Add(CreateItem(context, "op-fin-cargados", "Pagos cargados", "Comprobantes recibidos y listos para revisión financiera.", "fas fa-cloud-upload-alt", "Financiero", "Index", new { estado = EstadoOrden.EnRevisionFinanciera }, context.Badges.FinancialUploadedPaymentOrders, "info", context.EsFinancieroRol, true, string.Empty, new[] { "Index" }, "estado", EstadoOrden.EnRevisionFinanciera, string.Empty));
            group.Items.Add(CreateItem(context, "op-fin-observados", "Pagos observados", "Casos con inconsistencias o requerimientos de ajuste financiero.", "fas fa-exclamation-circle", "Financiero", "Index", new { estado = EstadoOrden.Devuelta }, context.Badges.FinancialObservedOrders, "danger", context.EsFinancieroRol, true, string.Empty, new[] { "Index" }, "estado", EstadoOrden.Devuelta, string.Empty));
            group.Items.Add(CreateItem(context, "op-fin-aprobados", "Pagos aprobados", "Órdenes con validación financiera completada.", "fas fa-check-circle", "Financiero", "TodasOrdenes", new { estado = EstadoOrden.Facturada }, context.Badges.FinancialApprovedOrders, "success", context.EsFinancieroRol, true, string.Empty, new[] { "TodasOrdenes" }, "estado", EstadoOrden.Facturada, string.Empty));
            group.Items.Add(CreateItem(context, "op-fin-fr3", "Facturas / FR3", "Consulta de reportes, facturación y consolidado financiero.", "fas fa-file-invoice", "ReportesFinancieros", "Index", null, context.Badges.FinancialFacturedOrders, "success", context.EsFinancieroRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "op-fin-historial", "Historial financiero", "Histórico completo de órdenes, pagos y resultados de validación.", "fas fa-clock-rotate-left", "Financiero", "TodasOrdenes", null, context.Badges.FinancialHistoryOrders, "neutral", context.EsFinancieroRol, true, string.Empty, new[] { "TodasOrdenes" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "op-admin-todas", "Todas las órdenes", "Consulta transversal de órdenes, pagos y trazabilidad económica.", "fas fa-layer-group", "Financiero", "TodasOrdenes", null, context.Badges.FinancialHistoryOrders, "info", context.EsAdministrador, true, string.Empty, new[] { "TodasOrdenes" }, null, null, string.Empty));
            group.Items.Add(CreateDisabledItem(context, "op-admin-conceptos", "Conceptos de recaudación", "Mantenimiento institucional de conceptos y criterios económicos.", "fas fa-list-alt", "Disponible desde la parametrización funcional del sistema.", context.EsAdministrador));
            group.Items.Add(CreateItem(context, "op-admin-pagos", "Pagos y facturas", "Vista consolidada de pagos, comprobantes y reportes FR3.", "fas fa-file-invoice-dollar", "ReportesFinancieros", "Index", null, context.Badges.FinancialFacturedOrders, "success", context.EsAdministrador, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "op-admin-historial", "Historial financiero", "Seguimiento histórico económico para auditoría administrativa.", "fas fa-clock-rotate-left", "Financiero", "TodasOrdenes", null, context.Badges.FinancialHistoryOrders, "neutral", context.EsAdministrador, true, string.Empty, new[] { "TodasOrdenes" }, null, null, string.Empty));

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildSolicitudMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("solicitud-aocr", "Solicitud AOCR", "fas fa-file-signature", "Complete datos, dé seguimiento al trámite y gestione solicitudes observadas o finalizadas.", "primary");

            group.Items.Add(CreateItem(context, "sol-rt-registro", "Registro RT", "Registro y designación del Representante Técnico ante la DGAC.", "fas fa-id-card", "RT", "Registro", null, 0, "neutral", context.EsSolicitanteORT, true, string.Empty, new[] { "Registro", "Declaracion", "Designacion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-rt-nueva", context.PuedeAbrirFormularioEmisionRt ? "Nueva / continuar solicitud" : (DebeMostrarSeguimientoSolicitudRt(context) ? "Ver seguimiento AOCR" : "Nueva / continuar solicitud"), context.PuedeAbrirFormularioEmisionRt ? "Inicia o retoma la solicitud principal de emisión AOCR." : (DebeMostrarSeguimientoSolicitudRt(context) ? "Consulte el estado del trámite y continúe el flujo desde el detalle." : "Inicia o retoma la solicitud principal de emisión AOCR."), "fas fa-play-circle", "SolicitudAOCR", ResolverAccionSolicitudRtSidebar(context, "FormularioEmisionAOCR"), ResolverRouteValuesSolicitudRtSidebar(context, 1), context.Badges.RtActiveRequests, "info", context.EsSolicitanteORT, context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt, context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt ? string.Empty : context.MensajeBloqueoRtSidebar, ResolverActiveActionsSolicitudRtSidebar(context, "FormularioEmisionAOCR", "Index"), "tipoSolicitud", "1", string.Empty));
            group.Items.Add(CreateItem(context, "sol-rt-mis", "Mis solicitudes AOCR", "Consulta sus solicitudes activas, observadas y cerradas.", "fas fa-folder-tree", "SolicitudAOCR", "MisSolicitudes", null, context.Badges.RtActiveRequests, "neutral", context.EsSolicitanteORT, true, string.Empty, new[] { "MisSolicitudes" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-rt-completar", context.PuedeAbrirFormularioEmisionRt ? "Completar solicitud" : (DebeMostrarSeguimientoSolicitudRt(context) ? "Ver detalle de solicitud" : "Completar solicitud"), context.PuedeAbrirFormularioEmisionRt ? "Continúe el formulario y la carga requerida del trámite AOCR." : (DebeMostrarSeguimientoSolicitudRt(context) ? "La solicitud ya avanzó en el flujo AOCR. Consulte el detalle institucional del trámite." : "Continúe el formulario y la carga requerida del trámite AOCR."), "fas fa-pen-to-square", "SolicitudAOCR", ResolverAccionSolicitudRtSidebar(context, "FormularioEmisionAOCR"), ResolverRouteValuesSolicitudRtSidebar(context, 1), 0, "neutral", context.EsSolicitanteORT, context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt, context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt ? string.Empty : context.MensajeBloqueoRtSidebar, ResolverActiveActionsSolicitudRtSidebar(context, "FormularioEmisionAOCR"), "tipoSolicitud", "1", string.Empty));
            if (context.CodigoSolicitudRtSeguimiento.HasValue
                && context.CodigoSolicitudRtSeguimiento.Value > 0
                && context.PuedeAbrirFormularioEmisionRt)
            {
                group.Items.Add(CreateItem(context, "sol-rt-seguimiento", "Ver seguimiento del trámite", "Consulte el avance de la solicitud vinculada que ya no admite edición en el formulario.", "fas fa-route", "SolicitudAOCR", "Detalle", new { id = context.CodigoSolicitudRtSeguimiento.Value }, 0, "info", context.EsSolicitanteORT, true, string.Empty, new[] { "Detalle" }, null, null, string.Empty));
            }
            group.Items.Add(CreateDisabledItem(context, "sol-rt-enviar", "Enviar solicitud", "El envío formal se ejecuta dentro del formulario completo del trámite.", "fas fa-paper-plane", "Disponible al finalizar la solicitud AOCR desde su pantalla de edición.", context.EsSolicitanteORT));
            group.Items.Add(CreateItem(context, "sol-rt-observadas", "Solicitudes observadas", "Trámites que requieren corrección o atención del RT.", "fas fa-triangle-exclamation", "SolicitudAOCR", "MisSolicitudes", null, context.Badges.RtObservedRequests, "danger", context.EsSolicitanteORT, true, string.Empty, new[] { "MisSolicitudes" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "sol-coord-recibidas", "Solicitudes recibidas", "Bandeja institucional de solicitudes ingresadas para revisión.", "fas fa-inbox", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorDocumentalQueue, "info", context.EsCoordinadorRol || context.EsLegalRol || context.EsDirdacRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-coord-revision", "Solicitudes en revisión", "Casos en verificación documental, técnica o legal.", "fas fa-search", "CoordinacionJefatura", "RevisionVerificacion", null, context.Badges.CoordinatorDocumentalQueue, "warning", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "RevisionVerificacion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-coord-observadas", "Solicitudes observadas", "Solicitudes devueltas o con observaciones institucionales.", "fas fa-exclamation-circle", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.ExecutiveApprovalQueue, "danger", context.EsCoordinadorRol || context.EsLegalRol || context.EsDirdacRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-coord-finalizadas", "Solicitudes finalizadas", "Consulta de solicitudes cerradas y documentos emitidos.", "fas fa-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "success", context.EsCoordinadorRol || context.EsLegalRol || context.EsDirdacRol, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "sol-admin-recibidas", "Solicitudes recibidas", "Vista administrativa integral de solicitudes ingresadas al sistema.", "fas fa-folder-open", "SolicitudAOCR", "RevisarPorJefatura", null, context.Badges.ExecutiveApprovalQueue, "info", context.EsAdministrador, true, string.Empty, new[] { "RevisarPorJefatura" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-admin-revision", "Solicitudes en revisión", "Seguimiento transversal de solicitudes en análisis institucional.", "fas fa-clipboard-check", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorDocumentalQueue, "warning", context.EsAdministrador, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-admin-observadas", "Solicitudes observadas", "Control administrativo de solicitudes con devoluciones o ajustes.", "fas fa-triangle-exclamation", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.ExecutiveApprovalQueue, "danger", context.EsAdministrador, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "sol-admin-finalizadas", "Solicitudes finalizadas", "Consulta de solicitudes concluidas y documentación final emitida.", "fas fa-flag-checkered", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "success", context.EsAdministrador, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildDocumentosMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("documentos-main", "Documentos", "fas fa-folder-open", "Carga, revisión, subsanación y seguimiento del expediente documental AOCR.", "secondary");

            group.Items.Add(CreateItem(context, "doc-rt-cargar", "Cargar documentos", "Carga general de documentación del trámite AOCR.", "fas fa-upload", "Documento", "Subir", null, 0, "neutral", context.EsSolicitanteORT, true, string.Empty, new[] { "Subir" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-rt-observados", "Documentos observados", "Documentación devuelta con observaciones o ajustes pendientes.", "fas fa-triangle-exclamation", "SolicitudAOCR", "MisSolicitudes", null, context.Badges.RtObservedRequests, "danger", context.EsSolicitanteORT, true, string.Empty, new[] { "MisSolicitudes" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-rt-subsanar", "Subsanar documentos", "Correcciones documentales requeridas por revisión institucional.", "fas fa-screwdriver-wrench", "SolicitudAOCR", "MisSolicitudes", null, context.Badges.RtPendingSubsanations, "warning", context.EsSolicitanteORT, true, string.Empty, new[] { "MisSolicitudes" }, null, null, string.Empty));
            group.Items.Add(CreateDisabledItem(context, "doc-rt-expediente", "Expediente documental", "Consulta integral del expediente documental del trámite específico.", "fas fa-folder-tree", "Disponible desde el detalle de la solicitud específica; evita enlaces sin solicitudId.", context.EsSolicitanteORT));
            group.Items.Add(CreateDisabledItem(context, "doc-rt-inspeccion-ext", "Solicitud de inspecciones firmada", "Documento aplicable cuando la orden incluya INSPECCION_EXT.", "fas fa-file-signature", "Se gestiona dentro del expediente documental cuando el flujo INSPECCION_EXT aplica.", context.EsSolicitanteORT));

            group.Items.Add(CreateItem(context, "doc-inspector-revision", "Revisión documental", "Pantalla principal de revisión documental asignada al inspector.", "fas fa-file-check", "RevisionDocumental", "Index", null, context.Badges.InspectorPendingRevision, "warning", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-inspector-pendiente", "Documentación pendiente", "Documentos pendientes de revisión o validación técnica.", "fas fa-hourglass-half", "RevisionDocumental", "Index", null, context.Badges.InspectorPendingRevision, "warning", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-inspector-subsanada", "Documentación subsanada", "Subsanaciones recibidas para nueva verificación documental.", "fas fa-arrow-rotate-right", "RevisionDocumental", "Index", null, context.Badges.InspectorPendingRevision, "info", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-inspector-observada", "Documentos observados", "Documentos devueltos por hallazgos o inconsistencias.", "fas fa-exclamation-circle", "RevisionDocumental", "Index", null, context.Badges.InspectorPendingRevision, "danger", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "doc-coord-bandeja", "Bandeja documental", "Gestión integral de expedientes, observaciones y verificaciones documentales.", "fas fa-table-columns", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorDocumentalQueue, "info", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-coord-revisiones", "Revisiones pendientes", "Documentos y expedientes que requieren verificación institucional.", "fas fa-folder-open", "CoordinacionJefatura", "RevisionVerificacion", null, context.Badges.CoordinatorDocumentalQueue, "warning", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "RevisionVerificacion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-coord-observaciones", "Observaciones documentales", "Casos documentales devueltos o en ajuste por el operador.", "fas fa-triangle-exclamation", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.ExecutiveApprovalQueue, "danger", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateDisabledItem(context, "doc-coord-expedientes", "Expedientes", "Acceso al expediente documental completo del caso seleccionado.", "fas fa-folder-tree", "Disponible desde la pantalla de detalle o revisión del trámite específico.", context.EsCoordinadorRol || context.EsLegalRol));

            group.Items.Add(CreateItem(context, "doc-admin-bandeja", "Bandeja documental", "Vista administrativa del trabajo documental transversal del sistema.", "fas fa-briefcase", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorDocumentalQueue, "info", context.EsAdministrador, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-admin-revisiones", "Revisiones pendientes", "Control administrativo de revisiones documentales abiertas.", "fas fa-search", "CoordinacionJefatura", "RevisionVerificacion", null, context.Badges.CoordinatorDocumentalQueue, "warning", context.EsAdministrador, true, string.Empty, new[] { "RevisionVerificacion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "doc-admin-observadas", "Observaciones documentales", "Expedientes con devoluciones o inconsistencias documentales.", "fas fa-exclamation-circle", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.ExecutiveApprovalQueue, "danger", context.EsAdministrador, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateDisabledItem(context, "doc-admin-expedientes", "Expedientes", "Consulta del expediente documental completo por caso seleccionado.", "fas fa-folder-tree", "Disponible desde el caso específico para no romper rutas con parámetros requeridos.", context.EsAdministrador));

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildInspeccionesMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("inspecciones-main", "Inspecciones", "fas fa-plane-departure", "Asignación, ejecución, seguimiento y cierre del trabajo técnico de inspección.", "primary");

            group.Items.Add(CreateItem(context, "insp-mis", "Mis inspecciones", "Bandeja principal de inspecciones asignadas al inspector.", "fas fa-plane", "Inspeccion", "Index", null, context.Badges.InspectorTotalInspections, "neutral", context.EsInspectorRol, true, string.Empty, new[] { "Index", "Detalle" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "insp-asignadas", "Inspecciones asignadas", "Casos asignados al inspector para atención técnica.", "fas fa-user-check", "Inspeccion", "Index", new { estado = "ASIGNADA" }, context.Badges.InspectorAssignedInspections, "info", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, "estado", "ASIGNADA", string.Empty));
            group.Items.Add(CreateItem(context, "insp-ejecutar", "Fase inspección", "Ingreso al flujo operativo y al detalle de la inspección asignada.", "fas fa-play", "Inspeccion", "Index", new { estado = "EN_INSPECCION" }, context.Badges.InspectorInProgressInspections, "neutral", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, "estado", "EN_INSPECCION", string.Empty));
            group.Items.Add(CreateItem(context, "insp-observadas", "Inspecciones con observaciones", "Casos técnicos con observaciones o ajustes pendientes.", "fas fa-triangle-exclamation", "Inspeccion", "Index", new { estado = "OBSERVADA" }, context.Badges.InspectorObservedInspections, "danger", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, "estado", "OBSERVADA", string.Empty));
            group.Items.Add(CreateItem(context, "insp-finalizadas", "Inspecciones finalizadas", "Consulta de inspecciones concluidas y cerradas.", "fas fa-circle-check", "Inspeccion", "Index", new { estado = "FINALIZADA" }, context.Badges.InspectorFinishedInspections, "success", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, "estado", "FINALIZADA", string.Empty));

            group.Items.Add(CreateItem(context, "insp-coord-asignar", "Asignar inspector", "Designación y seguimiento de inspectores para cada solicitud.", "fas fa-user-plus", "Tecnico", "Index", null, context.Badges.CoordinatorPendingAssignment, "warning", context.EsCoordinadorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "insp-coord-curso", "Inspecciones en curso", "Seguimiento de inspecciones en ejecución y trabajo operativo activo.", "fas fa-route", "Inspeccion", "Index", null, 0, "info", context.EsCoordinadorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "insp-coord-pendientes", "Inspecciones pendientes", "Casos pendientes de planificación, ejecución o cierre técnico.", "fas fa-hourglass-half", "Inspeccion", "Index", null, 0, "warning", context.EsCoordinadorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "insp-coord-seguimiento", "Seguimiento de inspecciones", "Vista integral de seguimiento técnico y operacional.", "fas fa-chart-line", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorDocumentalQueue, "neutral", context.EsCoordinadorRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "insp-admin-todas", "Todas las inspecciones", "Consulta administrativa integral de las inspecciones del sistema.", "fas fa-layer-group", "Inspeccion", "Index", null, 0, "neutral", context.EsAdministrador, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "insp-admin-asignaciones", "Asignaciones", "Control administrativo de asignaciones y carga de trabajo técnica.", "fas fa-clipboard-list", "Tecnico", "Index", null, context.Badges.CoordinatorPendingAssignment, "warning", context.EsAdministrador, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "insp-admin-seguimiento", "Seguimiento general", "Vista consolidada de inspecciones, estados y pendientes técnicos.", "fas fa-chart-area", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorDocumentalQueue, "info", context.EsAdministrador, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildInformeTecnicoMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("informe-main", "Informe Técnico", "fas fa-file-lines", "LV/EAE, elaboración del informe técnico y seguimiento de revisión o aprobación.", "secondary");

            group.Items.Add(CreateItem(context, "inf-lv", "Lista de Verificación LV/EAE", "Checklist y evidencias operativas previas al informe técnico.", "fas fa-list-check", "Inspeccion", "Index", new { vista = "operativa" }, 0, "neutral", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, "vista", "operativa", string.Empty));
            group.Items.Add(CreateItem(context, "inf-crear", "Crear Informe Técnico", "Ingreso general al módulo de informes técnicos del proceso.", "fas fa-file-circle-plus", "Informe", "Index", null, 0, "info", context.EsInspectorRol, true, "Disponible después de firmar la LV cuando la inspección corresponda.", new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inf-borrador", "Informes en borrador", "Consulta o continuación de informes técnicos en elaboración.", "fas fa-pen-ruler", "Informe", "Index", null, 0, "neutral", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inf-firmados", "Informes firmados", "Consulta de informes técnicos ya firmados o emitidos.", "fas fa-file-circle-check", "Informe", "Index", null, 0, "success", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inf-observados", "Informes observados", "Casos con observaciones, ajustes o devolución técnica.", "fas fa-triangle-exclamation", "Informe", "Index", null, 0, "danger", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "inf-dir-pendientes", "Informes pendientes de revisión", "Informes técnicos listos para revisión o decisión institucional.", "fas fa-hourglass-half", "Inspeccion", "PendientesDireccion", null, context.Badges.DirdacPendingSignatures, "warning", context.EsDirdacRol || context.EsDirectorGeneralRol, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inf-dir-observados", "Informes observados", "Informes técnicos devueltos para corrección o ampliación.", "fas fa-exclamation-circle", "Inspeccion", "PendientesDireccion", null, context.Badges.DirdacPendingSignatures, "danger", context.EsDirdacRol || context.EsDirectorGeneralRol, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inf-dir-aprobados", "Informes aprobados", "Consulta de informes técnicos ya resueltos a nivel institucional.", "fas fa-circle-check", "Inspeccion", "PendientesDireccion", null, 0, "success", context.EsDirdacRol || context.EsDirectorGeneralRol, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "inf-admin-todos", "Todos los informes técnicos", "Consulta administrativa transversal de informes y su estado.", "fas fa-layer-group", "Informe", "Index", null, 0, "neutral", context.EsAdministrador, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "inf-admin-seguimiento", "Seguimiento de informes", "Vista consolidada de revisión, observación y aprobación de informes.", "fas fa-chart-line", "CoordinacionJefatura", "DashboardGerencial", null, context.Badges.DirdacPendingSignatures, "info", context.EsAdministrador, true, string.Empty, new[] { "DashboardGerencial" }, null, null, string.Empty));

            if (IsInformeTecnicoFlowActive(context))
            {
                group.Expanded = true;
            }

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildAocrCondicionesMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("aocr-condiciones-main", "AOCR y Condiciones", "fas fa-certificate", "Generación, revisión, descarga y seguimiento de AOCR y condiciones o limitaciones.", "primary");

            group.Items.Add(CreateItem(context, "aocr-ins-genera", "Generar AOCR", "Gestión de AOCR desde la fase técnica e inspección satisfactoria.", "fas fa-file-signature", "Inspeccion", "Index", null, 0, "info", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-ins-condiciones", "Generar Condiciones y Limitaciones", "Generación de condiciones desde el flujo técnico correspondiente.", "fas fa-file-contract", "Inspeccion", "Index", null, 0, "neutral", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-ins-devueltas", "AOCR devueltas para corrección", "Casos AOCR observados o devueltos para ajuste técnico.", "fas fa-rotate-left", "Inspeccion", "Index", null, 0, "danger", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-ins-enviadas", "AOCR enviadas", "AOCR remitidas a revisión coordinadora o firma institucional.", "fas fa-paper-plane", "Inspeccion", "Index", null, 0, "neutral", context.EsInspectorRol, true, string.Empty, new[] { "Index" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "aocr-coord-revisar", "Revisar AOCR", "Revisión coordinadora de AOCR y condiciones antes de firma.", "fas fa-search", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorAocrFormalReview, "warning", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-coord-modificar", "Solicitar modificación", "AOCR o condiciones que requieren ajuste previo a continuar el flujo.", "fas fa-pen-to-square", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorAocrFormalReview, "danger", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-coord-enviar", "Enviar a DIRDAC", "Remisión institucional de AOCR listos para firma o validación final.", "fas fa-share", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorAocrFormalReview, "info", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-coord-firma", "AOCR listas para firma", "Casos AOCR preparados para la fase final de firma institucional.", "fas fa-stamp", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.CoordinatorAocrFormalReview, "warning", context.EsCoordinadorRol || context.EsLegalRol, true, string.Empty, new[] { "DashboardInspeccion" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "aocr-rt-firmadas", "AOCR firmadas", "Consulta de AOCR emitidas y firmadas disponibles para el RT.", "fas fa-certificate", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtAocrFirmadas, "success", context.EsSolicitanteORT, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-rt-condiciones", "Condiciones firmadas", "Condiciones y limitaciones emitidas para descarga o consulta.", "fas fa-file-contract", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtCondicionesFirmadas, "success", context.EsSolicitanteORT, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-rt-descargar", "Descargar documentos finales", "Descarga de AOCR, condiciones y documentos finales emitidos.", "fas fa-download", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtDescargasFinales, "success", context.EsSolicitanteORT, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));

            group.Items.Add(CreateItem(context, "aocr-admin-firmadas", "AOCR generadas y firmadas", "Consulta integral de AOCR y condiciones finalizadas.", "fas fa-file-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "success", context.EsAdministrador, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-admin-todas", "Todas las AOCR", "Seguimiento administrativo global del universo de AOCR emitidas o en proceso.", "fas fa-layer-group", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "neutral", context.EsAdministrador, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-admin-condiciones", "Condiciones y Limitaciones", "Consulta de condiciones emitidas y control administrativo asociado.", "fas fa-file-contract", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "neutral", context.EsAdministrador, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "aocr-admin-seguimiento", "Seguimiento AOCR", "Vista gerencial y operativa del estado AOCR en todo el sistema.", "fas fa-chart-line", "CoordinacionJefatura", "DashboardGerencial", null, context.Badges.ExecutiveApprovalQueue, "info", context.EsAdministrador, true, string.Empty, new[] { "DashboardGerencial" }, null, null, string.Empty));

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildFirmasAprobacionesMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("firmas-aprobaciones", "Firmas y Aprobaciones", "fas fa-file-signature", "Flujo institucional final de informes, firma AOCR y documentos liberados.", "danger");
            var visibleDireccion = context.EsDirdacRol || context.EsDirectorGeneralRol || context.EsAdministrador;

            group.Items.Add(CreateItem(context, "firm-dir-informes", "Informes tecnicos por aprobar", "Informes tecnicos listos para revision institucional.", "fas fa-file-lines", "Inspeccion", "PendientesDireccion", null, context.Badges.DirdacPendingSignatures, "warning", visibleDireccion, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "firm-dir-aocr", "Firma institucional AOCR", "Entrada unica para completar datos, generar PDF oficial y firmar AOCR.", "fas fa-signature", "Inspeccion", "PendientesDireccion", null, context.Badges.ExecutiveApprovalQueue, "info", visibleDireccion, true, string.Empty, new[] { "PendientesDireccion" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "firm-dir-firmados", "Documentos firmados", "Consulta de AOCR y documentos finales firmados.", "fas fa-file-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "success", visibleDireccion, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "firm-dir-historial", "Historial institucional", "Seguimiento institucional de tramites y documentos finales.", "fas fa-clock-rotate-left", "Direccion", "DashboardGerencial", null, 0, "neutral", visibleDireccion, true, string.Empty, new[] { "DashboardGerencial" }, null, null, string.Empty));

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildHistorialMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("historial-main", "Historial", "fas fa-clock-rotate-left", "Consulta de trámites cerrados, observaciones, documentos finales y descargas.", "secondary");

            string historyController = "SolicitudAOCR";
            string historyAction = "MisSolicitudes";
            string historyDescription = "Historial del trámite AOCR del operador o RT.";
            int historyBadge = context.Badges.RtFinalDocuments;

            if (context.EsInspectorRol)
            {
                historyController = "Inspeccion";
                historyAction = "Index";
                historyDescription = "Historial de inspecciones, revisiones y seguimiento técnico.";
                historyBadge = 0;
            }
            else if (context.EsCoordinadorRol || context.EsLegalRol)
            {
                historyController = "CoordinacionJefatura";
                historyAction = "DashboardInspeccion";
                historyDescription = "Historial coordinador de casos, observaciones y seguimiento institucional.";
                historyBadge = context.Badges.CoordinatorFinalDocuments;
            }
            else if (context.EsFinancieroRol)
            {
                historyController = "Financiero";
                historyAction = "TodasOrdenes";
                historyDescription = "Historial financiero de órdenes, pagos y observaciones resueltas.";
                historyBadge = 0;
            }
            else if (context.EsDirdacRol || context.EsDirectorGeneralRol)
            {
                historyController = "SolicitudAOCR";
                historyAction = "GeneradasFirmadas";
                historyDescription = "Documentos finales y resoluciones emitidas por dirección.";
                historyBadge = context.Badges.CoordinatorFinalDocuments;
            }
            else if (context.EsAdministrador)
            {
                historyController = "SolicitudAOCR";
                historyAction = "GeneradasFirmadas";
                historyDescription = "Consulta administrativa global de finalizados, documentos y trazabilidad.";
                historyBadge = context.Badges.CoordinatorFinalDocuments;
            }

            group.Items.Add(CreateItem(context, "hist-tramites", "Historial de trámites", historyDescription, "fas fa-folder-tree", historyController, historyAction, null, historyBadge, "neutral", true, true, string.Empty, new[] { historyAction, "MisSolicitudes", "GeneradasFirmadas", "TodasOrdenes", "DashboardInspeccion", "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "hist-observaciones", "Observaciones", "Consulta de devoluciones, ajustes y observaciones registradas en el proceso.", "fas fa-triangle-exclamation", historyController, historyAction, null, 0, "danger", true, true, string.Empty, new[] { historyAction, "MisSolicitudes", "DashboardInspeccion", "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "hist-finales", "Documentos finales", "AOCR, condiciones y documentos finales listos para consulta o descarga.", "fas fa-file-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtFinalDocuments > 0 ? context.Badges.RtFinalDocuments : context.Badges.CoordinatorFinalDocuments, "success", true, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));
            group.Items.Add(CreateDisabledItem(context, "hist-auditoria", "Auditoría del trámite", "Seguimiento detallado del caso específico y su bitácora funcional.", "fas fa-shield-alt", "Disponible desde el detalle del trámite o desde pantallas administrativas de auditoría.", true));
            group.Items.Add(CreateItem(context, "hist-descargas", "Descargas", "Acceso directo a descargas y documentos emitidos del flujo AOCR.", "fas fa-download", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtFinalDocuments > 0 ? context.Badges.RtFinalDocuments : context.Badges.CoordinatorFinalDocuments, "success", true, true, string.Empty, new[] { "GeneradasFirmadas" }, null, null, string.Empty));

            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static SidebarMenuGroupViewModel BuildAdministracionMenuGroup(SidebarBuildContext context)
        {
            var group = NewGroup("administracion-main", "Administración", "fas fa-user-cog", "Usuarios, roles, catálogos, parámetros, auditoría general y servicios de soporte.", "warning");
            group.Items.Add(CreateItem(context, "adm-usuarios", "Usuarios", "Gestión institucional de usuarios y estados de acceso.", "fas fa-users-cog", "AdminUsuarios", "Index", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "Index", "Edit", "Create" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "adm-roles", "Roles", "Configuración de permisos y perfiles institucionales.", "fas fa-key", "AdminUsuarios", "PermisosRol", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "PermisosRol" }, null, null, string.Empty));
            group.Items.Add(CreateDisabledItem(context, "adm-catalogos", "Catálogos", "Catálogos funcionales y tablas maestras del sistema.", "fas fa-list-alt", "Disponible desde los módulos de parametrización y configuración institucional.", context.PuedeAdministracion));
            group.Items.Add(CreateDisabledItem(context, "adm-conceptos", "Conceptos de recaudación", "Administración de conceptos asociados a órdenes y pagos.", "fas fa-money-check-dollar", "Disponible desde la parametrización económica del sistema.", context.PuedeAdministracion));
            group.Items.Add(CreateItem(context, "adm-parametros", "Parámetros del sistema", "Configuración funcional y técnica del entorno AOCR.", "fas fa-cogs", "Direccion", "ConfiguracionSistema", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "ConfiguracionSistema" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "adm-correos", "Cola de correos", "Mantenimiento de destinatarios y comunicaciones institucionales.", "fas fa-envelope-open-text", "CorreoInstitucional", "Index", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "adm-correos-prueba", "Correos de prueba", "Ejecución controlada de plantillas reales hacia el buzón sandbox administrativo.", "fas fa-paper-plane", "Notificacion", "CorreosPrueba", null, 0, "neutral", context.EsAdministrador, true, string.Empty, new[] { "CorreosPrueba" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "adm-auditoria", "Auditoría general", "Monitoreo global, salud del sistema y trazabilidad funcional.", "fas fa-heart-pulse", "Health", "Dashboard", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "Dashboard" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "adm-logs", "Logs funcionales", "Herramientas de monitoreo, revisión y soporte institucional.", "fas fa-clipboard-list", "Health", "Dashboard", null, 0, "neutral", context.PuedeAdministracion, true, string.Empty, new[] { "Dashboard" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "adm-designaciones", "Designaciones RT", "Aprobación institucional de designaciones y constancias RT.", "fas fa-id-badge", "Usuario", "RevisarDesignaciones", null, context.Badges.AdminApprovalUsers, "warning", context.PuedeAprobarUsuarios, true, string.Empty, new[] { "RevisarDesignaciones" }, null, null, string.Empty));
            group.Items.Add(CreateItem(context, "adm-integraciones", "Integraciones y sincronización", "Herramientas de soporte e integración institucional.", "fas fa-sync-alt", "SyncAdmin", "Index", null, 0, "neutral", context.EsAdministrador, true, string.Empty, new[] { "Index" }, null, null, string.Empty));
            group.Visible = group.Items.Any(item => item.Visible);
            return group;
        }

        private static IList<SidebarMenuItemViewModel> BuildQuickActions(SidebarBuildContext context)
        {
            var actions = new List<SidebarMenuItemViewModel>();

            if (context.EsSolicitanteORT)
            {
                var bloquearNuevaOrden = context.ExisteProcesoActivoCompania
                    && !context.TieneOrdenBorrador
                    && !context.TieneOrdenPendienteProceso;
                var ordenTitle = context.TieneSolicitudRtHabilitada
                    ? "Ver orden aprobada"
                    : (context.TieneOrdenPendienteProceso
                        ? "Continuar orden"
                        : (context.TieneOrdenBorrador ? "Completar orden" : (bloquearNuevaOrden ? "Nueva orden (bloqueada)" : "Nueva orden")));
                var ordenAction = context.TieneSolicitudRtHabilitada
                    ? "Index"
                    : (context.TieneOrdenPendienteProceso
                        ? "Index"
                        : (context.TieneOrdenBorrador ? "Obligatoria" : "Nueva"));
                actions.Add(CreateQuickAction(
                    context,
                    "qa-orden",
                    ordenTitle,
                    context.TieneSolicitudRtHabilitada ? "fas fa-file-circle-check" : "fas fa-file-invoice-dollar",
                    "OrdenRecaudacion",
                    ordenAction,
                    null,
                    context.TieneOrdenPendienteProceso || context.TieneOrdenBorrador ? 1 : 0,
                    bloquearNuevaOrden && !context.TieneSolicitudRtHabilitada ? "neutral" : "warning",
                    !bloquearNuevaOrden || context.TieneOrdenPendienteProceso || context.TieneOrdenBorrador || context.TieneSolicitudRtHabilitada,
                    !bloquearNuevaOrden || context.TieneOrdenPendienteProceso || context.TieneOrdenBorrador || context.TieneSolicitudRtHabilitada,
                    bloquearNuevaOrden && !context.TieneSolicitudRtHabilitada ? AocrProcesoActivoService.MensajeBloqueoGenerico : string.Empty));
                actions.Add(CreateQuickAction(context, "qa-solicitud", context.PuedeAbrirFormularioEmisionRt || context.TieneSolicitudRtHabilitada ? "Continuar Solicitud AOCR" : (DebeMostrarSeguimientoSolicitudRt(context) ? "Ver seguimiento AOCR" : "Continuar solicitud"), context.PuedeAbrirFormularioEmisionRt || context.TieneSolicitudRtHabilitada ? "fas fa-file-signature" : (DebeMostrarSeguimientoSolicitudRt(context) ? "fas fa-route" : "fas fa-file-signature"), "SolicitudAOCR", ResolverAccionSolicitudRtSidebar(context, "FormularioEmisionAOCR"), ResolverRouteValuesSolicitudRtSidebar(context, 1), context.Badges.RtActiveRequests, "info", true, context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt || context.TieneSolicitudRtHabilitada || context.EsAdministrador, context.EsAdministrador || context.TieneAccesoSolicitudRt || context.PuedeAbrirFormularioEmisionRt || context.TieneSolicitudRtHabilitada ? string.Empty : context.MensajeBloqueoRtSidebar));
                actions.Add(CreateQuickAction(context, "qa-subsanar", "Ver subsanaciones", "fas fa-screwdriver-wrench", "SolicitudAOCR", "MisSolicitudes", null, context.Badges.RtPendingSubsanations, "warning", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-finales", "Descargar final", "fas fa-file-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.RtFinalDocuments, "success", true, true, string.Empty));
            }

            if (context.EsInspectorRol)
            {
                actions.Add(CreateQuickAction(context, "qa-revision", "Revisar documentos", "fas fa-file-check", "RevisionDocumental", "Index", null, context.Badges.InspectorPendingRevision, "warning", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-inspecciones", "Mis inspecciones", "fas fa-plane-departure", "Inspeccion", "Index", null, context.Badges.InspectorTotalInspections, "neutral", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-informe", "Informe Técnico", "fas fa-file-lines", "Informe", "Index", null, 0, "neutral", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-aocr-corregir", "AOCR por corregir", "fas fa-certificate", "Inspeccion", "Index", new { estado = "OBSERVADA" }, context.Badges.InspectorObservedInspections, "warning", true, true, string.Empty));
            }

            if (context.EsCoordinadorRol || context.EsLegalRol)
            {
                actions.Add(CreateQuickAction(context, "qa-asignar", "Asignar inspector", "fas fa-user-plus", "Tecnico", "Index", null, context.Badges.CoordinatorPendingAssignment, "warning", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-aocr", "Revisar AOCR", "fas fa-certificate", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.ExecutiveApprovalQueue, "warning", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-enviar-dirdac", "Enviar a DIRDAC", "fas fa-share", "CoordinacionJefatura", "DashboardInspeccion", null, context.Badges.ExecutiveApprovalQueue, "info", true, true, string.Empty));
            }

            if (context.EsDirdacRol || context.EsDirectorGeneralRol)
            {
                actions.Add(CreateQuickAction(context, "qa-firma", "Firmar pendientes", "fas fa-signature", "Inspeccion", "PendientesDireccion", null, context.Badges.DirdacPendingSignatures, "danger", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-firmados", "Documentos firmados", "fas fa-file-circle-check", "SolicitudAOCR", "GeneradasFirmadas", null, context.Badges.CoordinatorFinalDocuments, "success", true, true, string.Empty));
            }

            if (context.EsFinancieroRol)
            {
                actions.Add(CreateQuickAction(context, "qa-pagos", "Revisar pagos", "fas fa-money-check-dollar", "Financiero", "Index", new { estado = "PENDIENTES_FINANCIERO" }, context.Badges.FinancialPendingReviewOrders, "warning", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-observados-fin", "Pagos observados", "fas fa-triangle-exclamation", "Financiero", "Index", new { estado = EstadoOrden.Devuelta }, context.Badges.FinancialObservedOrders, "danger", true, true, string.Empty));
            }

            if (context.EsAdministrador)
            {
                actions.Add(CreateQuickAction(context, "qa-admin-dashboard", "Dashboard global", "fas fa-chart-area", "Health", "Dashboard", null, 0, "neutral", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-admin-solicitudes", "Todas las solicitudes", "fas fa-folder-tree", "SolicitudAOCR", "RevisarPorJefatura", null, context.Badges.ExecutiveApprovalQueue, "info", true, true, string.Empty));
                actions.Add(CreateQuickAction(context, "qa-admin-auditoria", "Auditoría", "fas fa-shield-alt", "Health", "Dashboard", null, 0, "neutral", true, true, string.Empty));
            }

            return actions.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        }

        private static SidebarMenuItemViewModel CreateDisabledItem(
            SidebarBuildContext context,
            string key,
            string title,
            string description,
            string iconClass,
            string tooltip,
            bool visible,
            int? badgeCount = null,
            string badgeTone = "neutral")
        {
            var item = CreateItem(context, key, title, description, iconClass, "Home", "Index", null, badgeCount, badgeTone, visible, false, tooltip, null, null, null, string.Empty);
            item.Url = string.Empty;
            item.IsActive = false;
            return item;
        }

        private static SidebarMenuGroupViewModel NewGroup(string key, string title, string iconClass, string description, string accentClass)
        {
            return new SidebarMenuGroupViewModel
            {
                Key = key,
                Title = title,
                IconClass = iconClass,
                Description = description,
                AccentClass = accentClass,
                CollapseId = "sidebar-group-" + key,
                Visible = true
            };
        }

        private static SidebarMenuItemViewModel CreateQuickAction(
            SidebarBuildContext context,
            string key,
            string title,
            string iconClass,
            string controller,
            string action,
            object routeValues,
            int badgeCount,
            string badgeTone,
            bool visible,
            bool enabled,
            string tooltip)
        {
            var item = CreateItem(context, key, title, string.Empty, iconClass, controller, action, routeValues, badgeCount, badgeTone, visible, enabled, tooltip, new[] { action }, null, null, string.Empty);
            item.IsQuickAction = true;
            return item;
        }

        private static SidebarMenuItemViewModel CreateItem(
            SidebarBuildContext context,
            string key,
            string title,
            string description,
            string iconClass,
            string controller,
            string action,
            object routeValues,
            int? badgeCount,
            string badgeTone,
            bool visible,
            bool enabled,
            string tooltip,
            string[] activeActions,
            string matchQueryKey,
            string matchQueryValue,
            string matchFragment,
            string cssClass = "")
        {
            return new SidebarMenuItemViewModel
            {
                Key = key,
                Title = title,
                Description = description,
                IconClass = iconClass,
                Url = context.Url.Action(action, controller, routeValues),
                Visible = visible,
                Enabled = enabled,
                BadgeCount = badgeCount.HasValue ? badgeCount.Value : 0,
                ShowBadge = badgeCount.HasValue,
                BadgeToneClass = string.IsNullOrWhiteSpace(badgeTone) ? "neutral" : badgeTone,
                Tooltip = tooltip,
                CssClass = cssClass,
                IsActive = IsItemActive(context, controller, activeActions, matchQueryKey, matchQueryValue, matchFragment)
            };
        }

        private static bool IsItemActive(SidebarBuildContext context, string controller, string[] actions, string matchQueryKey, string matchQueryValue, string matchFragment)
        {
            if (!string.Equals(context.CurrentController, controller, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (actions != null && actions.Length > 0)
            {
                var actionMatch = false;
                foreach (var action in actions)
                {
                    if (string.Equals(context.CurrentAction, action, StringComparison.OrdinalIgnoreCase))
                    {
                        actionMatch = true;
                        break;
                    }
                }

                if (!actionMatch)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(matchQueryKey))
            {
                var requestValue = Convert.ToString(context.HttpContext.Request[matchQueryKey] ?? string.Empty);
                if (!string.Equals(requestValue, matchQueryValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(matchFragment) && !string.Equals(context.CurrentFragment ?? string.Empty, matchFragment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool IsInformeTecnicoFlowActive(SidebarBuildContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.CurrentController))
            {
                return false;
            }

            if (string.Equals(context.CurrentController, "Informe", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(context.CurrentController, "Inspeccion", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var informeActions = new[]
            {
                "ModalInformeTecnico",
                "GuardarInformeTecnico",
                "PrevisualizarInformeTecnico",
                "FinalizarInformeTecnico",
                "VerPreviewInformeTecnico",
                "VerInforme",
                "DescargarInforme"
            };

            return informeActions.Any(action => string.Equals(context.CurrentAction, action, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveCompanyText(SidebarBuildContext context, string[] names)
        {
            if (names == null)
            {
                return string.Empty;
            }

            foreach (var name in names)
            {
                var value = Convert.ToString(context.ViewData[name] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return ReadPropertyText(context.Model, names);
        }

        private static string ReadPropertyText(object source, string[] names)
        {
            if (source == null || names == null)
            {
                return string.Empty;
            }

            var sourceType = source.GetType();
            foreach (var name in names)
            {
                var property = sourceType.GetProperty(name);
                if (property == null)
                {
                    continue;
                }

                try
                {
                    var rawValue = property.GetValue(source, null);
                    var textValue = Convert.ToString(rawValue ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(textValue))
                    {
                        return textValue;
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static List<string> ParseLegacyCompanies(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            return raw
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => (item ?? string.Empty).Trim().ToUpperInvariant())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int ParseUserId(object rawValue)
        {
            int userId;
            return rawValue != null && int.TryParse(rawValue.ToString(), out userId) ? userId : 0;
        }

        private static bool DebeCargarContadorRol(SidebarBuildContext context, string modulo)
        {
            if (context == null || string.IsNullOrWhiteSpace(modulo))
            {
                return false;
            }

            var rolActivo = RoleGroupingHelper.NormalizeSelectedRole(context.RolActual ?? string.Empty);
            if (context.EsAdministrador
                && (string.IsNullOrWhiteSpace(rolActivo) || RoleGroupingHelper.IsAdministrador(rolActivo)))
            {
                return true;
            }

            switch (modulo.Trim().ToLowerInvariant())
            {
                case "rt":
                    return context.EsSolicitanteORT;
                case "inspector":
                    return context.EsInspectorRol || RoleGroupingHelper.IsInspectorTecnico(rolActivo);
                case "coordinacion":
                    return context.EsCoordinadorRol || context.EsLegalRol || RoleGroupingHelper.IsCoordinacion(rolActivo);
                case "direccion":
                    return context.EsDirdacRol
                        || context.EsDirectorGeneralRol
                        || RoleGroupingHelper.IsDireccionJefaturaTecnica(rolActivo);
                case "financiero":
                    return context.EsFinancieroRol || RoleGroupingHelper.IsFinanciero(rolActivo);
                default:
                    return false;
            }
        }

        private static bool IsOpenWorkflowState(string estado)
        {
            var normalized = EstadoSolicitud.Normalizar(estado);
            return !string.IsNullOrWhiteSpace(normalized)
                && !string.Equals(normalized, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "ANULADA", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "CANCELADA", StringComparison.OrdinalIgnoreCase);
        }
    }
}
