using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using CapaDatos.Services;
using CapaUtilidades;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;
using System.Web.Routing;
using AocrAuthorizationContextType = CapaNegocio.Services.AocrAuthorizationContext;
using AocrAuthorizationResultType = CapaNegocio.Services.AocrAuthorizationResult;
using AocrAuthorizationServiceType = CapaNegocio.Services.AocrAuthorizationService;

namespace CapaPresentacion.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var response = filterContext.HttpContext.Response;

            response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            response.Cache.SetValidUntilExpires(false);
            response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();

            response.AppendHeader("Pragma", "no-cache");
            response.AppendHeader("Expires", "0");

            base.OnResultExecuting(filterContext);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controller = filterContext.Controller;
            var modelState = controller?.ViewData?.ModelState;

            if (modelState != null && !modelState.IsValid)
            {
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    var errors = modelState
                        .Where(kvp => kvp.Value != null && kvp.Value.Errors != null && kvp.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.Select(e =>
                                string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Error de validación." : e.ErrorMessage
                            ).ToArray()
                        );

                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, errors },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                    return;
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class AuthorizeRolesAttribute : AuthorizeAttribute
    {
        public AuthorizeRolesAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles ?? new string[0]);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary {
                        { "controller", "Error" },
                        { "action", "AccesoDenegado" }
                    });
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class AocrAuthorizeAttribute : AuthorizeAttribute
    {
        private static readonly ILoggingService Logger = LoggingServiceFactory.Create();
        private static readonly IUserContextAccessor UserContextAccessor = new UserContextAccessor();

        public string Modulo { get; set; }
        public string Accion { get; set; }
        public bool RequireCompanySelection { get; set; }
        public string CodigoSolicitudParameter { get; set; }
        public string CodigoInspeccionParameter { get; set; }
        public string CodigoOrdenParameter { get; set; }
        public string CodigoInformeParameter { get; set; }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                return false;
            }

            if (httpContext.User == null || httpContext.User.Identity == null || !httpContext.User.Identity.IsAuthenticated)
            {
                httpContext.Items["AOCR_AUTH_RESULT"] = AocrAuthorizationResultType.Denied(Modulo ?? string.Empty, Accion ?? string.Empty, "La sesión expiró o no ha iniciado sesión.");
                return false;
            }

            var bootstrapStatus = AuthenticatedSessionBootstrapper.EnsureSession(httpContext);
            if (bootstrapStatus == AuthenticatedSessionBootstrapStatus.Failed)
            {
                httpContext.Items["AOCR_AUTH_RESULT"] = AocrAuthorizationResultType.Denied(Modulo ?? string.Empty, Accion ?? string.Empty, "No se pudo restaurar la sesión autenticada.");
                return false;
            }

            if (!base.AuthorizeCore(httpContext))
            {
                httpContext.Items["AOCR_AUTH_RESULT"] = AocrAuthorizationResultType.Denied(Modulo ?? string.Empty, Accion ?? string.Empty, "No tiene permisos para acceder a este módulo.");
                return false;
            }

            var authResult = EvaluarAutorizacion(httpContext);
            httpContext.Items["AOCR_AUTH_RESULT"] = authResult;
            return authResult.Permitido;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext == null || filterContext.HttpContext == null)
            {
                base.HandleUnauthorizedRequest(filterContext);
                return;
            }

            var httpContext = filterContext.HttpContext;
            var request = httpContext.Request;
            var response = httpContext.Response;
            var isAuthenticated = httpContext.User != null
                && httpContext.User.Identity != null
                && httpContext.User.Identity.IsAuthenticated;
            var isAjax = IsAjaxLikeRequest(request);
            var authResult = httpContext.Items["AOCR_AUTH_RESULT"] as AocrAuthorizationResultType;
            var returnUrl = request != null
                ? (request.RawUrl ?? (request.Url != null ? request.Url.PathAndQuery : string.Empty))
                : string.Empty;

            LogUnauthorizedAttempt(httpContext, isAjax, isAuthenticated, returnUrl, authResult);

            if (authResult != null && authResult.RequiereSeleccionCompania && isAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new
                    {
                        controller = "Account",
                        action = "SeleccionarCompania",
                        returnUrl
                    }));
                return;
            }

            if (isAjax)
            {
                var statusCode = isAuthenticated ? 403 : 401;
                response.StatusCode = statusCode;
                response.TrySkipIisCustomErrors = true;
                response.SuppressFormsAuthenticationRedirect = true;

                filterContext.Result = new JsonResult
                {
                    Data = new
                    {
                        success = false,
                        code = statusCode,
                        requiresLogin = !isAuthenticated,
                        redirectUrl = !isAuthenticated ? BuildLoginUrl(returnUrl) : null,
                        requiresCompanySelection = authResult != null && authResult.RequiereSeleccionCompania,
                        message = isAuthenticated
                            ? (authResult != null && !string.IsNullOrWhiteSpace(authResult.Motivo)
                                ? authResult.Motivo
                                : "No tiene permisos para acceder a este recurso.")
                            : "La sesión expiró o no ha iniciado sesión."
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            if (isAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(
                        new
                        {
                            controller = "Error",
                            action = "NoAutorizado"
                        }));
                return;
            }

            base.HandleUnauthorizedRequest(filterContext);
        }

        private AocrAuthorizationResultType EvaluarAutorizacion(HttpContextBase httpContext)
        {
            var context = BuildAuthorizationContext(httpContext);
            var service = new AocrAuthorizationServiceType();
            var controller = ResolveController(httpContext);
            var action = ResolveAction(httpContext);
            var codigoSolicitud = ResolveIntParameter(httpContext, CodigoSolicitudParameter, "codigoSolicitud", "solicitudId", "oid");
            var codigoInspeccion = ResolveIntParameter(httpContext, CodigoInspeccionParameter, "codigoInspeccion", "inspeccionId");
            var codigoOrden = ResolveIntParameter(httpContext, CodigoOrdenParameter, "codigoOrden", "ordenId");
            var codigoInforme = ResolveIntParameter(httpContext, CodigoInformeParameter, "codigoInforme", "informeId");

            if (string.Equals(controller, "SolicitudAOCR", StringComparison.OrdinalIgnoreCase) && !codigoSolicitud.HasValue)
            {
                var idGenerico = ResolveIntParameter(httpContext, null, "id");
                codigoSolicitud = idGenerico;
            }

            if (string.Equals(controller, "Inspeccion", StringComparison.OrdinalIgnoreCase) && !codigoInspeccion.HasValue)
            {
                codigoInspeccion = ResolveIntParameter(httpContext, null, "id");
            }

            if ((string.Equals(controller, "OrdenRecaudacion", StringComparison.OrdinalIgnoreCase) || string.Equals(controller, "Financiero", StringComparison.OrdinalIgnoreCase)) && !codigoOrden.HasValue)
            {
                codigoOrden = ResolveIntParameter(httpContext, null, "id", "ordenId");
            }

            var result = service.PuedeEjecutarAccion(
                accion: (Accion ?? action).Trim(),
                usuario: context,
                codigoSolicitud: codigoSolicitud,
                codigoInspeccion: codigoInspeccion,
                codigoOrden: codigoOrden,
                codigoInforme: codigoInforme,
                modulo: (Modulo ?? controller).Trim());

            if (RequireCompanySelection
                && string.IsNullOrWhiteSpace(context.CompanyCode)
                && string.Equals(RoleGroupingHelper.NormalizeSelectedRole(context.SelectedRole), RoleGroupingHelper.Solicitante, StringComparison.OrdinalIgnoreCase))
            {
                result = AocrAuthorizationResultType.Denied(Modulo ?? controller, Accion ?? action, "Debe seleccionar una compañía activa antes de continuar.", true);
            }

            result.CodigoSolicitud = codigoSolicitud;
            result.CodigoInspeccion = codigoInspeccion;
            result.CodigoOrden = codigoOrden;
            result.CodigoInforme = codigoInforme;
            return result;
        }

        private static bool IsAjaxLikeRequest(HttpRequestBase request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.IsAjaxRequest())
            {
                return true;
            }

            var requestedWith = request.Headers["X-Requested-With"];
            if (string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var acceptHeader = request.Headers["Accept"] ?? string.Empty;
            return acceptHeader.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildLoginUrl(string returnUrl)
        {
            var loginUrl = VirtualPathUtility.ToAbsolute(string.IsNullOrWhiteSpace(FormsAuthentication.LoginUrl)
                ? "~/Account/Login"
                : FormsAuthentication.LoginUrl);

            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return loginUrl;
            }

            return loginUrl + (loginUrl.Contains("?") ? "&" : "?") + "ReturnUrl=" + HttpUtility.UrlEncode(returnUrl);
        }

        private void LogUnauthorizedAttempt(HttpContextBase httpContext, bool isAjax, bool isAuthenticated, string returnUrl, AocrAuthorizationResultType authResult)
        {
            try
            {
                var request = httpContext.Request;
                var userName = httpContext.User != null && httpContext.User.Identity != null && httpContext.User.Identity.IsAuthenticated
                    ? httpContext.User.Identity.Name
                    : "ANON";
                var path = request != null && request.Url != null
                    ? request.Url.AbsolutePath
                    : string.Empty;

                Logger.LogWarning(string.Format(
                    "[AUTH] Acceso bloqueado. Path={0}; Method={1}; Authenticated={2}; Ajax={3}; User={4}; ReturnUrl={5}; AttrRoles={6}; Rol={7}; Roles={8}; RolesRaw={9}",
                    path,
                    request != null ? request.HttpMethod : string.Empty,
                    isAuthenticated,
                    isAjax,
                    userName,
                    returnUrl ?? string.Empty,
                    Roles ?? string.Empty,
                    ReadSessionValue(httpContext, "Rol"),
                    ReadSessionValue(httpContext, "Roles"),
                    ReadSessionValue(httpContext, "RolesRaw")));

                int userId;
                UserContextAccessor.TryGetUserId(httpContext.Session, out userId);
                new AuditTrailService().RegistrarAuditoria(
                    tabla: path,
                    registroId: authResult != null ? (authResult.CodigoSolicitud ?? authResult.CodigoInspeccion ?? authResult.CodigoOrden ?? authResult.CodigoInforme) : null,
                    accion: "DENEGADO",
                    campoModificado: authResult != null ? (authResult.Modulo + "/" + authResult.Accion) : "SEGURIDAD",
                    valorAnterior: null,
                    valorNuevo: "Intento de acceso denegado.",
                    usuarioId: userId > 0 ? (int?)userId : null,
                    usuarioNombre: userName,
                    ipOrigen: request != null ? request.UserHostAddress : null,
                    modulo: authResult != null ? authResult.Modulo : "Seguridad",
                    metadata: string.Format(
                        "Rol={0}; Roles={1}; Url={2}; Motivo={3}; Resultado=DENEGADO",
                        ReadSessionValue(httpContext, "Rol"),
                        ReadSessionValue(httpContext, "RolesRaw"),
                        returnUrl ?? string.Empty,
                        authResult != null ? (authResult.Motivo ?? string.Empty) : string.Empty),
                    userAgent: request != null ? request.UserAgent : null);
            }
            catch
            {
            }
        }

        private static AocrAuthorizationContextType BuildAuthorizationContext(HttpContextBase httpContext)
        {
            int userId;
            UserContextAccessor.TryGetUserId(httpContext.Session, out userId);

            int codigoUsuario;
            UserContextAccessor.TryGetCodigoUsuario(httpContext.Session, out codigoUsuario);

            var sessionRoles = RoleGroupingHelper.ExtractRoles(
                httpContext.Session != null ? (httpContext.Session["RolesRaw"] ?? httpContext.Session["Roles"]) : null,
                httpContext.Session != null ? httpContext.Session["Rol"] as string : null);
            var ticketRoleData = ReadFormsTicketRoleData(httpContext);
            var ticketRoles = ticketRoleData.Roles;
            var principalRoles = ReadPrincipalRoles(httpContext != null ? httpContext.User : null);
            var effectiveRoles = RoleGroupingHelper.BuildUnifiedRoles(sessionRoles
                .Concat(ticketRoles)
                .Concat(principalRoles));
            var selectedRole = AuthTicketRoleDataHelper.ReadSelectedRoleFromCookie(
                httpContext != null && httpContext.Request != null ? httpContext.Request.Cookies : null);
            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                selectedRole = RoleGroupingHelper.NormalizeSelectedRole(ticketRoleData.SelectedRole);
            }
            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                selectedRole = UserContextAccessor.GetRol(httpContext.Session);
            }
            if (string.IsNullOrWhiteSpace(selectedRole) && effectiveRoles.Count == 1)
            {
                selectedRole = effectiveRoles[0];
            }

            return new AocrAuthorizationContextType
            {
                IsAuthenticated = httpContext.User != null && httpContext.User.Identity != null && httpContext.User.Identity.IsAuthenticated,
                UserId = userId,
                CodigoUsuario = codigoUsuario > 0
                    ? codigoUsuario.ToString()
                    : Convert.ToString(httpContext.Session != null ? httpContext.Session["CodigoUsuario"] : null),
                UserName = UserContextAccessor.GetNombreUsuario(httpContext.Session, httpContext.User),
                SelectedRole = selectedRole,
                Roles = effectiveRoles,
                CompanyCode = CompaniaActivaSessionHelper.ObtenerCodigo(httpContext.Session),
                CompanyName = CompaniaActivaSessionHelper.ObtenerNombre(httpContext.Session)
            };
        }

        private static AuthTicketRoleData ReadFormsTicketRoleData(HttpContextBase httpContext)
        {
            if (httpContext == null || httpContext.Request == null)
            {
                return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
            }

            var authCookie = httpContext.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
            {
                return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
            }

            try
            {
                var authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                if (authTicket == null || authTicket.Expired || string.IsNullOrWhiteSpace(authTicket.UserData))
                {
                    return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
                }

                return AuthTicketRoleDataHelper.Deserialize(authTicket.UserData);
            }
            catch
            {
                return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
            }
        }

        private static IList<string> ReadPrincipalRoles(System.Security.Principal.IPrincipal principal)
        {
            var knownRoles = new[]
            {
                "Administrador",
                "Admin",
                "Solicitante",
                "Operador",
                "RepresentanteTecnico",
                "Representante Tecnico",
                "RepresentanteLegal",
                "RT",
                "Inspector",
                "Tecnico",
                "EvaluadorTecnico",
                "InspectorTecnico",
                "Coordinador",
                "CoordinadorInspecciones",
                "Coordinacion",
                "CoordinacionLegal",
                "CoordinadorLegal",
                "DIRDAC",
                "Direccion",
                "JefaturaTecnica",
                "DirectorGeneral",
                "DireccionJefaturaTecnica",
                "Financiero",
                "CoordinadorFinanciero",
                "DirectorFinanciero"
            };

            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                return new List<string>();
            }

            return knownRoles
                .Where(principal.IsInRole)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ResolveController(HttpContextBase httpContext)
        {
            return Convert.ToString(httpContext.Request.RequestContext.RouteData.Values["controller"] ?? string.Empty);
        }

        private static string ResolveAction(HttpContextBase httpContext)
        {
            return Convert.ToString(httpContext.Request.RequestContext.RouteData.Values["action"] ?? string.Empty);
        }

        private static int? ResolveIntParameter(HttpContextBase httpContext, string explicitName, params string[] fallbackNames)
        {
            var request = httpContext.Request;
            var names = new[] { explicitName }
                .Concat(fallbackNames ?? new string[0])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                int parsed;
                if (TryParseInt(request.RequestContext.RouteData.Values[name], out parsed))
                {
                    return parsed;
                }

                if (TryParseInt(request.QueryString[name], out parsed))
                {
                    return parsed;
                }

                if (TryParseInt(request.Form[name], out parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static bool TryParseInt(object value, out int parsed)
        {
            parsed = 0;
            if (value == null)
            {
                return false;
            }

            return int.TryParse(Convert.ToString(value), out parsed) && parsed > 0;
        }

        private static string ReadSessionValue(HttpContextBase httpContext, string key)
        {
            if (httpContext == null || httpContext.Session == null)
            {
                return string.Empty;
            }

            var value = httpContext.Session[key];
            if (value == null)
            {
                return string.Empty;
            }

            var raw = value as string;
            if (raw != null)
            {
                return raw;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                return string.Join(",",
                    enumerable.Cast<object>()
                        .Where(item => item != null)
                        .Select(item => Convert.ToString(item)));
            }

            return Convert.ToString(value);
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class SanitizeInputAttribute : FilterAttribute, IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext filterContext) { }
        public void OnActionExecuted(ActionExecutedContext filterContext) { }
    }

    /// <summary>
    /// Filtro global para validación de seguridad
    /// </summary>
    public class GlobalSecurityFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Validar ModelState automáticamente para POST
            if (filterContext.HttpContext.Request.HttpMethod == "POST")
            {
                if (!filterContext.Controller.ViewData.ModelState.IsValid)
                {
                    // Log de modelo inválido
                    System.Diagnostics.Debug.WriteLine(
                        "ModelState inválido en: " + filterContext.ActionDescriptor.ActionName);
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }

    public class RestoreAuthenticatedSessionAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null || filterContext.HttpContext == null || filterContext.IsChildAction)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            if (ShouldSkip(filterContext))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var status = AuthenticatedSessionBootstrapper.EnsureSession(filterContext.HttpContext);
            if (status == AuthenticatedSessionBootstrapStatus.RequiresCompanySelection &&
                ShouldRedirectToCompanySelection(filterContext.HttpContext.Request))
            {
                var returnUrl = BuildReturnUrl(filterContext.HttpContext.Request);
                if (!string.IsNullOrWhiteSpace(returnUrl) && filterContext.HttpContext.Session != null)
                {
                    filterContext.HttpContext.Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] = returnUrl;
                }

                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new
                    {
                        controller = "Account",
                        action = "SeleccionarCompania",
                        returnUrl
                    }));
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        private static bool ShouldSkip(ActionExecutingContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            if (request == null || !request.IsAuthenticated)
            {
                return true;
            }

            var controller = Convert.ToString(filterContext.RouteData.Values["controller"]);
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller, "Error", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldRedirectToCompanySelection(HttpRequestBase request)
        {
            if (request == null || !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsAjaxLikeRequest(request))
            {
                return false;
            }

            var acceptHeader = request.Headers["Accept"] ?? string.Empty;
            return string.IsNullOrWhiteSpace(acceptHeader) ||
                   acceptHeader.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   acceptHeader.IndexOf("*/*", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAjaxLikeRequest(HttpRequestBase request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.IsAjaxRequest())
            {
                return true;
            }

            var requestedWith = request.Headers["X-Requested-With"];
            if (string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var acceptHeader = request.Headers["Accept"] ?? string.Empty;
            return acceptHeader.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildReturnUrl(HttpRequestBase request)
        {
            if (request == null)
            {
                return null;
            }

            var rawUrl = request.RawUrl ?? (request.Url != null ? request.Url.PathAndQuery : string.Empty);
            return string.IsNullOrWhiteSpace(rawUrl) ? null : rawUrl;
        }
    }

    /// <summary>
    /// Validador de archivos subidos
    /// </summary>
    public static class FileUploadValidator
    {
        // Extensiones permitidas
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".gif" };
        
        // Tamaño máximo: 5MB
        private const long MaxFileSize = 5 * 1024 * 1024;

        // Magic bytes para validación de tipo real
        private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };
        private static readonly byte[] GifMagic = { 0x47, 0x49, 0x46 }; // GIF

        /// <summary>
        /// Resultado de validación de archivo
        /// </summary>
        public class FileValidationResult
        {
            public bool IsValid { get; set; }
            public string Error { get; set; }
            public string SafeFileName { get; set; }
            public string FileHash { get; set; }
            public string DetectedType { get; set; }

            public static FileValidationResult Success(string safeFileName, string hash, string type)
            {
                return new FileValidationResult
                {
                    IsValid = true,
                    SafeFileName = safeFileName,
                    FileHash = hash,
                    DetectedType = type
                };
            }

            public static FileValidationResult Fail(string error)
            {
                return new FileValidationResult { IsValid = false, Error = error };
            }
        }

        /// <summary>
        /// Valida un archivo subido de forma segura
        /// </summary>
        public static FileValidationResult ValidateFile(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                return FileValidationResult.Fail("No se proporcionó ningún archivo.");
            }

            // 1. Validar tamaño
            if (file.ContentLength > MaxFileSize)
            {
                return FileValidationResult.Fail(
                    string.Format("El archivo excede el tamaño máximo permitido de {0}MB.",
                        MaxFileSize / 1024 / 1024));
            }

            // 2. Obtener nombre seguro (prevenir path traversal)
            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
            {
                return FileValidationResult.Fail("Nombre de archivo inválido.");
            }

            // 3. Validar extensión
            var extension = Path.GetExtension(originalName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return FileValidationResult.Fail(
                    string.Format("Extensión no permitida. Permitidas: {0}",
                        string.Join(", ", AllowedExtensions)));
            }

            // 4. Leer bytes para validar magic bytes y calcular hash
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                file.InputStream.Position = 0;
                file.InputStream.CopyTo(ms);
                fileBytes = ms.ToArray();
                file.InputStream.Position = 0; // Reset para uso posterior
            }

            // 5. Validar magic bytes
            var detectedType = DetectFileType(fileBytes);
            if (string.IsNullOrEmpty(detectedType))
            {
                return FileValidationResult.Fail("Tipo de archivo no reconocido o potencialmente malicioso.");
            }

            // 6. Verificar que extensión coincide con tipo detectado
            if (!ExtensionMatchesType(extension, detectedType))
            {
                return FileValidationResult.Fail(
                    string.Format("La extensión ({0}) no coincide con el tipo real del archivo ({1}).",
                        extension, detectedType));
            }

            // 7. Generar nombre seguro con GUID
            var safeFileName = string.Format("{0}_{1}{2}",
                Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.Ticks,
                extension);

            // 8. Calcular hash SHA256
            string fileHash;
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(fileBytes);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            return FileValidationResult.Success(safeFileName, fileHash, detectedType);
        }

        /// <summary>
        /// Detecta el tipo real del archivo por magic bytes
        /// </summary>
        private static string DetectFileType(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length < 4)
            {
                return null;
            }

            if (StartsWithBytes(fileBytes, PdfMagic))
            {
                return "application/pdf";
            }

            if (StartsWithBytes(fileBytes, JpegMagic))
            {
                return "image/jpeg";
            }

            if (StartsWithBytes(fileBytes, PngMagic))
            {
                return "image/png";
            }

            if (StartsWithBytes(fileBytes, GifMagic))
            {
                return "image/gif";
            }

            return null;
        }

        /// <summary>
        /// Verifica si el archivo comienza con los bytes especificados
        /// </summary>
        private static bool StartsWithBytes(byte[] fileBytes, byte[] magicBytes)
        {
            if (fileBytes.Length < magicBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < magicBytes.Length; i++)
            {
                if (fileBytes[i] != magicBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Verifica que la extensión coincide con el tipo detectado
        /// </summary>
        private static bool ExtensionMatchesType(string extension, string detectedType)
        {
            switch (detectedType)
            {
                case "application/pdf":
                    return extension == ".pdf";
                case "image/jpeg":
                    return extension == ".jpg" || extension == ".jpeg";
                case "image/png":
                    return extension == ".png";
                case "image/gif":
                    return extension == ".gif";
                default:
                    return false;
            }
        }

        /// <summary>
        /// Guarda archivo de forma segura fuera del webroot
        /// </summary>
        public static string SaveFileSecurely(HttpPostedFileBase file, FileValidationResult validation, string baseDirectory)
        {
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("No se puede guardar un archivo inválido.");
            }

            var fullDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, baseDirectory);
            var ext = Path.GetExtension(validation.SafeFileName);

            var options = new FileUploadOptions
            {
                BasePath = fullDirectory,
                Subfolder = string.Empty,
                AllowedExtensions = string.IsNullOrWhiteSpace(ext) ? null : new[] { ext.ToLowerInvariant() },
                AllowedContentTypes = null,
                MaxSizeMb = 0,
                ValidateMagicBytes = false
            };

            string error;
            FileUploadResult result;
            if (!FileUploadService.TrySave(file, options, out result, out error))
            {
                throw new InvalidOperationException(error ?? "No se pudo guardar el archivo.");
            }

            return Path.Combine(baseDirectory, result.StoredName);
        }
    }

    /// <summary>
    /// Excepción de seguridad personalizada
    /// </summary>
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
}
