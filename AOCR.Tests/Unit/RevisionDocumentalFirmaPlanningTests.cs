using CapaDatos.Constants;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RevisionDocumentalFirmaPlanningTests
    {
        [DataTestMethod]
        [DataRow(1, EstadoSolicitud.PendienteAsignacionRT)]
        [DataRow(2, EstadoSolicitud.PendienteAsignacionRT)]
        [DataRow(3, EstadoSolicitud.FirmadoCoordinador)]
        [DataRow(null, EstadoSolicitud.FirmadoCoordinador)]
        public void ResolverEstadoDestinoFirmaAceptacionDocumental_DebeDerivarSegunTipo(int? tipoSolicitud, string destinoEsperado)
        {
            var destino = RevisionDocumentalService.ResolverEstadoDestinoFirmaAceptacionDocumental(tipoSolicitud);
            Assert.AreEqual(destinoEsperado, destino);
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        public void ResolverEstadoDestinoFirmaAceptacionDocumental_ConInspectorAsignado_DebeContinuarEnInspeccion(int tipoSolicitud)
        {
            var destino = RevisionDocumentalService.ResolverEstadoDestinoFirmaAceptacionDocumental(tipoSolicitud, true);
            Assert.AreEqual(EstadoSolicitud.EnInspeccion, destino);
        }
    }
}
