using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrCierrePorTipoTramiteServiceTests
    {
        private readonly AocrCierrePorTipoTramiteService _service = new AocrCierrePorTipoTramiteService();

        [TestMethod]
        public void EmisionYRenovacion_UsanModulo7YExigenAmbosDocumentos()
        {
            foreach (var tipo in new[] { 1, 2 })
            {
                var plan = _service.Resolver(new SolicitudAOCR { TipoSolicitud = tipo });
                Assert.IsTrue(plan.EsValido);
                Assert.AreEqual(AocrTipoCierre.EmisionRenovacion, plan.TipoCierre);
                Assert.AreEqual("MODULO_7", plan.Modulo);
                Assert.IsTrue(plan.GenerarAocr);
                Assert.IsTrue(plan.GenerarCondiciones);
                Assert.AreEqual(2, plan.DocumentosRequeridos.Count);
            }
        }

        [TestMethod]
        public void Modificacion_UsaModulo8YProhibeAocrNuevo()
        {
            var solicitud = new SolicitudAOCR { TipoSolicitud = 3 };
            var plan = _service.Resolver(solicitud);
            Assert.AreEqual(AocrTipoCierre.Modificacion, plan.TipoCierre);
            Assert.AreEqual("MODULO_8", plan.Modulo);
            Assert.IsFalse(plan.GenerarAocr);
            Assert.IsTrue(plan.GenerarCondiciones);
            Assert.AreEqual(1, plan.DocumentosRequeridos.Count);
            string motivo;
            Assert.IsFalse(_service.PuedeGenerarDocumento(solicitud, "RECONOCIMIENTO", out motivo));
            StringAssert.Contains(motivo, "no permite generar");
        }

        [TestMethod]
        public void ModificacionConNuevoAeropuerto_ConservaModulo8()
        {
            var plan = _service.Resolver(new SolicitudAOCR { TipoSolicitud = 3, AeropuertosEcuador = "SEQM" });
            Assert.AreEqual("MODIFICACION_CON_NUEVO_AEROPUERTO", plan.TipoTramite);
            Assert.AreEqual("MODULO_8", plan.Modulo);
            Assert.IsFalse(plan.GenerarAocr);
            Assert.IsTrue(plan.GenerarCondiciones);
        }
    }
}
