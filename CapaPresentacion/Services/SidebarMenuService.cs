using System.Web.Mvc;
using CapaPresentacion.Helpers;
using CapaPresentacion.Models.ViewModels;

namespace CapaPresentacion.Services
{
    public class SidebarMenuService
    {
        public SidebarMenuViewModel Build(ViewContext viewContext, ViewDataDictionary viewData, object model)
        {
            return SidebarMenuBuilder.Build(viewContext, viewData, model);
        }

        public static SidebarMenuViewModel BuildForView(ViewContext viewContext, ViewDataDictionary viewData, object model)
        {
            return new SidebarMenuService().Build(viewContext, viewData, model);
        }
    }
}