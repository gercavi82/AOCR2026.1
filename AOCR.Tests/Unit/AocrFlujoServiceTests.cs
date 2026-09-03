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

        [TestMethod]
        public void RolPuedeEjecutarAccion_AdministradorAccionesOperativas_DebeDenegar()
        {
            // REGLA 7: El Administrador no puede aprobar, devolver, designar o firmar en representación de roles operativos.
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.AprobarPago), "Admin no debe aprobar pago");
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.AceptarDocumentacion), "Admin no debe aceptar documentacion");
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.DevolverRtObservaciones), "Admin no debe devolver observaciones");
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.AsignarInspector), "Admin no debe asignar inspector");
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.FirmarListaVerificacion), "Admin no debe firmar LV");
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.FirmarInformeTecnico), "Admin no debe firmar informe tecnico");
            Assert.IsFalse(_flujo.RolPuedeEjecutarAccion("Administrador", AocrFlujoAcciones.FirmarAocrFinal), "Admin no debe firmar AOCR final");

            // Validar que los roles operativos legítimos SÍ pueden
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("Financiero", AocrFlujoAcciones.AprobarPago));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.AceptarDocumentacion));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("Coordinador", AocrFlujoAcciones.DevolverRtObservaciones));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("Coordinacion", AocrFlujoAcciones.AsignarInspector));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("Inspector", AocrFlujoAcciones.FirmarListaVerificacion));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("InspectorTecnico", AocrFlujoAcciones.FirmarInformeTecnico));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("DIRDAC", AocrFlujoAcciones.FirmarAocrFinal));
            Assert.IsTrue(_flujo.RolPuedeEjecutarAccion("DCAV", AocrFlujoAcciones.LiberarDocumentosFinales));
        }
    }
}
