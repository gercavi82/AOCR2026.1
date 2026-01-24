using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Dapper;
using Npgsql;
using Newtonsoft.Json;

using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaPresentacion.Models;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private readonly OrdenRecaudacionDAO _ordenDAO;
        private readonly ConceptoDAO _conceptoDAO;

        public OrdenRecaudacionController()
        {
            _ordenDAO = new OrdenRecaudacionDAO();
            _conceptoDAO = new ConceptoDAO();
        }

        [HttpGet]
        public ActionResult Obligatoria()
        {
            // Importante: en la vista usa: var tiene = (ViewBag.TieneOrdenBorrador as bool?) ?? false;
            return View();
        }

        public ActionResult Index(string estado = null, string buscar = null)
        {
            int codigoUsuario = ObtenerIdUsuario();
            if (codigoUsuario <= 0) return RedirectToAction("Login", "Account");

            List<OrdenRecaudacionModel> ordenes;

            if (!string.IsNullOrEmpty(buscar))
            {
                ordenes = _ordenDAO.BuscarOrdenes(buscar, codigoUsuario);
                ViewBag.Buscar = buscar;
            }
            else
            {
                ordenes = _ordenDAO.ObtenerOrdenes(codigoUsuario, estado);
            }

            ViewBag.Estados = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Todos" },
                new SelectListItem { Value = "BORRADOR", Text = "Borradores" },
                new SelectListItem { Value = "GENERADA", Text = "Generadas" },
                new SelectListItem { Value = "ENVIADA", Text = "Enviadas" },
                new SelectListItem { Value = "PAGADA", Text = "Pagadas" },
                new SelectListItem { Value = "ANULADA", Text = "Anuladas" }
            };

            ViewBag.EstadoSeleccionado = estado;
            ViewBag.Estadisticas = _ordenDAO.ObtenerEstadisticas(codigoUsuario);

            return View(ordenes);
        }

        public ActionResult Detalles(int id)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            int codigoUsuario = ObtenerIdUsuario();
            if (codigoUsuario <= 0) return RedirectToAction("Login", "Account");

            bool esAdmin = (User != null && User.IsInRole("ADMINISTRADOR"));
            if (orden.CodigoUsuario != codigoUsuario && !esAdmin)
                return RedirectToAction("AccesoDenegado", "Error");

            ViewBag.Documentos = ObtenerDocumentosOrden(id);
            ViewBag.Pagos = ObtenerPagosOrden(id);
            ViewBag.HistorialEstados = ObtenerHistorialEstados(id);

            return View(orden);
        }

        // ✅ GET: Nueva (DEVUELVE VM, NO MODEL)
        [HttpGet]
        public ActionResult Nueva()
        {
            int codigoUsuario = ObtenerIdUsuario();
            if (codigoUsuario <= 0) return RedirectToAction("Login", "Account");

            var vm = new OrdenRecaudacionNuevaVM();
            vm.Orden.CodigoUsuario = codigoUsuario;
            vm.Orden.NombreUsuario = (Session["NombreUsuario"] != null) ? Session["NombreUsuario"].ToString() : null;
            vm.Orden.CorreoUsuario = (Session["Correo"] != null) ? Session["Correo"].ToString() : null;
            vm.Orden.LugarEmision = "Quito";
            vm.Orden.Estado = "BORRADOR";
            vm.Orden.FechaCreacion = DateTime.Now;

            vm.Conceptos = CargarConceptosVM();

            return View(vm);
        }

        // ✅ POST: Nueva (RECIBE VM, NO MODEL)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Nueva(OrdenRecaudacionNuevaVM vm)
        {
            int codigoUsuario = ObtenerIdUsuario();
            if (codigoUsuario <= 0) return RedirectToAction("Login", "Account");

            if (vm == null) vm = new OrdenRecaudacionNuevaVM();
            if (vm.Orden == null) vm.Orden = new OrdenRecaudacionModel();

            // Siempre recargar conceptos si se retorna la vista por error
            vm.Conceptos = CargarConceptosVM();

            // Validaciones mínimas
            if (string.IsNullOrWhiteSpace(vm.Orden.RucCedula))
                ModelState.AddModelError("Orden.RucCedula", "RUC/Cédula es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.Orden.Correo))
                ModelState.AddModelError("Orden.Correo", "Correo es obligatorio.");

            // Leer detalles
            List<OrdenDetallePostVM> detallesPost;
            try
            {
                detallesPost = string.IsNullOrWhiteSpace(vm.DetallesJson)
                    ? new List<OrdenDetallePostVM>()
                    : JsonConvert.DeserializeObject<List<OrdenDetallePostVM>>(vm.DetallesJson) ?? new List<OrdenDetallePostVM>();
            }
            catch
            {
                ModelState.AddModelError("", "El detalle de conceptos no es válido.");
                return View(vm);
            }

            if (!detallesPost.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un concepto a la orden.");
                return View(vm);
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Seguridad: no confiar en el cliente → recalcular en servidor usando DB
            var conceptosDb = _conceptoDAO.ObtenerConceptos(true) ?? new List<ConceptoModel>();

            var detallesGuardar = new List<OrdenDetalleModel>();
            decimal subtotal = 0m, admin = 0m, total = 0m;

            foreach (var d in detallesPost)
            {
                if (d == null) continue;

                var c = conceptosDb.FirstOrDefault(x => x.Id == d.ConceptoId);
                if (c == null) continue;

                var cantidad = (d.Cantidad <= 0) ? 1 : d.Cantidad;

                var valorUnit = c.ValorBase;
                var porAdmin = c.PorcentajeAdmin; // aquí se asume 8 = 8%

                var sub = cantidad * valorUnit;
                var adm = Math.Round(sub * (porAdmin / 100m), 2);
                var totLinea = Math.Round(sub + adm, 2);

                subtotal += sub;
                admin += adm;
                total += totLinea;

                detallesGuardar.Add(new OrdenDetalleModel
                {
                    ConceptoId = c.Id,
                    ConceptoCodigo = c.Codigo,
                    ConceptoNombre = c.Nombre,
                    Cantidad = cantidad,
                    ValorUnitario = valorUnit,
                    PorcentajeAdmin = porAdmin,
                    Subtotal = sub,
                    Admin = adm,
                    TotalLinea = totLinea
                });
            }

            if (!detallesGuardar.Any())
            {
                ModelState.AddModelError("", "No hay detalles válidos para guardar.");
                return View(vm);
            }

            vm.Orden.CodigoUsuario = codigoUsuario;
            vm.Orden.Estado = "BORRADOR";
            vm.Orden.FechaCreacion = DateTime.Now;

            vm.Orden.Subtotal = Math.Round(subtotal, 2);
            vm.Orden.Admin = Math.Round(admin, 2);
            vm.Orden.Total = Math.Round(total, 2);

            // Si tu modelo tiene lista Detalles, la asignas para que DAO lo use
            vm.Orden.Detalles = detallesGuardar;

            try
            {
                var idOrden = _ordenDAO.CrearOrden(vm.Orden);

                TempData["SuccessMessage"] = "Orden creada exitosamente";
                return RedirectToAction("Detalles", new { id = idOrden });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al crear la orden: " + ex.Message);
                return View(vm);
            }
        }

        // ====== Tus métodos existentes (igual) ======

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Generar(int id)
        {
            try
            {
                var orden = _ordenDAO.ObtenerOrdenPorId(id);
                if (orden == null) return HttpNotFound();

                if (!string.Equals(orden.Estado, "BORRADOR", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Solo se pueden generar órdenes en estado BORRADOR";
                    return RedirectToAction("Detalles", new { id = id });
                }

                if (orden.Total <= 0)
                {
                    TempData["ErrorMessage"] = "La orden debe tener un total mayor a 0";
                    return RedirectToAction("Detalles", new { id = id });
                }

                var ok = _ordenDAO.CambiarEstadoOrden(id, "GENERADA");
                TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
                    ok ? "Orden generada exitosamente." : "Error al generar la orden.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction("Detalles", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Enviar(int id)
        {
            try
            {
                var orden = _ordenDAO.ObtenerOrdenPorId(id);
                if (orden == null) return HttpNotFound();

                if (!string.Equals(orden.Estado, "GENERADA", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Solo se pueden enviar órdenes en estado GENERADA";
                    return RedirectToAction("Detalles", new { id = id });
                }

                if (string.IsNullOrWhiteSpace(orden.Correo))
                {
                    TempData["ErrorMessage"] = "La orden no tiene correo del contribuyente";
                    return RedirectToAction("Detalles", new { id = id });
                }

                var pdfBytes = new byte[0]; // TODO: genera el PDF real

                var emailService = new EmailService();
                var enviado = emailService.EnviarOrdenRecaudacion(orden, pdfBytes);

                if (enviado)
                {
                    _ordenDAO.CambiarEstadoOrden(id, "ENVIADA");
                    TempData["SuccessMessage"] = "Orden enviada exitosamente a " + orden.Correo;
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo enviar el correo (revisa SMTP).";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction("Detalles", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarPago(int id, PagoModel pago)
        {
            try
            {
                var orden = _ordenDAO.ObtenerOrdenPorId(id);
                if (orden == null) return HttpNotFound();

                if (pago == null || pago.Monto <= 0)
                {
                    TempData["ErrorMessage"] = "El monto debe ser mayor a 0";
                    return RedirectToAction("Detalles", new { id = id });
                }

                // Seguridad archivo
                if (pago.ComprobanteArchivo != null && pago.ComprobanteArchivo.ContentLength > 0)
                {
                    var ext = Path.GetExtension(pago.ComprobanteArchivo.FileName) ?? "";
                    ext = ext.ToLowerInvariant();

                    var permitidas = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    if (!permitidas.Contains(ext))
                    {
                        TempData["ErrorMessage"] = "Formato no permitido. Solo PDF/JPG/PNG.";
                        return RedirectToAction("Detalles", new { id = id });
                    }

                    var maxBytes = 10 * 1024 * 1024; // 10MB
                    if (pago.ComprobanteArchivo.ContentLength > maxBytes)
                    {
                        TempData["ErrorMessage"] = "Archivo demasiado grande (máx 10MB).";
                        return RedirectToAction("Detalles", new { id = id });
                    }

                    var uploadPath = Server.MapPath("~/Uploads/Comprobantes/");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    var fileName = "PAGO_" + id + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ext;
                    var filePath = Path.Combine(uploadPath, fileName);

                    pago.ComprobanteArchivo.SaveAs(filePath);
                    pago.ComprobanteRuta = "/Uploads/Comprobantes/" + fileName;
                }

                pago.CodigoSolicitud = id;
                pago.FechaPago = DateTime.Now;
                pago.Estado = "Validado";
                pago.ValidadoPor = (User != null) ? User.Identity.Name : "";
                pago.FechaValidacion = DateTime.Now;

                var ok = _ordenDAO.RegistrarPago(id, pago);
                TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
                    ok ? "Pago registrado exitosamente" : "Error al registrar el pago";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction("Detalles", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Anular(int id, string motivo)
        {
            try
            {
                var orden = _ordenDAO.ObtenerOrdenPorId(id);
                if (orden == null) return HttpNotFound();

                if (string.Equals(orden.Estado, "PAGADA", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "No se puede anular una orden ya pagada";
                    return RedirectToAction("Detalles", new { id = id });
                }

                orden.Observacion = (orden.Observacion ?? "")
                    + "\n\nANULADA: " + (motivo ?? "") + " (" + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ")";

                _ordenDAO.ActualizarOrden(orden);
                _ordenDAO.CambiarEstadoOrden(id, "ANULADA");

                TempData["SuccessMessage"] = "Orden anulada exitosamente";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
            }

            return RedirectToAction("Detalles", new { id = id });
        }

        // ====== AUX ======

        private int ObtenerIdUsuario()
        {
            object v1 = Session["UserId"];
            object v2 = Session["IdUsuario"];

            int id;
            if (v1 != null && int.TryParse(v1.ToString(), out id)) return id;
            if (v2 != null && int.TryParse(v2.ToString(), out id)) return id;

            return 0;
        }

        private string Cs()
        {
            // ✅ TU web.config tiene AOCRConnection
            return ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
        }

        private List<ConceptoOptionVM> CargarConceptosVM()
        {
            var conceptos = _conceptoDAO.ObtenerConceptos(true) ?? new List<ConceptoModel>();

            return conceptos
                .OrderBy(x => x.Orden)
                .ThenBy(x => x.Codigo)
                .Select(x => new ConceptoOptionVM
                {
                    Id = x.Id,
                    Codigo = x.Codigo,
                    Nombre = x.Nombre,
                    Valor = x.ValorBase,
                    PorcentajeAdmin = x.PorcentajeAdmin
                })
                .ToList();
        }

        private List<PagoModel> ObtenerPagosOrden(int idOrden)
        {
            using (var cn = new NpgsqlConnection(Cs()))
            {
                cn.Open();
                var sql = "SELECT * FROM aocr_tbpago WHERE codigo_solicitud = @IdOrden ORDER BY fecha_pago DESC;";
                return cn.Query<PagoModel>(sql, new { IdOrden = idOrden }).ToList();
            }
        }

        private List<DocumentoModel> ObtenerDocumentosOrden(int idOrden)
        {
            using (var cn = new NpgsqlConnection(Cs()))
            {
                cn.Open();
                var sql = "SELECT * FROM aocr_tbdocumento WHERE codigo_solicitud = @IdOrden ORDER BY fecha_carga DESC;";
                return cn.Query<DocumentoModel>(sql, new { IdOrden = idOrden }).ToList();
            }
        }

        private List<dynamic> ObtenerHistorialEstados(int idOrden)
        {
            using (var cn = new NpgsqlConnection(Cs()))
            {
                cn.Open();
                var sql =
                    "SELECT estado_nuevo, fecha_cambio " +
                    "FROM aocr_tbhistorial_estado " +
                    "WHERE codigo_solicitud = @IdOrden " +
                    "ORDER BY fecha_cambio DESC;";

                return cn.Query<dynamic>(sql, new { IdOrden = idOrden }).ToList();
            }
        }
    }
}
