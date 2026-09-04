using System.Web.Mvc;
using System;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Services;
using CapaPresentacion.Filters;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdministradorController : Controller
    {
        public ActionResult ConfiguracionSistema()
        {
            return View();
        }

        [HttpGet]
        [RequirePermission(EntregaFinalService.PermisoAuditoria)]
        public ActionResult EstadoEntrega(int? solicitudId)
        {
            var raw = Convert.ToString(Session != null ? (Session["UsuarioId"] ?? Session["UserId"] ?? Session["CodigoUsuario"]) : null);
            int usuarioId; int.TryParse(raw, out usuarioId);
            var codigo = Convert.ToString(Session != null ? Session["CodigoUsuario"] : null);
            var rol = Convert.ToString(Session != null ? Session["Rol"] : null);
            var actor = new EntregaFinalActor
            {
                UsuarioId = usuarioId,
                UsuarioNombre = User != null && User.Identity != null ? User.Identity.Name : codigo,
                RolActivo = rol,
                Ip = Request != null ? Request.UserHostAddress : null,
                TienePermiso = SeguridadBL.UsuarioTienePermiso(codigo, EntregaFinalService.PermisoAuditoria, new[] { rol })
            };
            return View(new EntregaFinalService().ConsultarEstados(actor, solicitudId));
        }
    }
}
