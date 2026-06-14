using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrModificationNuevoAeropuertoTests
    {
        private static SolicitudAOCR CrearModificacionConAeropuerto()
        {
            return new SolicitudAOCR
            {
                CodigoSolicitud = 99,
                TipoSolicitud = 3,
                Estado = EstadoSolicitud.AceptacionDocumental,
                AeropuertosEcuador = "SEQM,SEGU"
            };
        }

        [TestMethod]
        public void TieneNuevoAeropuertoDeclarado_ModificacionConAeropuertos_DebeSerTrue()
        {
            Assert.IsTrue(AocrModificationWorkflowService.TieneNuevoAeropuertoDeclarado(CrearModificacionConAeropuerto()));
        }

        [TestMethod]
        public void PrepararGeneracionCondicionesLimitaciones_ConNuevoAeropuerto_DebeBloquear()
        {
            var service = new AocrModificationWorkflowService();
            var plan = service.PrepararGeneracionCondicionesLimitaciones(CrearModificacionConAeropuerto(), null);

            Assert.IsFalse(plan.PuedeContinuar);
            Assert.AreEqual(
                AocrModificationWorkflowService.MensajeRechazoClConInspeccionRequerida,
                plan.Mensaje);
        }

        [TestMethod]
        public void PrepararGeneracionCondicionesLimitaciones_SinAeropuerto_DesdeFirmadoCoordinador_DebePermitir()
        {
            var service = new AocrModificationWorkflowService();
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 101,
                TipoSolicitud = 3,
                Estado = EstadoSolicitud.FirmadoCoordinador
            };

            var plan = service.PrepararGeneracionCondicionesLimitaciones(solicitud, null);

            Assert.IsTrue(plan.PuedeContinuar);
            Assert.AreEqual(EstadoSolicitud.GeneradoCondicionesLimitaciones, plan.EstadoDestino);
        }

        [TestMethod]
        public void PrepararCierreFaseDocumentalNuevoAeropuerto_DesdeFirmadoCoordinador_DebeIrARequiereInspeccion()
        {
            var service = new AocrModificationWorkflowService();
            var solicitud = CrearModificacionConAeropuerto();
            solicitud.Estado = EstadoSolicitud.FirmadoCoordinador;

            var plan = service.PrepararCierreFaseDocumentalNuevoAeropuerto(solicitud, null);

            Assert.IsTrue(plan.PuedeContinuar);
            Assert.AreEqual(EstadoSolicitud.RequiereInspeccion, plan.EstadoDestino);
        }

        [TestMethod]
        public void EsEstadoResolucionModificacionPermitido_FirmadoCoordinador_DebeSerTrue()
        {
            Assert.IsTrue(AocrModificationWorkflowService.EsEstadoResolucionModificacionPermitido(EstadoSolicitud.FirmadoCoordinador));
        }

        [TestMethod]
        public void PrepararRequiereInspeccion_ConNuevoAeropuerto_DebeExigirCierreInstitucional()
        {
            var service = new AocrModificationWorkflowService();
            var plan = service.PrepararRequiereInspeccion(CrearModificacionConAeropuerto(), null);

            Assert.IsFalse(plan.PuedeContinuar);
            StringAssert.Contains(plan.Mensaje, "cierre institucional");
        }

        [TestMethod]
        public void PrepararCierreFaseDocumentalNuevoAeropuerto_DebeIrARequiereInspeccion()
        {
            var service = new AocrModificationWorkflowService();
            var plan = service.PrepararCierreFaseDocumentalNuevoAeropuerto(CrearModificacionConAeropuerto(), "Nuevo aeropuerto declarado.");

            Assert.IsTrue(plan.PuedeContinuar);
            Assert.AreEqual(EstadoSolicitud.RequiereInspeccion, plan.EstadoDestino);
            StringAssert.Contains(plan.ObservacionEstado, "orden de recaudación");
        }

        [TestMethod]
        public void PrepararCierreFaseDocumentalNuevoAeropuerto_SinAeropuerto_DebeBloquear()
        {
            var service = new AocrModificationWorkflowService();
            var solicitud = new SolicitudAOCR
            {
                CodigoSolicitud = 100,
                TipoSolicitud = 3,
                Estado = EstadoSolicitud.AceptacionDocumental
            };

            var plan = service.PrepararCierreFaseDocumentalNuevoAeropuerto(solicitud, null);

            Assert.IsFalse(plan.PuedeContinuar);
        }
    }
}
