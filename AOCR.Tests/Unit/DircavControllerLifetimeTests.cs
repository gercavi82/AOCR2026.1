using System;
using System.Web.Mvc;
using CapaPresentacion;
using CapaPresentacion.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace AOCR.Tests.Unit
{
    [TestClass]
    public class DircavControllerLifetimeTests
    {
        [TestMethod]
        public void Publicacion_IncluyeLosRecursosDelLayoutQueFallabanEnProduccion()
        {
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            var project = new System.Xml.XmlDocument();
            project.Load(System.IO.Path.Combine(root, "CapaPresentacion", "CapaPresentacion.csproj"));
            foreach (var asset in new[] { "Content/aocr-shell.css", "Content/aocr-cumulative-file-upload.css", "Scripts/aocr-cumulative-file-upload.js" })
            {
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(root, "CapaPresentacion", asset)), asset);
                bool included = false;
                foreach (System.Xml.XmlNode item in project.SelectNodes("//*[local-name()='Content']"))
                    if (item.Attributes["Include"].Value.Replace('\\', '/') == asset) included = true;
                Assert.IsTrue(included, "El recurso debe incluirse en la publicacion: " + asset);
            }
        }
        [TestMethod]
        public void Resolver_CreaUnControladorNuevoPorCadaSolicitud()
        {
            var previous = DependencyResolver.Current;
            var previousServer = Environment.GetEnvironmentVariable("AOCR_AS400_SERVER");
            DircavController first = null, second = null, third = null;
            try
            {
                // Constructor validation only: no database operation is executed.
                Environment.SetEnvironmentVariable("AOCR_AS400_SERVER", "127.0.0.1");
                UnityConfig.RegisterComponents();
                first = (DircavController)DependencyResolver.Current.GetService(typeof(DircavController));
                second = (DircavController)DependencyResolver.Current.GetService(typeof(DircavController));
                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                Assert.AreNotSame(first, second, "MVC no permite ejecutar dos solicitudes sobre el mismo controlador.");
                first.Dispose();
                third = (DircavController)DependencyResolver.Current.GetService(typeof(DircavController));
                Assert.IsNotNull(third);
                Assert.AreNotSame(first, third);
                Assert.AreNotSame(second, third);
            }
            finally
            {
                if (first != null) first.Dispose();
                if (second != null && !ReferenceEquals(first, second)) second.Dispose();
                if (third != null && !ReferenceEquals(first, third) && !ReferenceEquals(second, third)) third.Dispose();
                DependencyResolver.SetResolver(previous);
                Environment.SetEnvironmentVariable("AOCR_AS400_SERVER", previousServer);
            }
        }
    }
}
