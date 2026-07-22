using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class ApplicationVirtualPathTests
    {
        [TestMethod]
        public void Account_RedireccionesLocales_RespetanDirectorioVirtual()
        {
            var controller = Read("CapaPresentacion/Controllers/AccountController.cs");

            StringAssert.Contains(controller, "private string ResolverUrlAplicacion(string rutaLocal)");
            StringAssert.Contains(controller, "Request.ApplicationPath");
            StringAssert.Contains(controller, "ResolverUrlAplicacion(returnUrlPermitido)");
            StringAssert.Contains(controller, "ResolverUrlAplicacion(estadoFlujo.UrlDestino)");
            Assert.IsFalse(controller.Contains("return Redirect(returnUrlPermitido);"));
            Assert.IsFalse(controller.Contains("return Redirect(estadoFlujo.UrlDestino);"));
        }

        [TestMethod]
        public void Dashboard_RutaFlujoRt_SeResuelveDesdeRaizDeAplicacion()
        {
            var view = Read("CapaPresentacion/Views/Home/Index.cshtml");

            StringAssert.Contains(view, "var urlDestinoRt = Url.Content(\"~/\"");
            StringAssert.Contains(view, "href=\"@urlDestinoRt\"");
            Assert.IsFalse(view.Contains("href=\"@estadoRt.UrlDestino\""));
        }

        private static string Read(string relativePath)
        {
            var absolutePath = Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.IsTrue(File.Exists(absolutePath), "No se encontro el archivo: " + absolutePath);
            return File.ReadAllText(absolutePath);
        }
    }
}
