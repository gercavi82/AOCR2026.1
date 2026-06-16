using System;
using System.Web.Mvc;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Controlador para manejo de errores de la aplicación
    /// </summary>
    public class ErrorController : Controller
    {
        // GET: /Error
        public ActionResult Index()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            ViewBag.ErrorCode = 500;
            ViewBag.ErrorMessage = "Error interno del servidor";
            ViewBag.ErrorDescription = "Ha ocurrido un error inesperado. Por favor, intente nuevamente más tarde.";
            return View("Error");
        }

        // GET: /Error/NotFound
        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            ViewBag.ErrorCode = 404;
            ViewBag.ErrorMessage = "Página no encontrada";
            ViewBag.ErrorDescription = "El recurso solicitado no existe o ha sido movido.";
            return View("Error");
        }

        // GET: /Error/AccessDenied
        public ActionResult AccessDenied()
        {
            return NoAutorizado();
        }

        // GET: /Error/NoAutorizado
        public ActionResult NoAutorizado(string mensaje = null, string detalle = null)
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            Response.SuppressFormsAuthenticationRedirect = true;
            ViewBag.ErrorCode = 403;
            ViewBag.ErrorMessage = !string.IsNullOrWhiteSpace(mensaje)
                ? mensaje
                : "Acceso denegado";
            ViewBag.ErrorDescription = !string.IsNullOrWhiteSpace(detalle)
                ? detalle
                : "No tiene permisos para acceder a este recurso.";
            ViewBag.MostrarSeleccionCompania = true;
            ViewBag.Estilo = "denied";
            return View("~/Views/Shared/NoAutorizado.cshtml");
        }

        // GET: /Error/BadRequest
        public ActionResult BadRequest()
        {
            Response.StatusCode = 400;
            Response.TrySkipIisCustomErrors = true;
            ViewBag.ErrorCode = 400;
            ViewBag.ErrorMessage = "Solicitud inválida";
            ViewBag.ErrorDescription = "La solicitud no pudo ser procesada. Verifique los datos enviados.";
            return View("Error");
        }
    }
}
