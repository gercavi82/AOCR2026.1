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

        // ============================================================
        // APROBAR SOLICITUDES - DIRECCIÓN
        // ============================================================
        [Authorize(Roles = "Direccion,JefaturaTecnica,Administrador")]
        public ActionResult AprobarSolicitudes()
        {
            var solicitudesPendientes = _solicitudDao.ObtenerPorEstados(
                "VALIDADO_TECNICAMENTE",
                "ENVIADO_A_JEFATURA",
                EstadoSolicitud.AOCR_EnRevision,
                EstadoSolicitud.AOCR_Validado);

            return View(solicitudesPendientes);
        }

        [Authorize(Roles = "Direccion,JefaturaTecnica,Administrador")]
        public ActionResult ConfiguracionSistema()
        {
            var parametros = _parametroDao.ListarTodos() ?? new List<Parametro>();
            return View(parametros);
        }

        // ============================================================
        // VALIDACIÓN FINAL
        // ============================================================
        [Authorize(Roles = "Direccion")]
        public ActionResult ValidacionFinal(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || solicitud.Estado != "VALIDADO_TECNICAMENTE")
                return HttpNotFound("Solicitud no encontrada o no está lista para validación final");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Direccion")]
        [ValidateAntiForgeryToken]
        public ActionResult ValidacionFinal(int id, bool aprobada, string observaciones, string condicionesEspeciales, int vigencia)
        {
            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
                if (solicitud == null || solicitud.Estado != "VALIDADO_TECNICAMENTE")
                    return HttpNotFound("Solicitud no encontrada o no está lista para validación final");

                int userId = ObtenerUsuarioActualId();

                if (aprobada)
                {
                    // Cambiar estado a aprobado por dirección
                    string mensaje;
                    SolicitudAOCRBL.CambiarEstado(id, "APROBADO_POR_DIRECCION", userId, observaciones ?? "Aprobado por Dirección", out mensaje);

                    TempData["success"] = "Solicitud aprobada correctamente. Pasará a legalización.";
                    return RedirectToAction("Legalizar", new { id });
                }
                else
                {
                    // Rechazar solicitud
                    string mensaje;
                    SolicitudAOCRBL.CambiarEstado(id, "RECHAZADO_POR_DIRECCION", userId, observaciones ?? "Rechazado por Dirección", out mensaje);

                    TempData["error"] = "Solicitud rechazada.";
                    return RedirectToAction("AprobarSolicitudes");
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error al procesar la validación: " + ex.Message;
                return RedirectToAction("ValidacionFinal", new { id });
            }
        }

        // ============================================================
        // LEGALIZAR CERTIFICADO
        // ============================================================
        [Authorize(Roles = "Direccion")]
        public ActionResult Legalizar(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || solicitud.Estado != "APROBADO_POR_DIRECCION")
                return HttpNotFound("Solicitud no encontrada o no está lista para legalización");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Direccion")]
        [ValidateAntiForgeryToken]
        public ActionResult Legalizar(int id, string firmaDirector, string selloOficial)
        {
            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
                if (solicitud == null || solicitud.Estado != "APROBADO_POR_DIRECCION")
                    return HttpNotFound("Solicitud no encontrada o no está lista para legalización");

                int userId = ObtenerUsuarioActualId();

                // Cambiar estado a legalizado
                string mensaje;
                SolicitudAOCRBL.CambiarEstado(id, "LEGALIZADO", userId, "Certificado legalizado y firmado", out mensaje);

                TempData["success"] = "Certificado legalizado correctamente.";
                return RedirectToAction("EmitirAOCR", new { id });
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error al legalizar el certificado: " + ex.Message;
                return RedirectToAction("Legalizar", new { id });
            }
        }

        // ============================================================
        // EMITIR CERTIFICADO AOCR
        // ============================================================
        [Authorize(Roles = "Direccion")]
        public ActionResult EmitirAOCR(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || solicitud.Estado != "LEGALIZADO")
                return HttpNotFound("Solicitud no encontrada o no está lista para emisión");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Direccion")]
        [ValidateAntiForgeryToken]
        public ActionResult EmitirAOCRConfirm(int id)
        {
            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
                if (solicitud == null || solicitud.Estado != "LEGALIZADO")
                    return HttpNotFound("Solicitud no encontrada o no está lista para emisión");

                int userId = ObtenerUsuarioActualId();

                // Cambiar estado a emitido
                string mensaje;
                SolicitudAOCRBL.CambiarEstado(id, "CERTIFICADO_EMITIDO", userId, "Certificado AOCR emitido", out mensaje);

                TempData["success"] = "Certificado AOCR emitido correctamente.";
                return RedirectToAction("AprobarSolicitudes");
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error al emitir el certificado: " + ex.Message;
                return RedirectToAction("EmitirAOCR", new { id });
            }
        }

        private int ObtenerUsuarioActualId()
        {
            if (Session["CodigoUsuario"] != null && int.TryParse(Session["CodigoUsuario"].ToString(), out int idUsuario))
                return idUsuario;

            throw new Exception("No se pudo obtener el ID del usuario actual.");
        }
    }
}
