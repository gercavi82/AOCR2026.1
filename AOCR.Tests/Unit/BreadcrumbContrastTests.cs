using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class BreadcrumbContrastTests
    {
        [TestMethod]
        public void TodosLosBreadcrumbsUsanElComponenteInstitucional()
        {
            var viewsRoot = Absolute("CapaPresentacion/Views");
            var breadcrumbs = Directory.GetFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories)
                .SelectMany(path => Regex.Matches(File.ReadAllText(path), "<ol\\s+class=\"([^\"]*\\bbreadcrumb\\b[^\"]*)\"", RegexOptions.IgnoreCase)
                    .Cast<Match>()
                    .Select(match => new { Path = path, Classes = match.Groups[1].Value }))
                .ToList();

            Assert.IsTrue(breadcrumbs.Count > 0, "No se encontraron breadcrumbs para validar.");
            foreach (var breadcrumb in breadcrumbs)
            {
                StringAssert.Contains(
                    breadcrumb.Classes,
                    "aocr-breadcrumb",
                    "El breadcrumb no usa el componente institucional: " + breadcrumb.Path);
            }
        }

        [TestMethod]
        public void ReglaFinalFijaContrasteDeTextoEnCapsulaClara()
        {
            var css = Read("CapaPresentacion/Content/aocr-contrast.css");

            StringAssert.Contains(css, "body.aocr-body .aocr-breadcrumb");
            StringAssert.Contains(css, "background-color: #ffffff !important;");
            StringAssert.Contains(css, "-webkit-text-fill-color: #0f4c81 !important;");
            StringAssert.Contains(css, "-webkit-text-fill-color: #0f172a !important;");
            StringAssert.Contains(css, "-webkit-text-fill-color: #64748b !important;");
        }

        [TestMethod]
        public void HojaDeContrasteSeCargaDespuesDeEstilosDePagina()
        {
            var layout = Read("CapaPresentacion/Views/Shared/_LayoutAOCR.cshtml");
            var sectionIndex = layout.IndexOf("@RenderSection(\"Styles\"", StringComparison.Ordinal);
            var contrastIndex = layout.IndexOf("<link href=\"@aocrContrastCssHref\"", StringComparison.Ordinal);

            Assert.IsTrue(sectionIndex >= 0, "No se encontro la seccion de estilos del layout.");
            Assert.IsTrue(contrastIndex > sectionIndex, "La correccion de contraste debe ganar la cascada de estilos.");
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
