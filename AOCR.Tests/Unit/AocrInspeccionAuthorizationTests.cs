using System.Collections.Generic;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrInspeccionAuthorizationTests
    {
        [TestMethod]
        public void Inspeccion_MatrizDebeIncluirAccionesCriticasLvInforme()
        {
            var service = new AocrAuthorizationService();
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
            var service = new AocrAuthorizationService();
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
