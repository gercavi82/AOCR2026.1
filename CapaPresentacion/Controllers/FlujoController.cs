using System;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;
using CapaPresentacion.Filters;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public sealed class FlujoController : Controller
    {
        private readonly IEntregaFinalService _service;

        public FlujoController() : this(DependencyResolver.Current.GetService<IEntregaFinalService>() ?? new EntregaFinalService()) { }
        public FlujoController(IEntregaFinalService service) { _service = service; }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRDAC")]
        [RequirePermission(EntregaFinalService.PermisoSolicitar)]
        public ActionResult SolicitarEntregaFinal(SolicitarEntregaFinalRequest request)
        {
            if (request == null) return JsonResult(EntregaFinalResult.Error(400, "REQUEST_INVALIDO", "No se recibió la solicitud."));
            request.Actor = CrearActor(EntregaFinalService.PermisoSolicitar);
            request.BaseUrl = Request == null || Request.Url == null ? string.Empty
                : Request.Url.GetLeftPart(UriPartial.Authority) + Url.Content("~").TrimEnd('/');
            return JsonResult(_service.Solicitar(request));
        }

        private EntregaFinalActor CrearActor(string permiso)
        {
            var rol = Convert.ToString(Session != null ? Session["Rol"] : null);
            var codigo = Convert.ToString(Session != null ? Session["CodigoUsuario"] : null);
            var idRaw = Convert.ToString(Session != null ? (Session["UsuarioId"] ?? Session["UserId"] ?? Session["CodigoUsuario"]) : null);
            int id; int.TryParse(idRaw, out id);
            return new EntregaFinalActor
            {
                UsuarioId = id,
                UsuarioNombre = User != null && User.Identity != null ? User.Identity.Name : codigo,
                RolActivo = rol,
                Ip = Request != null ? Request.UserHostAddress : null,
                TienePermiso = SeguridadBL.UsuarioTienePermiso(codigo, permiso, new[] { rol })
            };
        }

        private ActionResult JsonResult(EntregaFinalResult result)
        {
            Response.StatusCode = result.HttpStatusCode > 0 ? result.HttpStatusCode : 500;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { ok=result.Exito,codigo=result.Codigo,mensaje=result.Mensaje,estadoEntrega=result.EstadoEntrega,
                estadoExpediente=result.EstadoExpediente,version=result.VersionExpediente,correlationId=result.CorrelationId });
        }
    }
}
