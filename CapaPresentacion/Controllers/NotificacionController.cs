using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Services;
using CapaDatos.Constants;
using CapaDatos.Entidades;
using CapaDatos.Services;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// API Controller para gestió de notificaciones en tiempo real
    /// Endpoints RESTful para consultar, marcar como leÃ­das y eliminar notificaciones
    /// </summary>
    [Authorize]
    public class NotificacionController : Controller
    {
        private static readonly IUserContextAccessor _userContext = new UserContextAccessor();
        private readonly CapaDatos.Services.ILoggingService _logger = CapaDatos.Services.LoggingServiceFactory.Create();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly OrdenRecaudacionDAO _ordenDao = new OrdenRecaudacionDAO();
        private readonly SolicitudAocrCorreoService _solicitudCorreoService = new SolicitudAocrCorreoService();
        private readonly InspeccionCorreoService _inspeccionCorreoService = new InspeccionCorreoService();
        private readonly OrdenRecaudacionCorreoService _ordenCorreoService = new OrdenRecaudacionCorreoService();

        // ============================================
        // OBTENER CODIGOUSUARIO DE SESIÃ“N
        // ============================================
        private int ObtenerCodigoUsuario()
        {
            int id;
            return _userContext.TryGetCodigoUsuario(Session, out id) ? id : 0;
        }


        // ============================================
        // GET: /Notificacion
        // Panel de notificaciones del usuario
        // ============================================
        public ActionResult Index()
        {
            int codigoUsuario = ObtenerCodigoUsuario();
            if (codigoUsuario == 0)
                return RedirectToAction("Login", "Account");

            var notificaciones = NotificacionBL.ObtenerPorUsuario(codigoUsuario);
            return View(notificaciones);
        }


        // ============================================
        // GET: /Notificacion/ObtenerNoLeidas
        // API JSON para obtener notificaciones no leÃ­das
        // ============================================
        [HttpGet]
        public JsonResult ObtenerNoLeidas()
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                if (codigoUsuario == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario no autenticado"
                    }, JsonRequestBehavior.AllowGet);
                }

                var notificaciones = NotificacionBL.ObtenerNoLeidas(codigoUsuario);
                int cantidad = NotificacionBL.ContarNoLeidas(codigoUsuario);

                return Json(new
                {
                    success = true,
                    cantidad = cantidad,
                    notificaciones = notificaciones.Select(n => new
                    {
                        id = n.CodigoNotificacion,
                        titulo = n.Titulo,
                        mensaje = n.Mensaje,
                        tipo = n.Tipo,
                        url = n.Url,
                        fecha = n.FechaCreacion.HasValue ? n.FechaCreacion.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        icono = TiposNotificacion.ObtenerIcono(n.Tipo),
                        color = TiposNotificacion.ObtenerColorBadge(n.Tipo)
                    }).ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al obtener notificaciones: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        // ============================================
        // GET: /Notificacion/ObtenerRecientes
        // API JSON para obtener últimas N notificaciones
        // ============================================
        [HttpGet]
        public JsonResult ObtenerRecientes(int cantidad = 10)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                if (codigoUsuario == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario no autenticado"
                    }, JsonRequestBehavior.AllowGet);
                }

                var notificaciones = NotificacionBL.ObtenerRecientes(codigoUsuario, cantidad);

                return Json(new
                {
                    success = true,
                    notificaciones = notificaciones.Select(n => new
                    {
                        id = n.CodigoNotificacion,
                        titulo = n.Titulo,
                        mensaje = n.Mensaje,
                        tipo = n.Tipo,
                        url = n.Url,
                        leida = n.Leida,
                        fecha = n.FechaCreacion.HasValue ? n.FechaCreacion.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        icono = TiposNotificacion.ObtenerIcono(n.Tipo),
                        color = TiposNotificacion.ObtenerColorBadge(n.Tipo)
                    }).ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al obtener notificaciones: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        // ============================================
        // POST: /Notificacion/MarcarComoLeida
        // Marca una notificació especÃ­fica como leÃ­da
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarcarComoLeida(int id)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                if (codigoUsuario == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario no autenticado"
                    });
                }

                string mensaje;
                bool resultado = NotificacionBL.MarcarComoLeida(id, codigoUsuario, out mensaje);

                return Json(new
                {
                    success = resultado,
                    message = mensaje
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al marcar notificació: " + ex.Message
                });
            }
        }


        // ============================================
        // POST: /Notificacion/MarcarTodasComoLeidas
        // Marca todas las notificaciones del usuario como leÃ­das
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarcarTodasComoLeidas()
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                if (codigoUsuario == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario no autenticado"
                    });
                }

                string mensaje;
                bool resultado = NotificacionBL.MarcarTodasComoLeidas(codigoUsuario, out mensaje);

                return Json(new
                {
                    success = resultado,
                    message = mensaje
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al marcar todas como leÃ­das: " + ex.Message
                });
            }
        }


        // ============================================
        // POST: /Notificacion/Eliminar
        // Elimina una notificació especÃ­fica
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Eliminar(int id)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                if (codigoUsuario == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario no autenticado"
                    });
                }

                string mensaje;
                bool resultado = NotificacionBL.Eliminar(id, out mensaje);

                return Json(new
                {
                    success = resultado,
                    message = mensaje
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al eliminar notificació: " + ex.Message
                });
            }
        }


        // ============================================
        // POST: /Notificacion/EliminarTodas
        // Elimina todas las notificaciones leÃ­das del usuario
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Operador")]
        public JsonResult EliminarTodas()
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                if (codigoUsuario == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Usuario no autenticado"
                    });
                }

                string mensaje;
                bool resultado = NotificacionBL.EliminarTodasLeidas(codigoUsuario, out mensaje);

                return Json(new
                {
                    success = resultado,
                    message = mensaje
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error al eliminar notificaciones: " + ex.Message
                });
            }
        }


        // ============================================
        // GET: /Notificacion/ContarNoLeidas
        // Obtiene solo el conteo de notificaciones no leÃ­das
        // ============================================
          [HttpGet]
          [AllowAnonymous]
          public JsonResult ContarNoLeidas()
          {
              var authenticated = User != null && User.Identity != null && User.Identity.IsAuthenticated;
              var codigoUsuario = ObtenerCodigoUsuario();
              var rol = Convert.ToString(Session != null ? Session["Rol"] ?? Session["RolActivo"] ?? Session["SelectedRole"] : null);
              var compania = Session != null ? CompaniaActivaSessionHelper.ObtenerCodigo(Session) : string.Empty;
              _logger.LogInfo(string.Format(
                  "[NOTIFICACIONES][CONTAR_IN] UsuarioId={0}; Rol={1}; CompaniaActiva={2}; Authenticated={3}",
                  codigoUsuario, rol, compania, authenticated));

              try
              {
                  if (!authenticated || codigoUsuario == 0)
                  {
                      Response.StatusCode = 401;
                      Response.SuppressFormsAuthenticationRedirect = true;
                      Response.TrySkipIisCustomErrors = true;
                      _logger.LogInfo("[NOTIFICACIONES][CONTAR_OUT] HttpStatus=401; Code=401; Total=0; Motivo=Sesion no activa");
                      return Json(new
                      {
                          success = false,
                          ok = false,
                          code = 401,
                          cantidad = 0,
                          message = "La sesión no está activa."
                      }, JsonRequestBehavior.AllowGet);
                  }

                  int cantidad = NotificacionBL.ContarNoLeidas(codigoUsuario);
                  _logger.LogInfo(string.Format(
                      "[NOTIFICACIONES][CONTAR_OUT] HttpStatus=200; Code=200; Total={0}; Motivo=OK",
                      cantidad));

                  return Json(new
                  {
                      success = true,
                      ok = true,
                      cantidad = cantidad
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Response.SuppressFormsAuthenticationRedirect = true;
                Response.TrySkipIisCustomErrors = true;
                _logger.LogError(ex, new CapaDatos.Services.LogContext { ErrorCode = "NOTIFICACIONES_CONTAR_ERROR", UserId = codigoUsuario.ToString() });
                _logger.LogInfo("[NOTIFICACIONES][CONTAR_OUT] HttpStatus=500; Code=500; Total=0; Motivo=Error interno");
                return Json(new
                {
                    success = false,
                    ok = false,
                    code = 500,
                    cantidad = 0,
                    message = "No se pudo consultar el total de notificaciones."
                }, JsonRequestBehavior.AllowGet);
            }
        }


        // ============================================
        // POST: /Notificacion/Enviar (Admin/Testing)
        // EnvÃ­a una notificació manual (solo para testing)
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public JsonResult Enviar(int codigoUsuarioDestino, string titulo, string mensaje, string tipo = "INFO", string url = null)
        {
            try
            {
                bool resultado = NotificacionBL.EnviarNotificacion(codigoUsuarioDestino, titulo, mensaje, tipo, url);

                return Json(new
                {
                    success = resultado,
                    message = resultado ? "Notificació enviada correctamente" : "Error al enviar notificació"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error: " + ex.Message
                });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public ActionResult CorreosPrueba()
        {
            var model = new CapaPresentacion.Models.CorreoPruebaViewModel
            {
                CorreoDestino = "gercavi82@gmail.com",
                NombreDestino = "Pruebas AOCR"
            };

            CargarPlantillas(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public ActionResult CorreosPrueba(CapaPresentacion.Models.CorreoPruebaViewModel model)
        {
            CargarPlantillas(model);

            if (!ModelState.IsValid)
            {
                model.ResultadoExitoso = false;
                model.ResultadoMensaje = "Revise los datos obligatorios antes de enviar el correo de prueba.";
                return View(model);
            }

            string mensaje;
            var exito = EnviarCorreoPrueba(model, out mensaje);
            model.ResultadoExitoso = exito;
            model.ResultadoMensaje = mensaje;
            return View(model);
        }

        private bool EnviarCorreoPrueba(CapaPresentacion.Models.CorreoPruebaViewModel model, out string mensaje)
        {
            string plantilla = (model.Plantilla ?? string.Empty).Trim();
            string[] partes = plantilla.Split(new[] { ':' });
            if (partes.Length < 2)
            {
                mensaje = "La plantilla seleccionada no es válida.";
                return false;
            }

            switch (partes[0])
            {
                case "LEGACY":
                    return EnviarSolicitudRegistradaPrueba(model, out mensaje);

                case "GENERIC":
                    return EnviarCambioEstadoPrueba(model, partes.Length >= 3 ? partes[2] : null, out mensaje);

                case "SOLICITUD":
                    return EnviarCorreoSolicitudPrueba(model, partes[1], out mensaje);

                case "INSPECCION":
                    return EnviarCorreoInspeccionPrueba(model, partes[1], out mensaje);

                case "ORDEN":
                    return EnviarCorreoOrdenPrueba(model, partes[1], out mensaje);

                default:
                    mensaje = "La categoría de plantilla seleccionada no está soportada.";
                    return false;
            }
        }

        private bool EnviarSolicitudRegistradaPrueba(CapaPresentacion.Models.CorreoPruebaViewModel model, out string mensaje)
        {
            if (!model.SolicitudId.HasValue || model.SolicitudId.Value <= 0)
            {
                mensaje = "Debe especificar una solicitud AOCR válida para el correo legacy de registro.";
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(model.SolicitudId.Value);
            if (solicitud == null)
            {
                mensaje = "No se encontró la solicitud AOCR indicada.";
                return false;
            }

            var numeroSolicitud = FirstNonEmpty(solicitud.NumeroSolicitud, "#" + solicitud.CodigoSolicitud);
            var operador = FirstNonEmpty(solicitud.NombreOperador, solicitud.RazonSocial, "Operador");
            var codigoOaci = FirstNonEmpty(solicitud.CodigoOaci, solicitud.CompaniasSeleccionadas, "No registrado");
            var fechaTexto = (solicitud.FechaSolicitud ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm");
            var enlaceDetalle = ConstruirUrlSolicitud(model.SolicitudId.Value);

            var asunto = "AOCR - Solicitud registrada " + numeroSolicitud;
            var cuerpo = "<p>Estimado/a solicitante,</p>"
                + "<p>Su solicitud AOCR se registró correctamente en el sistema.</p>"
                + "<ul>"
                + "<li><strong>Número de solicitud:</strong> " + HttpUtility.HtmlEncode(numeroSolicitud) + "</li>"
                + "<li><strong>Operador:</strong> " + HttpUtility.HtmlEncode(operador) + "</li>"
                + "<li><strong>Código OACI:</strong> " + HttpUtility.HtmlEncode(codigoOaci) + "</li>"
                + "<li><strong>Fecha de registro:</strong> " + HttpUtility.HtmlEncode(fechaTexto) + "</li>"
                + "</ul>"
                + (!string.IsNullOrWhiteSpace(enlaceDetalle)
                    ? "<p>Puede revisar el detalle en el siguiente enlace: <a href=\"" + HttpUtility.HtmlAttributeEncode(enlaceDetalle) + "\">Ver solicitud</a>.</p>"
                    : string.Empty)
                + "<p>Atentamente,<br/>Dirección General de Aviación Civil</p>";

            var servicioCorreo = new EnviarCorreo();
            var ok = servicioCorreo.enviaMensajeCorreo(model.CorreoDestino, asunto, cuerpo);
            mensaje = ok
                ? "Correo de prueba de solicitud registrada enviado correctamente."
                : "No fue posible enviar el correo de prueba de solicitud registrada: " + (servicioCorreo.LastError ?? "sin detalle adicional");
            return ok;
        }

        private bool EnviarCambioEstadoPrueba(CapaPresentacion.Models.CorreoPruebaViewModel model, string estado, out string mensaje)
        {
            if (!model.SolicitudId.HasValue || model.SolicitudId.Value <= 0)
            {
                mensaje = "Debe especificar una solicitud AOCR válida para el correo genérico de cambio de estado.";
                return false;
            }

            return NotificacionBL.EncolarCorreoCambioEstadoPrueba(
                model.CorreoDestino,
                model.NombreDestino,
                model.SolicitudId.Value,
                estado,
                out mensaje);
        }

        private bool EnviarCorreoSolicitudPrueba(CapaPresentacion.Models.CorreoPruebaViewModel model, string evento, out string mensaje)
        {
            if (!model.SolicitudId.HasValue || model.SolicitudId.Value <= 0)
            {
                mensaje = "Debe especificar una solicitud AOCR válida para la plantilla seleccionada.";
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(model.SolicitudId.Value);
            if (solicitud == null)
            {
                mensaje = "No se encontró la solicitud AOCR indicada.";
                return false;
            }

            var resultado = _solicitudCorreoService.NotificarEvento(
                solicitud,
                evento,
                model.Observacion,
                model.CorreoDestino,
                model.NombreDestino);

            mensaje = resultado != null ? resultado.Mensaje : "No se obtuvo respuesta del servicio de solicitud AOCR.";
            return EsResultadoExitoso(resultado);
        }

        private bool EnviarCorreoInspeccionPrueba(CapaPresentacion.Models.CorreoPruebaViewModel model, string evento, out string mensaje)
        {
            if (!model.InspeccionId.HasValue || model.InspeccionId.Value <= 0)
            {
                mensaje = "Debe especificar una inspección válida para la plantilla seleccionada.";
                return false;
            }

            var inspeccion = _inspeccionDao.ObtenerPorId(model.InspeccionId.Value);
            if (inspeccion == null)
            {
                mensaje = "No se encontró la inspección indicada.";
                return false;
            }

            var solicitudId = model.SolicitudId.HasValue && model.SolicitudId.Value > 0
                ? model.SolicitudId.Value
                : inspeccion.CodigoSolicitud;

            if (solicitudId <= 0)
            {
                mensaje = "La inspección indicada no tiene una solicitud AOCR asociada válida.";
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                mensaje = "No se encontró la solicitud AOCR asociada a la inspección.";
                return false;
            }

            var resultado = _inspeccionCorreoService.NotificarEvento(
                inspeccion,
                solicitud,
                evento,
                model.Observacion,
                model.CorreoDestino,
                model.NombreDestino);

            mensaje = resultado != null ? resultado.Mensaje : "No se obtuvo respuesta del servicio de inspección.";
            return EsResultadoExitoso(resultado);
        }

        private bool EnviarCorreoOrdenPrueba(CapaPresentacion.Models.CorreoPruebaViewModel model, string evento, out string mensaje)
        {
            if (!model.OrdenId.HasValue || model.OrdenId.Value <= 0)
            {
                mensaje = "Debe especificar una orden de recaudación válida para la plantilla seleccionada.";
                return false;
            }

            OrdenRecaudacion orden;
            try
            {
                orden = _ordenDao.ObtenerPorId(model.OrdenId.Value);
            }
            catch (Exception ex)
            {
                mensaje = "No fue posible cargar la orden indicada: " + ex.Message;
                return false;
            }

            if (orden == null)
            {
                mensaje = "No se encontró la orden de recaudación indicada.";
                return false;
            }

            var resultado = _ordenCorreoService.NotificarEvento(
                orden,
                evento,
                model.CorreoDestino,
                model.NombreDestino,
                null,
                null,
                model.Observacion);

            mensaje = resultado != null ? resultado.Mensaje : "No se obtuvo respuesta del servicio de orden de recaudación.";
            return EsResultadoExitoso(resultado);
        }

        private void CargarPlantillas(CapaPresentacion.Models.CorreoPruebaViewModel model)
        {
            model.PlantillasDisponibles = CrearPlantillas(model != null ? model.Plantilla : null);
        }

        private static IList<SelectListItem> CrearPlantillas(string seleccionActual)
        {
            return new List<SelectListItem>
            {
                CrearPlantilla("LEGACY:SOLICITUD_REGISTRADA", "Legacy - Solicitud registrada", seleccionActual),
                CrearPlantilla("GENERIC:CAMBIO_ESTADO:Observada", "Genérico - Cambio de estado Observada", seleccionActual),
                CrearPlantilla("GENERIC:CAMBIO_ESTADO:Aceptacion Documental", "Genérico - Cambio de estado Aceptación Documental", seleccionActual),
                CrearPlantilla("GENERIC:CAMBIO_ESTADO:Subsanada", "Genérico - Cambio de estado Subsanada", seleccionActual),
                CrearPlantilla("GENERIC:CAMBIO_ESTADO:Pago Aprobado", "Genérico - Cambio de estado Pago Aprobado", seleccionActual),
                CrearPlantilla("GENERIC:CAMBIO_ESTADO:AOCR Legalizado", "Genérico - Cambio de estado AOCR Legalizado", seleccionActual),
                CrearPlantilla("GENERIC:CAMBIO_ESTADO:AOCR Emitido/Recibido", "Genérico - Cambio de estado AOCR Emitido/Recibido", seleccionActual),
                CrearPlantilla("SOLICITUD:AOCR_APROBADO_DIRECCION", "Solicitud AOCR - Aprobado por Dirección", seleccionActual),
                CrearPlantilla("SOLICITUD:AOCR_LEGALIZADO", "Solicitud AOCR - Legalizado", seleccionActual),
                CrearPlantilla("SOLICITUD:AOCR_EMITIDO_RECIBIDO", "Solicitud AOCR - Emitido/Recibido", seleccionActual),
                CrearPlantilla("SOLICITUD:INSPECTOR_ASIGNADO", "Solicitud AOCR - Inspector asignado", seleccionActual),
                CrearPlantilla("SOLICITUD:OBSERVADA", "Solicitud AOCR - Observada", seleccionActual),
                CrearPlantilla("SOLICITUD:SUBSANADA", "Solicitud AOCR - Subsanada", seleccionActual),
                CrearPlantilla("SOLICITUD:ACEPTACION_DOCUMENTAL", "Solicitud AOCR - Aceptación documental", seleccionActual),
                CrearPlantilla("SOLICITUD:ACEPTACION_COORDINADOR_FIRMADA", "Solicitud AOCR - Revisión final de Coordinación", seleccionActual),
                CrearPlantilla("SOLICITUD:REVISION_FINAL_COORDINACION_REGISTRADA", "Solicitud AOCR - Revisión final de Coordinación registrada", seleccionActual),
                CrearPlantilla("SOLICITUD:PENDIENTE_ASIGNACION_INSPECTOR", "Solicitud AOCR - Pendiente asignación inspector", seleccionActual),
                CrearPlantilla("SOLICITUD:PAGO_APROBADO", "Solicitud AOCR - Pago aprobado", seleccionActual),
                CrearPlantilla("SOLICITUD:DIRDAC_APROBO_INFORME", "Solicitud AOCR - DIRDAC aprobó informe", seleccionActual),
                CrearPlantilla("SOLICITUD:DIRDAC_DEVOLVIO_INFORME", "Solicitud AOCR - DIRDAC devolvió informe", seleccionActual),
                CrearPlantilla("INSPECCION:NC_GENERADAS", "Inspección - NC generadas", seleccionActual),
                CrearPlantilla("INSPECCION:DOCUMENTOS_SUBSANADOS", "Inspección - Documentos subsanados", seleccionActual),
                CrearPlantilla("INSPECCION:DEVOLUCION_INSPECCION", "Inspección - Devolución", seleccionActual),
                CrearPlantilla("INSPECCION:APROBACION_INSPECCION", "Inspección - Aprobación", seleccionActual),
                CrearPlantilla("INSPECCION:REVALIDACION_OK", "Inspección - Revalidación OK", seleccionActual),
                CrearPlantilla("INSPECCION:REVALIDACION_RECHAZADA", "Inspección - Revalidación rechazada", seleccionActual),
                CrearPlantilla("INSPECCION:PENDIENTE_FIRMA_DIRDAC", "Inspección - Pendiente firma DIRDAC", seleccionActual),
                CrearPlantilla("INSPECCION:INFORME_TECNICO_FIRMADO", "Inspección - Informe técnico firmado", seleccionActual),
                CrearPlantilla("ORDEN:ORDEN_RECAUDACION_GENERADA_FINANCIERO", "Orden - Generada para financiero", seleccionActual),
                CrearPlantilla("ORDEN:ORDEN_CREADA", "Orden - Creada", seleccionActual),
                CrearPlantilla("ORDEN:PAGO_REGISTRADO", "Orden - Pago registrado", seleccionActual),
                CrearPlantilla("ORDEN:PAGO_VALIDADO", "Orden - Pago validado", seleccionActual),
                CrearPlantilla("ORDEN:FACTURA_GENERADA", "Orden - Factura generada", seleccionActual)
            };
        }

        private static SelectListItem CrearPlantilla(string valor, string texto, string seleccionActual)
        {
            return new SelectListItem
            {
                Value = valor,
                Text = texto,
                Selected = string.Equals(valor, seleccionActual, StringComparison.OrdinalIgnoreCase)
            };
        }

        private string ConstruirUrlSolicitud(int codigoSolicitud)
        {
            try
            {
                var scheme = Request != null && Request.Url != null ? Request.Url.Scheme : "http";
                return Url.Action("Detalle", "SolicitudAOCR", new { id = codigoSolicitud }, scheme);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FirstNonEmpty(params string[] valores)
        {
            return valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private static bool EsResultadoExitoso(object resultado)
        {
            if (resultado == null)
            {
                return false;
            }

            var tipo = resultado.GetType();

            var propiedadOk = tipo.GetProperty("Ok");
            if (propiedadOk != null && propiedadOk.PropertyType == typeof(bool))
            {
                return (bool)propiedadOk.GetValue(resultado, null);
            }

            var propiedadExito = tipo.GetProperty("Exito");
            if (propiedadExito != null && propiedadExito.PropertyType == typeof(bool))
            {
                return (bool)propiedadExito.GetValue(resultado, null);
            }

            var propiedadSuccess = tipo.GetProperty("Success");
            if (propiedadSuccess != null && propiedadSuccess.PropertyType == typeof(bool))
            {
                return (bool)propiedadSuccess.GetValue(resultado, null);
            }

            return false;
        }
    }
}
