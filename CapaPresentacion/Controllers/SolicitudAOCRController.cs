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
        private readonly UsuarioPGDAO _usuarioPgDao = new UsuarioPGDAO();
        // Postgres DAOs
        private readonly ContactoEcuadorPGDAO _contactoPgDao = new ContactoEcuadorPGDAO();
        private readonly SolicitudPGDAO _solicitudPgDao = new SolicitudPGDAO();
        private readonly AeronaveSolicitudPGDAO _aeronavePgDao = new AeronaveSolicitudPGDAO();

        // Legado / app actual
        private readonly AeronaveSolicitudDAO _aeronaveSolDAO = new AeronaveSolicitudDAO();
        private readonly PagoDAO _pagoDAO = new PagoDAO();

        public ActionResult Index() => View();

        // =========================================================
        // (NO BORRAR) Placeholder antiguo: RENOMBRADO para evitar CS0111
        // =========================================================
        [HttpGet]
        public JsonResult ObtenerContactoEcuador_Legacy(int codigoSolicitud)
        {
            // Endpoint legacy (si alguien lo llamaba antes). Mantengo para no "eliminar nada".
            return Json(new { success = false, mensaje = "Legacy endpoint. Use ObtenerContactoEcuador." },
                        JsonRequestBehavior.AllowGet);
        }

        // =========================================================
        // GET: Carga el formulario parcial con datos de BD
        // =========================================================
        [HttpGet]
        public ActionResult FormularioEmisionAOCR(int? oid)
        {
            try
            {
                var vm = new SolicitudAOCRViewModel();

                if (Session["CodigoUsuario"] == null)
                    return new HttpStatusCodeResult(401, "Sesión expirada.");

                int usuarioId = Convert.ToInt32(Session["CodigoUsuario"]);

                // 1) Cargar usuario logueado
                vm.Usuario = UsuarioDAO.ObtenerPorId(usuarioId);

                // 2) Si es edición
                if (oid.HasValue && oid.Value > 0)
                {
                    vm.Solicitud = _solicitudBL.ObtenerDetalle(oid.Value);
                    if (vm.Solicitud == null)
                        return Content("<div class='alert alert-danger'>Error: Solicitud no encontrada.</div>");

                    // Seguridad: si no es admin, solo su solicitud
                    if (!EsAdmin() && vm.Solicitud.CodigoUsuario != usuarioId)
                        return new HttpStatusCodeResult(403, "No tiene permisos para acceder a esta solicitud.");

                    // Aeronaves (aocr_tbaeronave_solicitud)
                    vm.Aeronaves = _aeronaveSolDAO.ObtenerPorSolicitud(oid.Value);

                    // Documentos
                    vm.DocumentosExistentes = _documentoDAO.ObtenerPorSolicitud(oid.Value);

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

                        // Nota: si tu proyecto está en C# 5 y tu Usuario NO soporta ?.,
                        // esto compila si tu proyecto usa C# 6+. Si estás en C# 5, cambia a ternarios.
                        Email = (vm.Usuario != null) ? vm.Usuario.Email : null,
                        RepresentanteLegal = (vm.Usuario != null) ? vm.Usuario.NombreCompleto : null,

                        // ✅ tu Usuario NO tiene NumeroRuc, así que usamos CodigoUsuario como fallback
                        // Si el RUC del usuario está en otra tabla/columna, luego lo mapeamos bien.
                        Ruc = (vm.Usuario != null) ? vm.Usuario.CodigoUsuario : null
                    };

                    vm.Aeronaves = new List<AeronaveSolicitud>();
                    vm.DocumentosExistentes = new List<Documento>();
                }

                return PartialView("_FormularioEmisionAOCR", vm);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, "Error interno: " + ex.Message);
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
                    return Json(new { success = false, mensaje = "Sesión expirada." });

                int usuarioId = Convert.ToInt32(Session["CodigoUsuario"]);
                string usuarioCorreo = (Session["Correo"] != null) ? Session["Correo"].ToString() : "sistema";

                if (vm == null || vm.Solicitud == null)
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
            if (obj == null) return;

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
            var pendientes = _solicitudDAO.ObtenerPendientesRevision();
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

        // =========================================================
        // Postgres: Contacto Ecuador
        // =========================================================
        [HttpGet]
        public JsonResult ObtenerContactoEcuador(int codigoSolicitud)
        {
            try
            {
                var c = _contactoPgDao.ObtenerPorSolicitud(codigoSolicitud);

                if (c == null)
                    return Json(new { success = true, data = (object)null }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        nombreRepresentante = c.NombreRepresentante,
                        rucRepresentante = c.RucRepresentante,
                        direccion = c.Direccion,
                        telefono = c.Telefono,
                        correo = c.Correo
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // =========================================================
        // Postgres: datos del formulario (si los necesitas por AJAX)
        // =========================================================
        [HttpGet]
        public JsonResult ObtenerDatosFormulario(int codigoSolicitud)
        {
            var sol = _solicitudPgDao.ObtenerSolicitudPorCodigo(codigoSolicitud);
            var aeronaves = _aeronavePgDao.ObtenerPorSolicitud(codigoSolicitud);

            return Json(new
            {
                success = (sol != null),
                solicitud = sol,
                aeronaves = aeronaves
            }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public JsonResult ObtenerUsuarioLogueadoPG()
        {
            try
            {
                if (Session["CodigoUsuario"] == null)
                    return Json(new { success = false, mensaje = "Sesión expirada." }, JsonRequestBehavior.AllowGet);

                int idUsuario = Convert.ToInt32(Session["CodigoUsuario"]);

                var u = _usuarioPgDao.ObtenerPorId(idUsuario);

                if (u == null)
                    return Json(new { success = false, mensaje = "Usuario no encontrado en Postgres." }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        idUsuario = GetProp(u, "IdUsuario"),
                        codigoUsuario = GetProp(u, "CodigoUsuario"),
                        nombre = GetProp(u, "Nombre"),
                        apellido = GetProp(u, "Apellido"),
                        correo = GetProp(u, "Email"),
                        ruc = GetProp(u, "NumeroRuc"),
                        cargo = GetProp(u, "Cargo"),
                        codigorol = GetProp(u, "CodigoRol")
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private static object GetProp(object obj, string prop)
        {
            var pi = obj.GetType().GetProperty(prop);
            return (pi == null) ? null : pi.GetValue(obj, null);
        }

    }
}
