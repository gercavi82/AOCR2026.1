using CapaDatos.Constants;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class RevisionDocumentalFirmaPlanningTests
    {
        [DataTestMethod]
        [DataRow(0, 0, 0, false, false)]
        [DataRow(7, 6, 0, false, false)]
        [DataRow(7, 5, 1, false, false)]
        [DataRow(7, 6, 1, false, true)]
        [DataRow(7, 6, 1, true, false)]
        [DataRow(7, 7, 0, true, true)]
        [DataRow(7, 8, 0, true, false)]
        public void CierreDocumental_ExigeRevisionCompletaYSubsanacionParaAvanzar(
            int total, int aceptados, int devueltos, bool avanzar, bool permitido)
        {
            Assert.AreEqual(permitido,
                RevisionDocumentalService.ValidarRevisionCompleta(total, aceptados, devueltos, avanzar) == null);
        }

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
