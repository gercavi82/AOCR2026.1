using System.Web.Mvc;
using CapaPresentacion.Filters;

namespace CapaPresentacion
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // GlobalExceptionFilter debe ser orden 1: en MVC5 IExceptionFilter corre
            // en orden ASCENDENTE, por lo que el menor número corre PRIMERO.
            // HandleErrorAttribute al final como respaldo.
            filters.Add(new GlobalExceptionFilter(), 1);

            // Captura el codigo funcional de respuestas JSON para correlacionarlo
            // con la traza HTTP global, incluso cuando el estado sea 200.
            filters.Add(new AjaxResponseMetadataFilter(), 2);

            // Filtro de seguridad
            filters.Add(new GlobalSecurityFilter(), 3);

            // Rehidrata sesion cuando existe cookie valida y fuerza seleccion de compania RT.
            filters.Add(new RestoreAuthenticatedSessionAttribute(), 4);

            // Restringe al Director de Certificaciones DCAV a su flujo operativo y lecturas relacionadas.
            filters.Add(new DirectorCertificacionesDcavRouteGuardAttribute(), 5);

            // Filtro de auditoría
            filters.Add(new AuditActionFilter(), 6);

            // HandleError removido: pasaba HandleErrorInfo a Error.cshtml que espera
            // ErrorViewModel, causando doble-falla (InvalidOperationException).
            // GlobalExceptionFilter ya maneja todas las excepciones.
        }
    }
}
