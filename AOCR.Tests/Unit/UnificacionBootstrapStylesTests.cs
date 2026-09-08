using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class UnificacionBootstrapStylesTests
    {
        [TestMethod]
        public void Layout_CargaUnicaVersionLocalBootstrap538()
        {
            var layout = Read("CapaPresentacion/Views/Shared/_LayoutAOCR.cshtml");

            // Verifica que cargue los assets locales de Bootstrap 5.3.8
            StringAssert.Contains(layout, "~/Content/bootstrap.min.css");
            StringAssert.Contains(layout, "~/Scripts/bootstrap.bundle.min.js");

            // Verifica que no se carguen versiones CDN de Bootstrap en el Layout
            Assert.IsFalse(layout.Contains("cdn.jsdelivr.net/npm/bootstrap@"), "El layout no debe incluir CDN de Bootstrap.");
            Assert.IsFalse(layout.Contains("bootstrap.bundle.js"), "No debe cargarse bootstrap sin minificar duplicadamente.");
        }

        [TestMethod]
        public void Layout_NoCargaAdminLteCdnNiJsIncompatible()
        {
            var layout = Read("CapaPresentacion/Views/Shared/_LayoutAOCR.cshtml");

            // No debe cargar el CSS ni el JS de AdminLTE incompatibles con BS5
            Assert.IsFalse(layout.Contains("admin-lte@3.2/dist/css/adminlte.min.css"), "El layout no debe cargar AdminLTE completo de CDN.");
            Assert.IsFalse(layout.Contains("adminlte.js"), "El layout no debe cargar adminlte.js.");

            // Debe cargar el shell encapsulado de AOCR
            StringAssert.Contains(layout, "~/Content/aocr-shell.css");
        }

        [TestMethod]
        public void DataTablesCss_ExcluyeBotonesDeReglasDeEnlaces()
        {
            var css = Read("CapaPresentacion/Content/aocr-datatables.css");

            // Todas las reglas de enlaces en tablas deben excluir .btn
            Assert.IsTrue(Regex.IsMatch(css, @"table\.table\s+tbody\s+td\s+a:not\(\.btn\)"), "Las reglas de enlaces en tablas deben excluir taxativamente a .btn");
        }

        [TestMethod]
        public void BotonesEnlace_ProtegidosContraSobreescritura()
        {
            var dtCss = Read("CapaPresentacion/Content/aocr-datatables.css");
            var instCss = Read("CapaPresentacion/Content/aocr-institucional.css");

            // En aocr-datatables.css los botones dentro de celdas deben tener regla explícita
            Assert.IsTrue(dtCss.Contains("table.table tbody td a.btn"), "DataTables CSS debe proteger a.btn dentro de celdas.");
            Assert.IsTrue(dtCss.Contains("var(--bs-btn-color"), "DataTables CSS debe referenciar variables de color de Bootstrap para botones.");

            // En aocr-institucional.css debe existir protección global para a.btn
            Assert.IsTrue(instCss.Contains("body.aocr-body a.btn"), "aocr-institucional.css debe proteger globalmente a.btn.");
        }

        [TestMethod]
        public void HeadersCss_ContentWrapperDespejaNavbarFijo()
        {
            var headersCss = Read("CapaPresentacion/Content/aocr-headers.css");

            // content-wrapper no debe tener un margin-top insuficiente que oculte el hero banner bajo el navbar fijo
            Assert.IsFalse(headersCss.Contains("margin-top: 12px !important;"), "aocr-headers.css no debe forzar margin-top: 12px que oculta la cabecera bajo el navbar.");
            Assert.IsTrue(headersCss.Contains("margin-top: calc(3.5rem + 2px) !important;"), "aocr-headers.css debe respetar la altura del navbar fijo para despejar el hero banner.");
        }

        [TestMethod]
        public void ContrastCss_NoSobrescribeBotonesGlobales()
        {
            var css = Read("CapaPresentacion/Content/aocr-contrast.css");

            // No deben existir redefiniciones masivas de .btn con !important que anulen las variantes Bootstrap
            Assert.IsFalse(Regex.IsMatch(css, @"\.btn:\s*is\(.*!important"), "aocr-contrast.css no debe tener overrides de botones con !important.");
            Assert.IsFalse(css.Contains("--aocr-button-bg:"), "Variables forzadas de botones retiradas en favor de Bootstrap 5.");
        }

        [TestMethod]
        public void BundleConfig_NoContieneRutasInexistentes()
        {
            var code = Read("CapaPresentacion/App_Start/BundleConfig.cs");

            // Verificar que no contenga rutas a archivos inexistentes
            Assert.IsFalse(code.Contains("adminlte.min.css"), "BundleConfig no debe contener adminlte.min.css inexistente.");
            Assert.IsFalse(code.Contains("aocr-contrast-fix.css"), "BundleConfig no debe referenciar contrast-fix obsoleto.");
            Assert.IsFalse(code.Contains("~/Content/DataTables/css/"), "BundleConfig no debe referenciar DataTables local inexistente.");
            Assert.IsFalse(code.Contains("~/Scripts/adminlte.min.js"), "BundleConfig no debe referenciar adminlte.min.js inexistente.");
        }

        private static string Read(string relativePath)
        {
            return File.ReadAllText(Absolute(relativePath));
        }

        private static string Absolute(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var absolutePath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(absolutePath) || Directory.Exists(absolutePath), "No se encontro la ruta: " + absolutePath);
            return absolutePath;
        }
    }
}
