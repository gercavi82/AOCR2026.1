using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaNegocio;
using CapaNegocio.Services;
using CapaModelo;
using CapaPresentacion.Models;
using DatosLoggingService = CapaDatos.Services.ILoggingService;
using DatosLoggingServiceFactory = CapaDatos.Services.LoggingServiceFactory;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class DireccionController : Controller
    {
        private readonly DireccionBL _bl = new DireccionBL();
        private readonly SolicitudEstadoTransitionBL _solicitudEstadoTransitionBL = new SolicitudEstadoTransitionBL();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly ParametroDAO _parametroDao = new ParametroDAO();
        private readonly SolicitudAocrCorreoService _solicitudAocrCorreoService = new SolicitudAocrCorreoService();
        private readonly DatosLoggingService _logger = DatosLoggingServiceFactory.Create();

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
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult AprobarSolicitudes()
        {
            var solicitudesPendientes = _solicitudDao.ObtenerParaBandejaEjecutivaAprobacion();

            var pendientes = solicitudesPendientes.Count;
            var enRevision = solicitudesPendientes.Count(s =>
                string.Equals(EstadoSolicitud.Normalizar(s.Estado), EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(EstadoSolicitud.Normalizar(s.Estado), EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(EstadoSolicitud.Normalizar(s.Estado), EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase));
            var observadas = solicitudesPendientes.Count(s => string.Equals(EstadoSolicitud.Normalizar(s.Estado), EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase));
            var subsanadas = solicitudesPendientes.Count(s => string.Equals(EstadoSolicitud.Normalizar(s.Estado), EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase));

            var roles = new List<string>();
            if (User != null)
            {
                if (User.IsInRole("DIRDAC")) roles.Add("DIRDAC");
                if (User.IsInRole("Direccion")) roles.Add("Direccion");
                if (User.IsInRole("JefaturaTecnica")) roles.Add("JefaturaTecnica");
                if (User.IsInRole("DirectorGeneral")) roles.Add("DirectorGeneral");
                if (User.IsInRole("Administrador")) roles.Add("Administrador");
            }

            var muestraEstados = solicitudesPendientes
                .Select(s => (s.Estado ?? string.Empty).Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            _logger.LogInfo("[Direccion] BandejaEjecutiva usuario=" + (User != null ? User.Identity.Name : "anon")
                + ", roles=" + string.Join(",", roles)
                + ", total=" + pendientes
                + ", enRevision=" + enRevision
                + ", observadas=" + observadas
                + ", subsanadas=" + subsanadas
                + ", estadosMuestra=" + string.Join(",", muestraEstados));

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
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult ValidacionFinal(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || !EstadoSolicitudEs(solicitud.Estado, EstadoSolicitud.AOCR_Validado))
                return HttpNotFound("Solicitud no encontrada o no está lista para validación final");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult ValidacionFinal(int id, bool aprobada, string observaciones, string condicionesEspeciales, int vigencia)
        {
            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
                if (solicitud == null || !EstadoSolicitudEs(solicitud.Estado, EstadoSolicitud.AOCR_Validado))
                    return HttpNotFound("Solicitud no encontrada o no está lista para validación final");

                int userId = ObtenerUsuarioActualId();

                if (aprobada)
                {
                    // Cambiar estado a aprobado por dirección
                    string mensaje;
                    if (!CambiarEstadoDireccionConReglas(id, EstadoSolicitud.Aprobada, observaciones ?? "Aprobado por Dirección", out mensaje))
                    {
                        TempData["error"] = string.IsNullOrWhiteSpace(mensaje)
                            ? "No fue posible aprobar la solicitud."
                            : mensaje;
                        return RedirectToAction("ValidacionFinal", new { id });
                    }

                    TempData["success"] = "Solicitud aprobada correctamente. Pasará a legalización.";
                    var solicitudActualizada = _solicitudDao.ObtenerPorId(id);
                    _solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, "AOCR_APROBADO_DIRECCION", observaciones);
                    return RedirectToAction("Legalizar", new { id });
                }
                else
                {
                    // Rechazar solicitud
                    string mensaje;
                    if (!CambiarEstadoDireccionConReglas(id, EstadoSolicitud.Observada, observaciones ?? "Rechazado por Dirección", out mensaje))
                    {
                        TempData["error"] = string.IsNullOrWhiteSpace(mensaje)
                            ? "No fue posible rechazar la solicitud."
                            : mensaje;
                        return RedirectToAction("ValidacionFinal", new { id });
                    }

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
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult Legalizar(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || !EstadoSolicitudEs(solicitud.Estado, EstadoSolicitud.Aprobada))
                return HttpNotFound("Solicitud no encontrada o no está lista para legalización");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Legalizar(int id, string firmaDirector, string selloOficial)
        {
            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
                if (solicitud == null || !EstadoSolicitudEs(solicitud.Estado, EstadoSolicitud.Aprobada))
                    return HttpNotFound("Solicitud no encontrada o no está lista para legalización");

                // Cambiar estado a legalizado
                string mensaje;
                if (!CambiarEstadoDireccionConReglas(id, EstadoSolicitud.AOCR_Legalizado, "Certificado legalizado y firmado", out mensaje))
                {
                    TempData["error"] = string.IsNullOrWhiteSpace(mensaje)
                        ? "No fue posible legalizar la solicitud."
                        : mensaje;
                    return RedirectToAction("Legalizar", new { id });
                }

                TempData["success"] = "Certificado legalizado correctamente.";
                var solicitudActualizada = _solicitudDao.ObtenerPorId(id);
                _solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, "AOCR_LEGALIZADO", "Certificado legalizado y firmado");
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
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult EmitirAOCR(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || !EstadoSolicitudEs(solicitud.Estado, EstadoSolicitud.AOCR_Legalizado))
                return HttpNotFound("Solicitud no encontrada o no está lista para emisión");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult EmitirAOCRConfirm(int id)
        {
            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
                if (solicitud == null || !EstadoSolicitudEs(solicitud.Estado, EstadoSolicitud.AOCR_Legalizado))
                    return HttpNotFound("Solicitud no encontrada o no está lista para emisión");

                // Cambiar estado a emitido
                string mensaje;
                if (!CambiarEstadoDireccionConReglas(id, EstadoSolicitud.AOCR_EmitidoRecibido, "Certificado AOCR emitido", out mensaje))
                {
                    TempData["error"] = string.IsNullOrWhiteSpace(mensaje)
                        ? "No fue posible emitir el certificado."
                        : mensaje;
                    return RedirectToAction("EmitirAOCR", new { id });
                }

                TempData["success"] = "Certificado AOCR emitido correctamente.";
                var solicitudActualizada = _solicitudDao.ObtenerPorId(id);
                _solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, "AOCR_EMITIDO_RECIBIDO", "Certificado AOCR emitido");
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

            throw new InvalidOperationException("No se pudo obtener el ID del usuario actual.");
        }

        private bool CambiarEstadoDireccionConReglas(int codigoSolicitud, string nuevoEstado, string observacion, out string mensaje)
        {
            var userId = ObtenerUsuarioActualId();
            return _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                codigoSolicitud,
                nuevoEstado,
                observacion,
                userId,
                UsuarioDireccionPuedeTransicionar,
                out mensaje);
        }

        private bool UsuarioDireccionPuedeTransicionar(string estadoDestino)
        {
            var destino = EstadoSolicitud.Normalizar(estadoDestino);
            if (User != null && (User.IsInRole("Administrador") || User.IsInRole("Direccion") || User.IsInRole("DIRDAC") || User.IsInRole("DirectorGeneral") || User.IsInRole("JefaturaTecnica")))
            {
                return true;
            }

            // Jefatura puede mantener capacidad de observación dentro de su flujo de revisión.
            if (User != null && User.IsInRole("JefaturaTecnica") && destino == EstadoSolicitud.Observada)
            {
                return true;
            }

            return false;
        }

        private static bool EstadoSolicitudEs(string estadoActual, string estadoObjetivo)
        {
            return string.Equals(
                EstadoSolicitud.Normalizar(estadoActual),
                EstadoSolicitud.Normalizar(estadoObjetivo),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
