using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Principal;
using System.Web;
using CapaDatos.Services;
using CapaPresentacion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class UsuarioContextoServiceTests
    {
        [TestMethod] public void LeeUsuarioIdCanonico() { Assert.AreEqual(11, CrearServicio(CrearContexto("UsuarioId", 11)).ObtenerUsuarioId()); }
        [TestMethod] public void LeeAliasUserId() { Assert.AreEqual(12, CrearServicio(CrearContexto("UserId", 12)).ObtenerUsuarioId()); }
        [TestMethod] public void LeeAliasIdUsuario() { Assert.AreEqual(13, CrearServicio(CrearContexto("IdUsuario", 13)).ObtenerUsuarioId()); }

        [TestMethod]
        public void UsuarioIdCanonicoTienePrecedencia()
        {
            var contexto = CrearContexto("UsuarioId", 21);
            contexto.Session["UserId"] = 99;
            Assert.AreEqual(21, CrearServicio(contexto).ObtenerUsuarioId());
        }

        [TestMethod]
        public void SincronizaLasTresClaves()
        {
            var contexto = CrearContexto("UsuarioId", 31);
            CrearServicio(contexto).ObtenerContextoActual();
            Assert.AreEqual(31, contexto.Session["UsuarioId"]);
            Assert.AreEqual(31, contexto.Session["UserId"]);
            Assert.AreEqual(31, contexto.Session["IdUsuario"]);
        }

        [TestMethod]
        public void CacheaContextoEnElRequest()
        {
            var contexto = CrearContexto("UsuarioId", 41);
            var servicio = CrearServicio(contexto);
            Assert.AreSame(servicio.ObtenerContextoActual(), servicio.ObtenerContextoActual());
        }

        [TestMethod]
        public void InvalidarCacheFuerzaNuevaResolucion()
        {
            var contexto = CrearContexto("UsuarioId", 42);
            var servicio = CrearServicio(contexto);
            var primero = servicio.ObtenerContextoActual();
            servicio.InvalidarCache();
            Assert.AreNotSame(primero, servicio.ObtenerContextoActual());
        }

        [TestMethod]
        [ExpectedException(typeof(UsuarioContextoInvalidoException))]
        public void SinHttpContextLanzaExcepcionControlada()
        {
            new UsuarioContextoService(() => null, new LoggerFalso()).ObtenerContextoActual();
        }

        [TestMethod]
        [ExpectedException(typeof(UsuarioContextoInvalidoException))]
        public void UsuarioNoAutenticadoLanzaExcepcionControlada()
        {
            CrearServicio(CrearContexto("UsuarioId", 51, false)).ObtenerContextoActual();
        }

        [TestMethod]
        public void TryObtenerRetornaFalseSinAutenticacion()
        {
            UsuarioContextoDto resultado;
            Assert.IsFalse(CrearServicio(CrearContexto("UsuarioId", 52, false)).TryObtenerContextoActual(out resultado));
            Assert.IsNull(resultado);
        }

        [TestMethod] public void MarcaContextoValido() { Assert.IsTrue(CrearServicio(CrearContexto("UsuarioId", 61)).ObtenerContextoActual().EsValido); }
        [TestMethod] public void MarcaContextoAutenticado() { Assert.IsTrue(CrearServicio(CrearContexto("UsuarioId", 62)).ObtenerContextoActual().EstaAutenticado); }
        [TestMethod] public void ResuelveLoginDesdeSesion() { Assert.AreEqual("GACAJAS", CrearServicio(CrearContexto("UsuarioId", 63)).ObtenerContextoActual().Login); }
        [TestMethod] public void ResuelveNombreCompleto() { Assert.AreEqual("Usuario Prueba", CrearServicio(CrearContexto("UsuarioId", 64)).ObtenerContextoActual().NombreCompleto); }
        [TestMethod] public void ResuelveCorreo() { Assert.AreEqual("prueba@aocr.test", CrearServicio(CrearContexto("UsuarioId", 65)).ObtenerContextoActual().Correo); }
        [TestMethod] public void ResuelveRolActivo() { Assert.AreEqual("Administrador", CrearServicio(CrearContexto("UsuarioId", 66)).ObtenerContextoActual().RolActivo); }
        [TestMethod] public void DetectaAdministrador() { Assert.IsTrue(CrearServicio(CrearContexto("UsuarioId", 67)).ObtenerContextoActual().EsAdministrador); }

        [TestMethod]
        public void DetectaSolicitanteYCompania()
        {
            var contexto = CrearContexto("UsuarioId", 68, true, "Solicitante");
            contexto.Session["CompaniaActivaCodigo"] = "EC-001";
            contexto.Session["CompaniaActivaNombre"] = "Compania Uno";
            var resultado = CrearServicio(contexto).ObtenerContextoActual();
            Assert.IsTrue(resultado.EsSolicitante);
            Assert.AreEqual("EC-001", resultado.CompaniaCodigo);
            Assert.AreEqual("Compania Uno", resultado.CompaniaNombre);
        }

        [TestMethod]
        public void ConservaRolesCrudosYUnificados()
        {
            var resultado = CrearServicio(CrearContexto("UsuarioId", 69)).ObtenerContextoActual();
            CollectionAssert.Contains(new List<string>(resultado.RolesRaw), "Administrador");
            CollectionAssert.Contains(new List<string>(resultado.Roles), "Administrador");
        }

        [TestMethod]
        public void DetectaRolLegal()
        {
            var resultado = CrearServicio(CrearContexto("UsuarioId", 70, true, "CoordinacionLegal")).ObtenerContextoActual();
            Assert.IsTrue(resultado.EsLegal);
        }

        private static UsuarioContextoService CrearServicio(ContextoFalso contexto)
        {
            return new UsuarioContextoService(() => contexto, new LoggerFalso());
        }

        private static ContextoFalso CrearContexto(string claveId, int id, bool autenticado = true, string rol = "Administrador")
        {
            var sesion = new SesionFalsa();
            sesion[claveId] = id;
            sesion["CodigoUsuario"] = "GACAJAS";
            sesion["NombreUsuario"] = "Usuario Prueba";
            sesion["Correo"] = "prueba@aocr.test";
            sesion["Rol"] = rol;
            sesion["RolActivo"] = rol;
            sesion["RolesRaw"] = new List<string> { rol };
            sesion["Roles"] = new List<string> { rol };
            // Evita que el bootstrap consulte persistencia: estas pruebas validan exclusivamente
            // la resolucion de un contexto de sesion ya inicializado.
            sesion["CompaniaActivaCodigo"] = "TEST";
            sesion["CompaniaActivaNombre"] = "Compania de prueba";
            return new ContextoFalso(sesion, autenticado);
        }

        private sealed class ContextoFalso : HttpContextBase
        {
            private readonly IDictionary _items = new Hashtable();
            private readonly HttpSessionStateBase _session;
            private readonly IPrincipal _user;
            private readonly HttpRequestBase _request = new RequestFalso();

            public ContextoFalso(HttpSessionStateBase session, bool autenticado)
            {
                _session = session;
                _user = new GenericPrincipal(new GenericIdentity(autenticado ? "GACAJAS" : string.Empty, autenticado ? "Forms" : string.Empty), new string[0]);
            }

            public override IDictionary Items { get { return _items; } }
            public override HttpSessionStateBase Session { get { return _session; } }
            public override IPrincipal User { get { return _user; } set { } }
            public override HttpRequestBase Request { get { return _request; } }
        }

        private sealed class RequestFalso : HttpRequestBase
        {
            private readonly HttpCookieCollection _cookies = new HttpCookieCollection();
            public override HttpCookieCollection Cookies { get { return _cookies; } }
            public override string Path { get { return "/pruebas/contexto"; } }
            public override string ApplicationPath { get { return "/"; } }
        }

        private sealed class SesionFalsa : HttpSessionStateBase
        {
            private readonly Dictionary<string, object> _datos = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            public override object this[string name] { get { object valor; return _datos.TryGetValue(name, out valor) ? valor : null; } set { _datos[name] = value; } }
            public override string SessionID { get { return "TEST-SESSION"; } }
            public override int Timeout { get; set; }
            public override void Remove(string name) { _datos.Remove(name); }
        }

        private sealed class LoggerFalso : ILoggingService
        {
            public void LogInfo(string message, LogContext context = null) { }
            public void LogWarning(string message, LogContext context = null) { }
            public void LogError(Exception ex, LogContext context = null) { }
            public void LogError(string message, LogContext context = null) { }
            public void LogDebug(string message, LogContext context = null) { }
            public void LogAudit(string action, string entityType, int entityId, LogContext context = null) { }
            public IDisposable BeginScope(LogContext context) { return new DisposableFalso(); }
        }

        private sealed class DisposableFalso : IDisposable
        {
            public void Dispose() { }
        }
    }
}
