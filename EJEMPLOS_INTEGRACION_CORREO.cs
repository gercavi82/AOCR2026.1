using System;
using System.Web.Mvc;
using CapaDatos.Services;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Ejemplo de integración de notificaciones por correo en OrdenRecaudacionController
    /// </summary>
    public partial class OrdenRecaudacionController : Controller
    {
        // EJEMPLO 1: Enviar notificación al crear una orden
        [HttpPost]
        public ActionResult CrearOrden(/* tus parámetros */)
        {
            try
            {
                // Tu código para crear la orden...
                // int ordenId = ... crear orden en DB
                // decimal monto = ...
                // string concepto = ...
                // string emailContacto = ...

                // Ejemplo de datos
                int ordenId = 12345;
                decimal monto = 1500.00m;
                string concepto = "Tasa de certificación AOCR";
                string emailContacto = "contacto@empresa.com";

                // Enviar notificación por correo
                bool correoEnviado = NotificacionCorreoHelper.NotificarNuevaOrden(
                    ordenId, 
                    emailContacto, 
                    monto, 
                    concepto
                );

                if (correoEnviado)
                {
                    TempData["Mensaje"] = "Orden creada y notificación enviada correctamente";
                }
                else
                {
                    TempData["Mensaje"] = "Orden creada, pero hubo un error al enviar la notificación";
                }

                return RedirectToAction("Detalle", new { id = ordenId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear la orden: " + ex.Message;
                return View();
            }
        }

        // EJEMPLO 2: Enviar notificación al registrar un pago
        [HttpPost]
        public ActionResult RegistrarPago(int ordenId, decimal monto, string referencia)
        {
            try
            {
                // Tu código para registrar el pago...
                // string emailContacto = obtener email del contacto

                string emailContacto = "contacto@empresa.com";

                // Enviar notificación de pago recibido
                bool correoEnviado = NotificacionCorreoHelper.NotificarPagoRecibido(
                    ordenId, 
                    emailContacto, 
                    monto, 
                    referencia
                );

                if (correoEnviado)
                {
                    TempData["Success"] = "Pago registrado y notificación enviada";
                }

                return RedirectToAction("Detalle", new { id = ordenId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return View();
            }
        }

        // EJEMPLO 3: Enviar notificación al cambiar el estado
        [HttpPost]
        public ActionResult CambiarEstado(int ordenId, string nuevoEstado)
        {
            try
            {
                // Tu código para cambiar el estado...
                string estadoAnterior = "Pendiente"; // obtener de DB
                string emailContacto = "contacto@empresa.com"; // obtener de DB

                // Actualizar estado en DB...

                // Enviar notificación de cambio de estado
                bool correoEnviado = NotificacionCorreoHelper.NotificarCambioEstado(
                    ordenId, 
                    emailContacto, 
                    estadoAnterior, 
                    nuevoEstado
                );

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return View();
            }
        }

        // EJEMPLO 4: Proceso batch para enviar recordatorios de vencimiento
        [HttpGet]
        public ActionResult EnviarRecordatoriosVencimiento()
        {
            try
            {
                // Obtener órdenes próximas a vencer
                // var ordenesProximasVencer = ObtenerOrdenesProximasVencer();
                
                int enviadosExitosos = 0;
                int errores = 0;

                // Ejemplo con datos ficticios
                var ordenesEjemplo = new[]
                {
                    new { OrdenId = 12345, Email = "cliente1@empresa.com", FechaVencimiento = DateTime.Now.AddDays(5), Monto = 1500.00m },
                    new { OrdenId = 12346, Email = "cliente2@empresa.com", FechaVencimiento = DateTime.Now.AddDays(3), Monto = 2000.00m }
                };

                foreach (var orden in ordenesEjemplo)
                {
                    bool enviado = NotificacionCorreoHelper.NotificarVencimientoProximo(
                        orden.OrdenId,
                        orden.Email,
                        orden.FechaVencimiento,
                        orden.Monto
                    );

                    if (enviado)
                        enviadosExitosos++;
                    else
                        errores++;
                }

                TempData["Mensaje"] = $"Recordatorios enviados: {enviadosExitosos} exitosos, {errores} errores";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al enviar recordatorios: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // EJEMPLO 5: Envío manual desde la vista de detalle
        [HttpPost]
        public JsonResult ReenviarNotificacion(int ordenId, string tipoNotificacion)
        {
            try
            {
                // Obtener datos de la orden
                // var orden = ObtenerOrdenPorId(ordenId);
                
                // Ejemplo con datos ficticios
                string emailContacto = "contacto@empresa.com";
                decimal monto = 1500.00m;
                string concepto = "Tasa de certificación";

                bool enviado = false;

                switch (tipoNotificacion)
                {
                    case "nueva":
                        enviado = NotificacionCorreoHelper.NotificarNuevaOrden(
                            ordenId, emailContacto, monto, concepto);
                        break;
                    
                    case "recordatorio":
                        enviado = NotificacionCorreoHelper.NotificarVencimientoProximo(
                            ordenId, emailContacto, DateTime.Now.AddDays(5), monto);
                        break;
                    
                    default:
                        return Json(new { success = false, message = "Tipo de notificación no válido" });
                }

                if (enviado)
                {
                    return Json(new { 
                        success = true, 
                        message = "Notificación reenviada correctamente" 
                    });
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = "Error al reenviar la notificación" 
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Error: " + ex.Message 
                });
            }
        }

        // EJEMPLO 6: Envío personalizado con la clase EnviarCorreo directamente
        [HttpPost]
        public ActionResult EnviarCorreoPersonalizado(int ordenId, string destinatario, string asuntoCustom, string mensajeCustom)
        {
            try
            {
                var emailService = new EnviarCorreo();

                bool enviado = emailService.enviaMensajeCorreo(
                    coreoPara: destinatario,
                    asunto: asuntoCustom ?? $"Notificación Orden #{ordenId}",
                    mensajeDetalle: mensajeCustom ?? $"<p>Información sobre la orden #{ordenId}</p>"
                );

                if (enviado)
                {
                    return Json(new { success = true, message = "Correo enviado" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al enviar" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
