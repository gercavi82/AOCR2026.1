using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RevisionDocumentalBandejaServiceTests
    {
        [TestMethod]
        public void InspectorConfirmoCierreDocumental_SinConfirmacionExplicita_DebeSerFalse()
        {
            var inspeccion = new Inspeccion
            {
                CodigoInspeccion = 11,
                CodigoSolicitud = 12,
                CodigoInspector = 43,
                Estado = EstadosInspeccion.VERIFICACION_SOLICITUD
            };

            Assert.IsFalse(RevisionDocumentalService.InspectorConfirmoCierreDocumental(inspeccion));
        }

        [TestMethod]
        public void InspectorConfirmoCierreDocumental_ConEstadoDocumentalEnRevision_DebeSerTrue()
        {
            var inspeccion = new Inspeccion
            {
                CodigoInspeccion = 11,
                EstadoDocumental = "EN_REVISION"
            };

            Assert.IsTrue(RevisionDocumentalService.InspectorConfirmoCierreDocumental(inspeccion));
        }

        [TestMethod]
        public void DebeMostrarAccionInspeccion_SinConfirmacionInspector_DebeSerFalse()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 12,
                Estado = EstadoSolicitud.EnInspeccion
            };
            var inspeccion = new Inspeccion
            {
                CodigoInspeccion = 11,
                CodigoSolicitud = 12,
                CodigoInspector = 43
            };

            Assert.IsFalse(RevisionDocumentalBandejaService.DebeMostrarAccionInspeccion(solicitud, inspeccion));
        }

        [TestMethod]
        public void DebeMostrarAccionInspeccion_ConConfirmacionInspector_DebeSerTrue()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 12,
                Estado = EstadoSolicitud.EnInspeccion
            };
            var inspeccion = new Inspeccion
            {
                CodigoInspeccion = 11,
                CodigoSolicitud = 12,
                CodigoInspector = 43,
                EstadoDocumental = "EN_REVISION"
            };

            Assert.IsTrue(RevisionDocumentalBandejaService.DebeMostrarAccionInspeccion(solicitud, inspeccion));
        }

        [TestMethod]
        public void PuedeAccederRevisionDocumental_InspectorAsignadoEnInspeccion_DebePermitir()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 12,
                Estado = EstadoSolicitud.EnInspeccion,
                CodigoTecnico = 43
            };
            var estadoRevision = new EstadoRevisionDocumental
            {
                CodigoSolicitud = 12,
                DocumentacionAprobada = true,
                VisibleEnBandejaInspector = true
            };
            var inspecciones = new[]
            {
                new Inspeccion { CodigoInspeccion = 11, CodigoSolicitud = 12, CodigoInspector = 43 }
            };

            Assert.IsTrue(RevisionDocumentalBandejaService.PuedeAccederRevisionDocumental(
                solicitud,
                estadoRevision,
                inspecciones,
                new[] { 43 },
                new[] { "1709565459" }));
        }
    }
}
