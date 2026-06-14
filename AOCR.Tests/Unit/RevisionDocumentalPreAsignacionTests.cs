using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RevisionDocumentalPreAsignacionTests
    {
        [TestMethod]
        public void EsRevisionDocumentalPreAsignacion_SinInspectorEnRevision_DebeSerTrue()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 12,
                TipoSolicitud = 1,
                Estado = EstadoSolicitud.EnRevision
            };

            Assert.IsTrue(SolicitudAocrInfraBL.EsRevisionDocumentalPreAsignacion(solicitud, null));
        }

        [TestMethod]
        public void EsRevisionDocumentalPreAsignacion_ConInspectorAsignado_DebeSerFalse()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 12,
                TipoSolicitud = 1,
                Estado = EstadoSolicitud.EnRevision,
                CodigoTecnico = 99
            };

            Assert.IsFalse(SolicitudAocrInfraBL.EsRevisionDocumentalPreAsignacion(solicitud, null));
        }
    }
}
