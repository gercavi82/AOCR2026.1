using System;
using System.Web.Mvc;
using CapaDatos.DAOs; // Donde reside EmpresaAS400DAO

namespace CapaPresentacion.Controllers
{
    [AllowAnonymous] // Permite el acceso desde el Login sin estar autenticado
    public class EmpresaController : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public JsonResult ObtenerEmpresas()
        {
            try
            {
                Response.SuppressFormsAuthenticationRedirect = true;
                // Conexión directa al AS/400 (IP 190.152.8.185)
                var dao = new EmpresaAS400DAO();
                var empresas = dao.ObtenerEmpresas();

                // Retorna la lista de empresas (Codigo y Nombre)
                return Json(empresas, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Si falla el AS/400, no afecta el resto de la página
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                Response.SuppressFormsAuthenticationRedirect = true;
                return Json(new { error = "Fallo conexión AS400: " + ex.Message }, JsonRequestBehavior.AllowGet);
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
            try
            {
                Response.SuppressFormsAuthenticationRedirect = true;
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    Response.StatusCode = 400;
                    Response.TrySkipIisCustomErrors = true;
                    return Json(new { error = "Código requerido" }, JsonRequestBehavior.AllowGet);
                }

                var dao = new EmpresaAS400DAO();
                var empresa = dao.ObtenerEmpresaPorCodigo(codigo);

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
                Response.SuppressFormsAuthenticationRedirect = true;
                return Json(new { error = "Error al consultar empresa: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
