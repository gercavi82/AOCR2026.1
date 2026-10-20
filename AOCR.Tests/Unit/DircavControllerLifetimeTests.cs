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
        private sealed class SesionPrueba : System.Web.HttpSessionStateBase
        {
            private readonly System.Collections.Generic.Dictionary<string, object> valores = new System.Collections.Generic.Dictionary<string, object>();
            public override object this[string name] { get { object valor; return valores.TryGetValue(name, out valor) ? valor : null; } set { valores[name] = value; } }
        }
        private sealed class ContextoPrueba : System.Web.HttpContextBase
        {
            public readonly SesionPrueba Sesion = new SesionPrueba();
            public override System.Web.HttpSessionStateBase Session { get { return Sesion; } }
            public override System.Security.Principal.IPrincipal User { get; set; }
            public override System.Web.HttpRequestBase Request { get { return new SolicitudPrueba(); } }
        }
        private sealed class SolicitudPrueba : System.Web.HttpRequestBase
        {
            public override string this[string name] { get { return null; } }
            public override System.Collections.Specialized.NameValueCollection Headers { get { return new System.Collections.Specialized.NameValueCollection(); } }
        }
        [TestMethod]
        public void Dircav_ResuelveUsuarioDeSesionCanonicaSinPerderRolExclusivo()
        {
            var previo = Environment.GetEnvironmentVariable("AOCR_AS400_SERVER");
            try
            {
                Environment.SetEnvironmentVariable("AOCR_AS400_SERVER", "127.0.0.1");
                using (var controller = new DircavController())
                {
                    foreach (var clave in new[] { "UserId", "IdUsuario", "CodigoUsuario", "UsuarioId" })
                    {
                        var ctx = new ContextoPrueba { User = new System.Security.Principal.GenericPrincipal(new System.Security.Principal.GenericIdentity("GEN_DIRCAV"), new[] { "DIRCAV" }) };
                        ctx.Sesion[clave] = 97;
                        ctx.Sesion["Rol"] = "DIRCAV";
                        controller.ControllerContext = new ControllerContext(ctx, new System.Web.Routing.RouteData(), controller);
                        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        Assert.AreEqual(97, controller.GetType().GetMethod("ObtenerUsuarioIdActual", flags).Invoke(controller, null), clave);
                        Assert.AreEqual(true, controller.GetType().GetMethod("EsDircavAutorizado", flags).Invoke(controller, null));
                        var conflicto = (RedirectToRouteResult)controller.GetType().GetMethod("ResponderConflictoFirma", flags)
                            .Invoke(controller, new object[] { 7, "Falta la designación formal" });
                        Assert.AreEqual("Detalle", conflicto.RouteValues["action"]);
                        Assert.AreEqual(7, conflicto.RouteValues["id"]);
                        Assert.AreEqual("Falta la designación formal", controller.TempData["Error"]);
                        ctx.Sesion["Rol"] = "DIRDAC";
                        Assert.AreEqual(false, controller.GetType().GetMethod("EsDircavAutorizado", flags).Invoke(controller, null), "DIRDAC no obtiene firma DIRCAV");
                    }
                }
            }
            finally { Environment.SetEnvironmentVariable("AOCR_AS400_SERVER", previo); }
        }
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
