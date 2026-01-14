using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private readonly HallazgoBL _hallazgoBL;
        // CORRECCIÓN: Se eliminó la variable privada _checklistBL porque ChecklistBL es estática.

        private const string ROL_ADMIN = "Administrador";
        private const string ROL_COORD = "CoordinadorInspecciones";
        private const string ROL_INSPECTOR = "Inspector";
        private const string ROL_JEFATURA = "JefaturaTecnica";

        private const int MAX_PDF_BYTES = 10 * 1024 * 1024; // 10MB
        private const string CARPETA_VIRTUAL_INFORMES = "~/Uploads/Inspecciones";

        public InspeccionController()
        {
            _hallazgoBL = new HallazgoBL();
            // No se instancia ChecklistBL aquí.
        }

        #region Helpers de Seguridad y Sesión

        private int ObtenerCodigoUsuario()
        {
            if (Session["CodigoUsuario"] != null && int.TryParse(Session["CodigoUsuario"].ToString(), out var id))
                return id;
            return 0;
        }

        private bool EsAdmin() => User != null && User.IsInRole(ROL_ADMIN);

        private bool PuedeAccederInspeccion(Inspeccion ins)
        {
            if (ins == null) return false;
            if (EsAdmin()) return true;
            if (User.IsInRole(ROL_COORD) || User.IsInRole(ROL_JEFATURA)) return true;

            var codigoUsuario = ObtenerCodigoUsuario();
            if (User.IsInRole(ROL_INSPECTOR))
                return ins.CodigoInspector.HasValue && ins.CodigoInspector.Value == codigoUsuario;

            return false;
        }

        #endregion

        // ✅ LISTADO
        [Authorize(Roles = ROL_COORD + "," + ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        public ActionResult Index()
        {
            List<Inspeccion> lista;
            if (EsAdmin() || User.IsInRole(ROL_COORD) || User.IsInRole(ROL_JEFATURA))
                lista = InspeccionBL.ListarTodas();
            else
                lista = InspeccionBL.ListarPorInspector(ObtenerCodigoUsuario());

            return View(lista);
        }

        // ✅ DETALLE (Con Hallazgos y Estadísticas)
        public ActionResult Detalle(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            var inspeccion = InspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound();

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403);

            ViewBag.Hallazgos = _hallazgoBL.ObtenerPorInspeccion(id);

            // LLAMADA ESTÁTICA: Se usa el nombre de la clase directamente
            ViewBag.StatsChecklist = ChecklistBL.ObtenerEstadisticas(inspeccion.CodigoSolicitud);

            return View(inspeccion);
        }

        // ✅ GESTIÓN DE CHECKLIST (EJECUCIÓN)
        [HttpGet]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        public ActionResult EjecutarChecklist(int id) // id = CodigoSolicitud
        {
            // LLAMADA ESTÁTICA
            var plantilla = ChecklistBL.ObtenerPorSolicitud(id);
            ViewBag.SolicitudId = id;
            return View(plantilla);
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        public JsonResult GuardarChecklist(List<ChecklistItem> items)
        {
            if (items == null || items.Count == 0)
                return Json(new { success = false, message = "No se recibieron datos." });

            string mensaje;
            int solicitudId = items[0].CodigoSolicitud;

            // LLAMADA ESTÁTICA: Usando la lógica masiva definida en la Capa de Negocio
            bool ok = ChecklistBL.InsertarMasivo(items, solicitudId, out mensaje);

            return Json(new { success = ok, message = mensaje });
        }

        // ✅ PLANIFICACIÓN (POST)
        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Planificacion(Inspeccion model, string contactoSitio, string telefonoContacto, string equiposNecesarios)
        {
            var inspeccion = InspeccionDAO.ObtenerPorId(model.CodigoInspeccion);
            if (inspeccion == null || !PuedeAccederInspeccion(inspeccion)) return RedirectToAction("Index");

            inspeccion.FechaProgramada = model.FechaProgramada;
            inspeccion.HoraProgramada = model.HoraProgramada;
            inspeccion.Lugar = model.Lugar;
            inspeccion.Latitud = model.Latitud;
            inspeccion.Longitud = model.Longitud;
            inspeccion.Comentarios = $"Contacto: {contactoSitio} | Tel: {telefonoContacto} | Equipos: {equiposNecesarios}";
            inspeccion.Estado = "PROGRAMADA";

            inspeccion.UpdatedAt = DateTime.Now;
            inspeccion.UpdatedBy = ObtenerCodigoUsuario();

            bool ok = InspeccionBL.Actualizar(inspeccion);
            TempData[ok ? "Success" : "Error"] = ok ? "Planificación exitosa." : "Error al planificar.";

            return RedirectToAction("Detalle", new { id = inspeccion.CodigoInspeccion });
        }

        // ✅ SUBIR INFORME PDF
        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult SubirInforme(int id)
        {
            var inspeccion = InspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null || !PuedeAccederInspeccion(inspeccion)) return HttpNotFound();

            HttpPostedFileBase archivo = Request.Files["Informe"];
            if (archivo == null || archivo.ContentLength <= 0) return RedirectToAction("Detalle", new { id });

            if (archivo.ContentLength > MAX_PDF_BYTES || !archivo.FileName.ToLower().EndsWith(".pdf") || !TieneFirmaPdf(archivo))
            {
                TempData["Error"] = "Archivo inválido. Debe ser PDF menor a 10MB.";
                return RedirectToAction("Detalle", new { id });
            }

            string nombreUnico = $"{Guid.NewGuid():N}.pdf";
            string rutaFisica = Path.Combine(Server.MapPath(CARPETA_VIRTUAL_INFORMES), nombreUnico);

            if (!Directory.Exists(Server.MapPath(CARPETA_VIRTUAL_INFORMES)))
                Directory.CreateDirectory(Server.MapPath(CARPETA_VIRTUAL_INFORMES));

            archivo.SaveAs(rutaFisica);

            bool ok = InspeccionBL.GuardarInforme(id, "/Uploads/Inspecciones/" + nombreUnico, ObtenerCodigoUsuario());
            return RedirectToAction("Detalle", new { id });
        }

        private bool TieneFirmaPdf(HttpPostedFileBase archivo)
        {
            byte[] buffer = new byte[4];
            archivo.InputStream.Read(buffer, 0, 4);
            archivo.InputStream.Position = 0;
            return Encoding.ASCII.GetString(buffer) == "%PDF";
        }
    }
}