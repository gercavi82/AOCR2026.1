using System.Collections.Generic;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrInspeccionAuthorizationTests
    {
        private sealed class ContextoPrincipal : System.Web.HttpContextBase
        {
            public override System.Security.Principal.IPrincipal User { get; set; }
        }

        [DataTestMethod]
        [DataRow("DIRCAV")]
        [DataRow("DCAV")]
        public void ConsultaLv_ReconoceRolCanonicoSinPermisoDeOperacion(string rol)
        {
            // La deteccion de rol solo usa HttpContext: no inicializar conexiones AS400.
            var controller = (CapaPresentacion.Controllers.InspeccionController)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(CapaPresentacion.Controllers.InspeccionController));
            var http = new ContextoPrincipal {
                User = new System.Security.Principal.GenericPrincipal(new System.Security.Principal.GenericIdentity("director"), new[] { rol })
            };
            controller.ControllerContext = new System.Web.Mvc.ControllerContext(http, new System.Web.Routing.RouteData(), controller);
            var metodo = controller.GetType().GetMethod("ObtenerRolActual", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var detectado = (string)metodo.Invoke(controller, null);
            Assert.AreEqual(rol, detectado);
            var service = new ListaVerificacionService();
            Assert.IsTrue(service.EsRolAutorizadoLectura(detectado));
            Assert.IsFalse(service.EsRolAutorizadoOperacion(detectado));
        }

        [DataTestMethod]
        [DataRow("RevisionDireccion")]
        [DataRow("VerInformeFirmadoInspectorDireccion")]
        [DataRow("DescargarInformeFirmadoInspectorDireccion")]
        [DataRow("AprobarDecisionFinalDireccion")]
        [DataRow("DevolverDecisionFinalDireccion")]
        public void FiltroMvc_InformeReconoceRolDircavCanonico(string accion)
        {
            var metodo = typeof(CapaPresentacion.Controllers.InspeccionController).GetMethod(accion);
            Assert.IsNotNull(metodo);
            var filtros = metodo.GetCustomAttributes(typeof(CapaPresentacion.Filters.AocrAuthorizeAttribute), true);
            var encontroFiltroRoles = false;
            foreach (CapaPresentacion.Filters.AocrAuthorizeAttribute filtro in filtros)
            {
                if (string.IsNullOrWhiteSpace(filtro.Roles)) continue;
                encontroFiltroRoles = true;
                var roles = new List<string>(filtro.Roles.Split(','));
                CollectionAssert.Contains(roles, "DIRCAV", "El filtro MVC no debe rechazar un rol permitido por la matriz central.");
                CollectionAssert.Contains(roles, "DCAV");
                CollectionAssert.DoesNotContain(roles, "Inspector");
                CollectionAssert.DoesNotContain(roles, "COORDINADOR");
            }
            Assert.IsTrue(encontroFiltroRoles);
        }

        [DataTestMethod]
        [DataRow("DIRCAV")]
        [DataRow("DIRDAC")]
        public void Autoridad_ConsultaInspeccionExistenteSinPermisoDeEditar(string rol)
        {
            CapaModelo.Inspeccion inspeccion;
            try
            {
                var lista = new CapaDatos.DAOs.InspeccionDAO().ListarTodas();
                inspeccion = lista != null && lista.Count > 0 ? lista[0] : null;
            }
            catch (Npgsql.NpgsqlException)
            {
                Assert.Inconclusive("Base de datos no disponible para verificar el acceso al recurso.");
                return;
            }
            if (inspeccion == null) { Assert.Inconclusive("No existen inspecciones para validar el acceso."); return; }
            var service = new AocrAuthorizationService(new FakeUsuarioAS400DAO(), new FakeEmpresaAS400DAO());
            var usuario = new AocrAuthorizationContext {
                UserId = 97, IsAuthenticated = true, SelectedRole = rol, Roles = new List<string> { rol }
            };
            foreach (var accion in new[] { "VerInforme", "DescargarInforme", "VerListaVerificacionOperacionalEae", "DescargarListaVerificacionOperacionalEae" })
            {
                var resultado = service.PuedeEjecutarAccion(accion, usuario, codigoInspeccion: inspeccion.CodigoInspeccion, modulo: "Inspeccion");
                Assert.IsTrue(resultado.Permitido, rol + ": " + accion + ": " + resultado.Motivo);
            }
            Assert.IsFalse(service.PuedeEjecutarAccion("GuardarListaVerificacionOperacionalEae", usuario,
                codigoInspeccion: inspeccion.CodigoInspeccion, modulo: "Inspeccion").Permitido);
        }

    public class FakeUsuarioAS400DAO : CapaDatos.Interfaces.IUsuarioAS400DAO
    {
        public string ObtenerCodigoCiudadPorCodigoUsuario(string codigoUsuario) => "UIO";
        public CapaDatos.Models.UsuarioInternoAs400Info ObtenerDatosUsuarioInterno(string codigoUsuario) => new CapaDatos.Models.UsuarioInternoAs400Info();
        public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario) => "1790000000001";
        public string ObtenerCedulaPorCodigoUsuario(string codigoUsuario) => "1700000000";
        public bool UpsertUsuarioCompleto(CapaDatos.Models.UsuarioAs400Record record, out string error) { error = null; return true; }
    }

    public class FakeEmpresaAS400DAO : CapaDatos.Interfaces.IEmpresaAS400DAO
    {
        public bool TestConnection() => true;
        public System.Collections.Generic.List<CapaDatos.DAOs.Empresa> ObtenerEmpresas() => new System.Collections.Generic.List<CapaDatos.DAOs.Empresa>();
        public CapaDatos.DAOs.Empresa ObtenerEmpresaPorCodigo(string codigoOaci) => new CapaDatos.DAOs.Empresa { CodigoOaci = codigoOaci };
    }
        [TestMethod]
        public void Inspeccion_MatrizDebeIncluirAccionesCriticasLvInforme()
        {
            var service = new AocrAuthorizationService(new FakeUsuarioAS400DAO(), new FakeEmpresaAS400DAO());
            var contextoAdmin = new AocrAuthorizationContext
            {
                UserId = 1,
                IsAuthenticated = true,
                Roles = new List<string> { "Administrador" },
                SelectedRole = "Administrador"
            };

            Assert.IsTrue(service.TieneAccesoModulo("Inspeccion", contextoAdmin));

            var accionesAdminPermitidas = new[]
            {
                "ConfirmarRevisionDocumentalInspector",
                "GuardarListaVerificacionOperacionalEae",
                "FinalizarInformeTecnico",
                "RevisionDireccion"
            };

            foreach (var accion in accionesAdminPermitidas)
            {
                var resultado = service.PuedeEjecutarAccion(accion, contextoAdmin, modulo: "Inspeccion");
                Assert.IsTrue(resultado.Permitido, "Administrador debe poder invocar " + accion + ". Motivo=" + resultado.Motivo);
            }

            // Regla 7: El Administrador no puede firmar ni aprobar en representacion de roles operativos
            Assert.IsFalse(service.PuedeEjecutarAccion("FirmarInformeInspector", contextoAdmin, modulo: "Inspeccion").Permitido, "Admin no debe firmar informe inspector");
            Assert.IsFalse(service.PuedeEjecutarAccion("AprobarDecisionFinalDireccion", contextoAdmin, modulo: "Inspeccion").Permitido, "Admin no debe aprobar decision direccion");
        }

        [TestMethod]
        public void Inspeccion_InspectorSinRecurso_DebeDenegarDetalle()
        {
            var service = new AocrAuthorizationService(new FakeUsuarioAS400DAO(), new FakeEmpresaAS400DAO());
            var contexto = new AocrAuthorizationContext
            {
                UserId = 99999,
                IsAuthenticated = true,
                Roles = new List<string> { "Inspector" },
                SelectedRole = "Inspector",
                CodigoUsuario = "99999"
            };

            var resultado = service.PuedeEjecutarAccion("Detalle", contexto, codigoInspeccion: 99999, modulo: "Inspeccion");
            Assert.IsFalse(resultado.Permitido);
        }
    }
}
