using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class OrdenRecaudacionNomenclaturaTests
    {
        [TestMethod]
        public void SolicitudCanonica_ConservaAnioYCorrelativoEnOrden()
        {
            var numero = OrdenRecaudacionService.ConstruirNumeroOrdenDesdeNumeroSolicitud(
                "DGAC-GOP-2026-AOCR001",
                2026);

            Assert.AreEqual("DGAC-OR-2026-AOCR001", numero);
        }

        [TestMethod]
        public void SolicitudLegacyConGuionesBajos_SeNormalizaSinCambiarCorrelativo()
        {
            var numero = OrdenRecaudacionService.ConstruirNumeroOrdenDesdeNumeroSolicitud(
                "DGAC_GOP_2026_AOCR01",
                2025);

            Assert.AreEqual("DGAC-OR-2026-AOCR001", numero);
        }

        [TestMethod]
        public void CorrelativoDieciseis_NoSeReenumera()
        {
            var numero = OrdenRecaudacionService.ConstruirNumeroOrdenDesdeNumeroSolicitud(
                "DGAC-GOP-2026-AOCR016",
                2025);

            Assert.AreEqual("DGAC-OR-2026-AOCR016", numero);
        }

        [TestMethod]
        public void SolicitudSinCorrelativoAocr_NoInventaNumeroOrden()
        {
            var numero = OrdenRecaudacionService.ConstruirNumeroOrdenDesdeNumeroSolicitud(
                "SOLICITUD-2026-001",
                2026);

            Assert.IsNull(numero);
        }
    }
}
