using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RTServiceStateTests
    {
        private readonly RTService _service = new RTService();

        [DataTestMethod]
        [DataRow(null, RTService.EstadoBorrador)]
        [DataRow("", RTService.EstadoBorrador)]
        [DataRow("ENVIADA", RTService.EstadoEnviado)]
        [DataRow("en_revision_coordinador", RTService.EstadoEnRevisionCoordinador)]
        [DataRow("finalizado", RTService.EstadoFinalizado)]
        public void NormalizarEstado_DebeResolverAliasYMayusculas(string entrada, string esperado)
        {
            var actual = _service.NormalizarEstado(entrada);

            Assert.AreEqual(esperado, actual);
        }

        [DataTestMethod]
        [DataRow(RTService.EstadoBorrador, true)]
        [DataRow(RTService.EstadoDevueltoConObservaciones, true)]
        [DataRow(RTService.EstadoEnviado, false)]
        [DataRow(RTService.EstadoEnRevisionCoordinador, false)]
        [DataRow(RTService.EstadoFinalizado, false)]
        public void EsEstadoEditable_DebePermitirSoloEstadosReprocesables(string estado, bool esperado)
        {
            var actual = _service.EsEstadoEditable(estado);

            Assert.AreEqual(esperado, actual);
        }
    }
}