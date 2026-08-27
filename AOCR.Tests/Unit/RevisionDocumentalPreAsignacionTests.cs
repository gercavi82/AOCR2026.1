using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

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

        [TestMethod]
        public void OficioAceptacionGeneradoPorSistema_NoDebeReabrirRevisionDocumental()
        {
            var metodo = typeof(SolicitudAocrInfraBL).GetMethod(
                "DebeIncluirEnRevisionDocumental",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(metodo);
            var incluir = (bool)metodo.Invoke(null, new object[] { "OFICIO_ACEPTACION_REVISION_DOCUMENTAL" });

            Assert.IsFalse(incluir);
        }
    }
}
