using System;
using System.Linq;
using System.Web.Mvc;
using CapaNegocio;
using CapaDatos.DAOs;
using CapaPresentacion.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class SolicitudAOCRController : Controller
    {
        private readonly SolicitudBL _solicitudBL = new SolicitudBL();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FormularioCompleto(SolicitudAOCRViewModel vm)
        {
            if (vm == null || vm.Solicitud == null)
                return Json(new { success = false, mensaje = "Datos incompletos." });

            try
            {
                int usuarioId = ObtenerUsuarioActualId();
                if (usuarioId <= 0)
                    return Json(new { success = false, mensaje = "Sesión inválida. Vuelva a iniciar sesión." });

                // Mapear campos extra del VM a tu entidad (si existen en SolicitudAOCR)
                // Si tu modelo NO tiene Banco/NumComp, borra estas 2 líneas.
                vm.Solicitud.Banco = vm.Banco;
                vm.Solicitud.NumComp = vm.NumeroComprobante;

                // Normalizar aeronaves (evitar nulos y matrículas vacías)
                var aeronaves = (vm.Aeronaves ?? Enumerable.Empty<CapaModelo.Aeronave>())
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Matricula))
                    .ToList();

                // Seguridad: evitar duplicados por matrícula dentro del mismo envío
                var duplicadas = aeronaves
                    .GroupBy(a => (a.Matricula ?? "").Trim().ToUpperInvariant())
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicadas.Any())
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "Matrículas duplicadas en el formulario: " + string.Join(", ", duplicadas)
                    });
                }

                string mensaje;
                int idSolicitud;

                // Crear
                if (vm.Solicitud.CodigoSolicitud <= 0)
                {
                    idSolicitud = _solicitudBL.GuardarSolicitudCompleta(vm.Solicitud, aeronaves, usuarioId, out mensaje);
                    if (idSolicitud <= 0)
                        return Json(new { success = false, mensaje });
                }
                else
                {
                    // Actualizar (si ya existe)
                    bool ok = _solicitudBL.Actualizar(vm.Solicitud, usuarioId, out mensaje, esAdmin: true);
                    if (!ok)
                        return Json(new { success = false, mensaje });

                    idSolicitud = vm.Solicitud.CodigoSolicitud;

                    // Si quieres insertar aeronaves nuevas en edición (opcional):
                    // OJO: si ya las insertaste antes, esto puede duplicar.
                    // Lo mejor es que tengas una función BL/DAO que reemplace la flota.
                    foreach (var nave in aeronaves)
                    {
                        nave.CodigoSolicitud = idSolicitud;

                        // Extra: no insertar si ya existe la matrícula global
                        // (si tu negocio lo permite)
                        if (!AeronaveDAO.ExisteMatricula(nave.Matricula))
                            AeronaveDAO.Insertar(nave);
                    }
                }

                return Json(new
                {
                    success = true,
                    mensaje = "Solicitud guardada correctamente.",
                    id = idSolicitud
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error interno: " + ex.Message });
            }
        }

        private int ObtenerUsuarioActualId()
        {
            // Ajusta a tu sistema real:
            if (Session["UsuarioId"] != null && int.TryParse(Session["UsuarioId"].ToString(), out int id))
                return id;

            return 0;
        }
    }
}
