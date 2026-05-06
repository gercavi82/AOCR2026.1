using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs; // Donde reside EmpresaAS400DAO
using CapaDatos.Services;
using CapaNegocio.Integraciones.As400Sync;

namespace CapaPresentacion.Controllers
{
    [AllowAnonymous] // Permite el acceso desde el Login sin estar autenticado
    public class EmpresaController : Controller
    {
        private static readonly ILoggingService _logger = LoggingServiceFactory.Create();

        [HttpGet]
        [AllowAnonymous]
        public JsonResult ObtenerEmpresas()
        {
            Response.SuppressFormsAuthenticationRedirect = true;
            var totalStopwatch = Stopwatch.StartNew();
            _logger.LogInfo("[PERF][LOGIN][EMPRESAS] Inicio ObtenerEmpresas");

            try
            {
                var mirrorStopwatch = Stopwatch.StartNew();
                var mirror = new MirrorReadService();
                var mirrorCompanias = mirror.ListarCompaniasActivas(5000);
                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN][EMPRESAS] MirrorReadService.ListarCompaniasActivas completado en {0} ms. Registros={1}",
                    mirrorStopwatch.ElapsedMilliseconds,
                    mirrorCompanias != null ? mirrorCompanias.Count : 0));

                if (mirrorCompanias != null && mirrorCompanias.Count > 0)
                {
                    var empresasMirror = mirrorCompanias
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CodigoOaci))
                        .Select(c => new
                        {
                            CodigoOaci = (c.CodigoOaci ?? string.Empty).Trim(),
                            CodigoIata = (c.CodigoIata ?? string.Empty).Trim(),
                            CodigoNumeroCia = (c.CodigoNumeroCia ?? string.Empty).Trim(),
                            Nombre = (c.NombreCompania ?? string.Empty).Trim()
                        })
                        .OrderBy(c => c.Nombre)
                        .ToList();

                    if (empresasMirror.Count > 0)
                    {
                        _logger.LogInfo(string.Format(
                            "[PERF][LOGIN][EMPRESAS] Respuesta desde mirror. Total={0} ms. Registros={1}",
                            totalStopwatch.ElapsedMilliseconds,
                            empresasMirror.Count));
                        return Json(empresasMirror, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch (Exception exMirror)
            {
                _logger.LogWarning("[PERF][LOGIN][EMPRESAS] MirrorReadService fallo: " + exMirror.Message);
            }

            try
            {
                var as400Stopwatch = Stopwatch.StartNew();
                var dao = new EmpresaAS400DAO(new SecureConfigurationService());
                var empresas = dao.ObtenerEmpresas();
                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN][EMPRESAS] EmpresaAS400DAO.ObtenerEmpresas completado en {0} ms. Registros={1}",
                    as400Stopwatch.ElapsedMilliseconds,
                    empresas != null ? empresas.Count : 0));
                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN][EMPRESAS] Respuesta desde AS400. Total={0} ms",
                    totalStopwatch.ElapsedMilliseconds));
                return Json(empresas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[PERF][LOGIN][EMPRESAS] AS400 fallo: " + ex.Message);
                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN][EMPRESAS] Respuesta vacia por error. Total={0} ms",
                    totalStopwatch.ElapsedMilliseconds));
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Obtiene una empresa específica por su código OACI
        /// Útil para mostrar datos de empresa en vistas cuando solo tienes el código
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public JsonResult ObtenerEmpresaPorCodigo(string codigo)
        {
            Response.SuppressFormsAuthenticationRedirect = true;

            try
            {
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    Response.StatusCode = 400;
                    Response.TrySkipIisCustomErrors = true;
                    return Json(new { error = "Código requerido" }, JsonRequestBehavior.AllowGet);
                }

                var codigoNormalizado = codigo.Trim().ToUpperInvariant();

                try
                {
                    var mirror = new MirrorReadService();
                    var empresaMirror = mirror.ObtenerCompaniaPorCodigo(codigoNormalizado);
                    if (empresaMirror != null)
                    {
                        return Json(new
                        {
                            CodigoOaci = (empresaMirror.CodigoOaci ?? string.Empty).Trim(),
                            CodigoIata = (empresaMirror.CodigoIata ?? string.Empty).Trim(),
                            CodigoNumeroCia = (empresaMirror.CodigoNumeroCia ?? string.Empty).Trim(),
                            Nombre = (empresaMirror.NombreCompania ?? string.Empty).Trim()
                        }, JsonRequestBehavior.AllowGet);
                    }
                }
                catch (Exception exMirror)
                {
                    System.Diagnostics.Debug.WriteLine("EmpresaController.ObtenerEmpresaPorCodigo mirror error: " + exMirror.Message);
                }

                var dao = new EmpresaAS400DAO(new SecureConfigurationService());
                var empresa = dao.ObtenerEmpresaPorCodigo(codigoNormalizado);

                if (empresa == null)
                {
                    Response.StatusCode = 404;
                    Response.TrySkipIisCustomErrors = true;
                    return Json(new { error = "Empresa no encontrada" }, JsonRequestBehavior.AllowGet);
                }

                return Json(empresa, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { error = "Error al consultar empresa: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
