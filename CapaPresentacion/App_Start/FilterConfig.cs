using System.Web.Mvc;

namespace CapaPresentacion
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new CapaPresentacion.Filters.GlobalExceptionFilter());
            filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        }
    }
}
