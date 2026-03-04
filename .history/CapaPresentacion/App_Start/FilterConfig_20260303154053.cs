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

            // Filtro de seguridad
            filters.Add(new GlobalSecurityFilter(), 2);

            // Filtro de auditoría
            filters.Add(new AuditActionFilter(), 3);

            // HandleError por defecto (respaldo, siempre último)
            filters.Add(new HandleErrorAttribute(), 99);
        }
    }
}
