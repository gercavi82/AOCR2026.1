using CapaDatos.Entidades;
using CapaNegocio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class NullSafetyRegressionTests
    {
        [TestMethod]
        public void OrdenRecaudacion_DefaultContribuyenteId_NoDebeSerNull()
        {
            var orden = new OrdenRecaudacion();

            Assert.IsTrue(orden.ContribuyenteId.HasValue);
            Assert.AreEqual(0, orden.ContribuyenteId.Value);
        }

        [TestMethod]
        public void SolicitudEstadoTransition_EstadoFinalIdempotente_DebePermitir()
        {
            var service = new SolicitudEstadoTransitionBL();
            var ok = service.EsTransicionPermitidaParaPruebas("CERTIFICADO_EMITIDO", "AOCR_EMITIDO_RECIBIDO");

            Assert.IsTrue(ok);
        }
    }
}
