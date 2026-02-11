using System;
using System.Web.Mvc;
using CapaModelo.RT.ViewModels;
using CapaNegocio.Services;

namespace CapaPresentacion.Controllers
{
    // [Authorize] // Habilitar cuando el flujo esté integrado con autenticación
    public class RTController : Controller
    {
        private readonly RTService _service = new RTService();

        private int ObtenerUsuarioId()
        {
            var v = Session["UserId"] ?? Session["IdUsuario"] ?? Session["CodigoUsuario"]; // ajustar según auth
            if (v != null && int.TryParse(v.ToString(), out var id))
                return id;

            // Simulación para entorno sin autenticación (pendiente de integración)
            return 1;
        }

        [HttpGet]
        public ActionResult Registro()
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            var vm = new RegistroRTVM();

            if (solicitud != null)
            {
                var compania = _service.GetCompaniaById(solicitud.CompaniaId);
                vm.SolicitudId = solicitud.Id;
                vm.RazonSocial = compania?.RazonSocial;
                vm.Ruc = compania?.Ruc;
                vm.Telefono = compania?.Telefono;
                vm.Email = compania?.EmailContacto;
                vm.AreaContableJson = compania?.AreaContableJson;

                ViewBag.Estado = solicitud.Estado;
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarRegistro(RegistroRTVM vm)
        {
            var usuarioId = ObtenerUsuarioId();

            if (!ModelState.IsValid)
            {
                return View("Registro", vm);
            }

            try
            {
                var solicitudId = _service.GuardarBorrador(vm, usuarioId);
                TempData["Ok"] = "Borrador guardado correctamente.";
                return RedirectToAction("Declaracion", new { solicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("Registro", vm);
            }
        }

        [HttpGet]
        public ActionResult Declaracion(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Registro");
            }

            var vm = new DeclaracionRTVM
            {
                SolicitudId = solicitud.Id,
                TextoDeclaracion = solicitud.DeclaracionTexto,
                Acepto = solicitud.DeclaracionAceptada,
                Estado = solicitud.Estado
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AceptarDeclaracion(DeclaracionRTVM vm)
        {
            var usuarioId = ObtenerUsuarioId();
            if (!ModelState.IsValid)
            {
                return View("Declaracion", vm);
            }

            try
            {
                _service.AceptarDeclaracion(vm.SolicitudId, usuarioId);
                TempData["Ok"] = "Declaración aceptada.";
                return RedirectToAction("Designacion", new { solicitudId = vm.SolicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("Declaracion", vm);
            }
        }

        [HttpGet]
        public ActionResult Designacion(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Registro");
            }

            var doc = _service.GetDocumentoDesignacion(solicitudId);
            var vm = new DesignacionUploadVM
            {
                SolicitudId = solicitud.Id,
                NombreArchivoActual = doc?.NombreArchivo,
                Estado = solicitud.Estado
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubirDesignacion(DesignacionUploadVM vm)
        {
            var usuarioId = ObtenerUsuarioId();
            if (!ModelState.IsValid)
            {
                return View("Designacion", vm);
            }

            try
            {
                _service.SubirDesignacionPdf(vm.SolicitudId, usuarioId, vm.ArchivoPdf);
                TempData["Ok"] = "Documento cargado correctamente.";
                return RedirectToAction("Designacion", new { solicitudId = vm.SolicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("Designacion", vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Enviar(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            try
            {
                _service.EnviarSolicitud(solicitudId, usuarioId);
                TempData["Ok"] = "Solicitud enviada. En revisión por Coordinador.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Designacion", new { solicitudId });
        }
    }
}
