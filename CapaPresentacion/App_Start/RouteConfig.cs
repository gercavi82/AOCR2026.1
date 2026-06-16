using System.Web.Mvc;
using System.Web.Routing;

namespace CapaPresentacion
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("favicon.ico");
            routes.IgnoreRoute("Content/imagenes/favicon.ico");
            routes.IgnoreRoute(".well-known/{*pathInfo}");

            // RUTA ESPECÍFICA para el Dashboard - debe ir ANTES de la ruta por defecto
            routes.MapRoute(
                name: "Dashboard",
                url: "Dashboard",
                defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional }
            );

            // RUTA para Detalle de Orden - también específica
            routes.MapRoute(
                name: "OrdenDetalle",
                url: "Orden/Detalle/{id}",
                defaults: new { controller = "Orden", action = "Detalle", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "LegacyLvEaeOficialPdf",
                url: "ListaVerificacion/PdfListaVerificacionEaeOficial/{codigoInspeccion}",
                defaults: new { controller = "Inspeccion", action = "VerLvEaeOficial", codigoInspeccion = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "LegacyLvEaePdf",
                url: "Inspeccion/ListaVerificacionOperacionalEaePdf/{id}",
                defaults: new { controller = "Inspeccion", action = "VerListaVerificacionOperacionalEae", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "InformeTecnicoPendientesDireccion",
                url: "InformeTecnico/PendientesDireccion",
                defaults: new { controller = "Inspeccion", action = "PendientesDireccion" }
            );

            routes.MapRoute(
                name: "InformeTecnicoRevisionDireccion",
                url: "InformeTecnico/RevisionDireccion/{codigoInforme}",
                defaults: new { controller = "Inspeccion", action = "RevisionDireccion", codigoInforme = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "InformeTecnicoVerFirmadoInspectorDireccion",
                url: "InformeTecnico/VerInformeFirmadoInspector/{codigoInforme}",
                defaults: new { controller = "Inspeccion", action = "VerInformeFirmadoInspectorDireccion", codigoInforme = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "InformeTecnicoDescargarFirmadoInspectorDireccion",
                url: "InformeTecnico/DescargarInformeFirmadoInspector/{codigoInforme}",
                defaults: new { controller = "Inspeccion", action = "DescargarInformeFirmadoInspectorDireccion", codigoInforme = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "InformeTecnicoAprobarDireccion",
                url: "InformeTecnico/AprobarDecisionFinalDireccion",
                defaults: new { controller = "Inspeccion", action = "AprobarDecisionFinalDireccion" }
            );

            routes.MapRoute(
                name: "InformeTecnicoDevolverDireccion",
                url: "InformeTecnico/DevolverDecisionFinalDireccion",
                defaults: new { controller = "Inspeccion", action = "DevolverDecisionFinalDireccion" }
            );

            routes.MapRoute(
                name: "InformeTecnicoFirmarDireccion",
                url: "InformeTecnico/FirmarDireccion",
                defaults: new { controller = "Inspeccion", action = "FirmarDireccion" }
            );

            routes.MapRoute(
                name: "InspeccionDetalleAmigable",
                url: "Detalle/{id}",
                defaults: new { controller = "Inspeccion", action = "Detalle", id = UrlParameter.Optional }
            );

            // RUTA POR DEFECTO - debe ir ÚLTIMA
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
