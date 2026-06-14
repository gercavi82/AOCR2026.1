using System.Linq;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrFlujoValidacionServiceTests
    {
        private static AocrFlujoValidacionService CrearServicio()
        {
            return new AocrFlujoValidacionService();
        }

        [TestMethod]
        public void PuedeGenerarInformeTecnico_InspeccionInvalida_DebeBloquear()
        {
            string motivo;
            Assert.IsFalse(CrearServicio().PuedeGenerarInformeTecnico(0, out motivo));
            StringAssert.Contains(motivo, "Inspección inválida");
        }

        [TestMethod]
        public void AocrEmailFlujoService_DebeExponerEventosInstitucionales()
        {
            var eventos = AocrEmailFlujoService.EventosFlujoInstitucionales;
            Assert.IsTrue(eventos.Count >= 20);
            Assert.IsTrue(eventos.Contains("INSPECTOR_ASIGNADO"));
            Assert.IsTrue(eventos.Contains("AOCR_FIRMADO"));
        }
    }
}
