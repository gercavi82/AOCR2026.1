using CapaDatos.Constants;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class GateDNoConformidadTests
    {
        [TestMethod]
        public void BuildAocrEventKey_InspectorAsignado_DebeGenerarClaveUnica()
        {
            var key = SolicitudAocrCorreoService.BuildAocrEventKey(
                "INSPECTOR_ASIGNADO",
                12,
                null,
                null,
                "inspector@aviacioncivil.gob.ec");

            Assert.IsFalse(string.IsNullOrWhiteSpace(key));
            StringAssert.Contains(key, "12");
            StringAssert.Contains(key, "inspector@aviacioncivil.gob.ec");
        }

        [TestMethod]
        public void BuildAocrEventKey_MismaClave_DebeSerDeterministica()
        {
            var key1 = SolicitudAocrCorreoService.BuildAocrEventKey("INSPECTOR_ASIGNADO", 12, null, "NC_1", "a@b.ec");
            var key2 = SolicitudAocrCorreoService.BuildAocrEventKey("INSPECTOR_ASIGNADO", 12, null, "NC_1", "a@b.ec");
            Assert.AreEqual(key1, key2);
        }

        [TestMethod]
        public void EstadosInspeccion_ResultadoNoSatisfactorio_NoDebePermitirCargaRt()
        {
            var estado = EstadosInspeccion.NormalizarEstado(EstadosInspeccion.RESULTADO_NO_SATISFACTORIO);
            var permite = string.Equals(estado, EstadosInspeccion.OBSERVADA, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadosInspeccion.SUBSANADA, System.StringComparison.OrdinalIgnoreCase);
            Assert.IsFalse(permite, "RT no debe subsanar antes de aprobación formal de NC.");
        }

        [TestMethod]
        public void AocrFlujoService_EnInspeccion_A_Observada_DebePermitir()
        {
            var flujo = new AocrFlujoService();
            Assert.IsTrue(flujo.EsTransicionPermitida(EstadoSolicitud.EnInspeccion, EstadoSolicitud.Observada));
        }
    }
}
