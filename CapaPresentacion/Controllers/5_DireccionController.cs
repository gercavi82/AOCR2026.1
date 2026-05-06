using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaNegocio;
using CapaModelo;
using CapaPresentacion.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class DireccionController : Controller
    {
        private readonly DireccionBL _bl = new DireccionBL();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly ParametroDAO _parametroDao = new ParametroDAO();

        // ============================================================
        // LISTADO
        // ============================================================
        public ActionResult Index()
        {
            var lista = _bl.ObtenerTodos();
            return View(lista);
        }

        // ============================================================
        // DASHBOARD GERENCIAL
        // ============================================================
        [Authorize(Roles = "Direccion,JefaturaTecnica,Administrador")]
        public ActionResult DashboardGerencial()
        {
            var solicitudes = _solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>();
            var inspecciones = new InspeccionDAO().ListarTodas() ?? new List<Inspeccion>();

            var estados = solicitudes
                .GroupBy(s => EstadoSolicitud.Normalizar(s.Estado))
                .Select(g => new EstadoResumenItem
                {
                    Estado = g.Key,
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var cuellosBotella = estados
                .Where(x => x.Total > 0
                    && x.Estado != EstadoSolicitud.AOCR_EmitidoRecibido)
                .Take(5)
                .ToList();

            var model = new DashboardGerencialViewModel
            {
                TotalSolicitudes = solicitudes.Count,
                SolicitudesPendientes = solicitudes.Count(s => EstadoSolicitud.Normalizar(s.Estado) == EstadoSolicitud.Pendiente),
                SolicitudesObservadas = solicitudes.Count(s => EstadoSolicitud.Normalizar(s.Estado) == EstadoSolicitud.Observada),
                SolicitudesAceptadasDocumental = solicitudes.Count(s => EstadoSolicitud.Normalizar(s.Estado) == EstadoSolicitud.AceptacionDocumental),
                InspeccionesPendientes = inspecciones.Count(i => string.IsNullOrWhiteSpace(i.Estado) || i.Estado.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase) || i.Estado.Equals("INSPECCION_A_PROGRAMAR", StringComparison.OrdinalIgnoreCase)),
                InspeccionesEnCurso = inspecciones.Count(i => (i.Estado ?? string.Empty).Equals("EN_INSPECCION", StringComparison.OrdinalIgnoreCase)),
                InspeccionesFinalizadas = inspecciones.Count(i =>
                    (i.Estado ?? string.Empty).Equals("CERRADA", StringComparison.OrdinalIgnoreCase) ||
                    (i.Estado ?? string.Empty).Equals("APROBADA", StringComparison.OrdinalIgnoreCase) ||
                    (i.Resultado ?? string.Empty).Equals("SATISFACTORIO", StringComparison.OrdinalIgnoreCase) ||
                    (i.Resultado ?? string.Empty).Equals("APROBADO", StringComparison.OrdinalIgnoreCase)),
                AocrEnRevision = solicitudes.Count(s => EstadoSolicitud.Normalizar(s.Estado) == EstadoSolicitud.AOCR_EnRevision),
                AocrValidados = solicitudes.Count(s => EstadoSolicitud.Normalizar(s.Estado) == EstadoSolicitud.AOCR_Validado),
                AocrLegalizados = solicitudes.Count(s => EstadoSolicitud.Normalizar(s.Estado) == EstadoSolicitud.AOCR_Legalizado),
                AocrEmitidosRecibidos = solicitudes.Count(s => EstadoSolicitud.Normalizar(s.Estado) == EstadoSolicitud.AOCR_EmitidoRecibido),
                EstadosSolicitud = estados,
                CuellosBotella = cuellosBotella
            };

            return View(model);
        }

        // ============================================================
        // DETALLE
        // ============================================================
        public ActionResult Detalle(int id)
        {
            var direccion = _bl.ObtenerPorId(id);
            if (direccion == null)
                return HttpNotFound("Dirección no encontrada");

            return View(direccion);
        }

        // ============================================================
        // CREAR
        // ============================================================
        public ActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Direccion d)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(d);

                _bl.Crear(d, User.Identity.Name);

                TempData["msg"] = "Dirección creada correctamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View(d);
            }
        }

        // ============================================================
        // EDITAR
        // ============================================================
        public ActionResult Editar(int id)
        {
            var direccion = _bl.ObtenerPorId(id);
            if (direccion == null)
                return HttpNotFound("Dirección no encontrada");

            return View(direccion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Direccion d)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(d);

                _bl.Actualizar(d, User.Identity.Name);

                TempData["msg"] = "Dirección actualizada correctamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View(d);
            }
        }

        // ============================================================
        // ELIMINAR
        // ============================================================
        public ActionResult Eliminar(int id)
        {
            var direccion = _bl.ObtenerPorId(id);
            if (direccion == null)
                return HttpNotFound("Dirección no encontrada");

            return View(direccion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmarEliminar(int id)
        {
            try
            {
                _bl.Eliminar(id, User.Identity.Name);
                TempData["msg"] = "Dirección eliminada correctamente";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
                return RedirectToAction("Eliminar", new { id });
            }
        }

        [Authorize(Roles = "Direccion,JefaturaTecnica,Administrador")]
        public ActionResult ConfiguracionSistema()
        {
            var parametros = _parametroDao.ListarTodos() ?? new List<Parametro>();
            return View(parametros);
        }
    }
}
