using System.Web.Mvc;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    public abstract class AocrBaseController : Controller
    {
        private readonly IUsuarioContextoService _usuarioContextoService;

        protected AocrBaseController(IUsuarioContextoService usuarioContextoService)
        {
            _usuarioContextoService = usuarioContextoService;
        }

        protected UsuarioContextoDto UsuarioActual
        {
            get { return _usuarioContextoService.ObtenerContextoActual(); }
        }

        protected int UsuarioActualId
        {
            get { return UsuarioActual.UsuarioId; }
        }

        protected string RolActual
        {
            get { return UsuarioActual.RolActivo; }
        }

        protected string CompaniaActual
        {
            get { return UsuarioActual.CompaniaCodigo; }
        }
    }
}
