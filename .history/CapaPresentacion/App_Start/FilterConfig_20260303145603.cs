using System.Web.Mvc;
using CapaPresentacion.Filters;

namespace CapaPresentacion
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // HandleError por defecto (respaldo, corre último en excepciones)
            filters.Add(new HandleErrorAttribute(), 1);

            // Filtro de seguridad
            filters.Add(new GlobalSecurityFilter(), 2);

            // Filtro de auditoría
            filters.Add(new AuditActionFilter(), 3);

            // Filtro de errores global — debe correr PRIMERO en excepciones
            // (MVC5 ejecuta IExceptionFilter en orden inverso: mayor Order = primero)
            filters.Add(new GlobalExceptionFilter(), 100);
        }
    }
}
