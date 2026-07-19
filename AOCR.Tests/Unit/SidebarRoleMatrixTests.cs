using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SidebarRoleMatrixTests
    {
        [TestMethod]
        public void MatrizIncluyeLosSeisRolesConAccesosAcotados()
        {
            var body = RoleMatrixBody();
            AssertRoleKeys(body, "rt-", 3);
            AssertRoleKeys(body, "inspector-", 3);
            Assert.AreEqual(1, Regex.Matches(body, "\"ins-certificados\"").Count);
            AssertRoleKeys(body, "financiero-", 3);
            AssertRoleKeys(body, "coordinador-", 3);
            AssertRoleKeys(body, "dirdac-", 3);
            AssertRoleKeys(body, "dcav-", 2);
        }

        [TestMethod]
        public void DirdacYDcavTienenFirmasMutuamenteExcluyentes()
        {
            var body = RoleMatrixBody();
            var dcav = Slice(body, "if (context.EsDcavRol)", "else if (context.EsDirdacRol)");
            var dirdac = Slice(body, "else if (context.EsDirdacRol)", "else if (context.EsCoordinadorRol");

            StringAssert.Contains(dcav, "DocumentoCondiciones");
            Assert.IsFalse(dcav.Contains("DocumentoAocr"));
            StringAssert.Contains(dirdac, "DocumentoAocr");
            Assert.IsFalse(dirdac.Contains("DocumentoCondiciones"));
        }

        [TestMethod]
        public void RolesInstitucionalesNoRecibenContextoCompania()
        {
            var builder = Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs");
            StringAssert.Contains(builder, "ShowCompanyContext = context.EsSolicitanteORT");
            StringAssert.Contains(builder, "ShowCompanySelector = context.EsSolicitanteORT && context.MostrarSelectorCompaniaRt");
        }

        [TestMethod]
        public void DirdacYDcavSeConservanComoRolesSeleccionablesDistintos()
        {
            var helper = Read("CapaPresentacion/Helpers/RoleGroupingHelper.cs");
            StringAssert.Contains(helper, "public const string Dirdac = \"DIRDAC\"");
            StringAssert.Contains(helper, "public const string Dcav = \"DCAV\"");
            StringAssert.Contains(helper, "public static bool IsDirdac");
            StringAssert.Contains(helper, "public static bool IsDcav");
        }

        [TestMethod]
        public void PanelDirdacRespetaRolActivoYNoTodosLosRolesDelPrincipal()
        {
            var controller = Read("CapaPresentacion/Controllers/InspeccionController.cs");
            var start = controller.IndexOf("private bool EsRolDireccionOJefatura()", StringComparison.Ordinal);
            var end = controller.IndexOf("private bool EsRolInspector()", start, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0 && end > start);
            var method = controller.Substring(start, end - start);
            StringAssert.Contains(method, "SidebarPermissionHelper.Resolve");
            StringAssert.Contains(method, "permisosRolActivo.EsDirdacRol");
            Assert.IsFalse(method.Contains("User.IsInRole"));
        }

        [TestMethod]
        public void EncabezadoDirdacMantieneContrasteYAdaptacionMovil()
        {
            var view = Read("CapaPresentacion/Views/InformeTecnico/PendientesDireccion.cshtml");
            StringAssert.Contains(view, ".dirdac-tray-header h2 { color:#fff!important");
            StringAssert.Contains(view, "@@media (max-width:768px)");
            StringAssert.Contains(view, ".dirdac-tray-header .btn { width:100%");
        }

        [TestMethod]
        public void MatrizNoGeneraAccionesRapidasDuplicadasNiBuscadorParaMenuCorto()
        {
            var builder = Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs");
            var view = Read("CapaPresentacion/Views/Shared/_Sidebar.cshtml");
            StringAssert.Contains(builder, "var quickActions = !context.TieneNavegacionRol || navegacionPorRol");
            StringAssert.Contains(builder, "? new List<SidebarMenuItemViewModel>()");
            StringAssert.Contains(builder, "vm.ShowSearch = vm.Groups.Sum");
            StringAssert.Contains(view, "@if (sidebar.ShowSearch)");
        }

        [TestMethod]
        public void RutasSeGeneranConUrlActionYSinPrefijoVirtualCodificado()
        {
            var builder = Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs");
            StringAssert.Contains(builder, "Url = context.Url.Action(action, controller, routeValues)");
            Assert.IsFalse(RoleMatrixBody().Contains("/aocr/"));
        }

        [TestMethod]
        public void VistaUsaViewModelTipadoYNoRenderizaGruposVacios()
        {
            var builder = Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs");
            var model = Read("CapaPresentacion/Models/ViewModels/SidebarMenuContextViewModel.cs");
            StringAssert.Contains(builder, "group.Items.Any(item => item.Visible)");
            StringAssert.Contains(model, "PermissionGranted");
            StringAssert.Contains(model, "ActiveRoleKey");
            StringAssert.Contains(model, "NavigationSectionTitle");
        }

        [TestMethod]
        public void MatrizNoContieneClavesDuplicadas()
        {
            var keys = Regex.Matches(RoleMatrixBody(), "CreateItem\\(context, \\\"([^\\\"]+)\\\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToList();
            Assert.AreEqual(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [TestMethod]
        public void UsuarioAutenticadoSinIdInternoRecibeEstadoVacioControlado()
        {
            var builder = Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs");
            StringAssert.Contains(builder, "usuarioAutenticado && context.UserId <= 0");
            StringAssert.Contains(builder, "context.TieneNavegacionRol = permission.TieneNavegacionRol");
            StringAssert.Contains(builder, "if (!context.TieneNavegacionRol)");
        }

        [TestMethod]
        public void SidebarMantieneCssEncapsuladoYBreakpointsResponsive()
        {
            var css = Read("CapaPresentacion/Content/aocr-sidebar.css");
            StringAssert.Contains(css, "@media (max-width: 991.98px)");
            StringAssert.Contains(css, "@media (max-width: 767.98px)");
            StringAssert.Contains(css, ".aocr-menu-toggle:focus-visible");
            StringAssert.Contains(css, "overflow-x: hidden");
        }

        [TestMethod]
        public void SidebarNoUsaDynamicViewBagNiRuntimeBinder()
        {
            var view = Read("CapaPresentacion/Views/Shared/_Sidebar.cshtml");
            var model = Read("CapaPresentacion/Models/ViewModels/SidebarMenuContextViewModel.cs");
            Assert.IsFalse(view.Contains("ViewBag"));
            Assert.IsFalse(view.Contains("dynamic"));
            Assert.IsFalse(model.Contains("dynamic"));
            Assert.IsFalse(view.Contains("RuntimeBinderException"));
        }

        [TestMethod]
        public void JavascriptEsProgresivoYCierraSidebarMovilConNavegacionYEscape()
        {
            var script = Read("CapaPresentacion/Scripts/aocr-sidebar.js");
            StringAssert.Contains(script, "if (!shell)");
            StringAssert.Contains(script, "if (searchInput)");
            StringAssert.Contains(script, ".aocr-submenu-link[href]");
            StringAssert.Contains(script, ".aocr-footer-link[href]");
            StringAssert.Contains(script, "event.key === 'Escape'");
            StringAssert.Contains(script, "data-aocr-sidebar-ready");
        }

        [TestMethod]
        public void EndpointsInstitucionalesRechazanAccesoNoAutorizadoCon403()
        {
            var solicitud = Read("CapaPresentacion/Controllers/SolicitudAOCRController.cs");
            var inspeccion = Read("CapaPresentacion/Controllers/InspeccionController.cs");
            StringAssert.Contains(solicitud, "return new HttpStatusCodeResult(403, \"No tiene permisos para consultar esta bandeja.\")");
            StringAssert.Contains(inspeccion, "return new HttpStatusCodeResult(403, \"No tiene permisos para revisar los informes técnicos institucionales.\")");
        }

        private static void AssertRoleKeys(string body, string prefix, int expected)
        {
            var count = 0;
            var index = 0;
            while ((index = body.IndexOf("\"" + prefix, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += prefix.Length + 1;
            }

            Assert.AreEqual(expected, count, "Cantidad inesperada de accesos para " + prefix);
        }

        private static string RoleMatrixBody()
        {
            var source = Read("CapaPresentacion/Helpers/SidebarMenuBuilder.cs");
            return Slice(source, "private static SidebarMenuGroupViewModel BuildRoleWorkMenuGroup", "private static int? PendingBadge");
        }

        private static string Slice(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0 && end > start, "No se encontró el segmento esperado: " + startMarker);
            return source.Substring(start, end - start);
        }

        private static string Read(string path)
        {
            return File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Root()
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        }
    }
}
