using System;
using System.IO;
using System.Security.Cryptography;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;
using CapaPresentacion.Filters;

namespace CapaPresentacion.Controllers
{
    /// <summary>Superficie MVC exclusiva DIRDAC para AC-11.</summary>
    [Authorize(Roles = "DIRDAC")]
    public class DirdacController : Controller
    {
        private readonly IAocrFinalWorkflowService _workflow;

        public DirdacController() : this(new AocrFinalWorkflowService()) { }

        public DirdacController(IAocrFinalWorkflowService workflow)
        {
            _workflow = workflow ?? throw new ArgumentNullException("workflow");
        }

        [HttpGet]
        [RequirePermission(AocrFinalWorkflowService.PermisoBandejaDirdac)]
        public ActionResult BandejaAocr()
        {
            if (!EsDirdacActivo()) return new HttpStatusCodeResult(403, "La bandeja es exclusiva del rol activo DIRDAC.");
            return View("Bandeja", _workflow.ObtenerBandejaDirdac());
        }

        [HttpGet]
        public ActionResult Bandeja(string tab = null)
        {
            return RedirectToAction("BandejaAocr");
        }

        [HttpGet]
        [RequirePermission(AocrFinalWorkflowService.PermisoBandejaDirdac)]
        public ActionResult Detalle(int id)
        {
            if (!EsDirdacActivo()) return new HttpStatusCodeResult(403);
            var model = _workflow.ObtenerDetalleDirdac(id);
            return model == null ? (ActionResult)HttpNotFound("Expediente AOCR no encontrado o no visible para DIRDAC.") : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(AocrFinalWorkflowService.PermisoDevolverDircav)]
        public ActionResult DevolverAocrDircav(DevolverAocrDircavRequest request)
        {
            if (request == null) return Responder(AocrWorkflowResult.Error(400, "REQUEST_INVALIDO", "No se recibió la solicitud de devolución."));
            request.Actor = CrearActor(AocrFinalWorkflowService.PermisoDevolverDircav);
            request.BaseUrl = ConstruirBaseUrl();
            return Responder(_workflow.DevolverAocrDircav(request));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(AocrFinalWorkflowService.PermisoFirmarAocr)]
        public ActionResult FirmarLegalizarAocr(FirmarLegalizarAocrRequest request)
        {
            if (request == null) return Responder(AocrWorkflowResult.Error(400, "REQUEST_INVALIDO", "No se recibió evidencia de firma."));
            request.Actor = CrearActor(AocrFinalWorkflowService.PermisoFirmarAocr);
            request.BaseUrl = ConstruirBaseUrl();
            var raizRelativa = "~/App_Data/Uploads/AOCR/Firmados/" + request.SolicitudId + "/";
            if (string.IsNullOrWhiteSpace(request.RutaPdfFirmado)
                || !request.RutaPdfFirmado.Replace('\\', '/').StartsWith(raizRelativa.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return Responder(AocrWorkflowResult.Error(400, "RUTA_FIRMA_INVALIDA", "La evidencia firmada no pertenece al almacenamiento controlado del expediente."));
            var fisica = Server.MapPath(request.RutaPdfFirmado);
            if (string.IsNullOrWhiteSpace(fisica) || !System.IO.File.Exists(fisica) || new FileInfo(fisica).Length != request.TamanioPdfFirmado)
                return Responder(AocrWorkflowResult.Error(409, "EVIDENCIA_NO_EXISTE", "El PDF firmado no existe o cambió en almacenamiento."));
            using (var sha = SHA256.Create())
            using (var stream = System.IO.File.OpenRead(fisica))
            {
                var calculado = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                if (!string.Equals(calculado, request.HashPdfFirmado, StringComparison.OrdinalIgnoreCase))
                    return Responder(AocrWorkflowResult.Error(409, "HASH_INVALIDO", "El hash del PDF firmado no coincide con la evidencia almacenada."));
            }
            return Responder(_workflow.FirmarLegalizarAocr(request));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DevolverDIRCAV(int id, string motivo, long versionEsperada = 0)
        {
            return DevolverAocrDircav(new DevolverAocrDircavRequest { SolicitudId = id, Observacion = motivo, VersionEsperada = versionEsperada });
        }

        private bool EsDirdacActivo()
        {
            return AocrRolesInstitucionales.EsDirdac(Convert.ToString(Session != null ? Session["Rol"] : null));
        }

        private AocrWorkflowActor CrearActor(string permiso)
        {
            var rol = Convert.ToString(Session != null ? Session["Rol"] : null);
            var codigoUsuario = Convert.ToString(Session != null ? Session["CodigoUsuario"] : null);
            var idRaw = Convert.ToString(Session != null ? (Session["UsuarioId"] ?? Session["CodigoUsuario"]) : null);
            int id; int.TryParse(idRaw, out id);
            return new AocrWorkflowActor
            {
                UsuarioId = id,
                UsuarioNombre = User != null && User.Identity != null ? User.Identity.Name : codigoUsuario,
                RolActivo = rol,
                Ip = Request != null ? Request.UserHostAddress : null,
                TienePermiso = SeguridadBL.UsuarioTienePermiso(codigoUsuario, permiso, new[] { rol })
            };
        }

        private string ConstruirBaseUrl()
        {
            return Request == null || Request.Url == null ? string.Empty : Request.Url.GetLeftPart(UriPartial.Authority) + Url.Content("~").TrimEnd('/');
        }

        private ActionResult Responder(AocrWorkflowResult result)
        {
            Response.StatusCode = result.HttpStatusCode > 0 ? result.HttpStatusCode : (result.Exito ? 200 : 500);
            Response.TrySkipIisCustomErrors = true;
            return Json(new AocrWorkflowResponse { Ok=result.Exito,Codigo=result.Codigo,Mensaje=result.Mensaje,Estado=result.EstadoNuevo,Version=result.VersionNueva,CorrelationId=result.CorrelationId });
        }
    }
}
