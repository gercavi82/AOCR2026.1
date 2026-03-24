using CapaNegocio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class SolicitudEstadoTransitionBLTests
    {
        private readonly SolicitudEstadoTransitionBL _service = new SolicitudEstadoTransitionBL();

        [DataTestMethod]
        [DataRow("Pendiente", "Observada")]
        [DataRow("En Revision", "Aceptacion Documental")]
        [DataRow("Documentacion Completa", "En Inspeccion")]
        [DataRow("Aceptacion Documental", "Pendiente Asignacion RT")]
        [DataRow("Pendiente Asignacion RT", "En Inspeccion")]
        [DataRow("AOCR En Revision", "AOCR Validado")]
        [DataRow("AOCR Validado", "AOCR Legalizado")]
        [DataRow("AOCR Legalizado", "AOCR Emitido/Recibido")]
        public void EsTransicionPermitida_TransicionesValidas_DebeRetornarTrue(string actual, string destino)
        {
            var ok = _service.EsTransicionPermitidaParaPruebas(actual, destino);
            Assert.IsTrue(ok, "Se esperaba transicion valida: {0} -> {1}", actual, destino);
        }

        [DataTestMethod]
        [DataRow("Pendiente", "AOCR Legalizado")]
        [DataRow("Observada", "AOCR Validado")]
        [DataRow("Pendiente Asignacion RT", "AOCR Legalizado")]
        [DataRow("AOCR Validado", "En Inspeccion")]
        [DataRow("AOCR Emitido/Recibido", "AOCR En Revision")]
        [DataRow("Rechazada", "AOCR Emitido/Recibido")]
        public void EsTransicionPermitida_TransicionesInvalidas_DebeRetornarFalse(string actual, string destino)
        {
            var ok = _service.EsTransicionPermitidaParaPruebas(actual, destino);
            Assert.IsFalse(ok, "Se esperaba transicion invalida: {0} -> {1}", actual, destino);
        }

        [DataTestMethod]
        [DataRow("ENVIADO_A_JEFATURA", "AOCR_Validado")]
        [DataRow("APROBADO_POR_DIRECCION", "LEGALIZADO")]
        [DataRow("CERTIFICADO_EMITIDO", "AOCR_EMITIDO_RECIBIDO")]
        public void EsTransicionPermitida_EstadosLegacyNormalizados_DebeRetornarTrue(string actualLegacy, string destinoLegacy)
        {
            var ok = _service.EsTransicionPermitidaParaPruebas(actualLegacy, destinoLegacy);
            Assert.IsTrue(ok, "Se esperaba transicion valida tras normalizacion: {0} -> {1}", actualLegacy, destinoLegacy);
        }
    }
}
