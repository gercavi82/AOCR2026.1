using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly OrdenRecaudacionDAO _dao;

        public DashboardController()
        {
            _dao = new OrdenRecaudacionDAO();
        }

        // GET: Dashboard
        public ActionResult Index()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return RedirectToAction("Login", "Account");

                bool tieneOrdenGenerada = _dao.ExisteORGeneradaOPagada(idUsuario);
                bool tieneOrdenBorrador = _dao.ExisteORMinima(idUsuario);

                ViewBag.TieneOrdenGenerada = tieneOrdenGenerada;
                ViewBag.TieneOrdenBorrador = tieneOrdenBorrador;

                return View();
            }
            catch
            {
                return RedirectToAction("Login", "Account");
            }
        }

        // GET: Dashboard/ObtenerDatosDashboard
        [HttpGet]
        public JsonResult ObtenerDatosDashboard()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                // ✅ USAR LISTA (NO DataTable)
                var ordenes = _dao.ListarPorUsuario(idUsuario, null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();

                int ordenesPendientes = 0;
                int ordenesCompletadas = 0;
                decimal totalRecaudado = 0m;
                string ultimaOrden = "N/A";

                foreach (var o in ordenes)
                {
                    var estado = (o.Estado ?? "").Trim().ToUpperInvariant();

                    if (estado == "BORRADOR" || estado == "GENERADA" || estado == "ENVIADA")
                        ordenesPendientes++;
                    else if (estado == "PAGADA" || estado == "COMPLETADA")
                        ordenesCompletadas++;

                    totalRecaudado += o.Total ?? 0m;
                }

                var ultima = ordenes
                    .OrderByDescending(x => x.FechaCreacion)
                    .FirstOrDefault();

                if (ultima != null && !string.IsNullOrWhiteSpace(ultima.NumeroOrden))
                    ultimaOrden = ultima.NumeroOrden;

                var ordenesRecientes = ordenes
                    .OrderByDescending(x => x.FechaCreacion)
                    .Take(10)
                    .Select(x => new
                    {
                        id = x.Id,
                        numeroOrden = x.NumeroOrden,
                        fechaCreacion = x.FechaCreacion,
                        estado = x.Estado,
                        total = x.Total ?? 0m
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    ordenesPendientes,
                    ordenesCompletadas,
                    totalRecaudado,
                    ultimaOrden,
                    ordenesRecientes
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private int ObtenerIdUsuario()
        {
            if (Session["IdUsuario"] != null &&
                int.TryParse(Session["IdUsuario"].ToString(), out int idUsuario))
            {
                return idUsuario;
            }
            return 0;
        }
    }
}
