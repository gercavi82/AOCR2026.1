using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrDcavRevisionServiceTests
    {
        [TestMethod]
        public void EsInformeSatisfactorio_AceptaResultadoSatisfactorio()
        {
            var informe = new InspeccionInformeTecnico { Resultado = "Satisfactorio" };

            Assert.IsTrue(AocrDcavRevisionService.EsInformeSatisfactorio(informe));
        }

        [TestMethod]
        public void EsInformeSatisfactorio_RechazaInsatisfactorio()
        {
            var informe = new InspeccionInformeTecnico { Resultado = "Insatisfactorio" };

            Assert.IsFalse(AocrDcavRevisionService.EsInformeSatisfactorio(informe));
        }

        [TestMethod]
        public void EstadosDcav_DeclaranNivelPrevioAFirmaDirectorGeneral()
        {
            Assert.AreEqual("PENDIENTE_REVISION_DCAV", AocrEstadosProceso.PendienteRevisionDcav);
            Assert.AreEqual("APROBADO_POR_DCAV", AocrEstadosProceso.AprobadoPorDcav);
            Assert.AreEqual("PENDIENTE_FIRMA_DIRECTOR_GENERAL", AocrEstadosProceso.PendienteFirmaDirectorGeneral);
            Assert.AreEqual("FIRMADO_DIRECTOR_GENERAL", AocrEstadosProceso.FirmadoDirectorGeneral);
        }
    }
}
