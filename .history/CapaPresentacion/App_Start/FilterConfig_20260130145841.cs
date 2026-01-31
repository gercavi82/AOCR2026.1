using System.Web.Mvc;
using CapaPresentacion.Filters;

namespace CapaPresentacion
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Filtro de errores global (debe ser primero)
            filters.Add(new GlobalExceptionFilter(), 1);

            // Filtro de seguridad
            filters.Add(new GlobalSecurityFilter(), 2);

            // Filtro de auditoría
            filters.Add(new AuditActionFilter(), 3);

            // HandleError por defecto (respaldo)
            filters.Add(new HandleErrorAttribute(), 99);
        }
    }
}
