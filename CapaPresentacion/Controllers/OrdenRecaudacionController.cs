using System;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaPresentacion.Models.ViewModels;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using System.Collections.Generic;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private readonly IOrdenRecaudacionDAO _dao;

        public OrdenRecaudacionController()
        {
            _dao = new OrdenRecaudacionDAO();
        }

        // ============================
        // PANTALLA OBLIGATORIA DESPUÉS DE LOGIN
        // ============================
        [HttpGet]
        public ActionResult Obligatoria()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();

                if (idUsuario <= 0)
                {
                    TempData["Error"] = "Debe iniciar sesión";
                    return RedirectToAction("Login", "Account");
                }

                // Verificar si ya tiene orden válida (por si entró directamente)
                if (_dao.ExisteORGeneradaOPagada(idUsuario))
                {
                    return RedirectToAction("Index", "Dashboard");
                }

                bool tieneOrdenBorrador = _dao.ExisteORMinima(idUsuario);
                ViewBag.TieneOrdenBorrador = tieneOrdenBorrador;

                // Si tiene orden en borrador, obtener información
                if (tieneOrdenBorrador)
                {
                    var dtOrdenes = _dao.ObtenerOrdenesPorUsuario(idUsuario);
                    var ordenBorrador = dtOrdenes.AsEnumerable()
                        .FirstOrDefault(row => row.Field<string>("estado") == "BORRADOR");

                    if (ordenBorrador != null)
                    {
                        ViewBag.OrdenId = Convert.ToInt32(ordenBorrador["id"]);
                        ViewBag.NumeroOrden = ordenBorrador["numero_orden"].ToString();
                        ViewBag.FechaCreacion = Convert.ToDateTime(ordenBorrador["fecha_creacion"]).ToString("dd/MM/yyyy");
                        ViewBag.Total = Convert.ToDecimal(ordenBorrador["total"]).ToString("C");
                    }
                }

                // Obtener conceptos activos para mostrar
                var conceptos = _dao.ObtenerConceptosActivos();
                var listaConceptos = new List<dynamic>();

                if (conceptos.Rows.Count > 0)
                {
                    listaConceptos = conceptos.AsEnumerable()
                        .Take(4) // Mostrar solo 4 principales
                        .Select(row => new
                        {
                            codigo = row.Field<string>("codigo"),
                            nombre = row.Field<string>("nombre"),
                            valor = row.Field<decimal>("valor_base")
                        })
                        .ToList<dynamic>();
                }

                ViewBag.ConceptosPrincipales = listaConceptos;

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Login", "Account");
            }
        }

        // ============================
        // NUEVA ORDEN
        // ============================
        [HttpGet]
        public ActionResult Nueva()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    TempData["Error"] = "Debe iniciar sesión";
                    return RedirectToAction("Login", "Account");
                }

                if (_dao.ExisteORMinima(idUsuario))
                {
                    TempData["Advertencia"] = "Ya tiene una orden en estado BORRADOR. Debe completarla primero.";
                    return RedirectToAction("Obligatoria");
                }

                return View(new OrdenRecaudacionViewModel());
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Obligatoria");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Crear(OrdenRecaudacionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errores = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new
                    {
                        success = false,
                        mensaje = "Errores de validación",
                        errores = errores
                    });
                }

                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "Sesión expirada. Por favor, inicie sesión nuevamente."
                    });
                }

                if (!_dao.ConceptoExiste(model.ConceptoPrincipalCodigo))
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "El concepto seleccionado no es válido."
                    });
                }

                if (model.Estaciones < 0 || model.Estaciones > 50)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "El número de estaciones debe estar entre 0 y 50."
                    });
                }

                if (model.Dias < 0 || model.Dias > 30)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "El número de días debe estar entre 0 y 30."
                    });
                }

                int ordenId = await Task.Run(() =>
                    _dao.InsertarOrdenAOCR(
                        idUsuario,
                        model.CodigoSolicitud ?? 0,
                        model.ConceptoPrincipalCodigo,
                        model.Estaciones,
                        model.Dias,
                        model.Observacion ?? ""
                    ));

                if (ordenId > 0)
                {
                    // Actualizar sesión con que ya tiene orden
                    Session["TieneOrdenGenerada"] = true;
                    Session["TieneOrdenBorrador"] = false;

                    return Json(new
                    {
                        success = true,
                        ordenId = ordenId,
                        mensaje = "✅ Orden creada exitosamente",
                        redireccion = Url.Action("Detalle", "Orden", new { id = ordenId })
                    });
                }

                return Json(new
                {
                    success = false,
                    mensaje = "No se pudo crear la orden."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    mensaje = ex.Message
                });
            }
        }

        [HttpGet]
        public JsonResult ValidarOrdenBorrador()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                bool tieneBorrador = idUsuario > 0 ? _dao.ExisteORMinima(idUsuario) : false;
                return Json(new { tieneBorrador = tieneBorrador }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { tieneBorrador = false }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult ValidarConcepto(string codigo)
        {
            try
            {
                bool existe = _dao.ConceptoExiste(codigo);
                return Json(new
                {
                    valido = existe,
                    mensaje = existe ? "Concepto válido" : "Concepto no encontrado"
                }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { valido = false, mensaje = "Error al validar" }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult ObtenerConceptos()
        {
            try
            {
                var conceptos = _dao.ObtenerConceptosActivos();
                var lista = conceptos.AsEnumerable()
                    .Select(row => new
                    {
                        codigo = row.Field<string>("codigo"),
                        nombre = row.Field<string>("nombre"),
                        valor = row.Field<decimal>("valor_base"),
                        descripcion = row.Field<string>("descripcion")
                    })
                    .ToList();

                return Json(new { success = true, conceptos = lista }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { success = false, mensaje = "Error al cargar conceptos" }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult CalcularTotal(string concepto, int estaciones, int dias)
        {
            try
            {
                decimal valorBase = _dao.ObtenerValorConcepto(concepto);
                decimal inspeccion = estaciones * 500m;
                decimal viaticos = dias * 80m;
                decimal gastosAdmin = viaticos * 0.08m;
                decimal total = valorBase + inspeccion + viaticos + gastosAdmin;

                return Json(new
                {
                    success = true,
                    valorBase = valorBase,
                    inspeccion = inspeccion,
                    viaticos = viaticos,
                    gastosAdmin = gastosAdmin,
                    total = total
                }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { success = false, mensaje = "Error en cálculo" }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================
        // OBTENER ID USUARIO DE SESIÓN
        // ============================
        private int ObtenerIdUsuario()
        {
            if (Session["IdUsuario"] != null &&
                int.TryParse(Session["IdUsuario"].ToString(), out int idUsuario))
            {
                return idUsuario;
            }
            return 0;
        }

        // ============================
        // VERIFICAR ACCESO AL DASHBOARD
        // ============================
        [HttpGet]
        public JsonResult VerificarAccesoDashboard()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    return Json(new
                    {
                        accesoPermitido = false,
                        mensaje = "Sesión expirada",
                        redireccion = Url.Action("Login", "Account")
                    }, JsonRequestBehavior.AllowGet);
                }

                bool tieneOrden = _dao.ExisteORGeneradaOPagada(idUsuario);

                return Json(new
                {
                    accesoPermitido = tieneOrden,
                    tieneOrden = tieneOrden,
                    mensaje = tieneOrden ? "Acceso permitido" : "Requiere orden de recaudación",
                    redireccion = tieneOrden ? "" : Url.Action("Obligatoria")
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    accesoPermitido = false,
                    mensaje = $"Error: {ex.Message}",
                    redireccion = Url.Action("Login", "Account")
                }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
