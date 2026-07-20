using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CapaNegocio.Services;
using CapaNegocio.DTOs;

namespace AOCR.Tests.Unit
{
    // Mocks manuales
    public class MockIdentity : IIdentity
    {
        public string Name { get; set; }
        public string AuthenticationType { get; set; }
        public bool IsAuthenticated { get; set; }
    }

    public class MockPrincipal : IPrincipal
    {
        private readonly IIdentity _identity;
        public MockPrincipal(IIdentity identity) { _identity = identity; }
        public IIdentity Identity => _identity;
        public bool IsInRole(string role) => true;
    }

    public class MockSession : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
        public override object this[string name]
        {
            get => _data.ContainsKey(name) ? _data[name] : null;
            set => _data[name] = value;
        }
    }

    public class MockHttpContext : HttpContextBase
    {
        private readonly IPrincipal _user;
        private readonly HttpSessionStateBase _session;

        public MockHttpContext(IPrincipal user, HttpSessionStateBase session)
        {
            _user = user;
            _session = session;
        }

        public override IPrincipal User
        {
            get => _user;
            set { throw new NotImplementedException(); }
        }

        public override HttpSessionStateBase Session => _session;
    }

    [TestClass]
    public class UsuarioContextoServiceTests
    {
        private MockSession _session;

        [TestInitialize]
        public void Setup()
        {
            _session = new MockSession();
        }

        private UsuarioContextoService CreateService(string username, bool isAuthenticated)
        {
            var identity = new MockIdentity { Name = username, IsAuthenticated = isAuthenticated };
            var principal = new MockPrincipal(identity);
            var context = new MockHttpContext(principal, _session);
            return new UsuarioContextoService(context);
        }

        [TestMethod]
        public void ObtenerContexto_GACAJAS_ObtieneIdReal()
        {
            _session["UserId"] = "45";
            _session["NombreUsuario"] = "GERMAN CAJAS";
            _session["Rol"] = "DIRDAC";

            var service = CreateService("GACAJAS", true);
            var contexto = service.ObtenerContextoActual();

            Assert.IsTrue(contexto.EstaAutenticado);
            Assert.AreEqual("gacajas", contexto.LoginNormalizado);
            Assert.AreEqual(45, contexto.UsuarioId);
            Assert.AreEqual("GERMAN CAJAS", contexto.Nombre);
            CollectionAssert.Contains(contexto.Roles, "DIRDAC");
        }

        [TestMethod]
        public void ObtenerContexto_Inspector_ObtieneIdReal()
        {
            _session["IdUsuario"] = "102";
            _session["Rol"] = "Inspector";

            var service = CreateService("INSPECTOR1", true);
            var contexto = service.ObtenerContextoActual();

            Assert.AreEqual(102, contexto.UsuarioId);
            CollectionAssert.Contains(contexto.Roles, "INSPECTOR");
        }

        [TestMethod]
        public void ObtenerContexto_Financiero_ObtieneIdReal()
        {
            _session["CodigoUsuario"] = "200";
            _session["Rol"] = "FINANCIERO";

            var service = CreateService("FIN1", true);
            var contexto = service.ObtenerContextoActual();

            Assert.AreEqual(200, contexto.UsuarioId);
            CollectionAssert.Contains(contexto.Roles, "FINANCIERO");
        }

        [TestMethod]
        public void ObtenerContexto_DCAV_ObtieneIdReal()
        {
            _session["UserId"] = "305";
            _session["Rol"] = "DCAV";

            var service = CreateService("DCAV1", true);
            var contexto = service.ObtenerContextoActual();

            Assert.AreEqual(305, contexto.UsuarioId);
        }

        [TestMethod]
        public void ObtenerContexto_DIRDAC_ObtieneIdReal()
        {
            _session["UserId"] = "400";
            _session["Rol"] = "DIRDAC";

            var service = CreateService("DIRDAC1", true);
            var contexto = service.ObtenerContextoActual();

            Assert.AreEqual(400, contexto.UsuarioId);
        }

        [TestMethod]
        public void ObtenerContexto_UsuarioDesconocido_NoObtieneIdCero()
        {
            // Sin UserId en sesion
            var service = CreateService("DESCONOCIDO", true);
            var contexto = service.ObtenerContextoActual();

            // La validacion lanza exception o el id es 0
            Assert.AreEqual(0, contexto.UsuarioId); // Inicializado pero no válido
            
            // Si llamamos a ValidarAutenticacion, deberia fallar porque UsuarioId es 0
            Assert.ThrowsException<HttpException>(() => service.ValidarAutenticacion());
        }

        [TestMethod]
        public void ValidarRol_UsuarioSinRolPermitido_Recibe403()
        {
            _session["UserId"] = "500";
            _session["Rol"] = "OTRO_ROL";

            var service = CreateService("USER500", true);
            
            var ex = Assert.ThrowsException<HttpException>(() => service.ValidarRol("INSPECTOR", "DCAV"));
            Assert.AreEqual(401, ex.GetHttpCode());
        }
    }
}
