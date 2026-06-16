using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class AocrCompaniaContextServiceTests
    {
        private readonly AocrCompaniaContextService _service = new AocrCompaniaContextService();

        [TestMethod]
        public void SolicitudPerteneceACompania_PorCompaniasSeleccionadas()
        {
            var solicitud = new SolicitudAOCR
            {
                CompaniasSeleccionadas = "ABC, DEF",
                CodigoOaci = "XYZ"
            };

            Assert.IsTrue(_service.SolicitudPerteneceACompania(solicitud, "ABC"));
            Assert.IsFalse(_service.SolicitudPerteneceACompania(solicitud, "ZZZ"));
        }

        [TestMethod]
        public void SolicitudPerteneceACompania_PorCodigoOaciCuandoNoHayLista()
        {
            var solicitud = new SolicitudAOCR
            {
                CodigoOaci = "LAN",
                RazonSocial = "LATAM AIRLINES"
            };

            Assert.IsTrue(_service.SolicitudPerteneceACompania(solicitud, "LAN"));
            Assert.IsFalse(_service.SolicitudPerteneceACompania(solicitud, "AAA"));
        }

        [TestMethod]
        public void EsEstadoActivoProceso_EsInversoDeFinal()
        {
            var estadoService = new AocrEstadoService();
            Assert.IsTrue(estadoService.EsEstadoActivoProceso(EstadoSolicitud.EnRevision));
            Assert.IsFalse(estadoService.EsEstadoActivoProceso(EstadoSolicitud.Finalizado));
            Assert.IsFalse(estadoService.EsEstadoActivoProceso(EstadoSolicitud.Anulada));
            Assert.IsFalse(estadoService.EsEstadoActivoProceso(EstadoSolicitud.AOCR_Legalizado));
        }

        [TestMethod]
        public void OrdenPerteneceACompania_PorCompaniaCodigoPersistido()
        {
            var orden = new CapaDatos.Entidades.OrdenRecaudacion
            {
                CompaniaCodigo = "LAN",
                Compania = "Otra compañía"
            };

            Assert.IsTrue(_service.OrdenPerteneceACompania(orden, "LAN", null, 45));
            Assert.IsFalse(_service.OrdenPerteneceACompania(orden, "AAA", null, 45));
        }

        [TestMethod]
        public void ObtenerMensajeCompaniaInconsistente_DebeSerInstitucional()
        {
            StringAssert.Contains(_service.ObtenerMensajeCompaniaInconsistente(), "Recargue la pantalla");
        }
    }
}
