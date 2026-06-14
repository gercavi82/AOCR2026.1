using CapaDatos.Constants;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class CoordinacionBandejaEstadoTests
    {
        [DataTestMethod]
        [DataRow("EN_REVISION_COORDINADOR")]
        [DataRow("EN REVISION COORDINADOR")]
        [DataRow("En Revision")]
        [DataRow("ENVIADO_COORDINADOR")]
        [DataRow("PENDIENTE_ASIGNACION")]
        [DataRow("PENDIENTE_ASIGNACION_TECNICA")]
        [DataRow("DOCUMENTACION_COMPLETA")]
        [DataRow("ACEPTACION_DOCUMENTAL")]
        public void EstadoPermiteAsignacionInicial_DebeIncluirEstadosDeCoordinacion(string estado)
        {
            Assert.IsTrue(EstadoSolicitudSql.EstadoPermiteAsignacionInicial(estado));
        }

        [DataTestMethod]
        [DataRow("FINALIZADO")]
        [DataRow("AOCR_LEGALIZADO")]
        [DataRow("ANULADA")]
        [DataRow("EN_INSPECCION")]
        public void EstadoPermiteAsignacionInicial_NoDebeIncluirEstadosCerrados(string estado)
        {
            Assert.IsFalse(EstadoSolicitudSql.EstadoPermiteAsignacionInicial(estado));
        }

        [TestMethod]
        public void ExpresionTieneInspectorEfectivo_SoloUsaColumnasPresentesEnEsquema()
        {
            var columnasSolicitud = new[] { "codigo_tecnico" };
            var columnasInspeccion = new[] { "codigo_inspector" };

            var sql = EstadoSolicitudSql.ExpresionTieneInspectorEfectivo("s", columnasSolicitud, columnasInspeccion);

            StringAssert.Contains(sql, "s.codigo_tecnico");
            StringAssert.Contains(sql, "i_asg.codigo_inspector");
            Assert.IsFalse(sql.Contains("tecnico_responsable_cedula"));
            Assert.IsFalse(sql.Contains("inspector_principal_cedula"));
        }

        [TestMethod]
        public void ExpresionTieneInspectorEfectivo_SinColumnasDetectables_RetornaFalse()
        {
            var sql = EstadoSolicitudSql.ExpresionTieneInspectorEfectivo("s", new string[0], new string[0]);
            Assert.AreEqual("FALSE", sql);
        }
    }
}
