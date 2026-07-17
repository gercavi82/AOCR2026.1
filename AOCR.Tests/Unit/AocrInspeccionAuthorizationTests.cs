using System.Collections.Generic;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrInspeccionAuthorizationTests
    {
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
            var contexto = new AocrAuthorizationContext
            {
                UserId = 1,
                IsAuthenticated = true,
                Roles = new List<string> { "Administrador" },
                SelectedRole = "Administrador"
            };

            Assert.IsTrue(service.TieneAccesoModulo("Inspeccion", contexto));

            var acciones = new[]
            {
                "ConfirmarRevisionDocumentalInspector",
                "GuardarListaVerificacionOperacionalEae",
                "FinalizarInformeTecnico",
                "FirmarInformeInspector",
                "RevisionDireccion",
                "AprobarDecisionFinalDireccion"
            };

            foreach (var accion in acciones)
            {
                var resultado = service.PuedeEjecutarAccion(accion, contexto, modulo: "Inspeccion");
                Assert.IsTrue(resultado.Permitido, "Administrador debe poder invocar " + accion + ". Motivo=" + resultado.Motivo);
            }
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
