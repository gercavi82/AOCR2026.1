using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaModelo;
using CapaDatos.DAOs;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class InspeccionController : Controller
    {
        private readonly InspeccionBL _bl;
        private readonly HallazgoBL _hallazgoBL;

        private const string ROL_ADMIN = "Administrador";
        private const string ROL_COORD = "CoordinadorInspecciones";
        private const string ROL_INSPECTOR = "Inspector";
        private const string ROL_JEFATURA = "JefaturaTecnica";

        public InspeccionController()
        {
            _bl = new InspeccionBL();
            _hallazgoBL = new HallazgoBL();
        }

        private int ObtenerCodigoUsuario()
        {
            if (Session["CodigoUsuario"] != null &&
                int.TryParse(Session["CodigoUsuario"].ToString(), out var id))
            {
                return id;
            }
            return 0;
        }

        private bool EsAdmin() => User != null && User.IsInRole(ROL_ADMIN);

        private bool PuedeAccederInspeccion(Inspeccion ins)
        {
            if (ins == null) return false;
            if (EsAdmin()) return true;

            if (User.IsInRole(ROL_COORD) || User.IsInRole(ROL_JEFATURA))
                return true;

            var codigoUsuario = ObtenerCodigoUsuario();
            if (User.IsInRole(ROL_INSPECTOR))
            {
                if (ins.CodigoInspector.HasValue && ins.CodigoInspector.Value == codigoUsuario)
                    return true;
            }

            return false;
        }

        [Authorize(Roles = ROL_COORD + "," + ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        public ActionResult Index()
        {
            var lista = new List<Inspeccion>();
            return View(lista);
        }

        [Authorize(Roles = ROL_COORD + "," + ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        public ActionResult Detalle(int id)
        {
            var inspeccion = InspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound();

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para ver esta inspección.");

            ViewBag.Hallazgos = _hallazgoBL.ObtenerPorInspeccion(id);
            return View(inspeccion);
        }

        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        public ActionResult Crear(int codigoSolicitud)
        {
            var modelo = new Inspeccion
            {
                CodigoSolicitud = codigoSolicitud
            };

            return View(modelo);
        }

        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Inspeccion model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var codigoUsuario = ObtenerCodigoUsuario();
            if (codigoUsuario <= 0)
            {
                ViewBag.Error = "No se pudo identificar el usuario en sesión.";
                return View(model);
            }

            bool ok = InspeccionBL.Crear(model, codigoUsuario);

            if (ok)
                return RedirectToAction("Detalle", new { id = model.CodigoInspeccion });

            ViewBag.Error = "No se pudo crear la inspección.";
            return View(model);
        }

        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        public ActionResult Editar(int id)
        {
            var inspeccion = InspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound();

            return View(inspeccion);
        }

        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Inspeccion model)
        {
            ModelState.AddModelError("", "La edición de inspecciones aún no está implementada.");
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = ROL_JEFATURA + "," + ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstado(int id, string estado)
        {
            TempData["Warning"] = "La funcionalidad de cambio de estado aún no está implementada.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult SubirInforme(int id)
        {
            var inspeccion = InspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound();

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para subir informe.");

            var archivo = Request.Files["Informe"];
            if (archivo != null && archivo.ContentLength > 0)
            {
                string carpetaVirtual = "~/Uploads/Inspecciones";
                string carpetaFisica = Server.MapPath(carpetaVirtual);

                if (!System.IO.Directory.Exists(carpetaFisica))
                    System.IO.Directory.CreateDirectory(carpetaFisica);

                string nombreArchivo = Guid.NewGuid().ToString("N") + ".pdf";
                string rutaFisica = System.IO.Path.Combine(carpetaFisica, nombreArchivo);
                archivo.SaveAs(rutaFisica);

                string rutaRelativa = $"{carpetaVirtual.TrimStart('~')}/{nombreArchivo}";

                // Aquí podrías llamar a BL para guardar la ruta
                // InspeccionBL.GuardarInforme(id, rutaRelativa, ObtenerCodigoUsuario());

                TempData["Success"] = "Informe cargado. Falta asociarlo en la lógica de negocio.";
            }
            else
            {
                TempData["Error"] = "No se recibió ningún archivo.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarHallazgo(Hallazgo h)
        {
            if (!ModelState.IsValid)
                return RedirectToAction("Detalle", new { id = h.CodigoInspeccion });

            var inspeccion = InspeccionDAO.ObtenerPorId(h.CodigoInspeccion);
            if (inspeccion == null) return HttpNotFound();

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para registrar hallazgos.");

            var codigoUsuario = ObtenerCodigoUsuario();
            string usuarioNombre = User?.Identity?.Name ?? codigoUsuario.ToString();

            bool ok = _hallazgoBL.Crear(h, usuarioNombre);

            TempData[ok ? "Success" : "Error"] = ok ? "Hallazgo registrado correctamente." : "Error al registrar hallazgo.";
            return RedirectToAction("Detalle", new { id = h.CodigoInspeccion });
        }

        [HttpPost]
        [Authorize(Roles = ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Cerrar(int id, string resultado)
        {
            var inspeccion = InspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound();

            var codigoUsuario = ObtenerCodigoUsuario();

            bool ok = InspeccionBL.CerrarInspeccion(id, resultado, codigoUsuario);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Inspección cerrada correctamente."
                : "No se pudo cerrar la inspección.";

            return RedirectToAction("Detalle", new { id });
        }

        // ✅ ✅ ✅ POST Planificación CORRECTO
        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Planificacion(int CodigoInspeccion, DateTime fechaInspeccion, TimeSpan horaInicio,
                                          int duracionEstimada, string ubicacion, string latitud, string longitud,
                                          string tipoInspeccion, string alcance, string equiposNecesarios,
                                          string contactoSitio, string telefonoContacto, string observaciones)
        {
            var inspeccion = InspeccionDAO.ObtenerPorId(CodigoInspeccion);
            if (inspeccion == null)
            {
                TempData["Error"] = "No se encontró la inspección.";
                return RedirectToAction("Index");
            }

            inspeccion.FechaProgramada = fechaInspeccion;
            inspeccion.HoraProgramada = horaInicio;
            inspeccion.Lugar = ubicacion;
            inspeccion.Tipo = tipoInspeccion;
            inspeccion.ObservacionesGenerales = observaciones;
            inspeccion.Comentarios = $"Contacto: {contactoSitio} - Tel: {telefonoContacto}. Equipos: {equiposNecesarios}";
            inspeccion.HallazgosPrincipales = alcance;

            if (!string.IsNullOrWhiteSpace(latitud) || !string.IsNullOrWhiteSpace(longitud))
            {
                inspeccion.Comentarios += $" Coordenadas: {latitud}, {longitud}";
            }

            inspeccion.Estado = "PROGRAMADA";
            inspeccion.UpdatedAt = DateTime.Now;
            inspeccion.UpdatedBy = ObtenerCodigoUsuario();

            bool ok = InspeccionBL.Actualizar(inspeccion);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Planificación guardada correctamente."
                : "Error al guardar la planificación.";

            return RedirectToAction("Detalle", new { id = CodigoInspeccion });
        }
    }
}
