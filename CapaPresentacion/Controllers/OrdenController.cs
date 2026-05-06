using System;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaPresentacion.Models.ViewModels;
using System.Collections.Generic;
using System.Data;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenController : Controller
    {
        private readonly OrdenRecaudacionDAO _dao;

        public OrdenController()
        {
            _dao = new OrdenRecaudacionDAO();
        }

        // GET: Orden/Detalle/4
        public ActionResult Detalle(int id)
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    TempData["Error"] = "Debe iniciar sesi�n.";
                    return RedirectToAction("Login", "Account");
                }

                // 1) Traer la orden por ID
                var orden = _dao.ObtenerOrdenPorIdModel(id);

                // 2) Validar permisos (que sea del usuario logueado)
                if (orden == null || orden.CodigoUsuario != idUsuario)
                {
                    TempData["Error"] = "Orden no encontrada o no tiene permisos para verla.";
                    return RedirectToAction("Index", "Dashboard");
                }

                // 3) Map a ViewModel
                var vm = new OrdenRecaudacionViewModel
                {
                    Id = orden.Id,
                    NumeroOrden = orden.NumeroOrden ?? "",
                    FechaCreacion = orden.FechaCreacion,
                    Estado = orden.Estado ?? "",
                    Subtotal = orden.Subtotal,
                    Admin = orden.Admin,
                    Total = orden.Total,
                    Observacion = orden.Observacion ?? ""
                };

                ViewBag.OrdenId = id;
                ViewBag.Mensaje = "Detalles de la orden";

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar la orden: {ex.Message}";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        // GET: Orden/MisOrdenes
        public ActionResult MisOrdenes()
        {
            return RedirectToAction("Index", "OrdenRecaudacion");
        }

        private int ObtenerIdUsuario()
        {
            var sessionValue = Session["IdUsuario"] ?? Session["UserId"];
            if (sessionValue != null &&
                int.TryParse(sessionValue.ToString(), out int idUsuario))
            {
                return idUsuario;
            }
            return 0;
        }
    }
}


