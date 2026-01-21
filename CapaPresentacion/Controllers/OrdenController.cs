using System;
using System.Data;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaPresentacion.Models.ViewModels;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenController : Controller
    {
        private readonly IOrdenRecaudacionDAO _dao;

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
                    TempData["Error"] = "Debe iniciar sesión.";
                    return RedirectToAction("Login", "Account");
                }

                // Obtener datos de la orden específica
                var orden = ObtenerOrdenPorId(id, idUsuario);

                if (orden == null)
                {
                    TempData["Error"] = "Orden no encontrada o no tiene permisos para verla.";
                    return RedirectToAction("Index", "Dashboard");
                }

                // Pasar datos adicionales a ViewBag
                ViewBag.OrdenId = id;
                ViewBag.Mensaje = "Detalles de la orden";

                return View(orden);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar la orden: {ex.Message}";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        // ✅ MÉTODO CORREGIDO - Maneja DataTable correctamente
        private OrdenRecaudacionViewModel ObtenerOrdenPorId(int idOrden, int idUsuario)
        {
            try
            {
                // Obtener DataTable con las órdenes del usuario
                DataTable dtOrdenes = _dao.ObtenerOrdenesPorUsuario(idUsuario);

                if (dtOrdenes != null && dtOrdenes.Rows.Count > 0)
                {
                    // Buscar la orden específica usando LINQ con DataTable
                    var ordenRow = dtOrdenes.AsEnumerable()
                        .FirstOrDefault(row => row.Field<int>("id") == idOrden);

                    if (ordenRow != null)
                    {
                        return new OrdenRecaudacionViewModel
                        {
                            Id = ordenRow.Field<int>("id"),
                            NumeroOrden = ordenRow.Field<string>("numero_orden") ?? "",
                            FechaCreacion = ordenRow.Field<DateTime>("fecha_creacion"),
                            Estado = ordenRow.Field<string>("estado") ?? "",
                            Subtotal = ordenRow.Field<decimal>("subtotal"),
                            Admin = ordenRow.Field<decimal>("admin"),
                            Total = ordenRow.Field<decimal>("total"),
                            Observacion = ordenRow.IsNull("observacion") ? "" : ordenRow.Field<string>("observacion")
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log del error si es necesario
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerOrdenPorId: {ex.Message}");
                return null;
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