using System.Collections.Generic;
using System.Runtime.Serialization;
using CapaDatos.Constants;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrFinalWorkflowAuthorizationTests
    {
        private static AocrFinalWorkflowService CreateServiceWithoutDependencies()
        {
            return (AocrFinalWorkflowService)FormatterServices.GetUninitializedObject(typeof(AocrFinalWorkflowService));
        }

        [DataTestMethod]
        [DataRow("Solicitante")]
        [DataRow("Operador")]
        [DataRow("RepresentanteTecnico")]
        [DataRow("RepresentanteLegal")]
        [DataRow("RT")]
        public void UsuarioPuedeTransicionarEstadoAocr_Subsanada_ConRolExternoPermitido_DebeRetornarTrue(string rol)
        {
            var service = CreateServiceWithoutDependencies();

            var ok = service.UsuarioPuedeTransicionarEstadoAocr(
                EstadoSolicitud.Subsanada,
                new[] { rol },
                true);

            Assert.IsTrue(ok, "El rol {0} debe poder reenviar subsanaciones AOCR.", rol);
        }

        [TestMethod]
        public void UsuarioPuedeTransicionarEstadoAocr_Subsanada_SinRolExternoPermitido_DebeRetornarFalse()
        {
            var service = CreateServiceWithoutDependencies();

            var ok = service.UsuarioPuedeTransicionarEstadoAocr(
                EstadoSolicitud.Subsanada,
                new List<string> { "Inspector" },
                true);

            Assert.IsFalse(ok, "Un inspector no debe usar el flujo RT para reenviar subsanaciones AOCR.");
        }
    }
}