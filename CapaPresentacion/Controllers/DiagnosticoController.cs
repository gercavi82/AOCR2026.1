using System.Web.Mvc;
using CapaPresentacion.Services;
using CapaNegocio.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public sealed class DiagnosticoController : Controller
    {
        private readonly IUsuarioContextoService _usuarioContextoService;

        public DiagnosticoController()
            : this(new UsuarioContextoService())
        {
        }

        public DiagnosticoController(IUsuarioContextoService usuarioContextoService)
        {
            _usuarioContextoService = usuarioContextoService;
        }

        [HttpGet]
        public ActionResult ContextoUsuario()
        {
            UsuarioContextoDto contexto;
            if (!_usuarioContextoService.TryObtenerContextoActual(out contexto))
            {
                return new HttpStatusCodeResult(401);
            }

            if (!contexto.EsAdministrador)
            {
                return new HttpStatusCodeResult(403);
            }

            return Json(new
            {
                contexto.UsuarioId,
                contexto.Login,
                contexto.NombreCompleto,
                contexto.RolActivo,
                contexto.Roles,
                contexto.CompaniaCodigo,
                contexto.CompaniaNombre,
                contexto.EstaAutenticado,
                contexto.EsValido
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult ConsistenciaPdf()
        {
            UsuarioContextoDto contexto;
            if (!_usuarioContextoService.TryObtenerContextoActual(out contexto)) return new HttpStatusCodeResult(401);
            if (!contexto.EsAdministrador) return new HttpStatusCodeResult(403);
            var resultado = new DocumentoPdfConsistenciaService(Server.MapPath("~/App_Data/AOCR")).Ejecutar();
            return Json(resultado, JsonRequestBehavior.AllowGet);
        }
    }
}
