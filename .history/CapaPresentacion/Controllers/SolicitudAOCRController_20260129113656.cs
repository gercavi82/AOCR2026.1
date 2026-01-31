using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;
using CapaPresentacion.Models;
using CapaNegocio;
using CapaNegocio.Helpers;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class SolicitudAOCRController : Controller
    {
        private readonly SolicitudBL _solicitudBL = new SolicitudBL();
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();
        private readonly DocumentoDAO _documentoDAO = new DocumentoDAO();

        private readonly AeronaveSolicitudDAO _aeronaveSolDAO = new AeronaveSolicitudDAO();
        private readonly PagoDAO _pagoDAO = new PagoDAO();

        public ActionResult Index() => View();

        // =========================================================
        // GET: Carga el formulario parcial con datos de BD
        // =========================================================
        [HttpGet]
        public ActionResult FormularioEmisionAOCR(int? oid)
        {
            try
            {
                var vm = new SolicitudAOCRViewModel();

                // A veces el sistema guarda el id en IdUsuario en vez de CodigoUsuario.
                int usuarioId = 0;
                if (Session["CodigoUsuario"] != null)
                    int.TryParse(Session["CodigoUsuario"].ToString(), out usuarioId);
                else if (Session["IdUsuario"] != null)
                    int.TryParse(Session["IdUsuario"].ToString(), out usuarioId);

                if (usuarioId <= 0)
                    return Content("<div class='alert alert-danger m-3'><i class='fas fa-exclamation-circle'></i> Error: Sesión expirada. Por favor, inicie sesión nuevamente.</div>");

                // 1) Cargar usuario logueado
                vm.Usuario = UsuarioDAO.ObtenerPorId(usuarioId);
                if (vm.Usuario == null)
                    return Content("<div class='alert alert-warning m-3'><i class='fas fa-user-slash'></i> Advertencia: No se encontró la información del usuario.</div>");

                // 2) Si es edición
                if (oid.HasValue && oid.Value > 0)
                {
                    vm.Solicitud = _solicitudBL.ObtenerDetalle(oid.Value);
                    if (vm.Solicitud == null)
                        return Content("<div class='alert alert-danger m-3'><i class='fas fa-search'></i> Error: Solicitud no encontrada.</div>");

                    // Seguridad: si no es admin, solo su solicitud
                    if (!EsAdmin() && vm.Solicitud.CodigoUsuario != usuarioId)
                        return Content("<div class='alert alert-danger m-3'><i class='fas fa-lock'></i> Error: No tiene permisos para acceder a esta solicitud.</div>");

                    // Aeronaves (aocr_tbaeronave_solicitud)
                    vm.Aeronaves = _aeronaveSolDAO.ObtenerPorSolicitud(oid.Value) ?? new List<AeronaveSolicitud>();

                    // Documentos
                    vm.DocumentosExistentes = _documentoDAO.ObtenerPorSolicitud(oid.Value) ?? new List<Documento>();

                    // Pago/comprobante (aocr_tbpago)
                    var pago = _pagoDAO.ObtenerUltimoPorSolicitud(oid.Value);
                    if (pago != null)
                    {
                        vm.Banco = pago.MetodoPago;
                        vm.NumeroComprobante = pago.NumeroFactura;
                    }
                }
                else
                {
                    // NUEVO: precargar desde usuario
                    vm.Solicitud = new SolicitudAOCR
                    {
                        CodigoUsuario = usuarioId,
                        FechaSolicitud = DateTime.Now,
                        Estado = "BORRADOR",
                        Email = vm.Usuario != null ? vm.Usuario.Email : "",
                        RepresentanteLegal = vm.Usuario != null ? vm.Usuario.NombreCompleto : "",

                        // ✅ tu Usuario NO tiene NumeroRuc, así que usamos CodigoUsuario como fallback
                        // Si el RUC del usuario está en otra tabla/columna, luego lo mapeamos bien.
                        Ruc = vm.Usuario != null ? vm.Usuario.CodigoUsuario : 0
                    };

                    vm.Aeronaves = new List<AeronaveSolicitud>();
                    vm.DocumentosExistentes = new List<Documento>();
                }

                return PartialView("_FormularioEmisionAOCR", vm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en FormularioEmisionAOCR: " + ex.Message);
                return Content("<div class='alert alert-danger m-3'><i class='fas fa-exclamation-triangle'></i> Error interno: " + HttpUtility.HtmlEncode(ex.Message) + "</div>");
            }
        }

        // =========================================================
        // POST: Guarda todo el formulario (Solicitud + Aeronaves + Docs + Pago)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FormularioCompleto(SolicitudAOCRViewModel vm)
        {
            try
            {
                if (Session["CodigoUsuario"] == null)
                {
                    if (Session["IdUsuario"] == null)
                        return Json(new { success = false, mensaje = "Sesión expirada." });
                    Session["CodigoUsuario"] = Session["IdUsuario"];
                }

                int usuarioId = Convert.ToInt32(Session["CodigoUsuario"]);
                string usuarioCorreo = Session["Correo"]?.ToString() ?? "sistema";

                if (vm?.Solicitud == null)
                    return Json(new { success = false, mensaje = "Datos de solicitud incompletos." });

                if (string.IsNullOrWhiteSpace(vm.Solicitud.NombreOperador))
                    return Json(new { success = false, mensaje = "Nombre del operador es obligatorio." });

                // Dueño si es nuevo / seguridad si edita
                if (vm.Solicitud.CodigoSolicitud <= 0)
                {
                    vm.Solicitud.CodigoUsuario = usuarioId;
                    vm.Solicitud.TipoSolicitud = 1;
                }
                else
                {
                    var actual = _solicitudDAO.ObtenerPorId(vm.Solicitud.CodigoSolicitud);
                    if (actual == null)
                        return Json(new { success = false, mensaje = "Solicitud no encontrada." });

                    if (!EsAdmin() && actual.CodigoUsuario != usuarioId)
                        return Json(new { success = false, mensaje = "No tiene permisos para modificar esta solicitud." });

                    vm.Solicitud.CodigoUsuario = actual.CodigoUsuario;
                }

                // 1) Guardar Solicitud
                string mensajeOut;
                bool exito;

                if (vm.Solicitud.CodigoSolicitud > 0)
                    exito = _solicitudBL.Actualizar(vm.Solicitud, usuarioId, out mensajeOut, true);
                else
                    exito = _solicitudBL.Crear(vm.Solicitud, usuarioId, out mensajeOut);

                if (!exito)
                    return Json(new { success = false, mensaje = mensajeOut });

                int idFinal = vm.Solicitud.CodigoSolicitud;

                // 2) Aeronaves (reemplazar)
                var aeronaves = (vm.Aeronaves ?? new List<AeronaveSolicitud>())
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Matricula))
                    .ToList();

                _aeronaveSolDAO.ReemplazarPorSolicitud(idFinal, aeronaves, usuarioCorreo);

                // 3) Documentos
                ProcesarArchivos(vm.ArchivosSubidos, idFinal);

                // 4) Pago
                if (!string.IsNullOrWhiteSpace(vm.Banco) || !string.IsNullOrWhiteSpace(vm.NumeroComprobante))
                {
                    _pagoDAO.Insertar(new Pago
                    {
                        CodigoSolicitud = idFinal,
                        MetodoPago = vm.Banco,
                        NumeroFactura = vm.NumeroComprobante,
                        Estado = "REGISTRADO"
                    }, usuarioCorreo);
                }

                return Json(new { success = true, mensaje = "Solicitud AOCR registrada correctamente.", id = idFinal });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error crítico: " + ex.Message });
            }
        }

        // =========================================================
        // Guardar archivos sin depender de nombres de propiedades exactas
        // =========================================================
        private void ProcesarArchivos(IEnumerable<HttpPostedFileBase> archivos, int solicitudId)
        {
            if (archivos == null) return;

            string path = Server.MapPath("~/Uploads/AOCR/" + solicitudId);
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);

            foreach (var file in archivos)
            {
                if (file != null && file.ContentLength > 0)
                {
                    string fileName = System.IO.Path.GetFileName(file.FileName);
                    string rutaRelativa = "/Uploads/AOCR/" + solicitudId + "/" + fileName;

                    file.SaveAs(System.IO.Path.Combine(path, fileName));

                    var doc = new Documento();
                    doc.CodigoSolicitud = solicitudId;

                    // Estos nombres sí los usas tú: NombreArchivo y Estado (si existen)
                    SetIfExists(doc, "NombreArchivo", fileName);
                    SetIfExists(doc, "Estado", "PENDIENTE");

                    // En DB existe ruta_guardada y fecha_carga; tu modelo puede llamarse diferente:
                    SetIfExists(doc, "RutaGuardada", rutaRelativa);
                    SetIfExists(doc, "RutaArchivo", rutaRelativa);   // por si tu clase antigua lo tenía así
                    SetIfExists(doc, "FechaCarga", DateTime.Now);
                    SetIfExists(doc, "FechaSubida", DateTime.Now);   // por si tu clase antigua lo tenía así

                    _documentoDAO.Crear(doc);
                }
            }
        }

        private static void SetIfExists(object obj, string prop, object value)
        {
            var pi = obj.GetType().GetProperty(prop);
            if (pi == null || !pi.CanWrite) return;
            pi.SetValue(obj, value, null);
        }

        // =========================================================
        // Resto de acciones (tu código igual)
        // =========================================================
        public ActionResult MisSolicitudes()
        {
            if (Session["CodigoUsuario"] == null)
                return RedirectToAction("Login", "Account");

            return View(_solicitudDAO.ObtenerPorUsuario(Convert.ToInt32(Session["CodigoUsuario"])));
        }

        public ActionResult RevisarSolicitudes()
        {
            // Si no hay en ENVIADO_A_INSPECTOR, mostramos otros estados pendientes
            var pendientes = _solicitudDAO.ObtenerPendientesRevision();
            if (pendientes == null || pendientes.Count == 0)
            {
                pendientes = _solicitudDAO.ObtenerPorEstados(
                    "PENDIENTE",
                    "EN_REVISION",
                    "ENVIADO_A_INSPECTOR",
                    "ENVIADO_A_JEFATURA"
                );
            }
            return View("RevisarSolicitudes", pendientes);
        }

        [HttpPost]
        [Authorize(Roles = "Inspector")]
        [ValidateAntiForgeryToken]
        public ActionResult Aprobar(string id)
        {
            if (!int.TryParse(id, out int idSolicitud))
                return HttpNotFound();

            var solicitud = _solicitudDAO.ObtenerPorCodigo(idSolicitud);
            if (solicitud == null) return HttpNotFound();

            var stats = ChecklistDAO.ObtenerEstadisticasPorSolicitud(solicitud.CodigoSolicitud);

            bool incompleto =
                stats["Total"] == 0 ||
                stats["SinEvaluar"] > 0 ||
                stats["NoCumplen"] > 0;

            if (incompleto)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] =
                    $"No se puede aprobar. Checklist incompleto: Total={stats["Total"]}, SinEvaluar={stats["SinEvaluar"]}, NoCumplen={stats["NoCumplen"]}.";
                return RedirectToAction("RevisarSolicitudes");
            }

            _solicitudDAO.CambiarEstado(idSolicitud, "APROBADO_POR_INSPECTOR", ObtenerUsuarioActualId(), "Aprobado por inspector");

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "Solicitud aprobada correctamente.";
            return RedirectToAction("RevisarSolicitudes");
        }

        [HttpPost]
        [Authorize(Roles = "Inspector")]
        [ValidateAntiForgeryToken]
        public ActionResult Observar(string id, string observacion)
        {
            if (!int.TryParse(id, out int idSolicitud))
                return HttpNotFound();

            var solicitud = _solicitudDAO.ObtenerPorCodigo(idSolicitud);
            if (solicitud == null) return HttpNotFound();

            _solicitudDAO.CambiarEstado(idSolicitud, "OBSERVADO", ObtenerUsuarioActualId(), observacion ?? "");

            TempData["NotificacionTipo"] = "warning";
            TempData["NotificacionMensaje"] = "Solicitud marcada como observada.";

            if (!string.IsNullOrWhiteSpace(solicitud.Email))
            {
                EmailHelper.EnviarEmail(
                    solicitud.Email,
                    "Observación a su Solicitud AOCR",
                    $"Estimado operador,<br><br>Su solicitud <strong>#{solicitud.CodigoSolicitud}</strong> ha sido <b>observada</b>.<br><br><b>Observación:</b> {observacion}<br><br>Por favor revise y actualice su información.<br><br>Saludos."
                );
            }

            return RedirectToAction("RevisarSolicitudes");
        }

        [Authorize(Roles = "JefaturaTecnica")]
        public ActionResult RevisarPorJefatura()
        {
            var pendientes = _solicitudDAO.ObtenerPorEstado("ENVIADO_A_JEFATURA");
            return View(pendientes);
        }

        [HttpPost]
        [Authorize(Roles = "JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPorJefatura(int id)
        {
            int userId = ObtenerUsuarioActualId();
            _solicitudDAO.CambiarEstado(id, "VALIDADO_TECNICAMENTE", userId, "Aprobado por Jefatura Técnica");
            TempData["Exito"] = "La solicitud ha sido validada técnicamente.";
            return RedirectToAction("RevisarPorJefatura");
        }

        [HttpPost]
        [Authorize(Roles = "JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult ObservarPorJefatura(int id, string observaciones)
        {
            int userId = ObtenerUsuarioActualId();
            _solicitudDAO.CambiarEstado(id, "OBSERVADO_JEFATURA", userId, observaciones ?? "");
            TempData["Exito"] = "Se ha enviado una observación a la solicitud.";
            return RedirectToAction("RevisarPorJefatura");
        }

        public ActionResult Detalle(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            var historialDAO = new HistorialEstadoDAO();
            ViewBag.HistorialEstados = historialDAO.ObtenerPorSolicitud(id);

            return View(solicitud);
        }

        [Authorize(Roles = "CoordinacionLegal")]
        public ActionResult RevisarLegalizacion()
        {
            var lista = _solicitudDAO.ObtenerPorEstado("ENVIADO_A_LEGALIZACION");
            return View(lista);
        }

        [HttpPost]
        [Authorize(Roles = "CoordinacionLegal")]
        [ValidateAntiForgeryToken]
        public ActionResult Legalizar(int id, string observacionLegal = "")
        {
            try
            {
                int userId = ObtenerUsuarioActualId();

                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null) return HttpNotFound();

                string estadoAnterior = solicitud.Estado;

                _solicitudDAO.CambiarEstado(id, "LEGALIZADO", userId, observacionLegal ?? "Legalizado por Coordinación Legal");

                new HistorialEstadoDAO().RegistrarCambio(
                    id,
                    estadoAnterior,
                    "LEGALIZADO",
                    userId,
                    observacionLegal ?? "Legalizado por Coordinación Legal"
                );

                if (!string.IsNullOrWhiteSpace(solicitud.Email))
                {
                    EmailHelper.EnviarEmail(
                        solicitud.Email,
                        "AOCR Legalizado",
                        $"Estimado operador,<br><br>Su solicitud AOCR #{id} ha sido <strong>legalizada</strong>.<br><br><b>Observaciones:</b> {observacionLegal}<br><br>Gracias por su gestión."
                    );
                }

                TempData["Exito"] = "Solicitud legalizada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al legalizar: " + ex.Message;
            }

            return RedirectToAction("RevisarLegalizacion");
        }

        [Authorize(Roles = "Inspector,Administrador")]
        public ActionResult SolicitarInspeccion(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            _solicitudDAO.CambiarEstado(id, "ENVIADO_A_INSPECTOR", ObtenerUsuarioActualId(), "Inspección solicitada");

            TempData["NotificacionMensaje"] = "Inspección solicitada correctamente.";
            return RedirectToAction("Detalle", new { id });
        }

        private int ObtenerUsuarioActualId()
        {
            if (Session["CodigoUsuario"] != null && int.TryParse(Session["CodigoUsuario"].ToString(), out int idUsuario))
                return idUsuario;

            throw new Exception("No se pudo obtener el ID del usuario actual.");
        }

        private bool EsAdmin()
        {
            var rol = (Session["Rol"] ?? "").ToString();
            return rol.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                   rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase);
        }
    }
}
