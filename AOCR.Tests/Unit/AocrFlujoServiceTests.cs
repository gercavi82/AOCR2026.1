using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrFlujoServiceTests
    {
        private readonly AocrFlujoService _flujo = new AocrFlujoService();

        [TestMethod]
        public void RequiereRecaudacionFinalizadaParaAsignacion_EnRevisionCoordinador_DebeSerFalse()
        {
            Assert.IsFalse(_flujo.RequiereRecaudacionFinalizadaParaAsignacion("EN_REVISION_COORDINADOR"));
            Assert.IsFalse(_flujo.RequiereRecaudacionFinalizadaParaAsignacion("En Revision"));
        }

        [TestMethod]
        public void PuedeCoordinadorAsignarInspector_EnRevisionSinPago_DebePermitir()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 12,
                Estado = EstadoSolicitud.EnRevision
            };

            Assert.IsTrue(_flujo.PuedeCoordinadorAsignarInspector(solicitud, tieneAprobacionFinanciera: false));
        }

        [TestMethod]
        public void PuedeCoordinadorAsignarInspector_Finalizado_DebeBloquear()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 1,
                Estado = EstadoSolicitud.Finalizado
            };

            Assert.IsFalse(_flujo.PuedeCoordinadorAsignarInspector(solicitud, tieneAprobacionFinanciera: true));
        }

        [TestMethod]
        public void EsTransicionPermitida_EnRevision_A_EnInspeccion_DebePermitir()
        {
            Assert.IsTrue(_flujo.EsTransicionPermitida(EstadoSolicitud.EnRevision, EstadoSolicitud.EnInspeccion));
        }

        [TestMethod]
        public void RolPuedeEjecutarAccion_CoordinadorAsignarInspector_DebePermitir()
        {
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.AsignarInspector));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("Coordinador", AocrFlujoAcciones.AsignarInspector));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("CoordinadorInspecciones", AocrFlujoAcciones.AsignarInspector));
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Solicitante", AocrFlujoAcciones.AsignarInspector));
        }

        [TestMethod]
        public void EsTransicionPermitida_FirmadoCoordinador_A_Finalizado_DebeBloquear()
        {
            Assert.IsFalse(_flujo.EsTransicionPermitida(EstadoSolicitud.FirmadoCoordinador, EstadoSolicitud.Finalizado));
        }

        [TestMethod]
        public void EsTransicionPermitida_AceptacionDocumental_A_PendienteAsignacionRt_DebePermitir()
        {
            Assert.IsTrue(_flujo.EsTransicionPermitida(EstadoSolicitud.AceptacionDocumental, EstadoSolicitud.PendienteAsignacionRT));
        }
    }
}
