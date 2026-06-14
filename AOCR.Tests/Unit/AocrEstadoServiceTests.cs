using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrEstadoServiceTests
    {
        private readonly AocrEstadoService _estado = new AocrEstadoService();

        [TestMethod]
        public void NormalizarClaveInstitucional_EnRevisionCoordinador_DebeMapearCorrectamente()
        {
            Assert.AreEqual("EN_REVISION_COORDINADOR", _estado.NormalizarClaveInstitucional("EN REVISION COORDINADOR"));
            Assert.AreEqual("EN_REVISION_COORDINADOR", _estado.NormalizarClaveInstitucional("En Revision"));
        }

        [TestMethod]
        public void NormalizarClaveInstitucional_AocrEmitido_DebeMapearDocumentosFinales()
        {
            Assert.AreEqual("DOCUMENTOS_FINALES_DISPONIBLES", _estado.NormalizarClaveInstitucional("AOCR_EMITIDO"));
        }
    }
}
