using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using CapaDatos.Constants;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// API Controller para gestió de notificaciones en tiempo real
    /// Endpoints RESTful para consultar, marcar como leÃ­das y eliminar notificaciones
    /// </summary>
    [Authorize]
    public class NotificacionController : Controller
    {
        // ============================================
        // OBTENER CODIGOUSUARIO DE SESIÃ“N
        // ============================================
        private int ObtenerCodigoUsuario()
        {
            if (Session["CodigoUsuario"] != null &&
                int.TryParse(Session["CodigoUsuario"].ToString(), out var id))
                return id;

            return 0;
        }


        // ============================================
        // GET: /Notificacion
        // Panel de notificaciones del usuario
        // ============================================
        public ActionResult Index()
        {
            int codigoUsuario = ObtenerCodigoUsuario();
            if (codigoUsuario == 0)
                return RedirectToAction("Login", "Usuario");

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
                bool resultado = NotificacionBL.MarcarComoLeida(id, out mensaje);

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
        public JsonResult ContarNoLeidas()
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                if (codigoUsuario == 0)
                {
                    return Json(new
                    {
                        success = false,
                        cantidad = 0
                    }, JsonRequestBehavior.AllowGet);
                }

                int cantidad = NotificacionBL.ContarNoLeidas(codigoUsuario);

                return Json(new
                {
                    success = true,
                    cantidad = cantidad
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    cantidad = 0,
                    message = ex.Message
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
    }
}
