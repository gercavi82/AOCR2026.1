using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class NumeracionGopOrTests
    {
        [DataTestMethod]
        [DataRow("DGAC-GOP-2026-AOCR001", "DGAC-OR-2026-AOCR001")]
        [DataRow("DGAC-GOP-2026-AOCR009", "DGAC-OR-2026-AOCR009")]
        [DataRow("DGAC-GOP-2027-AOCR1000", "DGAC-OR-2027-AOCR1000")]
        public void Orden_ConservaAnioYCorrelativoDelExpediente(string gop, string orden)
        {
            Assert.AreEqual(orden, OrdenRecaudacionService.ConstruirNumeroOrdenDesdeNumeroSolicitud(gop, 2026));
        }
    }
}
