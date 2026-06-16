using System.Web.Mvc;

namespace CapaPresentacion.Helpers
{
    /// <summary>
    /// Respuestas HTML institucionales para acceso denegado y mensajes de bloqueo.
    /// </summary>
    public static class InstitutionalAccessViewHelper
    {
        public static ActionResult AccesoDenegado(
            Controller controller,
            string mensaje,
            string detalle = null,
            string tituloEncabezado = null,
            string panelHint = null,
            bool mostrarSeleccionCompania = true,
            string accionUrl = null,
            string accionTexto = null,
            string estilo = "denied")
        {
            ConfigurarRespuesta(controller, 403);

            controller.ViewBag.ErrorCode = 403;
            controller.ViewBag.ErrorMessage = mensaje;
            controller.ViewBag.ErrorDescription = detalle
                ?? "La autorización institucional del sistema AOCR bloqueó esta solicitud para proteger el flujo y la trazabilidad.";
            controller.ViewBag.TituloEncabezado = tituloEncabezado;
            controller.ViewBag.PanelHint = panelHint
                ?? "Verifique el rol seleccionado, la compañía activa y la etapa institucional del trámite antes de reintentar.";
            controller.ViewBag.MostrarSeleccionCompania = mostrarSeleccionCompania;
            controller.ViewBag.AccionUrl = accionUrl;
            controller.ViewBag.AccionTexto = accionTexto;
            controller.ViewBag.Estilo = estilo;

            return new ViewResult
            {
                ViewName = "~/Views/Shared/NoAutorizado.cshtml",
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };
        }

        public static ActionResult MensajeInstitucional(
            Controller controller,
            string mensaje,
            string detalle = null,
            string tituloEncabezado = null,
            string panelHint = null,
            bool mostrarSeleccionCompania = false,
            string accionUrl = null,
            string accionTexto = null,
            string estilo = "warning",
            int statusCode = 200)
        {
            ConfigurarRespuesta(controller, statusCode);

            controller.ViewBag.ErrorCode = statusCode;
            controller.ViewBag.ErrorMessage = mensaje;
            controller.ViewBag.ErrorDescription = detalle ?? string.Empty;
            controller.ViewBag.TituloEncabezado = tituloEncabezado ?? "Operación no disponible";
            controller.ViewBag.PanelHint = panelHint;
            controller.ViewBag.MostrarSeleccionCompania = mostrarSeleccionCompania;
            controller.ViewBag.AccionUrl = accionUrl;
            controller.ViewBag.AccionTexto = accionTexto;
            controller.ViewBag.Estilo = estilo;

            return new ViewResult
            {
                ViewName = "~/Views/Shared/NoAutorizado.cshtml",
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };
        }

        private static void ConfigurarRespuesta(Controller controller, int statusCode)
        {
            if (controller?.Response == null)
            {
                return;
            }

            controller.Response.StatusCode = statusCode;
            controller.Response.TrySkipIisCustomErrors = true;
            controller.Response.SuppressFormsAuthenticationRedirect = true;
        }
    }
}
