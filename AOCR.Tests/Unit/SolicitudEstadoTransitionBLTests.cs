using CapaDatos.Constants;
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
        [DataRow("En Revision", "En Inspeccion")]
        [DataRow("Documentacion Completa", "En Inspeccion")]
        [DataRow("Subsanada", "En Inspeccion")]
        [DataRow("Aceptacion Documental", "Pendiente Asignacion RT")]
        [DataRow("Aceptacion Documental", "Requiere Inspeccion")]
        [DataRow("Aceptacion Documental", "Generado Condiciones y Limitaciones")]
        [DataRow("Requiere Inspeccion", "Pendiente Asignacion RT")]
        [DataRow("Requiere Inspeccion", "En Inspeccion")]
        [DataRow("Generado Condiciones y Limitaciones", "En Revision Coordinador Final")]
        [DataRow("En Revision Coordinador Final", "Enviado DCAV")]
        [DataRow("Enviado DCAV", "Firmado DCAV")]
        [DataRow("Firmado DCAV", "Finalizado")]
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
        [DataRow("Aceptacion Documental", "Enviado DCAV")]
        [DataRow("Requiere Inspeccion", "Generado Condiciones y Limitaciones")]
        [DataRow("Generado Condiciones y Limitaciones", "Firmado DCAV")]
        [DataRow("Enviado DCAV", "Finalizado")]
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

        [TestMethod]
        public void EsTransicionPermitida_FlujoModificacionDirecta_DebeMantenerSecuenciaCompleta()
        {
            var flujo = new[]
            {
                EstadoSolicitud.AceptacionDocumental,
                EstadoSolicitud.GeneradoCondicionesLimitaciones,
                EstadoSolicitud.EnRevisionCoordinadorFinal,
                EstadoSolicitud.EnviadoDcav,
                EstadoSolicitud.FirmadoDcav,
                EstadoSolicitud.Finalizado
            };

            for (var index = 0; index < flujo.Length - 1; index++)
            {
                var ok = _service.EsTransicionPermitidaParaPruebas(flujo[index], flujo[index + 1]);
                Assert.IsTrue(ok, "Se esperaba transición válida en flujo directo: {0} -> {1}", flujo[index], flujo[index + 1]);
            }
        }

        [TestMethod]
        public void EsTransicionPermitida_FlujoModificacionConInspeccion_DebeDerivarCorrectamente()
        {
            var flujo = new[]
            {
                EstadoSolicitud.AceptacionDocumental,
                EstadoSolicitud.RequiereInspeccion,
                EstadoSolicitud.PendienteAsignacionRT,
                EstadoSolicitud.EnInspeccion
            };

            for (var index = 0; index < flujo.Length - 1; index++)
            {
                var ok = _service.EsTransicionPermitidaParaPruebas(flujo[index], flujo[index + 1]);
                Assert.IsTrue(ok, "Se esperaba transición válida en flujo con inspección: {0} -> {1}", flujo[index], flujo[index + 1]);
            }
        }
    }
}
