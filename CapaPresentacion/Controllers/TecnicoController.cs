using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using CapaDatos.DAOs;
using CapaDatos.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize] // No restringas aquí para no bloquear otras acciones por rol
    public class TecnicoController : Controller
    {
        private readonly ILoggingService _logger;

        public TecnicoController()
        {
            _logger = LoggingServiceFactory.Create();
        }

        // ✅ Según tu error, tu carpeta REAL parece ser: Views/Tecnico
        // Si NO es esa, cámbiala a la carpeta real (por ejemplo: "~/Views/Tecnico/")
        private const string VIEWS_TECNICO = "~/Views/Tecnico/";

        // =======================================================
        // LISTADO - Solicitudes pendientes de asignación
        // =======================================================
        [Authorize(Roles = "Administrador,Direccion,JefaturaTecnica")]
        public ActionResult Index()
        {
            _logger.LogInfo("[InspeccionesController] Inicio pantalla gestion (Tecnico/Index). Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual());

            // Obtener solicitudes que necesitan asignación de inspector
            var lista = SolicitudAOCRBL.ObtenerPendientesAsignacion();

            if (lista == null)
            {
                _logger.LogWarning("[InspeccionesController] Lista de pendientes vino NULL.");
            }
            else if (lista.Count == 0)
            {
                _logger.LogWarning("[InspeccionesController] No hay pendientes para asignacion de inspector.");
            }
            else
            {
                _logger.LogInfo("[InspeccionesController] Pendientes para asignacion=" + lista.Count);
            }

            return View(VIEWS_TECNICO + "Index.cshtml", lista);
        }

        // =======================================================
        // CREAR
        // =======================================================
        [Authorize(Roles = "Administrador")]
        public ActionResult Crear()
        {
            return View(VIEWS_TECNICO + "Crear.cshtml");
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Tecnico modelo)
        {
            if (!ModelState.IsValid)
                return View(VIEWS_TECNICO + "Crear.cshtml", modelo);

            string mensaje;
            bool ok = TecnicoBL.Insertar(modelo, out mensaje);

            if (!ok)
            {
                ViewBag.Error = mensaje;
                return View(VIEWS_TECNICO + "Crear.cshtml", modelo);
            }

            TempData["Success"] = "Técnico creado correctamente.";
            return RedirectToAction("Index");
        }

        // =======================================================
        // EDITAR
        // =======================================================
        [Authorize(Roles = "Administrador")]
        public ActionResult Editar(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var modelo = TecnicoBL.ObtenerPorId(id);
            if (modelo == null)
            {
                TempData["Error"] = "Técnico no encontrado.";
                return RedirectToAction("Index");
            }

            return View(VIEWS_TECNICO + "Editar.cshtml", modelo);
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Tecnico modelo)
        {
            if (!ModelState.IsValid)
                return View(VIEWS_TECNICO + "Editar.cshtml", modelo);

            string mensaje;
            bool ok = TecnicoBL.Actualizar(modelo, out mensaje);

            if (!ok)
            {
                ViewBag.Error = mensaje;
                return View(VIEWS_TECNICO + "Editar.cshtml", modelo);
            }

            TempData["Success"] = "Técnico actualizado correctamente.";
            return RedirectToAction("Index");
        }

        // =======================================================
        // ELIMINAR
        // =======================================================
        [Authorize(Roles = "Administrador")]
        public ActionResult Eliminar(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            string mensaje;
            bool ok = TecnicoBL.Eliminar(id, out mensaje);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Técnico eliminado correctamente."
                : mensaje;

            return RedirectToAction("Index");
        }

        // =======================================================
        // ASIGNAR INSPECTOR (GET)
        // =======================================================
        [HttpGet]
        [Authorize(Roles = "Administrador,Direccion,JefaturaTecnica")]
        public ActionResult AsignarInspector(int? solicitudId, string tipoInspector = "OPS")
        {
            _logger.LogInfo("[InspeccionesController] Inicio pantalla gestion de asignacion. Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual() + ", SolicitudId=" + (solicitudId.HasValue ? solicitudId.Value.ToString() : "null"));

            if (!solicitudId.HasValue || solicitudId.Value <= 0)
            {
                _logger.LogWarning("[InspeccionesController] Bloqueo funcional: solicitudId invalido.");
                TempData["Info"] = "Seleccione una solicitud pendiente para asignar inspector.";
                return RedirectToAction("Index");
            }

            var solicitud = SolicitudAOCRBL.ObtenerPorId(solicitudId.Value);
            if (solicitud == null)
            {
                _logger.LogWarning("[InspeccionesController] Bloqueo funcional: solicitud no encontrada. SolicitudId=" + solicitudId.Value);
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Index");
            }

            _logger.LogInfo("[InspeccionesController] SolicitudId=" + solicitud.CodigoSolicitud + ", EstadoActual=" + (solicitud.Estado ?? "(null)") + ", NumeroSolicitud=" + (solicitud.NumeroSolicitud ?? ""));

            var esReasignacion = TieneInspectorAsignado(solicitud);

            var tipoInspectorNormalizado = NormalizarTipoInspector(tipoInspector);
            var inspectores = new List<CapaDatos.Models.InspectorAs400Record>();
            var origenInspectores = "DB2";

            try
            {
                var inspectorAs400Dao = new InspectorAS400DAO(new SecureConfigurationService());
                inspectores = tipoInspectorNormalizado == "TODOS"
                    ? inspectorAs400Dao.ListarActivosPorTipos(new[] { "OPS", "AIR" })
                    : inspectorAs400Dao.ListarActivosPorTipo(tipoInspectorNormalizado);

                var mirrorDiagnostic = new InspectorMirrorPGDAO().DiagnosticarEspejo(inspectores);
                if (mirrorDiagnostic.TablaExiste)
                {
                    origenInspectores = "DB2 + diagnostico espejo PostgreSQL";
                }
                else
                {
                    _logger.LogWarning("[InspectoresDAO-PG] Espejo no disponible (tabla inexistente). Flujo usa DB2 directo.");
                }

                _logger.LogInfo("[InspeccionesController] Origen inspectores=" + origenInspectores + ", TipoFiltro=" + tipoInspectorNormalizado + ", InspectoresRecibidos=" + (inspectores == null ? -1 : inspectores.Count));

                if (inspectores == null || inspectores.Count == 0)
                {
                    _logger.LogWarning("[InspeccionesController] Lista de inspectores vacia para SolicitudId=" + solicitud.CodigoSolicitud + ".");
                    ViewBag.WarningInspectores = "No se encontraron inspectores en AS400 para el filtro seleccionado. Verifique estado/tipo en OPIAR2.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[InspeccionesController] Error al cargar inspectores: " + ex);
                TempData["Error"] = "No se pudo cargar inspectores institucionales desde AS400: " + ex.Message;
            }

            ViewBag.TipoInspector = tipoInspectorNormalizado;
            ViewBag.TiposInspector = new SelectList(
                new List<SelectListItem>
                {
                    new SelectListItem { Value = "OPS", Text = "Operaciones (OPS)" },
                    new SelectListItem { Value = "AIR", Text = "Aeronavegabilidad (AIR)" },
                    new SelectListItem { Value = "TODOS", Text = "Todos (OPS + AIR)" }
                },
                "Value",
                "Text",
                tipoInspectorNormalizado);
            ViewBag.Inspectores = new SelectList(
                inspectores.Select(i => new
                {
                    Cedula = i.Cedula,
                    Etiqueta = i.EtiquetaLista
                }),
                "Cedula",
                "Etiqueta",
                solicitud.TecnicoResponsableCedula);
            ViewBag.InspectoresApoyo = new SelectList(
                inspectores.Select(i => new
                {
                    Cedula = i.Cedula,
                    Etiqueta = i.EtiquetaLista
                }),
                "Cedula",
                "Etiqueta",
                solicitud.InspectorApoyoCedula);
            ViewBag.EsReasignacion = esReasignacion;

            _logger.LogInfo("[InspeccionesController] ViewModel cargado correctamente. SolicitudId=" + solicitud.CodigoSolicitud + ", ViewBagInspectores=" + inspectores.Count);

            return View(VIEWS_TECNICO + "AsignarInspector.cshtml", solicitud);
        }

        // =======================================================
        // ASIGNAR INSPECTOR (POST)
        // =======================================================
        [HttpPost]
        [Authorize(Roles = "Administrador,Direccion,JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarInspector(
            int solicitudId,
            string inspectorPrincipal,
            string inspectorApoyo,
            DateTime fechaInspeccion,
            string horaInspeccion,
            string observaciones,
            string tipoInspector = "OPS")
        {
            _logger.LogInfo("[GestionInspeccion] Inicio. SolicitudId=" + solicitudId + ", Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual() + ", TipoInspector=" + (tipoInspector ?? "") + ", InspectorPrincipal=" + (inspectorPrincipal ?? "") + ", InspectorApoyo=" + (inspectorApoyo ?? ""));

            if (solicitudId <= 0)
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=SolicitudId invalido");
                TempData["Error"] = "Solicitud inválida.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(inspectorPrincipal))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=No existe inspector asignado en request");
                TempData["Error"] = "Debe seleccionar un inspector principal activo.";
                return RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
            }

            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(solicitudId);
                var esReasignacion = TieneInspectorAsignado(solicitud);
                _logger.LogInfo("[GestionInspeccion] EstadoActual=" + (solicitud == null ? "(solicitud-null)" : (solicitud.Estado ?? "(null)")));

                var db2Dao = new InspectorAS400DAO(new SecureConfigurationService());
                var inspectorDb2 = db2Dao.ObtenerActivoPorCedula(inspectorPrincipal, tipoInspector);
                var existeEnDb2 = inspectorDb2 != null;
                var existeEnPg = new InspectorMirrorPGDAO().ExisteInspectorActivoEnPg(inspectorPrincipal, tipoInspector);

                TimeSpan horaRevision;
                if (!TimeSpan.TryParse(horaInspeccion, out horaRevision))
                {
                    horaRevision = new TimeSpan(9, 0, 0);
                }

                var fechaHoraInspeccion = fechaInspeccion.Date.Add(horaRevision);

                _logger.LogInfo("[GestionInspeccion] ExisteEnDB2=" + existeEnDb2 + ", ExisteEnPG=" + existeEnPg);

                string mensaje;
                bool ok = SolicitudAOCRBL.AsignarInspectores(
                    solicitudId,
                    inspectorPrincipal,
                    inspectorApoyo,
                    fechaHoraInspeccion,
                    observaciones,
                    tipoInspector,
                    ObtenerUsuarioActual(),
                    out mensaje
                );

                _logger.LogInfo("[GestionInspeccion] PuedeGestionar=" + ok + ", Motivo=" + (mensaje ?? "(sin mensaje)"));

                if (ok)
                {
                    var solicitudActualizada = SolicitudAOCRBL.ObtenerPorId(solicitudId) ?? solicitud;
                    var nombreTecnico = FirstNonEmpty(
                        solicitudActualizada != null ? solicitudActualizada.TecnicoResponsableNombre : null,
                        inspectorDb2 != null ? inspectorDb2.NombreCompleto : null,
                        inspectorPrincipal);

                    string mensajeCorreo;
                    var correoEnviado = NotificarSolicitanteAsignacionTecnico(
                        solicitudActualizada,
                        nombreTecnico,
                        fechaHoraInspeccion,
                        esReasignacion,
                        out mensajeCorreo);

                    if (correoEnviado)
                    {
                        TempData["Success"] = (mensaje ?? (esReasignacion ? "Reasignación realizada correctamente." : "Asignación realizada correctamente.")) + " Correo enviado al solicitante.";
                    }
                    else
                    {
                        TempData["Success"] = (mensaje ?? (esReasignacion ? "Reasignación realizada correctamente." : "Asignación realizada correctamente."));
                        if (!string.IsNullOrWhiteSpace(mensajeCorreo))
                        {
                            TempData["Warning"] = mensajeCorreo;
                        }
                    }
                }
                else
                {
                    TempData["Error"] = mensaje;
                }

                return ok
                    ? RedirectToAction("Index")
                    : RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error no controlado en asignacion: " + ex);
                TempData["Error"] = "Error crítico: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Direccion,JefaturaTecnica")]
        public JsonResult ListarInspectoresActivos(string tipoInspector = "OPS")
        {
            _logger.LogInfo("[InspeccionesController] Inicio endpoint AJAX inspectores. Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual() + ", TipoInspector=" + (tipoInspector ?? ""));

            var tipoNormalizado = NormalizarTipoInspector(tipoInspector);
            var dao = new InspectorAS400DAO(new SecureConfigurationService());
            var data = tipoNormalizado == "TODOS"
                ? dao.ListarActivosPorTipos(new[] { "OPS", "AIR" })
                : dao.ListarActivosPorTipo(tipoNormalizado);

            var payload = data
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Cedula))
                .Select(x => new
                {
                    cedula = x.Cedula,
                    nombre = x.NombreCompleto,
                    tipo = x.Tipo,
                    etiqueta = x.EtiquetaLista
                })
                .ToList();

            _logger.LogInfo("[InspeccionesController] Endpoint AJAX inspectores OK. Origen=DB2, Tipo=" + tipoNormalizado + ", Cantidad=" + payload.Count);

            return Json(new { success = true, tipo = tipoNormalizado, origen = "DB2", items = payload }, JsonRequestBehavior.AllowGet);
        }

        private static string NormalizarTipoInspector(string tipoInspector)
        {
            if (string.IsNullOrWhiteSpace(tipoInspector))
            {
                return "OPS";
            }

            var value = tipoInspector.Trim().ToUpperInvariant();
            if (value == "OPS" || value == "AIR" || value == "TODOS")
            {
                return value;
            }

            return "OPS";
        }

        private string ObtenerUsuarioActual()
        {
            if (Session != null && Session["Usuario"] != null)
            {
                return Session["Usuario"].ToString();
            }

            return (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                ? User.Identity.Name
                : "ANONIMO";
        }

        private string ObtenerRolActual()
        {
            var roles = new[] { "Administrador", "Direccion", "JefaturaTecnica" }
                .Where(r => User != null && User.IsInRole(r))
                .ToList();

            return roles.Count == 0 ? "SIN_ROL_DETECTADO" : string.Join(",", roles);
        }

        private bool NotificarSolicitanteAsignacionTecnico(SolicitudAOCR solicitud, string nombreTecnico, DateTime fechaInspeccion, bool esReasignacion, out string mensaje)
        {
            mensaje = string.Empty;

            if (solicitud == null)
            {
                mensaje = "No fue posible enviar correo: la solicitud no está disponible.";
                return false;
            }

            var destinatario = FirstNonEmpty(solicitud.CorreoRepresentanteTecnico, solicitud.Email);
            if (string.IsNullOrWhiteSpace(destinatario))
            {
                mensaje = "No se envió correo al solicitante porque no tiene correo registrado.";
                return false;
            }

            var tecnico = FirstNonEmpty(nombreTecnico, "Técnico asignado");
            var fechaTexto = fechaInspeccion.ToString("dd/MM/yyyy");
            var horaTexto = fechaInspeccion.TimeOfDay == TimeSpan.Zero
                ? "No especificada"
                : fechaInspeccion.ToString("HH:mm");
            var numeroSolicitud = FirstNonEmpty(solicitud.NumeroSolicitud, "#" + solicitud.CodigoSolicitud);

            string enlaceDetalle;
            try
            {
                enlaceDetalle = Url.Action("Detalle", "SolicitudAOCR", new { id = solicitud.CodigoSolicitud }, Request != null && Request.Url != null ? Request.Url.Scheme : "http");
            }
            catch
            {
                enlaceDetalle = string.Empty;
            }

            var asunto = esReasignacion
                ? "AOCR - Inspector reasignado para su proceso " + numeroSolicitud
                : "AOCR - Técnico asignado para su proceso " + numeroSolicitud;
            var cuerpo = "<p>Estimado/a solicitante,</p>"
                + "<p>Le informamos que "
                + (esReasignacion
                    ? "se actualizó la asignación del inspector para su proceso AOCR <strong>" + HttpUtility.HtmlEncode(numeroSolicitud) + "</strong>."
                    : "ya se asignó un técnico para su proceso AOCR <strong>" + HttpUtility.HtmlEncode(numeroSolicitud) + "</strong>.")
                + "</p>"
                + "<ul>"
                + "<li><strong>" + (esReasignacion ? "Inspector reasignado" : "Técnico asignado") + ":</strong> " + HttpUtility.HtmlEncode(tecnico) + "</li>"
                + "<li><strong>Fecha de inspección:</strong> " + HttpUtility.HtmlEncode(fechaTexto) + "</li>"
                + "<li><strong>Hora de inspección:</strong> " + HttpUtility.HtmlEncode(horaTexto) + "</li>"
                + "</ul>"
                + (!string.IsNullOrWhiteSpace(enlaceDetalle)
                    ? "<p>Puede revisar el detalle de su solicitud en el siguiente enlace: <a href=\"" + HttpUtility.HtmlAttributeEncode(enlaceDetalle) + "\">Ver detalle</a>.</p>"
                    : string.Empty)
                + "<p>Atentamente,<br/>Dirección General de Aviación Civil</p>";

            try
            {
                var servicioCorreo = new EnviarCorreo();
                var enviado = servicioCorreo.enviaMensajeCorreo(destinatario, asunto, cuerpo);
                if (!enviado)
                {
                    mensaje = "La asignación fue guardada, pero no se pudo enviar el correo al solicitante.";
                }

                return enviado;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error enviando correo de asignación. SolicitudId=" + solicitud.CodigoSolicitud + ", Error=" + ex.Message);
                mensaje = "La asignación fue guardada, pero ocurrió un error enviando el correo al solicitante.";
                return false;
            }
        }

        private static bool TieneInspectorAsignado(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return false;
            }

            return solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableCedula)
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return null;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

    }
}
