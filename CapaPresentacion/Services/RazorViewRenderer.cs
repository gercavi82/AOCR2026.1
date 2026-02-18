using System;
using System.IO;
using System.Web.Mvc;

namespace CapaPresentacion.Services
{
    public static class RazorViewRenderer
    {
        public static string RenderPartialViewToString(ControllerContext controllerContext, string viewName, object model)
        {
            if (controllerContext == null)
            {
                throw new ArgumentNullException(nameof(controllerContext));
            }

            var viewData = new ViewDataDictionary(model);
            var tempData = new TempDataDictionary();

            using (var sw = new StringWriter())
            {
                var viewResult = ViewEngines.Engines.FindPartialView(controllerContext, viewName);
                if (viewResult.View == null)
                {
                    throw new InvalidOperationException("No se encontro la vista: " + viewName);
                }

                var viewContext = new ViewContext(controllerContext, viewResult.View, viewData, tempData, sw);
                viewResult.View.Render(viewContext, sw);
                viewResult.ViewEngine.ReleaseView(controllerContext, viewResult.View);
                return sw.ToString();
            }
        }
    }
}
